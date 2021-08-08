using Microsoft.AspNetCore.SignalR;
using SkiaSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

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
        public RoomData(string id)
        {
            RoomId = id;
            ImageInfo = new SKImageInfo(800, 600);
            Image = SKImage.Create(new SKImageInfo(800, 600));
            Surface = SKSurface.Create(ImageInfo);
            Paint = new SKPaint
            {
                IsAntialias = true,
                Color = new SKColor(0, 0, 0, 255),
                StrokeWidth = 4,
                PathEffect = SKPathEffect.CreateCorner(50),
                Style = SKPaintStyle.Stroke
            };
            Path = new SKPath();
        }
        public string RoomId { get; }
        public SKImageInfo ImageInfo { get; set; }
        public SKSurface Surface { get; set; }
        public SKImage Image { get; set; }
        public SKPaint Paint { get; set; }
        public SKPath Path { get; set; }
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
        // TODO: make bounded
        public Channel<DrawLineEvent> DrawEvents { get; }
            = Channel.CreateUnbounded<DrawLineEvent>();
    }
    /// <summary>
    /// An interface describing methods that can be called on a client.
    /// </summary> 
    public interface IDrawClient
    {
        Task ReceiveChatMessage(string user, string message);
    }
    /// <summary>
    /// SignalR hub facilitating the realtime communication between all clients and the server.<br/>
    /// Contains several static properties and methods for storing persistent data related to rooms and connections.
    /// </summary>
    public class DrawHub : Hub<IDrawClient>
    {
        #region Static hub methods and properties.
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
        protected static void RedrawRoomImage(RoomData roomData)
        {
            roomData.Surface.Canvas.DrawImage(roomData.Image, 0, 0);
            roomData.Surface.Canvas.DrawPath(roomData.Path, roomData.Paint);
            roomData.Path.Reset();
            roomData.Image.Dispose();
            roomData.Image = roomData.Surface.Snapshot();
            roomData.Surface.Flush();
        }
        /// <summary>
        /// Sends the drawing event data to all users connected to the room.
        /// </summary>
        public static async Task AddDrawLineEvent(DrawLineEvent ev, string room)
        {
            // TODO: probably easier to just enqueue the event, then redraw later, locking the image data to avoid race conditions
            var roomData = Room(room);
            var path = roomData.Path;
            path.MoveTo(ev.From.X, ev.From.Y);
            path.LineTo(ev.To.X, ev.To.Y);
            RedrawRoomImage(roomData);

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
        /// A method that handles disconnecting clients and removes data associated with those connections.
        /// </summary>
        public override Task OnDisconnectedAsync(Exception exception)
        {
            var connectionId = Context.ConnectionId;
            RemoveConnection(connectionId);
            return base.OnDisconnectedAsync(exception);
        }
        /// <summary>
        /// A method called by a client when it initiates a connection to the server.
        /// Initializes the data for the client on the server.
        /// </summary>
        public async Task Connect(string room)
        {
            var connection = AddConnection(Context.ConnectionId, room);
            connection.Room = room;
            await Groups.AddToGroupAsync(Context.ConnectionId, room);
        }
        /// <summary>
        /// Called by a client after it's successfully synchronized an image.
        /// Returns a stream where DrawLineEvents are pushed in.
        /// </summary>
        /// <returns>Readable stream of drawing events to process client-side.</returns>
        public ChannelReader<DrawLineEvent> ClientBeginReceiveEvents()
        {
            return Connection(Context.ConnectionId).DrawEvents.Reader;
        }
        /// <summary>
        /// Called by a client when it needs to resynchronize the image.
        /// </summary>
        /// <param name="roomId">Room identifier</param>
        /// <returns></returns>
        public async IAsyncEnumerable<byte[]> SendImageToClient(string roomId)
        {
            var image = Room(roomId).Image.ToRasterImage().Encode(SKEncodedImageFormat.Png, 100);
            int chunkSize = 5000;
            var imageChunks =
                Enumerable
                .Range(0, (int)(image.Size - 1) / chunkSize + 1)
                .Select(i => {
                    return image.AsSpan().Slice(i * chunkSize, Math.Min(chunkSize, (int)image.Size - i * chunkSize)).ToArray();
                    });
            foreach (var s in imageChunks)
            {
                await Task.Yield();
                yield return s;
            }
        }
        #endregion
    }
}