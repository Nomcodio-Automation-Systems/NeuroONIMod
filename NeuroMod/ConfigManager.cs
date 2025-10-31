using PeterHan.PLib.Options;
using System;
using System.Collections.Generic;

namespace NeuroMod;

/// <summary>
/// Manages configuration loading and validation for NeuroMod
/// Provides centralized access to all configurable settings
/// Uses PLib options for all configuration
/// </summary>
public class ConfigManager
{
    private static ConfigManager? _instance;
    public static ConfigManager Instance => _instance ??= new ConfigManager();

    private ModConfig _config = null!;
    private bool _isLoaded = false;

    /// <summary>
    /// Gets the current configuration
    /// </summary>
    public ModConfig Config => _config;

    /// <summary>
    /// Indicates whether configuration has been successfully loaded
    /// </summary>
    public bool IsLoaded => _isLoaded;

    private ConfigManager()
    {
        // No initialization needed - using PLib options only
    }

    /// <summary>
    /// Loads configuration from PLib options
    /// </summary>
    /// <returns>True if configuration loaded successfully</returns>
    public bool LoadConfig()
    {
        try
        {
            Debug.Log($"[ConfigManager] Loading configuration from PLib options...");

            // Load from PLib options
            NeuroModOptions? options = POptions.ReadSettings<NeuroModOptions>();

            if (options != null)
            {
                Debug.Log("[ConfigManager] PLib options found, using user settings");
                _config = options.ToModConfig();
                ValidateConfig();
                _isLoaded = true;
                LogConfigSummary();
                return true;
            }

            // If no options found, use defaults
            Debug.Log("[ConfigManager] No PLib options found, using defaults");
            LoadDefaultConfig();
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ConfigManager] Failed to load configuration: {ex.Message}");
            LoadDefaultConfig();
            return false;
        }
    }

    /// <summary>
    /// Validates loaded configuration and applies defaults for missing values
    /// </summary>
    private void ValidateConfig()
    {
        if (_config == null)
        {
            throw new InvalidOperationException("Configuration is null");
        }

        // Validate Neuro settings
        _config.Neuro ??= new NeuroConfig();

        if (string.IsNullOrEmpty(_config.Neuro.EndpointUrl))
        {
            _config.Neuro.EndpointUrl = "ws://localhost:8000";
        }

        if (_config.Neuro.ResponseTimeout <= 0)
        {
            _config.Neuro.ResponseTimeout = 10;
        }

        // Validate Duplicant settings
        _config.Duplicant ??= new DuplicantConfig();

        // NOTE: Don't override DefaultName here - it should come from PLib options or the default config
        // The LoadDefaultConfig() method sets the proper default value
        if (string.IsNullOrEmpty(_config.Duplicant.DefaultName))
        {
            Debug.LogWarning("[ConfigManager] DefaultName is empty after loading - this shouldn't happen. Using 'Neuro' as fallback.");
            _config.Duplicant.DefaultName = "Neuro";
        }

        // Validate Timeout settings
        _config.Timeout ??= new TimeoutConfig();

        if (_config.Timeout.GlobalTimeout <= 0)
        {
            _config.Timeout.GlobalTimeout = 15;
        }

        Debug.Log("[ConfigManager] Configuration validation completed");
    }

    /// <summary>
    /// Creates a default configuration (no file saving - uses PLib options)
    /// </summary>
    private void CreateDefaultConfig()
    {
        try
        {
            LoadDefaultConfig();
            Debug.Log("[ConfigManager] Default configuration loaded");
        }
        catch (Exception ex)
        {
            Debug.LogError(
                $"[ConfigManager] Failed to create default configuration: {ex.Message}"
            );
        }
    }

    /// <summary>
    /// Loads default configuration values
    /// </summary>
    private void LoadDefaultConfig()
    {
        _config = new ModConfig
        {
            Neuro = new NeuroConfig
            {
                EndpointUrl = "ws://localhost:8000",
                ConnectionTimeout = 30,
                ResponseTimeout = 10,
                MaxRetryAttempts = 3,
                RetryDelay = 5,
                AutoReconnect = true,
            },
            Duplicant = new DuplicantConfig
            {
                DefaultName = "Neuro",
                AllowRename = true,
                FallbackBehavior = "idle",
                BioMonitoringEnabled = true,
                BioUpdateFrequency = 5,
            },
            Game = new GameConfig
            {
                ScheduleControlEnabled = true,
                RealtimeDecisions = true,
                CommandPriority = "high",
                DebugLogging = true,
                LogLevel = "info",
                PerformanceMonitoring = true,
            },
            Timeout = new TimeoutConfig
            {
                GlobalTimeout = 15,
                DecisionTimeout = 8,
                ActionTimeout = 12,
                QueryTimeout = 5,
                FallbackStrategies = new Dictionary<string, string>
                {
                    { "decision", "use_last_known_preference" },
                    { "action", "cancel_and_wait" },
                    { "query", "use_cached_data" },
                },
                ShowTimeoutWarnings = true,
                EscalationThreshold = 5,
                EscalationAction = "switch_to_manual_mode",
            },
        };

        _isLoaded = true;
    }

    /// <summary>
    /// Logs a summary of the loaded configuration
    /// </summary>
    private void LogConfigSummary()
    {
        Debug.Log($"[ConfigManager] Config Summary:");
        Debug.Log($"  Neuro Endpoint: {_config.Neuro.EndpointUrl}");
        Debug.Log($"  Response Timeout: {_config.Neuro.ResponseTimeout}s");
        Debug.Log($"  Max Retry Attempts: {_config.Neuro.MaxRetryAttempts}");
        Debug.Log($"  Duplicant Name: {_config.Duplicant.DefaultName}");
        Debug.Log($"  Fallback Behavior: {_config.Duplicant.FallbackBehavior}");
        Debug.Log($"  Auto-pick tasks on timeout: {_config.Duplicant.FallbackBehavior == "auto_pick_task"}");
        Debug.Log($"  Debug Logging: {_config.Game.DebugLogging}");
    }

    /// <summary>
    /// Gets a specific configuration value with fallback
    /// </summary>
    public T? GetConfigValue<T>(Func<ModConfig, T> selector, T? fallback = default)
    {
        try
        {
            return _isLoaded ? selector(_config) : fallback;
        }
        catch
        {
            return fallback;
        }
    }
}

