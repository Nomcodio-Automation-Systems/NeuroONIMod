#nullable enable

using System;
using UnityEngine;

namespace NeuroSdk.Actions;

/// <summary>
/// Partial class containing End Management functionality for ActionWindow
/// </summary>
public sealed partial class ActionWindow
{
    #region End Management

    private Func<bool>? _shouldEndFunc;

    /// <summary>
    /// Specify a condition under which the actions should be unregistered and this window closed.
    /// The condition is checked every frame while the window is in Registered state.
    /// </summary>
    /// <param name="shouldEnd">Function that returns true when the window should be ended</param>
    /// <returns>The <see cref="ActionWindow"/> itself for method chaining</returns>
    /// <exception cref="ArgumentNullException">Thrown when shouldEnd is null</exception>
    /// <example>
    /// <code>
    /// window.SetEnd(() => player.IsDead || gameOver);
    /// </code>
    /// </example>
    public ActionWindow SetEnd(Func<bool> shouldEnd)
    {
        if (shouldEnd == null)
        {
            string error = "shouldEnd function cannot be null";
            Debug.LogError($"{LOG_PREFIX} {error}");
            throw new ArgumentNullException(nameof(shouldEnd), error);
        }

        if (!ValidateFrozen())
        {
            return this;
        }

        _shouldEndFunc = shouldEnd;
        Debug.Log($"{LOG_PREFIX} Set custom end condition");
        return this;
    }

    /// <summary>
    /// Specify a time in seconds after which the actions should be unregistered and this window closed.
    /// This creates an automatic timeout for the action window.
    /// </summary>
    /// <param name="afterSeconds">Time in seconds after which to end the window (must be positive)</param>
    /// <returns>The <see cref="ActionWindow"/> itself for method chaining</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when afterSeconds is not positive</exception>
    /// <example>
    /// <code>
    /// window.SetEnd(60f); // Auto-close after 60 seconds
    /// </code>
    /// </example>
    public ActionWindow SetEnd(float afterSeconds)
    {
        if (afterSeconds <= 0)
        {
            string error = $"afterSeconds must be greater than 0, got: {afterSeconds}";
            Debug.LogError($"{LOG_PREFIX} {error}");
            throw new ArgumentOutOfRangeException(nameof(afterSeconds), afterSeconds, error);
        }

        float remainingTime = afterSeconds;
        Debug.Log($"{LOG_PREFIX} Set timed end condition: {afterSeconds} seconds");
        return SetEnd(ShouldEndAfterTime);

        bool ShouldEndAfterTime()
        {
            remainingTime -= Time.deltaTime;
            return remainingTime <= 0;
        }
    }

    /// <summary>
    /// Ends the ActionWindow, unregistering all actions and cleaning up resources.
    /// This transitions the window to the Ended state and destroys the component.
    /// </summary>
    /// <example>
    /// <code>
    /// window.End(); // Clean shutdown of the action window
    /// </code>
    /// </example>
    public void End()
    {
        if (CurrentState >= State.Ended)
        {
            Debug.LogWarning($"{LOG_PREFIX} Window is already ended");
            return;
        }

        try
        {
            Debug.Log($"{LOG_PREFIX} Ending window with {_actions.Count} actions in state {CurrentState}");

            // Unregister actions if they were registered
            if (CurrentState >= State.Registered)
            {
                NeuroActionHandler.UnregisterActions(_actions);
                Debug.Log($"{LOG_PREFIX} Unregistered {_actions.Count} actions");
            }

            // Clear all function references to prevent memory leaks
            _shouldForceFunc = null;
            _shouldEndFunc = null;
            _forceQueryGetter = null;
            _forceStateGetter = null;

            // Clear action references
            foreach (INeuroAction action in _actions)
            {
                try
                {
                    action.SetActionWindow(null!);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"{LOG_PREFIX} Failed to clear action window for '{action.Name}': {ex.Message}");
                }
            }

            CurrentState = State.Ended;
            Debug.Log($"{LOG_PREFIX} Window ended successfully");
            Destroy(this);
        }
        catch (Exception ex)
        {
            Debug.LogError($"{LOG_PREFIX} Error during End(): {ex.Message}");
            Debug.LogException(ex);
            CurrentState = State.Ended;
            Destroy(this);
        }
    }

    /// <summary>
    /// Unity OnDestroy method - ensures proper cleanup when the component is destroyed
    /// </summary>
    private void OnDestroy()
    {
        if (CurrentState != State.Ended)
        {
            Debug.Log($"{LOG_PREFIX} Component being destroyed, forcing end");
            End();
        }
    }

    #endregion End Management
}