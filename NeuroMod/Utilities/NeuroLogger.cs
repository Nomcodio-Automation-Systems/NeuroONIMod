using NeuroSdk.Messages.Outgoing;
using System;

namespace NeuroMod;

/// <summary>
/// Custom logging utility for the NeuroMod that uses simple Debug.Log format
/// Format: [tag] message (same as other ONI mods for consistency)
/// </summary>
public static class NeuroLogger
{
    /// <summary>
    /// Enable console output for external debug console mods
    /// </summary>
    public static bool EnableConsoleOutput => NeuroModConfig.EnableConsoleOutput;

    /// <summary>
    /// Enable verbose debug logging
    /// </summary>
    public static bool EnableDebugLogging => NeuroModConfig.EnableDebugLogging;

    /// <summary>
    /// Format a log message with tag only (simplified format like other ONI mods)
    /// Format: [tag] message
    /// </summary>
    private static string FormatMessage(string message, string tag)
    {
        return $"[{tag}] {message}";
    }

    /// <summary>
    /// Outputs a message using Unity Debug.Log directly
    /// Uses the same simple format as other successful log messages in the game
    /// </summary>
    private static void OutputLog(string formattedMessage, LogLevel level)
    {
        try
        {
            switch (level)
            {
                case LogLevel.Warning:
                    Debug.LogWarning(formattedMessage);
                    break;

                case LogLevel.Error:
                    Debug.LogError(formattedMessage);
                    break;

                default:
                    Debug.Log(formattedMessage);
                    break;
            }
        }
        catch (System.Exception ex)
        {
            // If Unity Debug fails, try System.Console as fallback
            try
            {
                Console.WriteLine($"[LOGGER ERROR] Failed to use Debug.Log: {ex.Message}");
                Console.WriteLine(formattedMessage);
            }
            catch
            {
                // If everything fails, there's nothing we can do
            }
        }
    }

    /// <summary>
    /// Log level enumeration for output routing
    /// </summary>
    private enum LogLevel
    {
        Info,
        Warning,
        Error,
        DebugLevel,
    }

    /// <summary>
    /// Logs an informational message
    /// </summary>
    /// <param name="message">The message to log</param>
    /// <param name="tag">Optional tag for categorization</param>
    public static void Log(string message, string tag = "NeuroMod")
    {
        string formattedMessage = FormatMessage(message, tag);
        OutputLog(formattedMessage, LogLevel.Info);
    }

    /// <summary>
    /// Logs a warning message
    /// </summary>
    /// <param name="message">The warning message to log</param>
    /// <param name="tag">Optional tag for categorization</param>
    public static void LogWarning(string message, string tag = "NeuroMod")
    {
        string formattedMessage = FormatMessage(message, tag);
        OutputLog(formattedMessage, LogLevel.Warning);
    }

    /// <summary>
    /// Logs an error message
    /// </summary>
    /// <param name="message">The error message to log</param>
    /// <param name="tag">Optional tag for categorization</param>
    public static void LogError(string message, string tag = "NeuroMod")
    {
        string formattedMessage = FormatMessage(message, tag);
        OutputLog(formattedMessage, LogLevel.Error);
    }

    /// <summary>
    /// Logs a debug message (only if debug logging is enabled)
    /// When debug logging is enabled, automatically outputs to Console mods
    /// </summary>
    /// <param name="message">The debug message to log</param>
    /// <param name="tag">Optional tag for categorization</param>
    public static void LogDebug(string message, string tag = "NeuroMod")
    {
        if (!EnableDebugLogging)
        {
            return;
        }

        string formattedMessage = FormatMessage(message, tag);
        OutputLog(formattedMessage, LogLevel.DebugLevel);
    }

    /// <summary>
    /// Safely sends a context message to Neuro with fallback to debug log
    /// </summary>
    /// <param name="message">The context message to send</param>
    /// <param name="isHighPriority">Whether this is a high priority message</param>
    /// <param name="tag">Optional tag for fallback logging</param>
    public static void SendContext(
        string message,
        bool isHighPriority = false,
        string tag = "NeuroMod"
    )
    {
        try
        {
            // Try to use NeuroSdk context if available
            Context.Send(message, isHighPriority);
            Log($"Context sent: {message}", tag);
        }
        catch (System.Exception ex)
        {
            // Fallback to Unity Debug.Log if NeuroSdk context fails
            Log($"Context (fallback): {message}", tag);
            LogWarning($"Context.Send failed: {ex.Message}", tag);
        }
    }

    /// <summary>
    /// Logs an exception with full details
    /// </summary>
    /// <param name="ex">The exception to log</param>
    /// <param name="context">Additional context about where the exception occurred</param>
    /// <param name="tag">Optional tag for categorization</param>
    public static void LogException(
        System.Exception ex,
        string context = "",
        string tag = "NeuroMod"
    )
    {
        string fullMessage = string.IsNullOrEmpty(context)
            ? $"Exception: {ex.Message}"
            : $"Exception in {context}: {ex.Message}";

        LogError(fullMessage, tag);

        // Log stack trace as separate debug message for detailed debugging
        if (EnableDebugLogging)
        {
            LogDebug($"Stack trace: {ex.StackTrace}", tag);
        }
    }
}