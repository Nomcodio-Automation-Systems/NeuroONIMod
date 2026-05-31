#nullable enable

using JetBrains.Annotations;
using NeuroSdk.Messages.Outgoing;
using NeuroSdk.Websocket;
using System;
using System.Diagnostics;
using NeuroMod;
using NeuroMod.Architecture;
using NeuroMod.Architecture.Events;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NeuroMod.Integration.Api;

namespace NeuroSdk.Actions;

/// <summary>
/// A wrapper class around the concept of an action window, which handles sending context,
/// registering actions, forcing actions and unregistering the actions afterwards.
/// </summary>
/// <pre>Callers create the component through <see cref="Create(UnityEngine.GameObject)"/> and configure it during the building phase.</pre>
/// <post>The action window owns a consistent lifecycle for context, action registration, forcing, results, and cleanup.</post>
/// <remarks>
/// This class follows the builder pattern and provides a fluent API for configuring action windows.
/// It automatically manages the lifecycle of actions and ensures proper cleanup when destroyed.
/// The action window state machine transitions: Building -> Registered -> (Forced) -> Ended
/// </remarks>
/// <example>
/// <code>
/// var window = ActionWindow.Create(gameObject)
///     .SetContext("Choose your next action", false)
///     .AddAction(new MoveAction())
///     .AddAction(new AttackAction())
///     .SetEnd(10f); // Auto-end after 10 seconds
/// window.Register();
/// </code>
/// </example>
[PublicAPI]
public sealed partial class ActionWindow : MonoBehaviour
{
    // Unique id for this window instance used to correlate logs
    private Guid _windowId;

    private string FormatLog(string message) => $"[Window:{_windowId}] {message}";

    private void LogInfo(string message) => NeuroLogger.Log(FormatLog(message), "ActionWindow", _windowId.ToString());
    private void LogWarn(string message) => NeuroLogger.LogWarning(FormatLog(message), "ActionWindow", _windowId.ToString());
    private void LogError(string message) => NeuroLogger.LogError(FormatLog(message), "ActionWindow", _windowId.ToString());
    private void LogDebug(string message) => NeuroLogger.LogDebug(FormatLog(message), "ActionWindow", _windowId.ToString());
    /// <summary>
    /// Separable API client used for sending context messages.
    /// </summary>
    /// <pre>The configured API facade is available.</pre>
    /// <post>The returned client is the current transport abstraction used by this action window.</post>
    private IApiClient Api => ApiClient.Instance;

    /// <summary>
    /// Event aggregator for decoupled publish/subscribe.
    /// Can be injected by a DI framework or will fall back to a singleton.
    /// </summary>
    /// <pre>The action window can publish lifecycle events through the configured event aggregator.</pre>
    /// <post>The property returns the current event aggregator used by this window.</post>
    public IEventAggregator EventAggregator { get; set; } = NeuroMod.Architecture.EventAggregator.Instance;

    /// <summary>
    /// Command manager (singleton helper). Use `CommandManager.Instance` or inject a custom one.
    /// </summary>
    /// <pre>The architecture command manager singleton is available.</pre>
    /// <post>The property returns the command manager used for command-based action-window operations.</post>
    public CommandManager CommandManager => NeuroMod.Architecture.CommandManager.Instance;
    #region Constants

    private const string ERROR_INCORRECT_CREATION = "ActionWindow should be created using Create method. This ActionWindow was either created with AddComponent or with Instantiate.";
    private const string ERROR_MULTIPLE_REGISTER = "Cannot register an ActionWindow multiple times.";
    private const string ERROR_NO_ACTIONS = "Cannot register an ActionWindow with no actions.";
    private const string ERROR_MUTATE_AFTER_REGISTER = "Cannot mutate ActionWindow after it has been registered.";
    private const string ERROR_DUPLICATE_ACTION = "Cannot add two actions with the same name to the same ActionWindow. Triggered by '{0}'";
    private const string ERROR_ACTION_IN_OTHER_WINDOW = "Cannot add action '{0}' to this ActionWindow because it is already included in another ActionWindow.";
    private const string ERROR_RESULT_BEFORE_REGISTER = "Cannot handle a result before registering the ActionWindow.";
    private const string ERROR_RESULT_AFTER_END = "Cannot handle a result after the ActionWindow has ended.";
    private const string ERROR_WEBSOCKET_NULL = "Cannot force actions - WebsocketConnection instance is null";
    private const string ERROR_FORCE_GETTERS_NULL = "Force query or state getters are null when trying to force actions";
    private const string LOG_PREFIX = "[ActionWindow]";