// Configuration data classes
[Serializable]
public class ModConfig
{
    public NeuroConfig Neuro { get; set; } = null!;
    public DuplicantConfig Duplicant { get; set; } = null!;
    public GameConfig Game { get; set; } = null!;
    public TimeoutConfig Timeout { get; set; } = null!;
    public PerformanceConfig? Performance { get; set; }
    public FeaturesConfig? Features { get; set; }
    public NotificationsConfig? Notifications { get; set; }
}

[Serializable]
public class NeuroConfig
{
    public string EndpointUrl { get; set; } = null!;
    public int ConnectionTimeout { get; set; }
    public int ResponseTimeout { get; set; }
    public int MaxRetryAttempts { get; set; }
    public int RetryDelay { get; set; }
    public bool AutoReconnect { get; set; }
}

[Serializable]
public class DuplicantConfig
{
    public string DefaultName { get; set; } = null!;
    public bool AllowRename { get; set; }
    public string FallbackBehavior { get; set; } = null!;
    public bool BioMonitoringEnabled { get; set; }
    public int BioUpdateFrequency { get; set; }
}

[Serializable]
public class GameConfig
{
    public bool ScheduleControlEnabled { get; set; }
    public bool RealtimeDecisions { get; set; }
    public string CommandPriority { get; set; } = null!;
    public bool DebugLogging { get; set; }
    public string LogLevel { get; set; } = null!;
    public bool PerformanceMonitoring { get; set; }
}

[Serializable]
public class TimeoutConfig
{
    public int GlobalTimeout { get; set; }
    public int DecisionTimeout { get; set; }
    public int ActionTimeout { get; set; }
    public int QueryTimeout { get; set; }
    public Dictionary<string, string> FallbackStrategies { get; set; } = null!;
    public bool ShowTimeoutWarnings { get; set; }
    public int EscalationThreshold { get; set; }
    public string EscalationAction { get; set; } = null!;
}

[Serializable]
public class PerformanceConfig
{
    public int MaxConcurrentRequests { get; set; }
    public Dictionary<string, int> UpdateFrequencies { get; set; } = null!;
    public bool EnableCaching { get; set; }
    public int CacheExpiration { get; set; }
}

[Serializable]
public class FeaturesConfig
{
    public bool ExperimentalMode { get; set; }
    public bool MultiDuplicantControl { get; set; }
    public bool VoiceFeedback { get; set; }
    public bool VisualIndicators { get; set; }
    public string CommandPrefix { get; set; } = null!;
}

[Serializable]
public class NotificationsConfig
{
    public bool Enabled { get; set; }
    public bool ShowConnectionStatus { get; set; }
    public bool ShowTimeoutWarnings { get; set; }
    public bool ShowErrorMessages { get; set; }
    public bool ShowSuccessMessages { get; set; }
    public int DisplayDuration { get; set; }
}