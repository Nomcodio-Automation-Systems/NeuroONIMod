using System;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using NeuroMod.Integration.Api;
using NeuroSdk.Messages.Outgoing;
using NeuroSdk.Websocket;
using NUnit.Framework;

namespace NeuroMod.Tests.Integration.Api
{
    [TestFixture]
    public class ApiClientTests
    {
        [SetUp]
        public void SetUp()
        {
            ApiClient.TestSendOverride = null;
        }

        [TearDown]
        public void TearDown()
        {
            ApiClient.TestSendOverride = null;
        }

        [Test]
        public void SendContext_InvokesTestOverride_WhenOverrideProvided()
        {
            WsMessage? captured = null;
            ApiClient.TestSendOverride = (Context builder) => captured = builder.GetWsMessage();

            ApiClient.SendContext("hello world", true);

            captured.Should().NotBeNull();
            captured!.Command.Should().Be("context");

            var data = JObject.FromObject(captured.Data!);
            data["message"]!.Value<string>().Should().Be("hello world");
            data["silent"]!.Value<bool>().Should().BeTrue();
        }

        [Test]
        public void BuildContextMessage_Returns_WsMessage_WithExpectedData()
        {
            WsMessage msg = ApiClient.BuildContextMessage("test message", true);
            msg.Command.Should().Be("context");

            var data = JObject.FromObject(msg.Data!);
            data["message"]!.Value<string>().Should().Be("test message");
            data["silent"]!.Value<bool>().Should().BeTrue();
        }
    }
}
