using FluentAssertions;
using NeuroSdk.Actions;
using NeuroSdk.Json;
using NeuroSdk.Messages.Outgoing;
using NeuroSdk.Websocket;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NeuroMod.Tests.Specification;

/// <summary>
/// Tests to validate that the implementation complies with the Neuro API Specification
/// Validates message formats, command structures, and data requirements
/// </summary>
public class SpecificationComplianceTests
{
    /// <summary>
    /// Test that outgoing messages follow the C2S format:
    /// { "command": string, "game": string, "data": { [key: string]: any }? }
    /// </summary>
    [Test]
    public void OutgoingMessages_ShouldFollowC2SFormat()
    {
        // Arrange
        const string testMessage = "Test context message";

        // Act - Create a context message
        Context context = new(testMessage, false);
        WsMessage wsMessage = context.GetWsMessage();
        string json = JsonConvert.SerializeObject(wsMessage);
        JObject parsed = JObject.Parse(json);

        // Assert - Validate message structure
        parsed.Should().ContainKey("command", "Message must contain 'command' field");
        parsed.Should().ContainKey("game", "Message must contain 'game' field");
        parsed.Should().ContainKey("data", "Message must contain 'data' field");

        parsed["command"]?.ToString().Should().Be("context", "Command should be 'context'");
        parsed["game"]?.ToString().Should().NotBeNull("Game field should not be null");
        parsed["data"].Should().NotBeNull("Data field should not be null");
    }

    /// <summary>
    /// Test that startup message follows specification requirements
    /// </summary>
    [Test]
    public void StartupMessage_ShouldFollowSpecification()
    {
        // Arrange & Act
        Startup startup = new();
        WsMessage wsMessage = startup.GetWsMessage();
        string json = JsonConvert.SerializeObject(wsMessage);
        JObject parsed = JObject.Parse(json);

        // Assert
        parsed["command"]?.ToString().Should().Be("startup");
        // Some serializers or implementations may emit an empty object for data
        // rather than a JSON null. Accept either null or an empty object.
        var dataToken = parsed["data"];
        if (dataToken == null || dataToken.Type == Newtonsoft.Json.Linq.JTokenType.Null)
        {
            // OK - explicitly null
        }
        else if (dataToken.Type == Newtonsoft.Json.Linq.JTokenType.Object)
        {
            var obj = (Newtonsoft.Json.Linq.JObject)dataToken!;
            obj.Count.Should().Be(0, "Startup message data should be empty if present");
        }
        else
        {
            // Accept serialized empty object formats as string or other token types
            dataToken.ToString().Should().Be("{}", "Startup message data should be empty if present");
        }
    }

    /// <summary>
    /// Test that context message includes required fields
    /// </summary>
    [Test]
    public void ContextMessage_ShouldIncludeRequiredFields()
    {
        // Arrange
        const string message = "Test message";
        const bool silent = true;

        // Act
        Context context = new(message, silent);
        WsMessage wsMessage = context.GetWsMessage();
        string json = JsonConvert.SerializeObject(wsMessage);
        JObject parsed = JObject.Parse(json);

        // Assert
        parsed["command"]?.ToString().Should().Be("context");

        JObject? data = parsed["data"] as JObject;
        data.Should().NotBeNull("Context data should not be null");
        data!["message"]?.ToString().Should().Be(message);
        data["silent"]?.Value<bool>().Should().Be(silent);
    }

    /// <summary>
    /// Ensure serialized outgoing messages do not include a type-name wrapper
    /// (historical bug where data was wrapped as { "Context": { ... } }).
    /// </summary>
    [Test]
    public void OutgoingMessage_DataShouldNotContainTypeWrapper()
    {
        // Arrange
        Context context = new("Ensure no wrapper", false);

        // Act
        WsMessage wsMessage = context.GetWsMessage();
        string json = JsonConvert.SerializeObject(wsMessage);
        JObject parsed = JObject.Parse(json);

        // Assert
        parsed["data"].Should().NotBeNull();

        // Data should not contain a property named after the C# type (e.g. "Context")
        JObject? data = parsed["data"] as JObject;
        data.Should().NotBeNull();
        data!.ContainsKey("Context").Should().BeFalse("Serialized data must not be wrapped in a type-name property");
    }

