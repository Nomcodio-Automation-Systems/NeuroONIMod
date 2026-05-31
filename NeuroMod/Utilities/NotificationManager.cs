using System.Collections.Generic;
using UnityEngine;

namespace NeuroMod;

/// <summary>
/// Simple notification system for NeuroMod events
/// Handles timeout warnings, connection status, and error messages
/// </summary>
/// <pre>
/// Notifications are emitted from production systems that respect the user configuration and cooldown rules.
/// </pre>
/// <post>
/// Notifications can be queued, deduplicated, and displayed through a singleton manager.
/// </post>
public class NotificationManager
{
    private static NotificationManager? _instance;
    public static NotificationManager Instance => _instance ??= new NotificationManager();

    /// <summary>
    /// Replace the singleton instance (intended for tests).
    /// </summary>
    /// <param name="instance">NotificationManager to use as the global instance.</param>
    /// <pre>
    /// The caller needs to replace the current singleton manager instance.
    /// </pre>
    /// <post>
    /// <see cref="Instance"/> returns the supplied manager instance.
    /// </post>
    public static void SetInstance(NotificationManager instance)
    {
        _instance = instance;
    }

    /// <summary>
    /// Separable API client used by notifications for sending context or telemetry.
    /// Tests may replace this with a test double.
    /// </summary>
    public Integration.Api.IApiClient Api { get; set; } = Integration.Api.ApiClient.Instance;

    private readonly Queue<Notification> _notificationQueue = new();
    private float _lastNotificationTime = 0f;
    private readonly float _notificationCooldown = 1f; // 1 second between notifications

    private NotificationManager()
    { }

    /// <summary>
    /// Shows a notification when the current configuration allows it.
    /// </summary>
    /// <param name="type">The notification type which can be used to gate display.</param>
    /// <param name="message">The human-readable message to display.</param>
    /// <param name="severity">Visual/severity hint used for logging and UI presentation.</param>
    /// <pre>
    /// Configuration is available and the notification message is suitable for user-facing display.
    /// </pre>
    /// <post>
    /// The notification is enqueued when enabled by configuration and type filters.
    /// </post>
    public void ShowNotification(NotificationType type, string message, NotificationSeverity severity = NotificationSeverity.Info)
    {
        NotificationsConfig? config = ConfigManager.Instance.Config?.Notifications;

        // Check if notifications are enabled
        if (config?.Enabled != true)
        {
            return;
        }

        // Check type-specific settings
        bool shouldShow = type switch
        {
            NotificationType.ConnectionStatus => config.ShowConnectionStatus,
            NotificationType.TimeoutWarning => config.ShowTimeoutWarnings,
            NotificationType.Error => config.ShowErrorMessages,
            NotificationType.Success => config.ShowSuccessMessages,
            _ => true
        };

        if (!shouldShow)
        {
            return;
        }

        Notification notification = new()
        {
            Type = type,
            Message = message,
            Severity = severity,
            DisplayDuration = config.DisplayDuration,
            Timestamp = Time.time
        };

        EnqueueNotification(notification);
    }

    /// <summary>
    /// Enqueues a timeout warning notification for the provided operation type.
    /// </summary>
    /// <param name="operationType">Logical operation type (e.g., "decision").</param>
    /// <param name="timeoutCount">Total number of timeouts observed.</param>
    /// <pre>
    /// The timeout count reflects the current escalation state for the named operation type.
    /// </pre>
    /// <post>
    /// A timeout warning notification is enqueued when allowed by configuration.
    /// </post>
    public void ShowTimeoutWarning(string operationType, int timeoutCount)
    {
        string message = $"Neuro {operationType} timed out ({timeoutCount} total timeouts). Using fallback behavior.";
        ShowNotification(NotificationType.TimeoutWarning, message, NotificationSeverity.Warning);
    }

    /// <summary>
    /// Shows a connection status notification indicating whether Neuro is
    /// currently connected.
    /// </summary>
    /// <param name="isConnected">True if connected; false if disconnected.</param>
    /// <pre>
    /// <paramref name="isConnected"/> reflects the latest known websocket state.
    /// </pre>
    /// <post>
    /// A connection-status notification is enqueued when allowed by configuration.
    /// </post>
    public void ShowConnectionStatus(bool isConnected)
    {
        string message = isConnected ? "Connected to Neuro" : "Disconnected from Neuro";
        NotificationSeverity severity = isConnected ? NotificationSeverity.Success : NotificationSeverity.Warning;
        ShowNotification(NotificationType.ConnectionStatus, message, severity);
    }

    /// <summary>
    /// Notifies the user that the system has entered manual mode due to
    /// repeated timeouts or other escalation conditions.
    /// </summary>
    /// <pre>
    /// The runtime has determined that automatic operation should be suspended.
    /// </pre>
    /// <post>
    /// A manual-mode error notification is enqueued.
    /// </post>
    public void ShowManualModeActivated()
    {
        ShowNotification(NotificationType.Error,
            "Too many timeouts detected. Switching to manual mode.",
            NotificationSeverity.Error);
    }

