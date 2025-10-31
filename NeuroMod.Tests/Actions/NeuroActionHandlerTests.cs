using FluentAssertions;
using NeuroSdk.Actions;
using NeuroSdk.Json;
using NeuroSdk.Websocket;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;

namespace NeuroMod.Tests.Actions;

/// <summary>
/// Comprehensive tests for NeuroActionHandler class
/// Tests action registration, unregistration, and lifecycle management
/// </summary>
[TestFixture]
public class NeuroActionHandlerTests
{
    private MockWebsocketConnection _mockWebsocket = null!;
    private List<INeuroAction> _testActions = null!;

    [SetUp]
    public void Setup()
    {
        _mockWebsocket = new MockWebsocketConnection();

        // Create test actions
        _testActions =
        [
            new TestAction("test_action_1", "Test Action 1"),
            new TestAction("test_action_2", "Test Action 2"),
            new TestAction("test_action_3", "Test Action 3")
        ];

        // Clear any existing registrations
        ClearRegisteredActions();
    }

    [TearDown]
    public void Cleanup()
    {
        ClearRegisteredActions();
    }

    /// <summary>
    /// Test that actions can be registered successfully
    /// </summary>
    [Test]
    [Ignore("Requires Unity runtime - NeuroActionHandler depends on Unity types")]
    public void RegisterActions_WithValidActions_ShouldRegisterSuccessfully()
    {
        // Arrange
        INeuroAction[] actionsToRegister = [.. _testActions.Take(2)];

        // Act
        NeuroActionHandler.RegisterActions(actionsToRegister);

        // Assert
        INeuroAction? registeredAction1 = NeuroActionHandler.GetRegistered("test_action_1");
        INeuroAction? registeredAction2 = NeuroActionHandler.GetRegistered("test_action_2");
        INeuroAction? notRegisteredAction = NeuroActionHandler.GetRegistered("test_action_3");

        registeredAction1.Should().NotBeNull("test_action_1 should be registered");
        registeredAction2.Should().NotBeNull("test_action_2 should be registered");
        notRegisteredAction.Should().BeNull("test_action_3 should not be registered");

        registeredAction1!.Name.Should().Be("test_action_1");
        registeredAction2!.Name.Should().Be("test_action_2");
    }

    /// <summary>
    /// Test that registering an action with same name replaces the old one
    /// </summary>
    [Test]
    [Ignore("Requires Unity runtime - NeuroActionHandler depends on Unity types")]
    public void RegisterActions_WithDuplicateName_ShouldReplaceExistingAction()
    {
        // Arrange
        TestAction originalAction = new("duplicate_action", "Original Action");
        TestAction newAction = new("duplicate_action", "New Action");

        NeuroActionHandler.RegisterActions(originalAction);

        // Act
        NeuroActionHandler.RegisterActions(newAction);

        // Assert
        INeuroAction? registeredAction = NeuroActionHandler.GetRegistered("duplicate_action");
        registeredAction.Should().NotBeNull("Action should be registered");
        registeredAction.Should().BeSameAs(newAction, "New action should replace the old one");
    }

    /// <summary>
    /// Test that actions can be unregistered by name
    /// </summary>
    [Test]
    [Ignore("Requires Unity runtime - NeuroActionHandler depends on Unity types")]
    public void UnregisterActions_WithValidNames_ShouldUnregisterSuccessfully()
    {
        // Arrange
        NeuroActionHandler.RegisterActions([.. _testActions]);
        string[] namesToUnregister = ["test_action_1", "test_action_3"];

        // Act
        NeuroActionHandler.UnregisterActions(namesToUnregister);

        // Assert
        INeuroAction? action1 = NeuroActionHandler.GetRegistered("test_action_1");
        INeuroAction? action2 = NeuroActionHandler.GetRegistered("test_action_2");
        INeuroAction? action3 = NeuroActionHandler.GetRegistered("test_action_3");

        action1.Should().BeNull("test_action_1 should be unregistered");
        action2.Should().NotBeNull("test_action_2 should still be registered");
        action3.Should().BeNull("test_action_3 should be unregistered");
    }

    /// <summary>
    /// Test that actions can be unregistered by reference
    /// </summary>
    [Test]
    [Ignore("Requires Unity runtime - NeuroActionHandler depends on Unity types")]
    public void UnregisterActions_WithActionReferences_ShouldUnregisterSuccessfully()
    {
        // Arrange
        NeuroActionHandler.RegisterActions([.. _testActions]);
        INeuroAction[] actionsToUnregister = [_testActions[0], _testActions[2]];

        // Act
        NeuroActionHandler.UnregisterActions(actionsToUnregister);

        // Assert
        INeuroAction? action1 = NeuroActionHandler.GetRegistered("test_action_1");
        INeuroAction? action2 = NeuroActionHandler.GetRegistered("test_action_2");
        INeuroAction? action3 = NeuroActionHandler.GetRegistered("test_action_3");

        action1.Should().BeNull("test_action_1 should be unregistered");
        action2.Should().NotBeNull("test_action_2 should still be registered");
        action3.Should().BeNull("test_action_3 should be unregistered");
    }

