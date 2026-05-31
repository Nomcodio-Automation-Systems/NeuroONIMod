using NUnit.Framework;
using Moq;
using NeuroMod.Integration.Api;
using NeuroSdk.Messages.Outgoing;
using NeuroSdk.Websocket;

namespace NeuroMod.Tests.Integration.Api
{
    [TestFixture]
    public class ApiClientInstanceTests
    {
        private IApiClient? _originalInstance;

        [SetUp]
        public void SetUp()
        {
            _originalInstance = ApiClient.Instance;
        }

        [TearDown]
        public void TearDown()
        {
            // Restore original instance to avoid test cross-contamination
            if (_originalInstance != null)
                ApiClient.Instance = _originalInstance;
        }

        [Test]
        public void SendContext_ForwardsTo_Instance()
        {
            var mock = new Mock<IApiClient>();
            ApiClient.Instance = mock.Object;

            ApiClient.SendContext("hello world", true);

            mock.Verify(m => m.SendContext("hello world", true, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()), Times.Once);
        }

        [Test]
        public void Send_Forwards_OutgoingMessageBuilder_To_Instance()
        {
            var mock = new Mock<IApiClient>();
            ApiClient.Instance = mock.Object;

            var msg = new ActionResult("test-id", ExecutionResult.Success("ok"));
            ApiClient.Send(msg);

            mock.Verify(m => m.Send(msg, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()), Times.Once);
        }

        [Test]
        public void SendImmediate_Forwards_OutgoingMessageBuilder_To_Instance()
        {
            var mock = new Mock<IApiClient>();
            ApiClient.Instance = mock.Object;

            var msg = new ActionResult("test-id", ExecutionResult.Success("ok"));
            ApiClient.SendImmediate(msg);

            mock.Verify(m => m.SendImmediate(msg, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()), Times.Once);
        }
    }
}
