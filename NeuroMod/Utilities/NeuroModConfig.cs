using System;

namespace NeuroMod;

/// <summary>
/// Configuration settings for NeuroMod
/// </summary>
public static class NeuroModConfig
{
    /// <summary>
    /// Enable console output for external debug console mods
    /// Can be overridden by environment variable NEURO_MOD_CONSOLE_OUTPUT
    /// </summary>
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
    /// Enable verbose debug logging
    /// Can be overridden by environment variable NEURO_MOD_DEBUG_LOGGING
    /// </summary>
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
}