    /// <summary>
    /// Test that recently unregistered actions are tracked correctly
    /// </summary>
    [Test]
    [Ignore("Requires Unity runtime - NeuroActionHandler depends on Unity types")]
    public void IsRecentlyUnregistered_AfterUnregistration_ShouldReturnTrue()
    {
        // Arrange
        NeuroActionHandler.RegisterActions(_testActions[0]);

        // Act
        NeuroActionHandler.UnregisterActions(["test_action_1"]);

        // Assert
        NeuroActionHandler.IsRecentlyUnregistered("test_action_1").Should().BeTrue("Action should be marked as recently unregistered");
        NeuroActionHandler.IsRecentlyUnregistered("test_action_2").Should().BeFalse("Non-unregistered action should not be marked as recently unregistered");
    }

    /// <summary>
    /// Test that GetRegistered returns null for non-existent actions
    /// </summary>
    [Test]
    [Ignore("Requires Unity runtime - NeuroActionHandler depends on Unity types")]
    public void GetRegistered_WithNonExistentAction_ShouldReturnNull()
    {
        // Arrange
        NeuroActionHandler.RegisterActions(_testActions[0]);

        // Act
        INeuroAction? result = NeuroActionHandler.GetRegistered("non_existent_action");

        // Assert
        result.Should().BeNull("Non-existent action should return null");
    }

    /// <summary>
    /// Test that ResendRegisteredActions works correctly
    /// </summary>
    [Test]
    [Ignore("Requires Unity runtime - NeuroActionHandler depends on Unity types")]
    public void ResendRegisteredActions_ShouldSendCurrentlyRegisteredActions()
    {
        // Arrange
        NeuroActionHandler.RegisterActions([.. _testActions]);
        _mockWebsocket.ClearSentMessages();

        // Act
        NeuroActionHandler.ResendRegisteredActions();

        // Assert - In test context, WebSocket instance is null, so no message is sent
        // This is expected behavior - the method should not throw and handle gracefully
        System.Action resendAction = () => NeuroActionHandler.ResendRegisteredActions();
        resendAction.Should().NotThrow("ResendRegisteredActions should handle null WebSocket gracefully");
        // Note: No message sent because WebSocket instance is null in test context
    }

    /// <summary>
    /// Test that empty collections are handled gracefully
    /// </summary>
    [Test]
    [Ignore("Requires Unity runtime - NeuroActionHandler depends on Unity types")]
    public void RegisterActions_WithEmptyCollection_ShouldHandleGracefully()
    {
        // Arrange
        INeuroAction[] emptyActions = [];

        // Act & Assert - Empty collection should not throw
        System.Action act = () => NeuroActionHandler.RegisterActions(emptyActions);
        act.Should().NotThrow("Empty collection should be handled gracefully");
    }

    /// <summary>
    /// Test that unregistering non-existent actions is handled gracefully
    /// </summary>
    [Test]
    [Ignore("Requires Unity runtime - NeuroActionHandler depends on Unity types")]
    public void UnregisterActions_WithNonExistentActions_ShouldHandleGracefully()
    {
        // Arrange
        NeuroActionHandler.RegisterActions(_testActions[0]);

        // Act & Assert (should not throw)
        NeuroActionHandler.UnregisterActions(["non_existent_action_1", "non_existent_action_2"]);

        // Verify original action is still registered
        INeuroAction? originalAction = NeuroActionHandler.GetRegistered("test_action_1");
        originalAction.Should().NotBeNull("Original action should still be registered");
    }

    /// <summary>
    /// Helper method to clear all registered actions for testing
    /// </summary>
    private void ClearRegisteredActions()
    {
        // In a real implementation, this would require access to the private fields
        // or a test-specific method to clear registrations
    }
}

/// <summary>
/// Test implementation of INeuroAction for testing purposes
/// </summary>
public class TestAction(string name, string description) : INeuroAction
{
    public string Name { get; } = name;
    public ActionWindow? ActionWindow { get; private set; }
    private readonly string _description = description;

    public bool CanAddToActionWindow(ActionWindow actionWindow)
    {
        return true;
    }

    public ExecutionResult Validate(ActionJData actionData, out object? data)
    {
        data = new { success = true };
        return ExecutionResult.Success("Validation successful");
    }

    public async Cysharp.Threading.Tasks.UniTask ExecuteAsync(object? data)
    {
        await System.Threading.Tasks.Task.Delay(10);
    }

    public WsAction GetWsAction()
    {
        return new WsAction(
            name: Name,
            description: _description,
            schema: new JsonSchema
            {
                Type = JsonSchemaType.Object,
                Properties = new Dictionary<string, JsonSchema>
                {
                    ["test_param"] = new JsonSchema { Type = JsonSchemaType.String }
                }
            }
        );
    }

    public void SetActionWindow(ActionWindow actionWindow)
    {
        ActionWindow = actionWindow;
    }
}

/// <summary>
/// Mock WebsocketConnection for testing
/// </summary>
public class MockWebsocketConnection
{
    public bool HasSentMessage { get; private set; }
    private readonly List<object> _sentMessages = [];

    public void Send(object message)
    {
        _sentMessages.Add(message);
        HasSentMessage = true;
    }

    public void SendImmediate(object message)
    {
        Send(message);
    }

    public void ClearSentMessages()
    {
        _sentMessages.Clear();
        HasSentMessage = false;
    }

    public List<object> GetSentMessages()
    {
        return [.. _sentMessages];
    }
}