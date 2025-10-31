using System.Collections.Generic;
using UnityEngine;

namespace NeuroMod;

/// <summary>
/// Simple notification system for NeuroMod events
/// Handles timeout warnings, connection status, and error messages
/// </summary>
public class NotificationManager
{
    private static NotificationManager? _instance;
    public static NotificationManager Instance => _instance ??= new NotificationManager();

    private readonly Queue<Notification> _notificationQueue = new();
    private float _lastNotificationTime = 0f;
    private readonly float _notificationCooldown = 1f; // 1 second between notifications

    private NotificationManager()
    { }

    /// <summary>
    /// Show a notification based on configuration settings
    /// </summary>
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
    /// Show a timeout warning notification
    /// </summary>
    public void ShowTimeoutWarning(string operationType, int timeoutCount)
    {
        string message = $"Neuro {operationType} timed out ({timeoutCount} total timeouts). Using fallback behavior.";
        ShowNotification(NotificationType.TimeoutWarning, message, NotificationSeverity.Warning);
    }

    /// <summary>
    /// Show connection status notification
    /// </summary>
    public void ShowConnectionStatus(bool isConnected)
    {
        string message = isConnected ? "Connected to Neuro" : "Disconnected from Neuro";
        NotificationSeverity severity = isConnected ? NotificationSeverity.Success : NotificationSeverity.Warning;
        ShowNotification(NotificationType.ConnectionStatus, message, severity);
    }

    /// <summary>
    /// Show manual mode activation notification
    /// </summary>
    public void ShowManualModeActivated()
    {
        ShowNotification(NotificationType.Error,
            "Too many timeouts detected. Switching to manual mode.",
            NotificationSeverity.Error);
    }

    /// <summary>
    /// Show error notification
    /// </summary>
    public void ShowError(string message)
    {
        ShowNotification(NotificationType.Error, message, NotificationSeverity.Error);
    }

    /// <summary>
    /// Show success notification
    /// </summary>
    public void ShowSuccess(string message)
    {
        ShowNotification(NotificationType.Success, message, NotificationSeverity.Success);
    }

    /// <summary>
    /// Process notification queue (should be called from Update or similar)
    /// </summary>
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
    /// Add notification to queue with deduplication
    /// </summary>
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
    /// Display the notification (currently uses Debug.Log, can be extended for UI)
    /// </summary>
    private void DisplayNotification(Notification notification)
    {
        string prefix = notification.Severity switch
        {
            NotificationSeverity.Success => "✅",
            NotificationSeverity.Info => "ℹ️",
            NotificationSeverity.Warning => "⚠️",
            NotificationSeverity.Error => "❌",
            _ => "📢"
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
    /// Clear all pending notifications
    /// </summary>
    public void ClearNotifications()
    {
        _notificationQueue.Clear();
    }

    /// <summary>
    /// Get the number of pending notifications
    /// </summary>
    public int GetPendingNotificationCount()
    {
        return _notificationQueue.Count;
    }
}

/// <summary>
/// Represents a single notification
/// </summary>
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
public enum NotificationSeverity
{
    Info,
    Success,
    Warning,
    Error
}