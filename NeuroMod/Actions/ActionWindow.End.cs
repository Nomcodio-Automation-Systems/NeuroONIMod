#nullable enable

using System;
using System.Diagnostics;
using UnityEngine;
using NeuroMod;

namespace NeuroSdk.Actions;

/// <summary>
/// Contains end-management functionality for <see cref="ActionWindow"/>.
/// </summary>
/// <pre>The action window may be configured with end conditions or explicit shutdown requests.</pre>
/// <post>These helpers transition the window toward cleanup while preserving lifecycle invariants.</post>
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
    /// <pre>The window is in the <see cref="State.Building"/> state and <paramref name="shouldEnd"/> is non-null.</pre>
    /// <post>The end condition is stored for later evaluation while the window is registered.</post>
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
        LogInfo($"Set custom end condition");
        return this;
    }

    /// <summary>
    /// Specify a time in seconds after which the actions should be unregistered and this window closed.
    /// This creates an automatic timeout for the action window.
    /// </summary>
    /// <param name="afterSeconds">Time in seconds after which to end the window (must be positive)</param>
    /// <returns>The <see cref="ActionWindow"/> itself for method chaining</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when afterSeconds is not positive</exception>
    /// <pre>The window is in the <see cref="State.Building"/> state and <paramref name="afterSeconds"/> is greater than zero.</pre>
    /// <post>A timed end condition is stored that will become true after the configured interval elapses.</post>
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
        LogInfo($"Set timed end condition: {afterSeconds} seconds");
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
    /// <pre>The window may be in any state prior to <see cref="State.Ended"/> and may still own registered actions.</pre>
    /// <post>Registered actions are unregistered, action ownership is cleared, the window transitions to <see cref="State.Ended"/>, and the component is destroyed.</post>
    /// <example>
    /// <code>
    /// window.End(); // Clean shutdown of the action window
    /// </code>
    /// </example>
    public void End()
    {
        if (CurrentState >= State.Ended)
        {
            LogWarn($"Window is already ended");
            return;
        }
        Stopwatch sw = Stopwatch.StartNew();
        try
        {
            LogInfo($"Ending window with {_actions.Count} actions in state {CurrentState}");

            // Unregister actions if they were registered
            if (CurrentState >= State.Registered)
            {
                NeuroActionHandler.UnregisterActions(_actions);
                LogInfo($"Unregistered {_actions.Count} actions");
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
                    action.SetActionWindow(null);
                }
                catch (Exception ex)
                {
                    LogWarn($"Failed to clear action window for '{action.Name}': {ex.Message}");
                }
            }

            State prev = CurrentState;
            CurrentState = State.Ended;
            LogInfo($"Window ended successfully; prevState={prev}; duration={sw.ElapsedMilliseconds}ms");
            Destroy(this);
        }
        catch (Exception ex)
        {
            LogError($"Error during End(): {ex.Message}");
            NeuroLogger.LogException(ex, "ActionWindow.End", "ActionWindow", _windowId.ToString());
            CurrentState = State.Ended;
            Destroy(this);
        }
    }

    /// <summary>
    /// Unity OnDestroy method - ensures proper cleanup when the component is destroyed
    /// </summary>
    /// <pre>The Unity component is being destroyed and the window may not yet be in the ended state.</pre>
    /// <post>If needed, end cleanup has been triggered before destruction completes.</post>
    private void OnDestroy()
    {
        if (CurrentState != State.Ended)
        {
            LogInfo($"Component being destroyed, forcing end");
            End();
        }
    }

    #endregion End Management
}