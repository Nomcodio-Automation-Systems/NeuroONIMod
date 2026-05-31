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
/// <pre>Most handler operations remain Unity-bound in the current environment, so many registration tests are intentionally skipped.</pre>
/// <post>The contained tests document the expected handler behavior and provide lightweight helper types for future runtime-backed coverage.</post>
public class NeuroActionHandlerTests
{
    private MockWebsocketConnection _mockWebsocket = null!;
    private List<INeuroAction> _testActions = null!;

    [SetUp]
    /// <summary>
    /// Prepares representative test actions and clears prior registrations before each test.
    /// </summary>
    /// <pre>Previous tests may have left helper state or action registrations behind.</pre>
    /// <post>The fixture contains a fresh mock websocket and representative test actions for the current test.</post>
    public void Setup()
    {
        _mockWebsocket = new MockWebsocketConnection();

        // Create test actions
        _testActions = new List<INeuroAction>
        {
            new TestAction("test_action_1", "Test Action 1"),
            new TestAction("test_action_2", "Test Action 2"),
            new TestAction("test_action_3", "Test Action 3")
        };

        // Clear any existing registrations
        ClearRegisteredActions();
    }

    [TearDown]
    /// <summary>
    /// Clears registrations after each test.
    /// </summary>
    /// <pre>A handler-oriented test has completed.</pre>
    /// <post>Any test-specific registration state is cleared for subsequent tests.</post>
    public void TearDown()
    {
        ClearRegisteredActions();
    }

    /// <summary>
    /// Test that actions can be registered successfully
    /// </summary>
    /// <pre>A representative action set exists, but the real handler remains Unity-bound in the current environment.</pre>
    /// <post>The skipped test preserves the intended successful-registration contract for future runtime-backed execution.</post>
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
    /// <pre>A representative action set exists, but the real handler remains Unity-bound in the current environment.</pre>
    /// <post>The skipped test preserves the intended duplicate-name replacement contract.</post>
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
    /// <pre>A representative action set exists, but the real handler remains Unity-bound in the current environment.</pre>
    /// <post>The skipped test preserves the intended unregister-by-name contract.</post>
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
    /// <pre>A representative action set exists, but the real handler remains Unity-bound in the current environment.</pre>
    /// <post>The skipped test preserves the intended unregister-by-reference contract.</post>
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
    /// <pre>A representative action set exists, but the real handler remains Unity-bound in the current environment.</pre>
    /// <post>The skipped test preserves the intended recently-unregistered tracking contract.</post>
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
    /// <pre>A representative action set exists, but the real handler remains Unity-bound in the current environment.</pre>
    /// <post>The skipped test preserves the intended missing-action lookup contract.</post>
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
    /// <pre>A representative action set exists, but the real handler remains Unity-bound in the current environment.</pre>
    /// <post>The skipped test preserves the intended resend behavior and graceful null-websocket handling contract.</post>
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
    /// <pre>The real handler remains Unity-bound in the current environment.</pre>
    /// <post>The skipped test preserves the intended empty-registration no-op contract.</post>
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
    /// <pre>A representative action set exists, but the real handler remains Unity-bound in the current environment.</pre>
    /// <post>The skipped test preserves the intended graceful handling of unknown unregistration requests.</post>
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
    /// <pre>Test setup or teardown may need to clear handler registration state.</pre>
    /// <post>The current placeholder performs no additional work beyond documenting the intended cleanup seam.</post>
    private void ClearRegisteredActions()
    {
        // In a real implementation, this would require access to the private fields
        // or a test-specific method to clear registrations
    }
}

/// <summary>
/// Test implementation of INeuroAction for testing purposes
/// </summary>
/// <pre>The helper action is used only within action-handler tests.</pre>
/// <post>The type provides a lightweight INeuroAction implementation suitable for non-Unity handler scenarios.</post>
public class TestAction(string name, string description) : INeuroAction
{
    /// <summary>
    /// Gets the test action name.
    /// </summary>
    /// <pre>The helper action was constructed with a stable name.</pre>
    /// <post>The property returns the action name captured during construction.</post>
    public string Name { get; } = name;

    /// <summary>
    /// Gets the action window last assigned to this helper action.
    /// </summary>
    /// <pre>The helper action may or may not have been associated with an action window.</pre>
    /// <post>The property returns the last action-window reference supplied through <see cref="SetActionWindow"/>.</post>
    public ActionWindow? ActionWindow { get; private set; }
    private readonly string _description = description;

