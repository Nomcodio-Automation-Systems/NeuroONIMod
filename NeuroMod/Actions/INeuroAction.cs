#nullable enable

using Cysharp.Threading.Tasks;
using JetBrains.Annotations;
using NeuroSdk.Websocket;

namespace NeuroSdk.Actions;

[UsedImplicitly(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature)]
/// <summary>
/// Defines a Neuro action that can be registered, validated, and executed through an action window.
/// </summary>
/// <pre>An implementing action exposes stable metadata, validation logic, and execution behavior for the SDK.</pre>
/// <post>The SDK can register the action, validate incoming payloads, execute the action, and associate it with an action window lifecycle.</post>
public interface INeuroAction
{
    /// <summary>
    /// Gets the unique action name used for registration and dispatch.
    /// </summary>
    /// <pre>Implementations expose a stable protocol name.</pre>
    /// <post>The returned value uniquely identifies the action for registration and dispatch.</post>
    string Name { get; }

    /// <summary>
    /// Gets the current action window that owns this action, if any.
    /// </summary>
    /// <pre>The action may or may not currently belong to an action window.</pre>
    /// <post>The property returns the current owning action window or null when the action is detached.</post>
    ActionWindow? ActionWindow { get; }

    /// <summary>
    /// Determines whether the action can be added to the specified action window.
    /// </summary>
    /// <param name="actionWindow">The candidate action window.</param>
    /// <returns><see langword="true"/> when the action can be added; otherwise, <see langword="false"/>.</returns>
    /// <pre><paramref name="actionWindow"/> identifies the target action window being configured.</pre>
    /// <post>The method returns whether the action is eligible to join the supplied window without mutating action state.</post>
    bool CanAddToActionWindow(ActionWindow actionWindow);

    /// <summary>
    /// Validates the incoming action payload and produces parsed execution data.
    /// </summary>
    /// <param name="actionData">The incoming raw action payload.</param>
    /// <param name="data">The parsed payload object for execution when validation succeeds.</param>
    /// <returns>An execution result describing whether the payload is valid.</returns>
    /// <pre><paramref name="actionData"/> contains the raw payload received for this action.</pre>
    /// <post>The method returns validation status and outputs parsed execution data when applicable.</post>
    ExecutionResult Validate(ActionJData actionData, out object? data);

    /// <summary>
    /// Executes the action asynchronously using the parsed payload.
    /// </summary>
    /// <param name="data">The parsed execution payload, if any.</param>
    /// <returns>A task representing the asynchronous execution.</returns>
    /// <pre><paramref name="data"/> contains the parsed payload produced during validation or <see langword="null"/> when no payload is required.</pre>
    /// <post>The action's execution behavior has been started and completes when the returned task finishes.</post>
    UniTask ExecuteAsync(object? data);

    /// <summary>
    /// Gets the WebSocket registration payload for this action.
    /// </summary>
    /// <returns>A serializable action descriptor for registration.</returns>
    /// <pre>The action exposes registration metadata such as name, description, and schema.</pre>
    /// <post>A <see cref="WsAction"/> is returned for outbound registration.</post>
    WsAction GetWsAction();

    /// <summary>
    /// Sets or clears the active action window for this action.
    /// </summary>
    /// <param name="actionWindow">The owning action window, or <see langword="null"/> to clear ownership.</param>
    /// <pre>The SDK is coordinating action window ownership changes for this action.</pre>
    /// <post>The action is associated with the supplied window or detached when <see langword="null"/> is provided.</post>
    void SetActionWindow(ActionWindow? actionWindow);
}