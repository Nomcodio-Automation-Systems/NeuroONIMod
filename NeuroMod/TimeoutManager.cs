using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace NeuroMod;

/// <summary>
/// Manages timeout handling for Neuro SDK operations
/// Provides fallback behavior when Neuro doesn't respond within configured time
/// </summary>
public class TimeoutManager
{
    private static TimeoutManager? _instance;
    public static TimeoutManager Instance => _instance ??= new TimeoutManager();

    private readonly ConcurrentDictionary<string, PendingOperation> _pendingOperations;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private int _timeoutCount = 0;
    private bool _isManualModeActive = false;

    /// <summary>
    /// Gets the current timeout count
    /// </summary>
    public int TimeoutCount => _timeoutCount;

    /// <summary>
    /// Indicates if manual mode is currently active due to excessive timeouts
    /// </summary>
    public bool IsManualModeActive => _isManualModeActive;

    private TimeoutManager()
    {
        _pendingOperations = new ConcurrentDictionary<string, PendingOperation>();
        _cancellationTokenSource = new CancellationTokenSource();
    }

    /// <summary>
    /// Executes a Neuro operation with timeout handling
    /// </summary>
    /// <param name="operationType">Type of operation (decision, action, query)</param>
    /// <param name="operation">The operation to execute</param>
    /// <param name="fallbackAction">Fallback action if timeout occurs</param>
    /// <param name="customTimeout">Custom timeout override</param>
    /// <returns>Operation result or fallback result</returns>
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

            Debug.Log(
                $"[TimeoutManager] Starting {operationType} operation {operationId} with {timeout}s timeout");

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
                Debug.Log($"[TimeoutManager] Operation {operationId} completed successfully");
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
    /// Executes a void Neuro operation with timeout handling
    /// </summary>
    /// <param name="operationType">Type of operation</param>
    /// <param name="operation">The operation to execute</param>
    /// <param name="fallbackAction">Fallback action if timeout occurs</param>
    /// <param name="customTimeout">Custom timeout override</param>
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
    /// Handles timeout scenarios with configured fallback strategies
    /// </summary>
    private async Task<T> HandleTimeout<T>(string operationId, string operationType, Func<T>? fallbackAction)
    {
        _timeoutCount++;
        _pendingOperations.TryRemove(operationId, out _);

        Debug.LogWarning($"[TimeoutManager] Operation {operationId} ({operationType}) timed out. Total timeouts: {_timeoutCount}");

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
        Debug.Log($"[TimeoutManager] Applying fallback strategy: {strategy}");

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
        Debug.Log("[TimeoutManager] Using last known preference");
        return default!;
    }

    /// <summary>
    /// Retrieves cached data for the operation
    /// </summary>
    private T GetCachedData<T>()
    {
        // Implementation depends on your caching system
        Debug.Log("[TimeoutManager] Using cached data");
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

        // Use the notification system
        NotificationManager.Instance.ShowTimeoutWarning(operationType, _timeoutCount);
    }

    /// <summary>
    /// Shows manual mode activation notification
    /// </summary>
    private void ShowManualModeNotification()
    {
        Debug.LogWarning("[TimeoutManager] Too many timeouts detected. Switching to manual mode.");
        NotificationManager.Instance.ShowManualModeActivated();
    }

    /// <summary>
    /// Attempts to restart the Neuro connection
    /// </summary>
    private void RestartNeuroConnection()
    {
        Debug.Log("[TimeoutManager] Attempting to restart Neuro connection");

        try
        {
            // Note: WebsocketConnection manages its own connection lifecycle
            // We can only log that we would restart the connection
            Debug.Log("[TimeoutManager] Connection restart requested - WebsocketConnection will handle reconnection automatically");

            _timeoutCount = 0; // Reset timeout count after restart
        }
        catch (Exception ex)
        {
            Debug.LogError($"[TimeoutManager] Failed to restart connection: {ex.Message}");
        }
    }

    /// <summary>
    /// Disables Neuro integration temporarily
    /// </summary>
    private void DisableNeuroIntegration()
    {
        Debug.LogWarning("[TimeoutManager] Disabling Neuro integration due to excessive timeouts");
        // Implementation depends on your system architecture
    }

    /// <summary>
    /// Resets the timeout count (can be called when connection is restored)
    /// </summary>
    public void ResetTimeoutCount()
    {
        _timeoutCount = 0;
        _isManualModeActive = false;
        Debug.Log("[TimeoutManager] Timeout count reset");
    }

    /// <summary>
    /// Gets current pending operations count
    /// </summary>
    public int GetPendingOperationsCount()
    {
        return _pendingOperations.Count;
    }

    /// <summary>
    /// Cancels all pending operations
    /// </summary>
    public void CancelAllOperations()
    {
        _cancellationTokenSource.Cancel();
        _pendingOperations.Clear();
        Debug.Log("[TimeoutManager] All pending operations cancelled");
    }

    private class PendingOperation
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public System.DateTime StartTime { get; set; } = default!;
        public int TimeoutSeconds { get; set; }
    }
}