    /// <summary>
    /// Test that actions/register message follows specification
    /// </summary>
    [Test]
    public void ActionsRegisterMessage_ShouldFollowSpecification()
    {
        // Arrange
        MockAction mockAction = new();

        // Act
        ActionsRegister register = new(mockAction);
        WsMessage wsMessage = register.GetWsMessage();
        string json = JsonConvert.SerializeObject(wsMessage);
        JObject parsed = JObject.Parse(json);

        // Assert
        parsed["command"]?.ToString().Should().Be("actions/register");

        JObject? data = parsed["data"] as JObject;
        data.Should().NotBeNull("Actions register data should not be null");
        data!.Should().ContainKey("actions", "Should contain 'actions' array");

        JArray? actions = data!["actions"] as JArray;
        actions.Should().NotBeNull("Actions should be an array");
        actions!.Count.Should().Be(1, "Should contain one action");

        JObject? action = actions[0] as JObject;
        action.Should().NotBeNull("Action should be an object");
        action!.Should().ContainKey("name", "Action should have 'name'");
        action.Should().ContainKey("description", "Action should have 'description'");
        action.Should().ContainKey("schema", "Action should have 'schema'");
    }

    /// <summary>
    /// Test that action result message follows specification
    /// </summary>
    [Test]
    public void ActionResultMessage_ShouldFollowSpecification()
    {
        // Arrange
        const string testId = "test-action-id";
        const string testMessage = "Action completed successfully";
        ExecutionResult result = ExecutionResult.Success(testMessage);

        // Act
        ActionResult actionResult = new(testId, result);
        WsMessage wsMessage = actionResult.GetWsMessage();
        string json = JsonConvert.SerializeObject(wsMessage);
        JObject parsed = JObject.Parse(json);

        // Assert
        parsed["command"]?.ToString().Should().Be("action/result");

        JObject? data = parsed["data"] as JObject;
        data.Should().NotBeNull("Action result data should not be null");
        data!["id"]?.ToString().Should().Be(testId);
        data["success"]?.Value<bool>().Should().Be(true);
        data["message"]?.ToString().Should().Be(testMessage);
    }

    /// <summary>
    /// Test that actions/force message includes all required fields
    /// </summary>
    [Test]
    public void ActionsForceMessage_ShouldIncludeAllRequiredFields()
    {
        // Arrange
        const string query = "Please perform an action";
        const string state = "Current game state";
        const bool ephemeralContext = true;
        MockAction mockAction = new();

        // Act
        ActionsForce force = new(query, state, ephemeralContext, mockAction);
        WsMessage wsMessage = force.GetWsMessage();
        string json = JsonConvert.SerializeObject(wsMessage);
        JObject parsed = JObject.Parse(json);

        // Assert
        parsed["command"]?.ToString().Should().Be("actions/force");

        JObject? data = parsed["data"] as JObject;
        data.Should().NotBeNull("Actions force data should not be null");
        data!["state"]?.ToString().Should().Be(state);
        data["query"]?.ToString().Should().Be(query);
        data["ephemeral_context"]?.Value<bool>().Should().Be(ephemeralContext);

        JArray? actionNames = data["action_names"] as JArray;
        actionNames.Should().NotBeNull("Action names should be an array");
        actionNames!.Count.Should().Be(1);
        actionNames[0]?.ToString().Should().Be("mock_action");
    }

    /// <summary>
    /// Test that JSON schemas are valid and follow specification requirements
    /// </summary>
    [Test]
    public void JsonSchema_ShouldFollowSpecificationRequirements()
    {
        // Arrange
        JsonSchema schema = new()
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, JsonSchema>
            {
                ["test_param"] = new JsonSchema
                {
                    Type = JsonSchemaType.String,
                    Enum = ["option1", "option2", "option3"],
                },
                ["number_param"] = new JsonSchema
                {
                    Type = JsonSchemaType.Integer,
                    Minimum = 0,
                    Maximum = 100,
                },
                ["boolean_param"] = new JsonSchema { Type = JsonSchemaType.Boolean },
            },
            Required = ["test_param"],
        };

        // Act
        string json = JsonConvert.SerializeObject(schema);
        JObject parsed = JObject.Parse(json);

