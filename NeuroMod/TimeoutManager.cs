using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace NeuroMod;

/// <summary>
/// Manages timeout handling for Neuro SDK operations.
/// </summary>
/// <pre>Timeout-related configuration is available via <see cref="ConfigManager"/> when operations are executed.</pre>
/// <post>Operations are tracked, timed out when necessary, and may trigger fallback or escalation behavior.</post>
public class TimeoutManager
{
    private static TimeoutManager? _instance;

    /// <summary>
    /// Gets the singleton timeout manager instance.
    /// </summary>
    public static TimeoutManager Instance => _instance ??= new TimeoutManager();

    /// <summary>
    /// Replaces the singleton instance.
    /// </summary>
    /// <param name="instance">The timeout manager instance to use.</param>
    /// <pre><paramref name="instance"/> is a non-null timeout manager, typically provided by tests.</pre>
    /// <post>Subsequent access to <see cref="Instance"/> returns the supplied manager.</post>
    public static void SetInstance(TimeoutManager instance)
    {
        _instance = instance;
    }

    private readonly ConcurrentDictionary<string, PendingOperation> _pendingOperations;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private int _timeoutCount = 0;
    private bool _isManualModeActive = false;

    /// <summary>
    /// Separable API client used by the manager. Tests may override this to intercept sends.
    /// </summary>
    protected virtual Integration.Api.IApiClient Api => Integration.Api.ApiClient.Instance;

    /// <summary>
    /// Separable NotificationManager used for display and user-facing messages. Tests may replace it.
    /// </summary>
    protected virtual NotificationManager Notifications => NotificationManager.Instance;

    /// <summary>
    /// Gets the current timeout count.
    /// </summary>
    public int TimeoutCount => _timeoutCount;

    /// <summary>
    /// Gets a value indicating whether manual mode is currently active because of excessive timeouts.
    /// </summary>
    public bool IsManualModeActive => _isManualModeActive;

    private TimeoutManager()
    {
        _pendingOperations = new ConcurrentDictionary<string, PendingOperation>();
        _cancellationTokenSource = new CancellationTokenSource();
    }

