using NeuroSdk.Messages.Outgoing;
using System;
using System.IO;
using Newtonsoft.Json;

namespace NeuroMod;

/// <summary>
/// Centralized logging helper for NeuroMod.
/// </summary>
/// <pre>
/// Callers provide already formatted semantic intent such as info, warning, error, or context updates.
/// </pre>
/// <post>
/// Log output is routed to Unity debug APIs or the console fallback, and context messages are forwarded only when enabled.
/// </post>
public static class NeuroLogger
{
    /// <summary>
    /// Separable API client used for sending context messages. Tests may replace
    /// this with a test double to intercept outgoing messages.
    /// </summary>
    public static Integration.Api.IApiClient Api { get; set; } = Integration.Api.ApiClient.Instance;

    /// <summary>
    /// When true, log output is also written to the system console for external
    /// debug console mods or test runners.
    /// </summary>
    public static bool EnableConsoleOutput => NeuroModConfig.EnableConsoleOutput;

    /// <summary>
    /// Enable verbose debug logging
    /// </summary>
    public static bool EnableDebugLogging => NeuroModConfig.EnableDebugLogging;

    /// <summary>
    /// When true, log messages are emitted as structured JSON objects instead of plain text.
    /// This is useful for external log collectors that parse JSON envelopes.
    /// </summary>
    public static bool UseJsonOutput { get; set; } = false;

    /// <summary>
    /// Format a log message with a tag only (simplified format used by ONI mods).
    /// </summary>
    /// <pre>
    /// <paramref name="message"/> and <paramref name="tag"/> are ready for human-readable logging.
    /// </pre>
    /// <post>
    /// A timestamped log line is returned.
    /// </post>
    private static string FormatMessage(string message, string tag)
    {
        return $"[{System.DateTime.UtcNow:O}] [{tag}] {message}";
    }

    /// <summary>
    /// Format a log message including caller/file/line information when available.
    /// Format: [tag] message (caller@file:line)
    /// </summary>
    /// <pre>
    /// Caller metadata may be empty when tracing information is unavailable.
    /// </pre>
    /// <post>
    /// A timestamped log line including caller metadata is returned.
    /// </post>
    private static string FormatMessage(string message, string tag, string caller, string file, int line)
    {
        string fileName = string.IsNullOrEmpty(file) ? "" : Path.GetFileName(file);
        string callerInfo = string.IsNullOrEmpty(caller) ? "" : $" ({caller}@{fileName}:{line})";
        return $"[{System.DateTime.UtcNow:O}] [{tag}] {message}{callerInfo}";
    }