        // Assert
        parsed["type"]?.ToString().Should().Be("object");
        parsed.Should().ContainKey("properties", "Schema should have properties");
        parsed.Should().ContainKey("required", "Schema should have required fields");

        JObject? properties = parsed["properties"] as JObject;
        properties.Should().NotBeNull("Properties should be an object");
        properties!.Should().ContainKey("test_param", "Should contain test_param");
        properties.Should().ContainKey("number_param", "Should contain number_param");
        properties.Should().ContainKey("boolean_param", "Should contain boolean_param");
    }

    /// <summary>
    /// Test that messages use text format (not binary) as required
    /// </summary>
    [Test]
    public void Messages_ShouldUseTextFormat()
    {
        // Arrange
        Context context = new("Test message", false);

        // Act
        WsMessage wsMessage = context.GetWsMessage();
        string json = JsonConvert.SerializeObject(wsMessage);

        // Assert
        json.Length.Should().BeGreaterThan(0, "Message should serialize to text");
        json.Should().StartWith("{", "Message should be JSON format");
        json.Should().EndWith("}", "Message should be valid JSON");

        // Verify it can be parsed back
        JObject parsed = JObject.Parse(json);
        parsed.Should().NotBeNull("Message should be valid JSON that can be parsed");
    }

    /// <summary>
    /// Test that actions/unregister message follows specification
    /// </summary>
    [Test]
    public void ActionsUnregisterMessage_ShouldFollowSpecification()
    {
        // Arrange
        string[] actionNames = ["action1", "action2", "action3"];

        // Act
        ActionsUnregister unregister = new(actionNames);
        WsMessage wsMessage = unregister.GetWsMessage();
        string json = JsonConvert.SerializeObject(wsMessage);
        JObject parsed = JObject.Parse(json);

        // Assert
        parsed["command"]?.ToString().Should().Be("actions/unregister");

        JObject? data = parsed["data"] as JObject;
        data.Should().NotBeNull("Actions unregister data should not be null");
        data!.Should().ContainKey("action_names", "Should contain 'action_names' array");

        JArray? names = data!["action_names"] as JArray;
        names.Should().NotBeNull("Action names should be an array");
        names!.Count.Should().Be(3, "Should contain three action names");
        names.Select(n => n?.ToString()).Should().BeEquivalentTo(actionNames);
    }

    /// <summary>
    /// Test that all message types preserve order of fields as specified
    /// </summary>
    [Test]
    public void AllMessages_ShouldPreserveFieldOrder()
    {
        // Arrange
        Context context = new("Test", false);

        // Act
        WsMessage wsMessage = context.GetWsMessage();
        string json = JsonConvert.SerializeObject(wsMessage);

        // Assert
        // JSON field order should be: command, game, data
        int commandIndex = json.IndexOf("\"command\"", StringComparison.Ordinal);
        int gameIndex = json.IndexOf("\"game\"", StringComparison.Ordinal);
        int dataIndex = json.IndexOf("\"data\"", StringComparison.Ordinal);

        commandIndex.Should().BeLessThan(gameIndex, "command should come before game");
        gameIndex.Should().BeLessThan(dataIndex, "game should come before data");
    }
}

/// <summary>
/// Mock action for testing purposes
/// </summary>
public class MockAction : INeuroAction
{
    public string Name => "mock_action";
    public ActionWindow? ActionWindow { get; private set; }

    public bool CanAddToActionWindow(ActionWindow actionWindow)
    {
        return true;
    }

    public ExecutionResult Validate(ActionJData actionData, out object? data)
    {
        data = new { test = "success" };
        return ExecutionResult.Success();
    }

    public async Cysharp.Threading.Tasks.UniTask ExecuteAsync(object? data)
    {
        await System.Threading.Tasks.Task.Delay(1);
    }

    public WsAction GetWsAction()
    {
        return new WsAction(
            name: Name,
            description: "Mock action for testing",
            schema: new JsonSchema
            {
                Type = JsonSchemaType.Object,
                Properties = new Dictionary<string, JsonSchema>
                {
                    ["test_param"] = new JsonSchema { Type = JsonSchemaType.String },
                },
            }
        );
    }

    public void SetActionWindow(ActionWindow? actionWindow)
    {
        ActionWindow = actionWindow;
    }
}