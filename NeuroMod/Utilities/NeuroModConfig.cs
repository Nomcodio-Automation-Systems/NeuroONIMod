using System;

namespace NeuroMod;

/// <summary>
/// Feature flags and runtime toggles used by NeuroMod.
/// </summary>
/// <pre>
/// Environment variables may override the in-process defaults for each toggle.
/// </pre>
/// <post>
/// Process-global runtime flags can be queried consistently across the mod.
/// </post>
public static class NeuroModConfig
{
    /// <summary>
    /// When true, log output is also written to the system console. This can be
    /// useful when using external debug console mods or when running outside of
    /// the Unity environment.
    /// </summary>
    /// <remarks>Override using environment variable <c>NEURO_MOD_CONSOLE_OUTPUT</c>.</remarks>
    /// <pre>
    /// The process may define <c>NEURO_MOD_CONSOLE_OUTPUT</c> to override the local default.
    /// </pre>
    /// <post>
    /// The effective console-output flag is returned or updated.
    /// </post>
    public static bool EnableConsoleOutput
    {
        get
        {
            string? envValue = Environment.GetEnvironmentVariable("NEURO_MOD_CONSOLE_OUTPUT");
            return !string.IsNullOrEmpty(envValue) && bool.TryParse(envValue, out bool result) ? result : _enableConsoleOutput;
        }
        set => _enableConsoleOutput = value;
    }

    private static bool _enableConsoleOutput = false;

    /// <summary>
    /// Enables verbose debug-level logging throughout the mod. This will allow
    /// additional debug messages to be output by <see cref="NeuroLogger"/>.
    /// </summary>
    /// <remarks>Override using environment variable <c>NEURO_MOD_DEBUG_LOGGING</c>.</remarks>
    /// <pre>
    /// The process may define <c>NEURO_MOD_DEBUG_LOGGING</c> to override the local default.
    /// </pre>
    /// <post>
    /// The effective debug-logging flag is returned or updated.
    /// </post>
    public static bool EnableDebugLogging
    {
        get
        {
            string? envValue = Environment.GetEnvironmentVariable("NEURO_MOD_DEBUG_LOGGING");
            return !string.IsNullOrEmpty(envValue) && bool.TryParse(envValue, out bool result) ? result : _enableDebugLogging;
        }
        set => _enableDebugLogging = value;
    }

    private static bool _enableDebugLogging = false;

    /// <summary>
    /// When true the mod will forward context messages to the Neuro SDK via
    /// WebSocket. If false, context messages are only logged locally.
    /// </summary>
    /// <remarks>Override using environment variable <c>NEURO_MOD_SEND_CONTEXT</c>.</remarks>
    /// <pre>
    /// The process may define <c>NEURO_MOD_SEND_CONTEXT</c> to override the local default.
    /// </pre>
    /// <post>
    /// The effective context-forwarding flag is returned or updated.
    /// </post>
    public static bool SendContextMessages
    {
        get
        {
            string? envValue = Environment.GetEnvironmentVariable("NEURO_MOD_SEND_CONTEXT");
            return !string.IsNullOrEmpty(envValue) && bool.TryParse(envValue, out bool result) ? result : _sendContextMessages;
        }
        set => _sendContextMessages = value;
    }

    private static bool _sendContextMessages = false;

    /// <summary>
    /// Enables API-level tracing which augments debug logs with caller,
    /// file, and line information. Useful when diagnosing message flows between
    /// the game and the Neuro server.
    /// </summary>
    /// <remarks>Override using environment variable <c>NEURO_MOD_API_TRACING</c>.</remarks>
    /// <pre>
    /// The process may define <c>NEURO_MOD_API_TRACING</c> to override the local default.
    /// </pre>
    /// <post>
    /// The effective API-tracing flag is returned or updated.
    /// </post>
    public static bool EnableApiTracing
    {
        get
        {
            string? envValue = Environment.GetEnvironmentVariable("NEURO_MOD_API_TRACING");
            return !string.IsNullOrEmpty(envValue) && bool.TryParse(envValue, out bool result) ? result : _enableApiTracing;
        }
        set => _enableApiTracing = value;
    }

    private static bool _enableApiTracing = false;
}