    #endregion Constants

    #region Creation

    private static bool _isCreatedCorrectly = false;

    /// <summary>
    /// Creates a new action window attached to the supplied parent GameObject.
    /// </summary>
    /// <param name="parent">The parent GameObject for this ActionWindow</param>
    /// <returns>A new ActionWindow instance</returns>
    /// <exception cref="ArgumentNullException">Thrown when parent is null</exception>
    /// <pre><paramref name="parent"/> references a live GameObject that will own the created component.</pre>
    /// <post>A new <see cref="ActionWindow"/> component is attached to the parent and begins in the <see cref="State.Building"/> state.</post>
    /// <example>
    /// <code>
    /// var window = ActionWindow.Create(gameObject);
    /// </code>
    /// </example>
    public static ActionWindow Create(GameObject parent)
    {
        if (parent == null)
        {
            string error = "Parent GameObject cannot be null";
            NeuroLogger.LogError($"{LOG_PREFIX} {error}", "ActionWindow");
            throw new ArgumentNullException(nameof(parent), error);
        }

        try
        {
            _isCreatedCorrectly = true;
            ActionWindow actionWindow = parent.AddComponent<ActionWindow>();
            // assign id in Awake; log creation now with temp id
            NeuroLogger.Log($"{LOG_PREFIX} Created new ActionWindow on '{parent.name}'", "ActionWindow");
            return actionWindow;
        }
        catch (Exception ex)
        {
            NeuroLogger.LogError($"Failed to create ActionWindow: {ex.Message}", "ActionWindow");
            NeuroLogger.LogException(ex, "ActionWindow.Create", "ActionWindow");
            throw;
        }
        finally
        {
            _isCreatedCorrectly = false;
        }
    }

    /// <summary>
    /// Unity Awake method - validates proper creation and initializes the component
    /// </summary>
    /// <pre>The component has just been attached and creation must have been mediated through <see cref="Create(UnityEngine.GameObject)"/>.</pre>
    /// <post>The window either initializes its trace id successfully or destroys itself when created incorrectly.</post>
    private void Awake()
    {
        if (!_isCreatedCorrectly)
        {
            NeuroLogger.LogError($"{LOG_PREFIX} {ERROR_INCORRECT_CREATION}", "ActionWindow");
            Destroy(this);
            return;
        }

        // Initialize instance id and log
        _windowId = Guid.NewGuid();
            NeuroLogger.Log($"{LOG_PREFIX} ActionWindow component initialized on '{gameObject.name}'", "ActionWindow", _windowId.ToString());
    }

    #endregion Creation

    #region State Management

    /// <summary>
    /// Represents the current state of the ActionWindow lifecycle.
    /// </summary>
    /// <pre>The action window transitions monotonically through its lifecycle states.</pre>
    /// <post>Each enum value denotes a distinct lifecycle phase used to gate valid operations.</post>
    public enum State
    {
        /// <summary>Actions are being added to the window (mutable state)</summary>
        Building,

        /// <summary>Actions have been registered with the server (immutable state)</summary>
        Registered,

        /// <summary>Actions have been forced to execute</summary>
        Forced,

        /// <summary>The window has been ended and cleaned up</summary>
        Ended
    }

    /// <summary>
    /// Gets the current lifecycle state of this action window.
    /// </summary>
    /// <pre>No input is required.</pre>
    /// <post>The returned state matches the window's current lifecycle phase.</post>
    public State CurrentState { get; private set; } = State.Building;

    /// <summary>
    /// Gets the number of actions currently in this window.
    /// </summary>
    /// <pre>No input is required.</pre>
    /// <post>The returned count matches the current number of actions owned by the window.</post>
    public int ActionCount => _actions.Count;