    /// <summary>
    /// Enqueues an error notification.
    /// </summary>
    /// <param name="message">The error message to show.</param>
    /// <pre>
    /// <paramref name="message"/> describes a user-visible failure condition.
    /// </pre>
    /// <post>
    /// An error notification is enqueued when allowed by configuration.
    /// </post>
    public void ShowError(string message)
    {
        ShowNotification(NotificationType.Error, message, NotificationSeverity.Error);
    }

    /// <summary>
    /// Enqueues a success notification.
    /// </summary>
    /// <param name="message">The success message to show.</param>
    /// <pre>
    /// <paramref name="message"/> describes a user-visible successful outcome.
    /// </pre>
    /// <post>
    /// A success notification is enqueued when allowed by configuration.
    /// </post>
    public void ShowSuccess(string message)
    {
        ShowNotification(NotificationType.Success, message, NotificationSeverity.Success);
    }

    /// <summary>
    /// Processes the notification queue and displays one notification per
    /// cooldown interval. Intended to be called from an update loop.
    /// </summary>
    /// <pre>
    /// The queue may contain pending notifications and the cooldown timer may or may not have elapsed.
    /// </pre>
    /// <post>
    /// At most one queued notification is displayed when cooldown permits.
    /// </post>
    public void ProcessNotifications()
    {
        if (_notificationQueue.Count == 0)
        {
            return;
        }

        // Respect cooldown between notifications
        if (Time.time - _lastNotificationTime < _notificationCooldown)
        {
            return;
        }

        Notification notification = _notificationQueue.Dequeue();
        DisplayNotification(notification);
        _lastNotificationTime = Time.time;
    }

    /// <summary>
    /// Adds a notification to the queue while deduplicating identical messages.
    /// </summary>
    /// <param name="notification">Notification to enqueue.</param>
    /// <pre>
    /// <paramref name="notification"/> is a fully populated notification record.
    /// </pre>
    /// <post>
    /// The notification is appended unless an identical queued entry already exists.
    /// </post>
    private void EnqueueNotification(Notification notification)
    {
        // Simple deduplication - don't add identical messages
        foreach (Notification existing in _notificationQueue)
        {
            if (existing.Message == notification.Message && existing.Type == notification.Type)
            {
                return;
            }
        }

        _notificationQueue.Enqueue(notification);
    }

    /// <summary>
    /// Displays the provided notification using Unity's logging APIs; this
    /// method may be overridden in tests to capture notifications.
    /// </summary>
    /// <param name="notification">Notification to display.</param>
    /// <pre>
    /// <paramref name="notification"/> has already passed configuration and cooldown checks.
    /// </pre>
    /// <post>
    /// The notification is emitted through the appropriate Unity logging API.
    /// </post>
    protected virtual void DisplayNotification(Notification notification)
    {
        string prefix = notification.Severity switch
        {
            NotificationSeverity.Success => "[OK]",
            NotificationSeverity.Info => "[INFO]",
            NotificationSeverity.Warning => "[WARN]",
            NotificationSeverity.Error => "[ERROR]",
            _ => "[NOTICE]"
        };

        string logMessage = $"[NeuroMod] {prefix} {notification.Message}";

        // Log to console based on severity
        switch (notification.Severity)
        {
            case NotificationSeverity.Error:
                Debug.LogError(logMessage);
                break;

            case NotificationSeverity.Warning:
                Debug.LogWarning(logMessage);
                break;

            default:
                Debug.Log(logMessage);
                break;
        }

        // TODO: Integrate with ONI's notification system when available
        // This could be extended to show in-game notifications using ONI's UI system
    }

    /// <summary>
    /// Clears all queued notifications immediately.
    /// </summary>
    /// <pre>
    /// The queue may contain pending notifications.
    /// </pre>
    /// <post>
    /// No notifications remain queued.
    /// </post>
    public void ClearNotifications()
    {
        _notificationQueue.Clear();
    }

    /// <summary>
    /// Returns the number of notifications currently queued.
    /// </summary>
    /// <returns>Count of pending notifications.</returns>
    /// <pre>
    /// The queue may contain zero or more notifications.
    /// </pre>
    /// <post>
    /// The current pending notification count is returned.
    /// </post>
    public int GetPendingNotificationCount()
    {
        return _notificationQueue.Count;
    }
}

/// <summary>
/// Represents a single notification
/// </summary>
/// <pre>
/// Property values describe one queued or displayed notification.
/// </pre>
/// <post>
/// Instances can carry notification metadata across queueing and display.
/// </post>
public class Notification
{
    public NotificationType Type { get; set; }
    public string Message { get; set; } = string.Empty;
    public NotificationSeverity Severity { get; set; }
    public int DisplayDuration { get; set; }
    public float Timestamp { get; set; }
}

/// <summary>
/// Types of notifications
/// </summary>
/// <pre>
/// Enum members categorize notification intent for filtering and display.
/// </pre>
/// <post>
/// A notification type value can be used to gate configuration-driven display behavior.
/// </post>
public enum NotificationType
{
    ConnectionStatus,
    TimeoutWarning,
    Error,
    Success,
    General
}

/// <summary>
/// Notification severity levels
/// </summary>
/// <pre>
/// Enum members define the urgency of a notification.
/// </pre>
/// <post>
/// A severity value can be used to choose display and logging behavior.
/// </post>
public enum NotificationSeverity
{
    Info,
    Success,
    Warning,
    Error
}