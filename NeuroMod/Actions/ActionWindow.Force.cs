#nullable enable

using NeuroSdk.Messages.Outgoing;
using NeuroSdk.Websocket;
using System;
using System.Diagnostics;
using UnityEngine;
using NeuroMod;

namespace NeuroSdk.Actions;

/// <summary>
/// Contains force-management functionality for <see cref="ActionWindow"/>.
/// </summary>
/// <pre>The action window may be configured with conditions that escalate from normal registration to forced selection.</pre>
/// <post>These helpers store force predicates and payloads or trigger forced action dispatch while preserving lifecycle rules.</post>
public sealed partial class ActionWindow
{
    #region Force Management

    private Func<bool>? _shouldForceFunc;
    private Func<string>? _forceQueryGetter;
    private Func<string?>? _forceStateGetter;
    private bool _forceEphemeralContext;
    
    /// <summary>
    /// When true, `Force()` will execute through the `CommandManager` using a `ForceActionsCommand`.
    /// Default is false to preserve existing behavior.
    /// </summary>
    /// <pre>No input is required.</pre>
    /// <post>The property value indicates whether forcing uses command execution or the direct transport path.</post>
    public bool UseCommandExecution { get; set; } = false;

    /// <summary>
    /// Specify a condition under which the actions should be forced.
    /// When the condition returns true, all actions in this window will be forced to execute.
    /// </summary>
    /// <param name="shouldForce">Function that returns true when actions should be forced</param>
    /// <param name="queryGetter">Function that returns the query text for the force</param>
    /// <param name="stateGetter">Function that returns the state information for the force</param>
    /// <param name="ephemeralContext">If true, the query and state won't be remembered after the action force is finished</param>
    /// <returns>The <see cref="ActionWindow"/> itself for method chaining</returns>
    /// <exception cref="ArgumentNullException">Thrown when any required parameter is null</exception>
    /// <pre>The window is in the <see cref="State.Building"/> state and the supplied delegates are non-null.</pre>
    /// <post>The force condition and force payload providers are stored for later evaluation while registered.</post>
    /// <example>
    /// <code>
    /// window.SetForce(
    ///     () => Time.time > startTime + 30f, // Force after 30 seconds
    ///     () => "Time's up! Choose quickly!",
    ///     () => "urgent_timeout",
    ///     false
    /// );
    /// </code>
    /// </example>
    public ActionWindow SetForce(Func<bool> shouldForce, Func<string> queryGetter, Func<string?> stateGetter, bool ephemeralContext = false)
    {
        if (shouldForce == null)
        {
            string error = "shouldForce function cannot be null";
            Debug.LogError($"{LOG_PREFIX} {error}");
            throw new ArgumentNullException(nameof(shouldForce), error);
        }
        if (queryGetter == null)
        {
            string error = "queryGetter function cannot be null";
            Debug.LogError($"{LOG_PREFIX} {error}");
            throw new ArgumentNullException(nameof(queryGetter), error);
        }
        if (stateGetter == null)
        {
            string error = "stateGetter function cannot be null";
            Debug.LogError($"{LOG_PREFIX} {error}");
            throw new ArgumentNullException(nameof(stateGetter), error);
        }

        if (!ValidateFrozen())
        {
            return this;
        }

        _shouldForceFunc = shouldForce;
        _forceQueryGetter = queryGetter;
        _forceStateGetter = stateGetter;
        _forceEphemeralContext = ephemeralContext;
        LogInfo($"Set force condition with ephemeral context: {ephemeralContext}");
        return this;
    }

