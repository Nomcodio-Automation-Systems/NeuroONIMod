#nullable enable

using System;
using System.Collections.Generic;
using NeuroMod;

namespace NeuroSdk.Actions;

#if DEBUG
/// <summary>
/// Contains debug and utility functionality for <see cref="ActionWindow"/>.
/// </summary>
/// <pre>The action window instance exists and its internal state may be inspected without mutation.</pre>
/// <post>These helpers expose diagnostics and validation without changing the action window lifecycle.</post>
public sealed partial class ActionWindow
{
    #region Debug and Utility Methods

    /// <summary>
    /// Returns a detailed string representation of the current ActionWindow state for debugging.
    /// </summary>
    /// <returns>String representation containing state, action count, and configuration details</returns>
    /// <pre>The current action window state can be inspected safely.</pre>
    /// <post>A stable diagnostic string describing the current window state is returned.</post>
    /// <example>
    /// <code>
    /// Debug.Log(window.ToString()); // Outputs: "ActionWindow [State: Registered, Actions: 3, ...]"
    /// </code>
    /// </example>
    public override string ToString()
    {
        return $"ActionWindow [State: {CurrentState}, Actions: {_actions.Count}, " +
               $"HasContext: {!string.IsNullOrEmpty(_contextMessage)}, " +
               $"HasForceCondition: {_shouldForceFunc != null}, " +
               $"HasEndCondition: {_shouldEndFunc != null}, " +
               $"GameObject: '{gameObject.name}']";
    }

    /// <summary>
    /// Validates the current state of the ActionWindow for debugging and testing purposes.
    /// Checks that the window state is consistent and all required dependencies are available.
    /// </summary>
    /// <returns>True if the window is in a valid state, false if there are issues</returns>
    /// <pre>The current action window state and supporting fields can be inspected without side effects.</pre>
    /// <post>The method returns whether the current state satisfies the expected lifecycle invariants.</post>
    /// <example>
    /// <code>
    /// if (!window.ValidateState())
    /// {
    ///     Debug.LogError("ActionWindow is in an invalid state!");
    /// }
    /// </code>
    /// </example>
    public bool ValidateState()
    {
        try
        {
            switch (CurrentState)
            {
                case State.Building:
                    // Building state is always valid
                    return true;

                case State.Registered:
                    // Must have actions to be registered
                    if (_actions.Count == 0)
                    {
                        LogError($"ValidateState: Registered state but no actions");
                        return false;
                    }
                    return true;

                case State.Forced:
                    // Must have actions and force getters
                    if (_actions.Count == 0)
                    {
                        LogError($"ValidateState: Forced state but no actions");
                        return false;
                    }
                    if (_forceQueryGetter == null)
                    {
                        LogError($"ValidateState: Forced state but no query getter");
                        return false;
                    }
                    return true;

                case State.Ended:
                    // Ended state is always valid
                    return true;

                default:
                    LogError($"ValidateState: Unknown state {CurrentState}");
                    return false;
            }
        }
        catch (Exception ex)
        {
            LogError($"Error validating state: {ex.Message}");
            NeuroLogger.LogException(ex, "ActionWindow.ValidateState", "ActionWindow", _windowId.ToString());
            return false;
        }
    }

    /// <summary>
    /// Gets diagnostic information about the current ActionWindow for debugging.
    /// </summary>
    /// <returns>Dictionary containing diagnostic information</returns>
    /// <pre>The current action window state can be inspected for debugging purposes.</pre>
    /// <post>A dictionary snapshot describing the current action window state is returned.</post>
    /// <example>
    /// <code>
    /// var diagnostics = window.GetDiagnostics();
    /// foreach (var kvp in diagnostics)
    /// {
    ///     Debug.Log($"{kvp.Key}: {kvp.Value}");
    /// }
    /// </code>
    /// </example>
    public Dictionary<string, object> GetDiagnostics()
    {
        return new Dictionary<string, object>
        {
            ["State"] = CurrentState.ToString(),
            ["ActionCount"] = _actions.Count,
            ["ActionNames"] = string.Join(", ", ActionNames),
            ["HasContext"] = !string.IsNullOrEmpty(_contextMessage),
            ["ContextMessage"] = _contextMessage ?? "None",
            ["ContextSilent"] = _contextSilent ?? false,
            ["HasForceCondition"] = _shouldForceFunc != null,
            ["HasEndCondition"] = _shouldEndFunc != null,
            ["ForceEphemeralContext"] = _forceEphemeralContext,
            ["GameObject"] = gameObject.name,
            ["IsValid"] = ValidateState()
        };
    }

    #endregion Debug and Utility Methods
}
#endif