#nullable enable

using JetBrains.Annotations;
using NeuroSdk.Messages.Outgoing;
using NeuroSdk.Websocket;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NeuroSdk.Actions;

/// <summary>
/// A wrapper class around the concept of an action window, which handles sending context,
/// registering actions, forcing actions and unregistering the actions afterwards.
/// </summary>
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
    /// Creates a new ActionWindow. If the parent is destroyed, this ActionWindow will be automatically ended.
    /// </summary>
    /// <param name="parent">The parent GameObject for this ActionWindow</param>
    /// <returns>A new ActionWindow instance</returns>
    /// <exception cref="ArgumentNullException">Thrown when parent is null</exception>
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
            Debug.LogError($"{LOG_PREFIX} {error}");
            throw new ArgumentNullException(nameof(parent), error);
        }

        try
        {
            _isCreatedCorrectly = true;
            ActionWindow actionWindow = parent.AddComponent<ActionWindow>();
            Debug.Log($"{LOG_PREFIX} Created new ActionWindow on '{parent.name}'");
            return actionWindow;
        }
        catch (Exception ex)
        {
            Debug.LogError($"{LOG_PREFIX} Failed to create ActionWindow: {ex.Message}");
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
    private void Awake()
    {
        if (!_isCreatedCorrectly)
        {
            Debug.LogError($"{LOG_PREFIX} {ERROR_INCORRECT_CREATION}");
            Destroy(this);
            return;
        }

        Debug.Log($"{LOG_PREFIX} ActionWindow component initialized on '{gameObject.name}'");
    }

    #endregion Creation

    #region State Management

    /// <summary>
    /// Represents the current state of the ActionWindow lifecycle
    /// </summary>
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
    /// Gets the current state of this ActionWindow
    /// </summary>
    public State CurrentState { get; private set; } = State.Building;

    /// <summary>
    /// Gets the number of actions currently in this window
    /// </summary>
    public int ActionCount => _actions.Count;

    /// <summary>
    /// Gets a read-only collection of action names in this window
    /// </summary>
    public IReadOnlyList<string> ActionNames => _actions.Select(a => a.Name).ToList().AsReadOnly();

    /// <summary>
    /// Validates that the ActionWindow is still in the building state and can be mutated
    /// </summary>
    /// <returns>True if the window can be mutated, false otherwise</returns>
    private bool ValidateFrozen()
    {
        if (CurrentState != State.Building)
        {
            Debug.LogError($"{LOG_PREFIX} {ERROR_MUTATE_AFTER_REGISTER}");
            return false;
        }
        return true;
    }

    /// <summary>
    /// Register this ActionWindow, sending an actions register to the websocket and making this window immutable.
    /// This transitions the window from Building to Registered state.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the window is not in Building state or has no actions</exception>
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
            Debug.LogError($"{LOG_PREFIX} {error}");
            throw new InvalidOperationException(error);
        }

        if (_actions.Count == 0)
        {
            string error = ERROR_NO_ACTIONS;
            Debug.LogError($"{LOG_PREFIX} {error}");
            throw new InvalidOperationException(error);
        }

        try
        {
            Debug.Log($"{LOG_PREFIX} Registering window with {_actions.Count} actions");

            // Send context message if set
            if (!string.IsNullOrEmpty(_contextMessage))
            {
                Context.Send(_contextMessage!, _contextSilent ?? false);
                Debug.Log($"{LOG_PREFIX} Sent context message: '{_contextMessage}' (Silent: {_contextSilent ?? false})");
            }

            // Register actions with the handler
            NeuroActionHandler.RegisterActions(_actions);
            CurrentState = State.Registered;

            Debug.Log($"{LOG_PREFIX} Successfully registered {_actions.Count} actions: [{string.Join(", ", ActionNames)}]");
        }
        catch (Exception ex)
        {
            Debug.LogError($"{LOG_PREFIX} Failed to register actions: {ex.Message}");
            Debug.LogException(ex);
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
            Debug.LogError($"{LOG_PREFIX} {error}");
            throw new ArgumentException(error, nameof(message));
        }

        if (!ValidateFrozen())
        {
            return this;
        }

        _contextMessage = message;
        _contextSilent = silent;
        Debug.Log($"{LOG_PREFIX} Set context message: '{message}' (Silent: {silent})");
        return this;
    }

    #endregion Context Management

    #region Action Management

    private readonly List<INeuroAction> _actions = [];

    /// <summary>
    /// Add a new action to the list of possible actions that Neuro can pick from.
    /// Actions must have unique names within the same window.
    /// </summary>
    /// <param name="action">The action to add to this window</param>
    /// <returns>The <see cref="ActionWindow"/> itself for method chaining</returns>
    /// <exception cref="ArgumentNullException">Thrown when action is null</exception>
    /// <exception cref="InvalidOperationException">Thrown when action is already in another window or has duplicate name</exception>
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
            Debug.LogError($"{LOG_PREFIX} {error}");
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
                Debug.LogError($"{LOG_PREFIX} {errorMsg}");
                throw new InvalidOperationException(errorMsg);
            }
            Debug.LogWarning($"{LOG_PREFIX} Action '{action.Name}' is already in this window");
            return this; // Already in this window
        }

        // Check if action can be added to this window
        if (!action.CanAddToActionWindow(this))
        {
            Debug.LogWarning($"{LOG_PREFIX} Action '{action.Name}' cannot be added to this window (CanAddToActionWindow returned false)");
            return this;
        }

        // Check for duplicate action names
        if (_actions.Any(a => a.Name == action.Name))
        {
            string errorMsg = string.Format(ERROR_DUPLICATE_ACTION, action.Name);
            Debug.LogError($"{LOG_PREFIX} {errorMsg}");
            throw new InvalidOperationException(errorMsg);
        }

        try
        {
            action.SetActionWindow(this);
            _actions.Add(action);
            Debug.Log($"{LOG_PREFIX} Added action '{action.Name}' to window (Total: {_actions.Count})");
        }
        catch (Exception ex)
        {
            Debug.LogError($"{LOG_PREFIX} Failed to add action '{action.Name}': {ex.Message}");
            Debug.LogException(ex);
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
            Debug.LogWarning($"{LOG_PREFIX} Cannot remove action with null or empty name");
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
                actionToRemove.SetActionWindow(null!);
                Debug.Log($"{LOG_PREFIX} Removed action '{actionName}' from window (Remaining: {_actions.Count})");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LOG_PREFIX} Failed to remove action '{actionName}': {ex.Message}");
                Debug.LogException(ex);
                return false;
            }
        }

        Debug.LogWarning($"{LOG_PREFIX} Action '{actionName}' not found in window");
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
            Debug.LogError($"{LOG_PREFIX} {error}");
            throw new ArgumentNullException(nameof(result), error);
        }

        if (CurrentState <= State.Building)
        {
            string error = ERROR_RESULT_BEFORE_REGISTER;
            Debug.LogError($"{LOG_PREFIX} {error}");
            throw new InvalidOperationException(error);
        }

        if (CurrentState >= State.Ended)
        {
            string error = ERROR_RESULT_AFTER_END;
            Debug.LogError($"{LOG_PREFIX} {error}");
            throw new InvalidOperationException(error);
        }

        try
        {
            Debug.Log($"{LOG_PREFIX} Processing result: Success={result.Successful}, Message='{result.Message}'");

            if (result.Successful)
            {
                Debug.Log($"{LOG_PREFIX} Result was successful, ending window");
                End();
            }
            else
            {
                Debug.LogWarning($"{LOG_PREFIX} Result was not successful: {result.Message}");
            }

            return result;
        }
        catch (Exception ex)
        {
            Debug.LogError($"{LOG_PREFIX} Error processing result: {ex.Message}");
            Debug.LogException(ex);
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
                Debug.Log($"{LOG_PREFIX} Force condition met, forcing actions");
                Force();
                return; // Early exit after forcing
            }

            // Check end condition
            if (_shouldEndFunc?.Invoke() == true)
            {
                Debug.Log($"{LOG_PREFIX} End condition met, ending window");
                End();
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"{LOG_PREFIX} Error in Update: {ex.Message}");
            Debug.LogException(ex);

            // Force end the window on critical errors to prevent infinite loops
            try
            {
                Debug.LogWarning($"{LOG_PREFIX} Force ending window due to Update error");
                End();
            }
            catch (Exception endEx)
            {
                Debug.LogError($"{LOG_PREFIX} Failed to end window after error: {endEx.Message}");
                Debug.LogException(endEx);
            }
        }
    }

    #endregion Unity Lifecycle
}