    /// <summary>
    /// Executes an asynchronous Neuro operation with configured timeout handling
    /// and applies fallback strategies when a timeout occurs.
    /// </summary>
    /// <typeparam name="T">Return type of the operation.</typeparam>
    /// <param name="operationType">Type of operation (for example, "decision", "action", "query").</param>
    /// <param name="operation">Delegate that performs the operation and returns a <see cref="Task{TResult}"/>.</param>
    /// <param name="fallbackAction">Optional fallback function to compute a result on timeout or error.</param>
    /// <param name="customTimeout">Optional per-call timeout in seconds; when null, configuration values are used.</param>
    /// <returns>A task that completes with the operation result or the fallback result when a timeout occurs.</returns>
    /// <pre>The supplied delegates are valid and any optional custom timeout is appropriate for the operation type.</pre>
    /// <post>The pending operation is removed from tracking and a result or fallback value is returned.</post>
    public async Task<T> ExecuteWithTimeout<T>(
        string operationType,
        Func<Task<T>> operation,
        Func<T>? fallbackAction = null,
        int? customTimeout = null)
    {
        string operationId = Guid.NewGuid().ToString();
        int timeout = GetTimeoutForOperation(operationType, customTimeout);

        try
        {
            // Register pending operation
            PendingOperation pendingOp = new()
            {
                Id = operationId,
                Type = operationType,
                StartTime = System.DateTime.UtcNow,
                TimeoutSeconds = timeout,
            };

            _pendingOperations.TryAdd(operationId, pendingOp);

            NeuroLogger.Log($"[TimeoutManager] Starting {operationType} operation {operationId} with {timeout}s timeout", "TimeoutManager");

            // Execute operation with timeout
            using CancellationTokenSource timeoutToken = new(TimeSpan.FromSeconds(timeout));
            using CancellationTokenSource combinedToken = CancellationTokenSource.CreateLinkedTokenSource(
                _cancellationTokenSource.Token, timeoutToken.Token);
            Task<T> task = operation();
            Task completedTask = await Task.WhenAny(task, Task.Delay(timeout * 1000, combinedToken.Token));

            if (completedTask == task)
            {
                // Operation completed successfully
                _pendingOperations.TryRemove(operationId, out _);
                NeuroLogger.Log($"[TimeoutManager] Operation {operationId} completed successfully", "TimeoutManager");
                return await task;
            }
            else
            {
                // Operation timed out
                return await HandleTimeout(operationId, operationType, fallbackAction);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[TimeoutManager] Operation {operationId} failed: {ex.Message}");
            _pendingOperations.TryRemove(operationId, out _);

            if (fallbackAction != null)
            {
                return fallbackAction();
            }
            throw;
        }
    }

    /// <summary>
    /// Executes a fire-and-forget Neuro operation with timeout handling and an
    /// optional fallback action invoked on timeout.
    /// </summary>
    /// <param name="operationType">Type identifier used to lookup timeout and fallback strategy.</param>
    /// <param name="operation">Asynchronous operation to run.</param>
    /// <param name="fallbackAction">Optional fallback action to invoke when a timeout occurs.</param>
    /// <param name="customTimeout">Optional custom timeout in seconds.</param>
    /// <returns>A task that completes when the operation and any fallback have finished.</returns>
    /// <pre>The supplied operation can be executed asynchronously and the optional fallback action may be invoked safely.</pre>
    /// <post>The operation has either completed or a fallback action has been applied after timeout handling.</post>
    public async Task ExecuteWithTimeout(
        string operationType,
        Func<Task> operation,
        System.Action? fallbackAction = null,
        int? customTimeout = null)
    {
        bool fallback()
        {
            fallbackAction?.Invoke();
            return true;
        }

        await ExecuteWithTimeout<bool>(
            operationType,
            async () =>
            {
                await operation();
                return true;
            },
fallback,
            customTimeout);
    }

    /// <summary>
    /// Internal: handles a timed-out operation, applying the configured fallback
    /// strategy and performing any configured escalation.
    /// </summary>
    private async Task<T> HandleTimeout<T>(string operationId, string operationType, Func<T>? fallbackAction)
    {
        _timeoutCount++;
        _pendingOperations.TryRemove(operationId, out _);

        NeuroLogger.LogWarning($"[TimeoutManager] Operation {operationId} ({operationType}) timed out. Total timeouts: {_timeoutCount}", "TimeoutManager");

        // Show warning if configured
        if (ConfigManager.Instance.Config?.Timeout?.ShowTimeoutWarnings == true)
        {
            ShowTimeoutWarning(operationType);
        }

        // Apply fallback strategy
        string fallbackStrategy = GetFallbackStrategy(operationType);
        T result = await ApplyFallbackStrategy(fallbackStrategy, fallbackAction);

        // Check for escalation
        CheckEscalation();

        return result;
    }

    /// <summary>
    /// Gets the appropriate timeout for an operation type
    /// </summary>
    private int GetTimeoutForOperation(string operationType, int? customTimeout)
    {
        if (customTimeout.HasValue)
        {
            return customTimeout.Value;
        }

        TimeoutConfig? config = ConfigManager.Instance.Config?.Timeout;
        if (config == null)
        {
            return 10; // Default fallback
        }

        return operationType.ToLower() switch
        {
            "decision" => config.DecisionTimeout,
            "action" => config.ActionTimeout,
            "query" => config.QueryTimeout,
            _ => config.GlobalTimeout
        };
    }

    /// <summary>
    /// Gets the fallback strategy for an operation type
    /// </summary>
    private string GetFallbackStrategy(string operationType)
    {
        System.Collections.Generic.Dictionary<string, string>? strategies = ConfigManager.Instance.Config?.Timeout?.FallbackStrategies;
        if (strategies != null && strategies.TryGetValue(operationType, out string strategy))
        {
            return strategy;
        }
        return "cancel_and_wait"; // Default fallback
    }

    /// <summary>
    /// Applies the specified fallback strategy
    /// </summary>
    private async Task<T> ApplyFallbackStrategy<T>(string strategy, Func<T>? fallbackAction)
    {
        NeuroLogger.Log($"[TimeoutManager] Applying fallback strategy: {strategy}", "TimeoutManager");

        switch (strategy.ToLower())
        {
            case "use_last_known_preference":
                return GetLastKnownPreference<T>();

            case "cancel_and_wait":
                await Task.Delay(1000); // Brief pause
                return default!;

            case "use_cached_data":
                return GetCachedData<T>();

            case "custom_fallback":
                return fallbackAction != null ? fallbackAction() : default!;

            default:
                Debug.LogWarning($"[TimeoutManager] Unknown fallback strategy: {strategy}");
                return default!;
        }
    }

    /// <summary>
    /// Retrieves last known preference for the operation type
    /// </summary>
    private T GetLastKnownPreference<T>()
    {
        // Implementation depends on your preference caching system
        NeuroLogger.Log("[TimeoutManager] Using last known preference", "TimeoutManager");
        return default!;
    }

    /// <summary>
    /// Retrieves cached data for the operation
    /// </summary>
    private T GetCachedData<T>()
    {
        // Implementation depends on your caching system
        NeuroLogger.Log("[TimeoutManager] Using cached data", "TimeoutManager");
        return default!;
    }

    /// <summary>
    /// Checks if escalation is needed based on timeout count
    /// </summary>
    private void CheckEscalation()
    {
        TimeoutConfig? config = ConfigManager.Instance.Config?.Timeout;
        if (config == null)
        {
            return;
        }

        if (_timeoutCount >= config.EscalationThreshold)
        {
            ApplyEscalation(config.EscalationAction);
        }
    }

    /// <summary>
    /// Applies escalation action when timeout threshold is reached
    /// </summary>
    private void ApplyEscalation(string escalationAction)
    {
        Debug.LogWarning($"[TimeoutManager] Escalation triggered: {escalationAction}");

        switch (escalationAction.ToLower())
        {
            case "switch_to_manual_mode":
                _isManualModeActive = true;
                ShowManualModeNotification();
                break;

            case "restart_connection":
                RestartNeuroConnection();
                break;

            case "disable_neuro_integration":
                DisableNeuroIntegration();
                break;

            default:
                Debug.LogWarning($"[TimeoutManager] Unknown escalation action: {escalationAction}");
                break;
        }
    }

    /// <summary>
    /// Displays timeout warning to the player
    /// </summary>
    private void ShowTimeoutWarning(string operationType)
    {
        Debug.LogWarning($"[TimeoutManager] Neuro {operationType} timed out. Using fallback behavior.");

        // Use the notification system (seam for tests)
        Notifications.ShowTimeoutWarning(operationType, _timeoutCount);
    }

    /// <summary>
    /// Shows manual mode activation notification
    /// </summary>
    private void ShowManualModeNotification()
    {
        Debug.LogWarning("[TimeoutManager] Too many timeouts detected. Switching to manual mode.");
        Notifications.ShowManualModeActivated();
    }

    /// <summary>
    /// Attempts to restart the Neuro connection
    /// </summary>
    private void RestartNeuroConnection()
    {
        NeuroLogger.Log("[TimeoutManager] Attempting to restart Neuro connection", "TimeoutManager");

        try
        {
            // Note: WebsocketConnection manages its own connection lifecycle
            // We can only log that we would restart the connection
            NeuroLogger.Log("[TimeoutManager] Connection restart requested - WebsocketConnection will handle reconnection automatically", "TimeoutManager");

            _timeoutCount = 0; // Reset timeout count after restart
        }
        catch (Exception ex)
        {
            NeuroLogger.LogError($"[TimeoutManager] Failed to restart connection: {ex.Message}", "TimeoutManager");
        }
    }

    /// <summary>
    /// Disables Neuro integration temporarily
    /// </summary>
    private void DisableNeuroIntegration()
    {
        NeuroLogger.LogWarning("[TimeoutManager] Disabling Neuro integration due to excessive timeouts", "TimeoutManager");
        // Implementation depends on your system architecture
    }

    /// <summary>
    /// Resets internal timeout counters and clears manual mode.
    /// </summary>
    /// <pre>The Neuro connection is considered healthy again and timeout escalation can be cleared.</pre>
    /// <post>The timeout count is reset and manual mode is deactivated.</post>
    public void ResetTimeoutCount()
    {
        _timeoutCount = 0;
        _isManualModeActive = false;
        NeuroLogger.Log("[TimeoutManager] Timeout count reset", "TimeoutManager");
    }

    /// <summary>
    /// Returns the number of currently tracked pending operations.
    /// </summary>
    /// <returns>Count of pending operations.</returns>
    /// <pre>Pending operation tracking is active within the timeout manager.</pre>
    /// <post>The current number of tracked pending operations is returned without mutation.</post>
    public int GetPendingOperationsCount()
    {
        return _pendingOperations.Count;
    }

    /// <summary>
    /// Cancels and clears all pending operations immediately.
    /// </summary>
    /// <pre>There may be tracked operations waiting on timeout handling.</pre>
    /// <post>The cancellation token source is signaled and all tracked pending operations are cleared.</post>
    public void CancelAllOperations()
    {
        _cancellationTokenSource.Cancel();
        _pendingOperations.Clear();
        NeuroLogger.Log("[TimeoutManager] All pending operations cancelled", "TimeoutManager");
    }

    /// <summary>
    /// Internal structure representing an outstanding operation tracked for timeouts.
    /// </summary>
    private class PendingOperation
    {
        /// <summary>
        /// Unique identifier for the operation instance.
        /// </summary>
        /// <pre>The pending-operation object represents one tracked timeout record.</pre>
        /// <post>The property stores the unique identifier of the tracked operation.</post>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Logical type of the operation (decision/action/query).
        /// </summary>
        /// <pre>The pending-operation object represents one tracked timeout record.</pre>
        /// <post>The property stores the logical operation category.</post>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// UTC time when the operation started.
        /// </summary>
        /// <pre>The pending-operation object represents one tracked timeout record.</pre>
        /// <post>The property stores the UTC timestamp when tracking began.</post>
        public System.DateTime StartTime { get; set; } = default!;

        /// <summary>
        /// Timeout in seconds for this operation.
        /// </summary>
        /// <pre>The pending-operation object represents one tracked timeout record.</pre>
        /// <post>The property stores the timeout threshold for the tracked operation.</post>
        public int TimeoutSeconds { get; set; }
    }
}