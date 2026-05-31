using System.Collections.Generic;
using FluentAssertions;
using NeuroSdk.Actions;
using NeuroSdk.Json;
using NeuroSdk.Messages.API;
using NeuroSdk.Messages.Incoming;
using NeuroSdk.Messages.Outgoing;
using NeuroSdk.Websocket;
using NeuroMod.Tests.Specification;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace NeuroMod.Tests.Randy;

/// <summary>
/// Tests the concrete NeuroMod websocket contract that Randy's mock server actually sends and consumes.
/// </summary>
public class RandyProtocolContractTests
{
    [SetUp]
    public void SetUp()
    {
        NeuroActionHandler.UnregisterActions("mock_action", RandyAwareAction.ActionName);
    }

    [TearDown]
    public void TearDown()
    {
        NeuroActionHandler.UnregisterActions("mock_action", RandyAwareAction.ActionName);
    }

    [Test]
    /// <summary>
    /// Verifies that NeuroMod emits the exact action registration envelope Randy consumes.
    /// </summary>
    /// <post>The serialized payload contains a data.actions array with the expected action metadata.</post>
    public void ActionsRegister_ShouldMatchRandyExpectedEnvelope()
    {
        ActionsRegister register = new(new MockAction());
        string json = JsonConvert.SerializeObject(register.GetWsMessage());
        JObject parsed = JObject.Parse(json);

        parsed["command"]?.ToString().Should().Be("actions/register");
        parsed["data"]?["actions"].Should().NotBeNull();
        parsed["data"]!["actions"]!.Type.Should().Be(JTokenType.Array);
        parsed["data"]!["actions"]![0]!["name"]?.ToString().Should().Be("mock_action");
        parsed["data"]!["actions"]![0]!["schema"]!["type"]?.ToString().Should().Be("object");
    }

    [Test]
    /// <summary>
    /// Verifies that NeuroMod emits the exact actions/force fields Randy reads.
    /// </summary>
    /// <post>The serialized payload includes the state, query, ephemeral_context, and action_names fields Randy reads.</post>
    public void ActionsForce_ShouldMatchRandyExpectedEnvelope()
    {
        ActionsForce force = new("Please act", "Current state", true, new MockAction());
        string json = JsonConvert.SerializeObject(force.GetWsMessage());
        JObject parsed = JObject.Parse(json);

        parsed["command"]?.ToString().Should().Be("actions/force");
        parsed["data"]!["state"]?.ToString().Should().Be("Current state");
        parsed["data"]!["query"]?.ToString().Should().Be("Please act");
        parsed["data"]!["ephemeral_context"]?.Value<bool>().Should().BeTrue();
        parsed["data"]!["action_names"]!.Values<string>().Should().Equal("mock_action");
    }

    [Test]
    /// <summary>
    /// Verifies that NeuroMod accepts Randy's re-register request without additional payload data.
    /// </summary>
    /// <post>The handler validates Randy's command successfully with a null payload.</post>
    public void ActionsReregisterAll_ShouldValidateRandyCommand()
    {
        IIncomingMessageHandler handler = new ActionsReregisterAll();

        ExecutionResult result = handler.Validate("actions/reregister_all", new MessageJData(null), out object? parsedData);

        handler.CanHandle("actions/reregister_all").Should().BeTrue();
        result.Successful.Should().BeTrue();
        parsedData.Should().BeNull();
    }

    [Test]
    /// <summary>
    /// Verifies that NeuroMod accepts Randy's stringified JSON action payload format.
    /// </summary>
    /// <post>The incoming action handler validates a Randy-shaped action request and resolves the registered action.</post>
    public void ActionHandler_ShouldAcceptRandyStringifiedPayload()
    {
        NeuroActionHandler.RegisterActions(new RandyAwareAction());
        IIncomingMessageHandler handler = new NeuroSdk.Messages.Incoming.Action();
        JObject payload = JObject.Parse("""
        {
          "id": "randy-123",
          "name": "randy_contract_action",
          "data": "{\"room\":\"lab\"}"
        }
        """);

        ExecutionResult result = handler.Validate("action", new MessageJData(payload), out object? parsedData);

        result.Successful.Should().BeTrue();
        parsedData.Should().BeOfType<NeuroSdk.Messages.Incoming.Action.ParsedData>();

        NeuroSdk.Messages.Incoming.Action.ParsedData typedData = (NeuroSdk.Messages.Incoming.Action.ParsedData)parsedData!;
        typedData.Id.Should().Be("randy-123");
        typedData.Action.Should().NotBeNull();
        typedData.Action!.Name.Should().Be(RandyAwareAction.ActionName);
        typedData.Data.Should().NotBeNull();
    }

    private sealed class RandyAwareAction : INeuroAction
    {
        public const string ActionName = "randy_contract_action";

        public string Name => ActionName;

        public ActionWindow? ActionWindow => null;

        public bool CanAddToActionWindow(ActionWindow actionWindow)
        {
            return true;
        }

        public ExecutionResult Validate(ActionJData actionData, out object? data)
        {
            actionData.Data.Should().NotBeNull();
            actionData.Data!["room"]?.Value<string>().Should().Be("lab");
            data = actionData.Data!.ToString(Formatting.None);
            return ExecutionResult.Success();
        }

        public Cysharp.Threading.Tasks.UniTask ExecuteAsync(object? data)
        {
            return Cysharp.Threading.Tasks.UniTask.CompletedTask;
        }

        public WsAction GetWsAction()
        {
            return new WsAction(
                name: Name,
                description: "Randy contract action",
                schema: new JsonSchema
                {
                    Type = JsonSchemaType.Object,
                    Properties = new Dictionary<string, JsonSchema>
                    {
                        ["room"] = new JsonSchema { Type = JsonSchemaType.String },
                    },
                });
        }

        public void SetActionWindow(ActionWindow? actionWindow)
        {
        }
    }
}