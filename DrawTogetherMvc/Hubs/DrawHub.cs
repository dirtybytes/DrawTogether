using Microsoft.AspNetCore.SignalR;
using System;
using System.Threading.Tasks;
using System.Web;
using System.Linq;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Channels;
using System.Threading;

namespace DrawTogetherMvc.Hubs
{
    /// <summary>
    /// Represents a coordinate on the canvas.
    /// </summary>
    public struct Point
    {
        public Point(int x, int y)
        {
            X = x;
            Y = y;
        }
        public int X;
        public int Y;
    }
    /// <summary>
    /// A class representing a line being drawn between two points on the canvas.
    /// </summary>
    public class DrawLineEvent
    {
        public Point From { get; set; }
        public Point To { get; set; }
    }
    /// <summary>
    /// A class for storing the image data for a given room.
    /// </summary>
    public class RoomData
    {
        /// <summary>
        /// Indicates that the canvas image is currently being re-rendered by the server.<br/>
        /// Redrawing == true implies that TrimDrawData() has already been called, but hasn't finished processing yet.
        /// </summary>
        public bool Redrawing { get; set; } = false;
        public RoomData(string id)
        {
            RoomId = id;
        }
        public string RoomId { get; }
        /// <summary>
        /// Data URI image data
        /// </summary>
        public string Image { get; set; } = "";
        public ConcurrentQueue<DrawLineEvent> DrawEvents { get; }
            = new ConcurrentQueue<DrawLineEvent>();
        /// <summary>
        /// Specifies the number of events that need to be removed from the DrawEvents queue.<br/>
        /// Events are removed after being rendered onto an image, following the image update.
        /// </summary>
        public int EventsToTrim = 0;
    }
    /// <summary>
    /// A class for storing information about a connection, including the channel for sending data about drawing events to the client.
    /// </summary>
    public class ConnectionData
    {
        public ConnectionData(string connectionId)
        {
            ConnectionId = connectionId;
        }
        public string ConnectionId { get; }
        public string Room { get; set; }
        /// <summary>
        /// A stream of drawing events for the client to process.
        /// </summary>
        public Channel<DrawLineEvent> DrawEvents { get; }
            = Channel.CreateUnbounded<DrawLineEvent>();
    }
    /// <summary>
    /// An interface describing methods that can be called on a client.
    /// </summary> 
    public interface IDrawClient
    {
        Task ReceiveChatMessage(string user, string message);
        /// <summary>
        /// Sends the pre-rendered image data to the client to use as the initial canvas image.
        /// </summary>
        Task ReloadImage(string imageData);
        /// <summary>
        /// Sends a request to the node.js worker to re-render the room image data.
        /// </summary>
        Task NodeRenderImage(string room);
    }
    /// <summary>
    /// SignalR hub facilitating the realtime communication between all clients and the server.<br/>
    /// Contains several static properties and methods for storing persistent data related to rooms and connections.
    /// </summary>
    public class DrawHub : Hub<IDrawClient>
    {
        #region Static hub methods and properties.
        /// <summary>
        /// The rate at which an image is redrawn server-side.
        /// </summary>
        private static int _imageRefreshRate = 100;
        /// <summary>
        /// ConnectionId of the node.js worker that renders room images server-side.
        /// </summary>
        private static string _nodeWorkerID;
        private static ConcurrentDictionary<string, RoomData> _roomData
            = new ConcurrentDictionary<string, RoomData>();
        private static ConcurrentDictionary<string, ConnectionData> _connectionData
            = new ConcurrentDictionary<string, ConnectionData>();
        private static ConcurrentDictionary<string, HashSet<string>> _roomToConnections
            = new ConcurrentDictionary<string, HashSet<string>>();
        /// <summary>
        /// Retrieve/create room data for a particular room.
        /// </summary>
        public static RoomData Room(string room)
        {
            if (!_roomData.ContainsKey(room))
            {
                _roomData[room] = new RoomData(room);
            }
            return _roomData[room];
        }
        /// <summary>
        /// Retrieve/create connection data for a particular connection.
        /// </summary>
        public static ConnectionData Connection(string connectionId)
        {
            if (!_connectionData.ContainsKey(connectionId))
            {
                _connectionData[connectionId] = new ConnectionData(connectionId);
            }
            return _connectionData[connectionId];
        }
        /// <summary>
        /// Connections assigned to a particular room.
        /// </summary>
        public static HashSet<string> RoomConnections(string room)
        {
            if (!_roomToConnections.ContainsKey(room))
            {
                _roomToConnections[room] = new HashSet<string>();
            }
            return _roomToConnections[room];
        }
        /// <summary>
        /// Sends the drawing event data to all users connected to the room.
        /// </summary>
        public static async Task AddDrawLineEvent(DrawLineEvent ev, string room)
        {
            Room(room).DrawEvents.Enqueue(ev);
            foreach (var connectionId in _roomToConnections[room])
            {
                await Connection(connectionId).DrawEvents.Writer.WriteAsync(ev);
            }
        }
        /// <summary>
        /// Assigns a connection to a room.
        /// </summary>
        public static ConnectionData AddConnection(string connectionId, string room)
        {
            var connection = new ConnectionData(connectionId);
            _connectionData[connectionId] = connection;
            RoomConnections(room).Add(connectionId);
            return connection;
        }
        /// <summary>
        /// Removes the data associated with a connection.
        /// </summary>
        public static void RemoveConnection(string connectionId)
        {
            var room = Connection(connectionId).Room;
            _connectionData.Remove(connectionId, out _);
            // TODO: sometimes throws an error with "key == null"
            _roomToConnections.TryGetValue(room, out var connections);
            connections?.Remove(connectionId);
        }
        #endregion
        #region Instance hub methods.
        private string ConnectionRoom => Connection(Context.ConnectionId).Room;
        /// <summary>
        /// A method called by a client when it wants to send us a drawing event.
        /// </summary>
        public async Task SendDrawLine(DrawLineEvent ev)
        {
            var room = ConnectionRoom;
            await AddDrawLineEvent(ev, room);
            var roomData = Room(room);
            // check if we should re-render the stored image for the room
            var storedEventsCount = roomData.DrawEvents.Count;
            if (!roomData.Redrawing && storedEventsCount > _imageRefreshRate)
            {
                roomData.Redrawing = true;
                roomData.EventsToTrim += storedEventsCount - _imageRefreshRate * 2;
                _ = Clients.Client(_nodeWorkerID).NodeRenderImage(room);
            }
        }
        /// <summary>
        /// A method called by a client when it wants to send a chat message.
        /// </summary>
        /// <returns>
        /// A string indicating the success or failure.
        /// </returns>
        public async Task<string> SendChatMessage(string user, string message)
        {
            if (String.IsNullOrEmpty(message))
            {
                return "Cannot send an empty message.";
            }
            var room = ConnectionRoom;
            await Clients.Group(room).ReceiveChatMessage(user, message);
            return "Server message received";
        }
        /// <summary>
        /// A method called by the node.js worker to establish communication with the server.
        /// </summary>
        public string NodeEstablishConnection()
        {
            _nodeWorkerID = Context.ConnectionId;
            return "Success";
        }
        /// <summary>
        /// A method called by the node.js worker to retrieve drawing events that need to be rendered onto the canvas.
        /// </summary>
        public async Task<ChannelReader<DrawLineEvent>> RequestDrawEvents(string room, CancellationToken cancellationToken)
        {
            var roomData = Room(room);
            // TODO: this might never finish if the node disconnects
            await Clients.Client(_nodeWorkerID).ReloadImage(roomData.Image);
            var channel = Channel.CreateUnbounded<DrawLineEvent>();
            var events = roomData.DrawEvents.GetEnumerator();
            for (int i = 0; i < _imageRefreshRate; ++i)
            {
                if (events.MoveNext())
                {
                    await channel.Writer.WriteAsync(events.Current);
                }
            }
            channel.Writer.Complete();
            return channel.Reader;
        }
        /// <summary>
        /// A method called by the node.js worker to send back the updated room image data.
        /// </summary>
        public void ReceiveUpdatedImage(string room, string imageData)
        {
            var roomData = Room(room);
            roomData.Image = imageData;
            TrimDrawData(room);
        }
        /// <summary>
        /// Removes the room drawing events that have already been rendered onto the image.
        /// </summary>
        private void TrimDrawData(string room)
        {
            var roomData = Room(room);
            var amount = roomData.EventsToTrim;
            int trimmed = 0;
            for (int i = 0; i < amount; ++i)
            {
                if (!roomData.DrawEvents.TryDequeue(out _))
                {
                    break;
                }
                ++trimmed;
            }
            roomData.EventsToTrim -= trimmed;
            roomData.Redrawing = false;
        }
        /// <summary>
        /// A method that handles disconnecting clients and removes data associated with those connections.
        /// </summary>
        public override Task OnDisconnectedAsync(Exception exception)
        {
            var connectionId = Context.ConnectionId;
            if (connectionId == _nodeWorkerID)
            {
                Console.WriteLine("Node worker disconnected.");
                _nodeWorkerID = null;
            }
            else
            {
                RemoveConnection(connectionId);
            }
            return base.OnDisconnectedAsync(exception);
        }
        /// <summary>
        /// A method called by a client when it initiates a connection to the server.
        /// </summary>
        /// <returns>Readable stream of drawing events to process client-side.</returns>
        public async Task<ChannelReader<DrawLineEvent>> Connect(string room, CancellationToken cancellationToken)
        {
            var connection = AddConnection(Context.ConnectionId, room);
            connection.Room = room;
            await Groups.AddToGroupAsync(Context.ConnectionId, room);
            var roomData = Room(room);
            foreach (var ev in roomData.DrawEvents)
            {
                await connection.DrawEvents.Writer.WriteAsync(ev);
            }
            await Clients.Caller.ReloadImage(roomData.Image);
            return connection.DrawEvents.Reader;
        }
        #endregion
    }
}