    /// <summary>
    /// Reports that the helper action can always be added to an action window.
    /// </summary>
    /// <param name="actionWindow">The candidate action window.</param>
    /// <returns>Always <see langword="true"/> for this helper implementation.</returns>
    /// <pre>The helper action participates only in lightweight handler tests.</pre>
    /// <post>The method returns true without additional validation.</post>
    public bool CanAddToActionWindow(ActionWindow actionWindow)
    {
        return true;
    }

    /// <summary>
    /// Produces a successful validation result for the helper action.
    /// </summary>
    /// <param name="actionData">The action payload supplied by the caller.</param>
    /// <param name="data">Receives a simple success payload.</param>
    /// <returns>A successful validation result.</returns>
    /// <pre>The helper action is used in lightweight handler tests and does not require real payload validation.</pre>
    /// <post>The method returns a success result and supplies a trivial success payload.</post>
    public ExecutionResult Validate(ActionJData actionData, out object? data)
    {
        data = new { success = true };
        return ExecutionResult.Success("Validation successful");
    }

    /// <summary>
    /// Executes the helper action asynchronously with a short artificial delay.
    /// </summary>
    /// <param name="data">Optional execution payload.</param>
    /// <pre>The helper action is used in lightweight handler tests and does not require real runtime side effects.</pre>
    /// <post>The returned task completes after a short delay without throwing.</post>
    public async Cysharp.Threading.Tasks.UniTask ExecuteAsync(object? data)
    {
        await System.Threading.Tasks.Task.Delay(10);
    }

    /// <summary>
    /// Produces the websocket action description for this helper action.
    /// </summary>
    /// <returns>A websocket action with a simple one-parameter schema.</returns>
    /// <pre>The helper action was constructed with a stable name and description.</pre>
    /// <post>The returned websocket action exposes a representative schema for handler tests.</post>
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

    /// <summary>
    /// Records the action window associated with this helper action.
    /// </summary>
    /// <param name="actionWindow">The action window to associate, or null.</param>
    /// <pre>The helper action may be associated with an action window during test setup.</pre>
    /// <post>The property <see cref="ActionWindow"/> stores the supplied reference.</post>
    public void SetActionWindow(ActionWindow? actionWindow)
    {
        ActionWindow = actionWindow;
    }
}

/// <summary>
/// Mock WebsocketConnection for testing
/// </summary>
/// <pre>The helper websocket is used only within action-handler tests.</pre>
/// <post>The type records sent messages in memory for later inspection by tests.</post>
public class MockWebsocketConnection
{
    /// <summary>
    /// Gets a value indicating whether at least one message has been sent.
    /// </summary>
    /// <pre>The helper websocket may or may not have recorded outbound messages.</pre>
    /// <post>The property reports whether any send method has been invoked since the last clear.</post>
    public bool HasSentMessage { get; private set; }
    private readonly List<object> _sentMessages = [];

    /// <summary>
    /// Records a sent websocket message.
    /// </summary>
    /// <param name="message">The outbound message to record.</param>
    /// <pre>The helper websocket is being used to simulate outbound message dispatch.</pre>
    /// <post>The supplied message is recorded and <see cref="HasSentMessage"/> becomes true.</post>
    public void Send(object message)
    {
        _sentMessages.Add(message);
        HasSentMessage = true;
    }

    /// <summary>
    /// Records an immediate-send websocket message using the standard send path.
    /// </summary>
    /// <param name="message">The outbound message to record.</param>
    /// <pre>The helper websocket is being used to simulate immediate outbound message dispatch.</pre>
    /// <post>The supplied message is recorded through the normal send path.</post>
    public void SendImmediate(object message)
    {
        Send(message);
    }

    /// <summary>
    /// Clears all recorded outbound messages.
    /// </summary>
    /// <pre>The helper websocket may already contain recorded messages.</pre>
    /// <post>The recorded message list is empty and <see cref="HasSentMessage"/> is false.</post>
    public void ClearSentMessages()
    {
        _sentMessages.Clear();
        HasSentMessage = false;
    }

    /// <summary>
    /// Returns a snapshot of the recorded outbound messages.
    /// </summary>
    /// <returns>A copy of the messages recorded so far.</returns>
    /// <pre>The helper websocket may have recorded outbound messages.</pre>
    /// <post>The method returns a copy of the recorded message list.</post>
    public List<object> GetSentMessages()
    {
        return [.. _sentMessages];
    }
}