    /// <summary>
    /// Specify a condition under which the actions should be forced using static strings.
    /// </summary>
    /// <param name="shouldForce">Function that returns true when actions should be forced</param>
    /// <param name="query">The query text for the force</param>
    /// <param name="state">The state information for the force</param>
    /// <param name="ephemeralContext">If true, the query and state won't be remembered after the action force is finished</param>
    /// <returns>The <see cref="ActionWindow"/> itself for method chaining</returns>
    /// <pre>The window is in the <see cref="State.Building"/> state and <paramref name="query"/> is non-empty.</pre>
    /// <post>Static force query and state values are wrapped and stored as deferred providers.</post>
    /// <example>
    /// <code>
    /// window.SetForce(
    ///     () => playerHealth <= 10,
    ///     "You're critically injured! Act fast!",
    ///     "critical_health",
    ///     true
    /// );
    /// </code>
    /// </example>
    public ActionWindow SetForce(Func<bool> shouldForce, string query, string? state, bool ephemeralContext = false)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            string error = "Query cannot be null, empty, or whitespace";
            Debug.LogError($"{LOG_PREFIX} {error}");
            throw new ArgumentException(error, nameof(query));
        }

        return SetForce(shouldForce, () => query, () => state, ephemeralContext);
    }

    /// <summary>
    /// Specify a time in seconds after which the actions should be forced.
    /// This creates a timeout mechanism for the action window.
    /// </summary>
    /// <param name="afterSeconds">Time in seconds after which to force actions (must be positive)</param>
    /// <param name="queryGetter">Function that returns the query text for the force</param>
    /// <param name="stateGetter">Function that returns the state information for the force</param>
    /// <param name="ephemeralContext">If true, the query and state won't be remembered after the action force is finished</param>
    /// <returns>The <see cref="ActionWindow"/> itself for method chaining</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when afterSeconds is not positive</exception>
    /// <pre>The window is in the <see cref="State.Building"/> state and <paramref name="afterSeconds"/> is greater than zero.</pre>
    /// <post>A timed force condition is stored that will become true after the configured interval elapses.</post>
    /// <example>
    /// <code>
    /// window.SetForce(15f, () => "Time's running out!", () => "timeout_warning", false);
    /// </code>
    /// </example>
    public ActionWindow SetForce(float afterSeconds, Func<string> queryGetter, Func<string?> stateGetter, bool ephemeralContext = false)
    {
        if (afterSeconds <= 0)
        {
            string error = $"afterSeconds must be greater than 0, got: {afterSeconds}";
            Debug.LogError($"{LOG_PREFIX} {error}");
            throw new ArgumentOutOfRangeException(nameof(afterSeconds), afterSeconds, error);
        }

        float remainingTime = afterSeconds;
        Debug.Log($"{LOG_PREFIX} Set timed force condition: {afterSeconds} seconds");
        return SetForce(ShouldForceAfterTime, queryGetter, stateGetter, ephemeralContext);

        bool ShouldForceAfterTime()
        {
            remainingTime -= Time.deltaTime;
            return remainingTime <= 0;
        }
    }

    /// <summary>
    /// Specify a time in seconds after which the actions should be forced using static strings.
    /// </summary>
    /// <param name="afterSeconds">Time in seconds after which to force actions (must be positive)</param>
    /// <param name="query">The query text for the force</param>
    /// <param name="state">The state information for the force</param>
    /// <param name="ephemeralContext">If true, the query and state won't be remembered after the action force is finished</param>
    /// <returns>The <see cref="ActionWindow"/> itself for method chaining</returns>
    /// <pre>The window is in the <see cref="State.Building"/> state and <paramref name="afterSeconds"/> is greater than zero.</pre>
    /// <post>A timed force condition with static query and state payloads is stored.</post>
    /// <example>
    /// <code>
    /// window.SetForce(30f, "Time's up! Make a decision!", "timeout", true);
    /// </code>
    /// </example>
    public ActionWindow SetForce(float afterSeconds, string query, string? state, bool ephemeralContext = false)
    {
        return SetForce(afterSeconds, () => query, () => state, ephemeralContext);
    }

    /// <summary>
    /// Forces the actions immediately if the window is in the registered state.
    /// This transitions the window from Registered to Forced state.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when WebSocket connection is unavailable or force getters are null</exception>
    /// <pre>The window is in the <see cref="State.Registered"/> state and valid force payload providers are configured.</pre>
    /// <post>The window transitions to <see cref="State.Forced"/> and an action-force message is sent or delegated through command execution.</post>
    /// <example>
    /// <code>
    /// if (emergencyCondition)
    /// {
    ///     window.Force(); // Immediately force action selection
    /// }
    /// </code>
    /// </example>
    public void Force()
    {
        if (UseCommandExecution)
        {
            try
            {
                CommandManager.Execute(new NeuroMod.Architecture.Commands.ForceActionsCommand(this));
                return;
            }
            catch (Exception ex)
            {
                LogError($"Command-based force failed: {ex.Message}");
                NeuroLogger.LogException(ex, "ActionWindow.Force(Command)", "ActionWindow", _windowId.ToString());
                // fall-through to built-in behavior
            }
        }

        if (CurrentState != State.Registered)
        {
            LogWarn($"Cannot force actions in state {CurrentState}");
            return;
        }

        if (WebsocketConnection.Instance == null)
        {
            string error = ERROR_WEBSOCKET_NULL;
            Debug.LogError($"{LOG_PREFIX} {error}");
            throw new InvalidOperationException(error);
        }

        if (_forceQueryGetter == null || _forceStateGetter == null)
        {
            string error = ERROR_FORCE_GETTERS_NULL;
            Debug.LogError($"{LOG_PREFIX} {error}");
            throw new InvalidOperationException(error);
        }

        try
        {
            Stopwatch sw = Stopwatch.StartNew();
            State prev = CurrentState;
            CurrentState = State.Forced;
            _shouldForceFunc = null;

            string query = _forceQueryGetter();
            string? state = _forceStateGetter();

            NeuroMod.Integration.Api.ApiClient.Send(new ActionsForce(query, state, _forceEphemeralContext, _actions));
            LogInfo($"Forced {_actions.Count} actions with query: '{query}', state: '{state}', ephemeral: {_forceEphemeralContext}; duration={sw.ElapsedMilliseconds}ms; prevState={prev}");
        }
        catch (Exception ex)
        {
            LogError($"Failed to force actions: {ex.Message}");
            NeuroLogger.LogException(ex, "ActionWindow.Force", "ActionWindow", _windowId.ToString());
            throw;
        }
    }

    #endregion Force Management
}