    private static bool IsTestRunner()
    {
        try
        {
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                var name = a.GetName().Name;
                if (string.Equals(name, "nunit.framework", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(name, "Microsoft.TestPlatform.TestHost", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        catch
        {
            // If detection fails, assume not a test runner
        }
        return false;
    }

    /// <summary>
    /// Writes a pre-formatted message to Unity's debug API, with level routing.
    /// </summary>
    /// <pre>
    /// <paramref name="formattedMessage"/> is already safe to emit and <paramref name="level"/> selects the routing target.
    /// </pre>
    /// <post>
    /// The message is written through Unity logging or the console fallback.
    /// </post>
    private static void OutputLog(string formattedMessage, LogLevel level)
    {
        try
        {
            if (UseJsonOutput)
            {
                var envelope = new
                {
                    timestamp = System.DateTime.UtcNow.ToString("O"),
                    level = level.ToString(),
                    message = formattedMessage
                };
                string json = JsonConvert.SerializeObject(envelope);
                switch (level)
                {
                    case LogLevel.Warning:
                        Debug.LogWarning(json);
                        break;

                    case LogLevel.Error:
                        Debug.LogError(json);
                        break;

                    default:
                        Debug.Log(json);
                        break;
                }
            }
            else
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
    /// Internal log levels used to route messages to the appropriate Unity API.
    /// </summary>
    private enum LogLevel
    {
        Info,
        Warning,
        Error,
        DebugLevel,
    }

    /// <summary>
    /// Logs an informational message.
    /// </summary>
    /// <param name="message">The message to log.</param>
    /// <param name="tag">Optional tag used to categorize the message.</param>
    /// <pre>
    /// Informational logging is allowed in the current runtime and may be suppressed in test hosts.
    /// </pre>
    /// <post>
    /// The message is emitted through the configured logging sink unless suppressed for tests.
    /// </post>
    public static void Log(string message, string tag = "NeuroMod", string? traceId = null)
    {
        // Suppress informational logging during unit test runs to avoid noisy
        // test output being interpreted as warnings by test hosts.
        if (IsTestRunner())
        {
            return;
        }

        string formattedMessage = FormatMessage(message, tag);
        if (UseJsonOutput)
        {
            var envelope = new { timestamp = System.DateTime.UtcNow.ToString("O"), level = "Info", tag, message, traceId };
            OutputLog(JsonConvert.SerializeObject(envelope), LogLevel.Info);
            if (EnableConsoleOutput)
            {
                Console.WriteLine(JsonConvert.SerializeObject(envelope));
            }
            return;
        }

        OutputLog(formattedMessage, LogLevel.Info);
    }

    /// <summary>
    /// Logs a warning message.
    /// </summary>
    /// <param name="message">The warning message to log.</param>
    /// <param name="tag">Optional tag used to categorize the message.</param>
    /// <pre>
    /// <paramref name="message"/> describes a recoverable or suspicious runtime condition.
    /// </pre>
    /// <post>
    /// The warning is emitted through the configured logging sink.
    /// </post>
    public static void LogWarning(string message, string tag = "NeuroMod", string? traceId = null)
    {
        string formattedMessage = FormatMessage(message, tag);
        if (UseJsonOutput)
        {
            var envelope = new { timestamp = System.DateTime.UtcNow.ToString("O"), level = "Warning", tag, message, traceId };
            OutputLog(JsonConvert.SerializeObject(envelope), LogLevel.Warning);
            if (EnableConsoleOutput)
            {
                Console.WriteLine(JsonConvert.SerializeObject(envelope));
            }
            return;
        }

        OutputLog(formattedMessage, LogLevel.Warning);
    }

    /// <summary>
    /// Logs an error message.
    /// </summary>
    /// <param name="message">The error message to log.</param>
    /// <param name="tag">Optional tag used to categorize the message.</param>
    /// <pre>
    /// <paramref name="message"/> describes a failure or invariant breach.
    /// </pre>
    /// <post>
    /// The error is emitted through the configured logging sink.
    /// </post>
    public static void LogError(string message, string tag = "NeuroMod", string? traceId = null)
    {
        string formattedMessage = FormatMessage(message, tag);
        if (UseJsonOutput)
        {
            var envelope = new { timestamp = System.DateTime.UtcNow.ToString("O"), level = "Error", tag, message, traceId };
            OutputLog(JsonConvert.SerializeObject(envelope), LogLevel.Error);
            if (EnableConsoleOutput)
            {
                Console.WriteLine(JsonConvert.SerializeObject(envelope));
            }
            return;
        }

        OutputLog(formattedMessage, LogLevel.Error);
    }

    /// <summary>
    /// Logs a debug-level message when debug logging is enabled.
    /// </summary>
    /// <param name="message">The debug message to log.</param>
    /// <param name="tag">Optional tag used to categorize the message.</param>
    /// <pre>
    /// Debug logging is only meaningful when debug output is enabled.
    /// </pre>
    /// <post>
    /// The debug message is emitted only when the relevant debug flags are enabled.
    /// </post>
    public static void LogDebug(string message, string tag = "NeuroMod", string? traceId = null)
    {
        if (!EnableDebugLogging)
        {
            return;
        }

        string formattedMessage = FormatMessage(message, tag);
        if (UseJsonOutput)
        {
            var envelope = new { timestamp = System.DateTime.UtcNow.ToString("O"), level = "Debug", tag, message, traceId };
            OutputLog(JsonConvert.SerializeObject(envelope), LogLevel.DebugLevel);
            if (EnableConsoleOutput)
            {
                Console.WriteLine(JsonConvert.SerializeObject(envelope));
            }
            return;
        }

        OutputLog(formattedMessage, LogLevel.DebugLevel);
    }

    /// <summary>
    /// Logs a debug message including caller/file/line information. This is
    /// intended for detailed tracing of API calls and will only write output
    /// when debug logging or API tracing is enabled.
    /// </summary>
    /// <param name="message">Message to log.</param>
    /// <param name="tag">Tag used to categorize the message.</param>
    /// <param name="caller">Caller member name supplied by the caller via <c>CallerMemberName</c>.</param>
    /// <param name="file">Caller file path supplied by the caller via <c>CallerFilePath</c>.</param>
    /// <param name="line">Caller source line supplied by the caller via <c>CallerLineNumber</c>.</param>
    /// <pre>
    /// Detailed caller tracing is only emitted when debug logging or API tracing is enabled.
    /// </pre>
    /// <post>
    /// A caller-annotated debug message is emitted when tracing is enabled.
    /// </post>
    public static void LogDebug(string message, string tag, string caller, string file, int line, string? traceId = null)
    {
        if (!EnableDebugLogging && !NeuroModConfig.EnableApiTracing)
        {
            return;
        }

        string formattedMessage = FormatMessage(message, tag, caller, file, line);
        if (UseJsonOutput)
        {
            var envelope = new { timestamp = System.DateTime.UtcNow.ToString("O"), level = "Debug", tag, message = formattedMessage, caller, file, line, traceId };
            OutputLog(JsonConvert.SerializeObject(envelope), LogLevel.DebugLevel);
            if (EnableConsoleOutput)
            {
                Console.WriteLine(JsonConvert.SerializeObject(envelope));
            }
            return;
        }

        OutputLog(formattedMessage, LogLevel.DebugLevel);
    }

    /// <summary>
    /// Attempts to send a simple context message using <see cref="NeuroMod.Integration.Api.ApiClient"/>
    /// and falls back to local logging when sending is disabled or fails.
    /// </summary>
    /// <param name="message">The context message to send.</param>
    /// <param name="isHighPriority">Whether this message should be treated as high priority by the server.</param>
    /// <param name="tag">Optional tag for fallback logging when sending is disabled.</param>
    /// <pre>
    /// <paramref name="message"/> is intended for the out-of-band context channel rather than the action result body.
    /// </pre>
    /// <post>
    /// The context message is forwarded through the API seam when enabled, or logged locally as fallback.
    /// </post>
    public static void SendContext(
        string message,
        bool isHighPriority = false,
        string tag = "NeuroMod",
        string? traceId = null
    )
    {
        // If sending context messages is disabled, log locally and do not forward over the SDK
        if (!NeuroModConfig.SendContextMessages)
        {
            Log($"Context (disabled): {message}", tag);
            return;
        }

        try
        {
            // Use Api (seam) to centralize outgoing context message sending
            Api.SendContext(message, isHighPriority);
            Log($"Context queued: {message}", tag, traceId);
        }
        catch (System.Exception ex)
        {
            // Fallback to Unity Debug.Log if NeuroSdk context fails
            Log($"Context (fallback): {message}", tag, traceId);
            LogWarning($"Context.Send failed: {ex.Message}", tag, traceId);
        }
    }

    /// <summary>
    /// Logs an exception with full details
    /// </summary>
    /// <param name="ex">The exception to log</param>
    /// <param name="context">Additional context about where the exception occurred</param>
    /// <param name="tag">Optional tag for categorization</param>
    /// <pre>
    /// <paramref name="ex"/> captures a runtime failure that should be surfaced to logs.
    /// </pre>
    /// <post>
    /// The exception message is logged as an error and the stack trace is logged when debug output is enabled.
    /// </post>
    public static void LogException(
        System.Exception ex,
        string context = "",
        string tag = "NeuroMod",
        string? traceId = null
    )
    {
        string fullMessage = string.IsNullOrEmpty(context)
            ? $"Exception: {ex.Message}"
            : $"Exception in {context}: {ex.Message}";

        LogError(fullMessage, tag, traceId);

        // Log stack trace as separate debug message for detailed debugging
        if (EnableDebugLogging)
        {
            LogDebug($"Stack trace: {ex.StackTrace}", tag, traceId);
        }
    }
}