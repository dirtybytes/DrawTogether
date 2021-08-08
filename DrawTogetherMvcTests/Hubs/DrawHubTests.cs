using Microsoft.AspNetCore.SignalR;
using Moq;
using NUnit.Framework;
using System.Threading.Tasks;

namespace DrawTogetherMvc.Hubs.Tests
{
    [TestFixture]
    public class DrawHubTests
    {
        public DrawHub CreateTestHub(string connectionId)
        {
            var hub = new DrawHub();
            var hubCallerContextMock = new Mock<HubCallerContext>();
            hubCallerContextMock.Setup(c => c.ConnectionId).Returns(connectionId);
            hub.Context = hubCallerContextMock.Object;
            var drawClientMock = new Mock<IDrawClient>();
            var hubCallerClientsMock = Mock.Of<IHubCallerClients<IDrawClient>>(c => c.Caller == drawClientMock.Object);
            hub.Clients = hubCallerClientsMock;
            var groupManagerMock = new Mock<IGroupManager>();
            hub.Groups = groupManagerMock.Object;
            return hub;
        }
        [SetUp]
        public void Setup()
        {
        }
        [Test]
        [TestCase("1")]
        public async Task ConnectTest(string room)
        {
            string connectionId = "mockConnection";
            var hub = CreateTestHub(connectionId);
            await hub.Connect(room);
            var reader = DrawHub.Connection(connectionId).DrawEvents.Reader;
            Assert.AreEqual(room, DrawHub.Connection(connectionId).Room);

            DrawLineEvent ev1 = new DrawLineEvent
            {
                From = new Point { X = 100, Y = 200 },
                To = new Point { X = 300, Y = 400 }
            };
            DrawLineEvent ev2 = new DrawLineEvent
            {
                From = new Point { X = 500, Y = 600 },
                To = new Point { X = 700, Y = 800 }
            };

            await hub.SendDrawLine(ev1);
            await hub.SendDrawLine(ev2);

            var ev1Returned = await reader.ReadAsync();
            var ev2Returned = await reader.ReadAsync();
            Assert.AreEqual(ev1.From, ev1Returned.From);
            Assert.AreEqual(ev1.To, ev1Returned.To);
            Assert.AreEqual(ev2.From, ev2Returned.From);
            Assert.AreEqual(ev2.To, ev2Returned.To);
        }
    }
}