    /// <summary>
    /// Gets the trace identifier for this ActionWindow instance as a string.
    /// This is intended for correlating logs across the system.
    /// </summary>
    /// <pre>The window has completed initialization and has a trace identifier.</pre>
    /// <post>The returned string identifies this action window instance for diagnostics.</post>
    public string TraceId => _windowId.ToString();

    /// <summary>
    /// Gets a read-only collection of action names in this window.
    /// </summary>
    /// <pre>No input is required.</pre>
    /// <post>The returned collection snapshots the action names currently owned by the window.</post>
    public IReadOnlyList<string> ActionNames => _actions.Select(a => a.Name).ToList().AsReadOnly();

    /// <summary>
    /// Validates that the ActionWindow is still in the building state and can be mutated
    /// </summary>
    /// <returns>True if the window can be mutated, false otherwise</returns>
    /// <pre>The window may be in any lifecycle state.</pre>
    /// <post>The returned value indicates whether mutation is currently allowed under the lifecycle rules.</post>
    private bool ValidateFrozen()
    {
        if (CurrentState != State.Building)
        {
            LogError(ERROR_MUTATE_AFTER_REGISTER);
            return false;
        }
        return true;
    }

    /// <summary>
    /// Register this ActionWindow, sending an actions register to the websocket and making this window immutable.
    /// This transitions the window from Building to Registered state.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the window is not in Building state or has no actions</exception>
    /// <pre>The window is in the <see cref="State.Building"/> state and contains at least one action.</pre>
    /// <post>The configured actions are registered, the optional context is sent, and the window transitions to <see cref="State.Registered"/>.</post>
    /// <example>
    /// <code>
    /// window.AddAction(new MoveAction())
    ///       .AddAction(new AttackAction())
    ///       .Register();
    /// </code>
    /// </example>
    public void Register()
    {
        if (CurrentState != State.Building)
        {
            string error = ERROR_MULTIPLE_REGISTER;
            LogError(error);
            throw new InvalidOperationException(error);
        }

        if (_actions.Count == 0)
        {
            string error = ERROR_NO_ACTIONS;
            LogError(error);
            throw new InvalidOperationException(error);
        }

        Stopwatch sw = Stopwatch.StartNew();
        try
        {
            LogInfo($"Registering window with {_actions.Count} actions (prevState={CurrentState})");

            // Send context message if set
            if (!string.IsNullOrEmpty(_contextMessage))
            {
                Api.SendContext(_contextMessage!, _contextSilent ?? false);
                LogInfo($"Sent context message: '{_contextMessage}' (Silent: {_contextSilent ?? false})");
            }

            // Register actions with the handler
            NeuroActionHandler.RegisterActions(_actions);
            State prev = CurrentState;
            CurrentState = State.Registered;
            LogInfo($"State transition: {prev} -> {CurrentState}; Registered {_actions.Count} actions: [{string.Join(", ", ActionNames)}]; duration={sw.ElapsedMilliseconds}ms");
            try
            {
                EventAggregator?.Publish(new WindowRegisteredEvent(TraceId, ActionNames));
            }
            catch { }
        }
        catch (Exception ex)
        {
            LogError($"Failed to register actions: {ex.Message}");
            NeuroLogger.LogException(ex, "ActionWindow.Register", "ActionWindow", _windowId.ToString());
            throw;
        }
    }

    #endregion State Management

    #region Context Management

    private string? _contextMessage;
    private bool? _contextSilent;

