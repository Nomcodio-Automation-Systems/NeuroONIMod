#nullable enable

using Cysharp.Threading.Tasks;
using JetBrains.Annotations;
using NeuroSdk.Json;
using NeuroSdk.Websocket;
using System;

namespace NeuroSdk.Actions;

[PublicAPI]
/// <summary>
/// Provides the base implementation for Neuro actions.
/// </summary>
/// <pre>Derived types provide action metadata plus validation and execution behavior.</pre>
/// <post>Consumers receive a consistent action implementation with managed action-window ownership and registration metadata.</post>
public abstract class BaseNeuroAction : INeuroAction
{
    /// <summary>
    /// Gets the current action window that owns this action, if any.
    /// </summary>
    /// <pre>The action may or may not currently belong to an action window.</pre>
    /// <post>The property returns the current owning action window or null when the action is detached.</post>
    public ActionWindow? ActionWindow { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="BaseNeuroAction"/> class.
    /// </summary>
    /// <pre>The action is being created without an owning action window.</pre>
    /// <post>The action starts detached from any action window.</post>
    protected BaseNeuroAction()
    {
        ActionWindow = null;
    }

    [Obsolete("Setting the action window is now handled by the Neuro SDK. Please use the parameterless constructor instead.")]
    /// <summary>
    /// Initializes a new instance of the <see cref="BaseNeuroAction"/> class with an initial action window.
    /// </summary>
    /// <param name="actionWindow">The initial owning action window.</param>
    /// <pre>This overload is retained for backward compatibility with older construction paths.</pre>
    /// <post>The action starts with the supplied action window association.</post>
    protected BaseNeuroAction(ActionWindow? actionWindow)
    {
        ActionWindow = actionWindow;
    }

    /// <summary>
    /// Gets the unique action name.
    /// </summary>
    /// <pre>Derived actions expose a stable protocol name.</pre>
    /// <post>The returned value uniquely identifies the action for registration and dispatch.</post>
    public abstract string Name { get; }

    /// <summary>
    /// Gets the action description used during registration.
    /// </summary>
    /// <pre>Derived actions expose human-readable registration metadata.</pre>
    /// <post>The returned value describes the action for remote consumers.</post>
    protected abstract string Description { get; }

    /// <summary>
    /// Gets the optional JSON schema used to validate action parameters.
    /// </summary>
    /// <pre>Derived actions may or may not require structured input.</pre>
    /// <post>The returned schema describes the accepted input payload, or null when no schema is required.</post>
    protected abstract JsonSchema? Schema { get; }

    /// <summary>
    /// Determines whether the action can be added to the specified action window.
    /// </summary>
    /// <param name="actionWindow">The target action window.</param>
    /// <returns><see langword="true"/> by default.</returns>
    /// <pre><paramref name="actionWindow"/> identifies the candidate window for registration.</pre>
    /// <post>The method returns eligibility without mutating action state.</post>
    public virtual bool CanAddToActionWindow(ActionWindow actionWindow)
    {
        return true;
    }

    ExecutionResult INeuroAction.Validate(ActionJData actionData, out object? parsedData)
    {
        ExecutionResult result = Validate(actionData, out parsedData);

        return ActionWindow != null ? ActionWindow.Result(result) : result;
    }

    UniTask INeuroAction.ExecuteAsync(object? data)
    {
        return ExecuteAsync(data);
    }

    /// <summary>
    /// Builds the WebSocket descriptor used to register the action.
    /// </summary>
    /// <returns>A <see cref="WsAction"/> containing the action metadata.</returns>
    /// <pre>The action exposes a stable name, description, and optional schema.</pre>
    /// <post>A new registration payload describing the action is returned.</post>
    public virtual WsAction GetWsAction()
    {
        return new WsAction(Name, Description, Schema);
    }

    protected abstract ExecutionResult Validate(ActionJData actionData, out object? parsedData);

    protected abstract UniTask ExecuteAsync(object? data);

    void INeuroAction.SetActionWindow(ActionWindow? actionWindow)
    {
        if (actionWindow is null)
        {
            ActionWindow = null;
            return;
        }

        if (ActionWindow != null)
        {
            if (ActionWindow != actionWindow)
            {
                Debug.LogError("Cannot set the action window for this action, it is already set.");
            }

            return;
        }

        ActionWindow = actionWindow;
    }
}