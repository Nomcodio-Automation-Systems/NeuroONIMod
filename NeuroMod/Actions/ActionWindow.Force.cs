#nullable enable

using NeuroSdk.Messages.Outgoing;
using NeuroSdk.Websocket;
using System;
using UnityEngine;

namespace NeuroSdk.Actions;

/// <summary>
/// Partial class containing Force Management functionality for ActionWindow
/// </summary>
public sealed partial class ActionWindow
{
    #region Force Management

    private Func<bool>? _shouldForceFunc;
    private Func<string>? _forceQueryGetter;
    private Func<string?>? _forceStateGetter;
    private bool _forceEphemeralContext;

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
        Debug.Log($"{LOG_PREFIX} Set force condition with ephemeral context: {ephemeralContext}");
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
        if (CurrentState != State.Registered)
        {
            Debug.LogWarning($"{LOG_PREFIX} Cannot force actions in state {CurrentState}");
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
            CurrentState = State.Forced;
            _shouldForceFunc = null;

            string query = _forceQueryGetter();
            string? state = _forceStateGetter();

            WebsocketConnection.Instance.Send(new ActionsForce(query, state, _forceEphemeralContext, _actions));
            Debug.Log($"{LOG_PREFIX} Forced {_actions.Count} actions with query: '{query}', state: '{state}', ephemeral: {_forceEphemeralContext}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"{LOG_PREFIX} Failed to force actions: {ex.Message}");
            Debug.LogException(ex);
            throw;
        }
    }

    #endregion Force Management
}