    /// <summary>
    /// Set a context message to be sent alongside the action register.
    /// This provides context to the AI about the current situation.
    /// </summary>
    /// <param name="message">The context message to send</param>
    /// <param name="silent">Whether the message should be sent silently (not visible to user)</param>
    /// <returns>The <see cref="ActionWindow"/> itself for method chaining</returns>
    /// <exception cref="ArgumentException">Thrown when message is null, empty, or whitespace</exception>
    /// <pre>The window is still mutable and <paramref name="message"/> contains a non-empty context string.</pre>
    /// <post>The context message and silent flag are stored for use during registration.</post>
    /// <example>
    /// <code>
    /// window.SetContext("The player is in combat and needs to choose an action", false);
    /// </code>
    /// </example>
    public ActionWindow SetContext(string message, bool silent = false)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            string error = "Context message cannot be null, empty, or whitespace";
            LogError(error);
            throw new ArgumentException(error, nameof(message));
        }

        if (!ValidateFrozen())
        {
            return this;
        }

        _contextMessage = message;
        _contextSilent = silent;
        LogInfo($"Set context message: '{message}' (Silent: {silent})");
        return this;
    }

    #endregion Context Management

    #region Action Management

    private readonly List<INeuroAction> _actions = new List<INeuroAction>();

    /// <summary>
    /// Add a new action to the list of possible actions that Neuro can pick from.
    /// Actions must have unique names within the same window.
    /// </summary>
    /// <param name="action">The action to add to this window</param>
    /// <returns>The <see cref="ActionWindow"/> itself for method chaining</returns>
    /// <exception cref="ArgumentNullException">Thrown when action is null</exception>
    /// <exception cref="InvalidOperationException">Thrown when action is already in another window or has duplicate name</exception>
    /// <pre>The window is in the <see cref="State.Building"/> state and the action is either detached or already owned by this window.</pre>
    /// <post>The action is associated with this window and added to the action list when accepted.</post>
    /// <example>
    /// <code>
    /// window.AddAction(new MoveAction("move_forward", "Move the character forward"))
    ///       .AddAction(new AttackAction("attack", "Attack the nearest enemy"));
    /// </code>
    /// </example>
    public ActionWindow AddAction(INeuroAction action)
    {
        if (action == null)
        {
            string error = "Action cannot be null";
            LogError(error);
            throw new ArgumentNullException(nameof(action), error);
        }

        if (!ValidateFrozen())
        {
            return this;
        }

        // Check if action is already in another window
        if (action.ActionWindow != null)
        {
            if (action.ActionWindow != this)
            {
                string errorMsg = string.Format(ERROR_ACTION_IN_OTHER_WINDOW, action.Name);
                LogError(errorMsg);
                throw new InvalidOperationException(errorMsg);
            }
            LogWarn($"Action '{action.Name}' is already in this window");
            return this; // Already in this window
        }

        // Check if action can be added to this window
        if (!action.CanAddToActionWindow(this))
        {
            LogWarn($"Action '{action.Name}' cannot be added to this window (CanAddToActionWindow returned false)");
            return this;
        }

        // Check for duplicate action names
        if (_actions.Any(a => a.Name == action.Name))
        {
            string errorMsg = string.Format(ERROR_DUPLICATE_ACTION, action.Name);
            LogError(errorMsg);
            throw new InvalidOperationException(errorMsg);
        }

        try
        {
            action.SetActionWindow(this);
            _actions.Add(action);
                LogInfo($"Added action '{action.Name}' to window (Total: {_actions.Count})");
                try
                {
                    EventAggregator?.Publish(new ActionAddedEvent(TraceId, action.Name));
                }
                catch { }
        }
        catch (Exception ex)
        {
            LogError($"Failed to add action '{action.Name}': {ex.Message}");
            NeuroLogger.LogException(ex, "ActionWindow.AddAction", "ActionWindow", _windowId.ToString());
            throw;
        }

        return this;
    }

    /// <summary>
    /// Remove an action from this window by name (only allowed in Building state).
    /// This is useful for dynamically managing actions based on game state.
    /// </summary>
    /// <param name="actionName">The name of the action to remove</param>
    /// <returns>True if the action was found and removed, false otherwise</returns>
    /// <pre>The window is in the <see cref="State.Building"/> state and <paramref name="actionName"/> identifies a candidate action.</pre>
    /// <post>The named action is detached from this window and removed from the action list when found.</post>
    /// <example>
    /// <code>
    /// bool removed = window.RemoveAction("attack_action");
    /// if (removed) Debug.Log("Attack action was removed");
    /// </code>
    /// </example>
    public bool RemoveAction(string actionName)
    {
        if (string.IsNullOrWhiteSpace(actionName))
        {
            LogWarn("Cannot remove action with null or empty name");
            return false;
        }

        if (!ValidateFrozen())
        {
            return false;
        }

        INeuroAction actionToRemove = _actions.FirstOrDefault(a => a.Name == actionName);
        if (actionToRemove != null)
        {
            try
            {
                _actions.Remove(actionToRemove);
                actionToRemove.SetActionWindow(null);
                LogInfo($"Removed action '{actionName}' from window (Remaining: {_actions.Count})");
                return true;
            }
            catch (Exception ex)
            {
                LogError($"Failed to remove action '{actionName}': {ex.Message}");
                NeuroLogger.LogException(ex, "ActionWindow.RemoveAction", "ActionWindow", _windowId.ToString());
                return false;
            }
        }

        LogWarn($"Action '{actionName}' not found in window");
        return false;
    }

    #endregion Action Management

    #region Result Handling

    /// <summary>
    /// Process an execution result through this ActionWindow.
    /// If the result is successful, the window will be automatically ended.
    /// This is typically called automatically by NeuroAction implementations.
    /// </summary>
    /// <param name="result">The execution result to process</param>
    /// <returns>The processed execution result (unchanged)</returns>
    /// <exception cref="ArgumentNullException">Thrown when result is null</exception>
    /// <exception cref="InvalidOperationException">Thrown when called in an invalid state</exception>
    /// <pre>The window has already been registered or forced and <paramref name="result"/> contains the action execution outcome.</pre>
    /// <post>The result is published to listeners and a successful result ends the window.</post>
    /// <example>
    /// <code>
    /// var result = ExecutionResult.Success("Action completed successfully");
    /// window.Result(result); // Window will be ended automatically
    /// </code>
    /// </example>
    public ExecutionResult Result(ExecutionResult result)
    {
        if (result == null)
        {
            string error = "ExecutionResult cannot be null";
            LogError(error);
            throw new ArgumentNullException(nameof(result), error);
        }

        if (CurrentState <= State.Building)
        {
            string error = ERROR_RESULT_BEFORE_REGISTER;
            LogError(error);
            throw new InvalidOperationException(error);
        }

        if (CurrentState >= State.Ended)
        {
            string error = ERROR_RESULT_AFTER_END;
            LogError(error);
            throw new InvalidOperationException(error);
        }

        Stopwatch sw = Stopwatch.StartNew();
        try
        {
            LogInfo($"Processing result: Success={result.Successful}, Message='{result.Message}'");

            if (result.Successful)
            {
                LogInfo("Result was successful, ending window");
                End();
            }
            else
            {
                LogWarn($"Result was not successful: {result.Message}");
            }

                try
                {
                    EventAggregator?.Publish(new ActionResultEvent(TraceId, result.Successful, result.Message ?? string.Empty));
                }
                catch { }

            LogInfo($"Processed result in {sw.ElapsedMilliseconds}ms");
            return result;
        }
        catch (Exception ex)
        {
            LogError($"Error processing result: {ex.Message}");
            NeuroLogger.LogException(ex, "ActionWindow.Result", "ActionWindow", _windowId.ToString());
            throw;
        }
    }

    #endregion Result Handling

    #region Unity Lifecycle

    /// <summary>
    /// Unity Update method - handles force and end conditions every frame.
    /// Only active when the window is in Registered state.
    /// </summary>
    private void Update()
    {
        if (CurrentState != State.Registered)
        {
            return;
        }

        try
        {
            // Check force condition first (higher priority)
            if (_shouldForceFunc?.Invoke() == true)
            {
                LogInfo("Force condition met, forcing actions");
                Force();
                return; // Early exit after forcing
            }

            // Check end condition
            if (_shouldEndFunc?.Invoke() == true)
            {
                LogInfo("End condition met, ending window");
                End();
            }
        }
        catch (Exception ex)
        {
            LogError($"Error in Update: {ex.Message}");
            NeuroLogger.LogException(ex, "ActionWindow.Update", "ActionWindow", _windowId.ToString());

            // Force end the window on critical errors to prevent infinite loops
            try
            {
                LogWarn("Force ending window due to Update error");
                End();
            }
            catch (Exception endEx)
            {
                LogError($"Failed to end window after error: {endEx.Message}");
                NeuroLogger.LogException(endEx, "ActionWindow.Update.End", "ActionWindow", _windowId.ToString());
            }
        }
    }

    #endregion Unity Lifecycle
}