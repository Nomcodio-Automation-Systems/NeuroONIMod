using PeterHan.PLib.Options;
using System;
using System.Collections.Generic;

namespace NeuroMod;

/// <summary>
/// Manages configuration loading, validation, and access for NeuroMod.
/// </summary>
/// <pre>PLib option data is available when configuration loading is requested.</pre>
/// <post>A validated <see cref="ModConfig"/> instance is available for consumers after a successful load.</post>
public class ConfigManager
{
    private static ConfigManager? _instance;

    /// <summary>
    /// Gets the singleton configuration manager instance.
    /// </summary>
    /// <pre>No input is required.</pre>
    /// <post>The returned value is the shared configuration manager instance.</post>
    public static ConfigManager Instance => _instance ??= new ConfigManager();

    private ModConfig _config = null!;
    private bool _isLoaded = false;

    /// <summary>
    /// Gets the current loaded configuration.
    /// </summary>
    /// <pre>Configuration has been loaded or initialized before callers depend on concrete settings.</pre>
    /// <post>The returned object is the manager's current configuration snapshot.</post>
    public ModConfig Config => _config;

    /// <summary>
    /// Gets a value indicating whether configuration has been loaded successfully.
    /// </summary>
    /// <pre>No input is required.</pre>
    /// <post>The returned value reflects whether the manager has completed configuration loading.</post>
    public bool IsLoaded => _isLoaded;

    private ConfigManager()
    {
        // No initialization needed - using PLib options only
    }

    /// <summary>
    /// Loads configuration from PLib options and validates the result.
    /// </summary>
    /// <returns><see langword="true"/> when configuration loaded successfully; otherwise, <see langword="false"/>.</returns>
    /// <pre>PLib settings may or may not already exist for the current mod installation.</pre>
    /// <post>The manager contains either validated user settings or a default configuration fallback.</post>
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
    /// <pre>The manager holds a configuration instance that may still contain missing or invalid values.</pre>
    /// <post>Required configuration sections and key values have been normalized to safe defaults.</post>
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
    /// <pre>Callers want to fall back to an in-memory default configuration.</pre>
    /// <post>The manager attempts to load built-in default values and logs failures if they occur.</post>
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
    /// <pre>No persisted user settings are required.</pre>
    /// <post>The manager holds a fully populated default configuration and marks itself loaded.</post>
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
    /// <pre>The manager holds a populated configuration instance.</pre>
    /// <post>A human-readable summary of key settings has been written to the log.</post>
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
    /// Safely retrieves a configuration value using the provided selector.
    /// </summary>
    /// <typeparam name="T">Type of the configuration value to return.</typeparam>
    /// <param name="selector">Function that selects a value from <see cref="ModConfig"/>.</param>
    /// <param name="fallback">Value to return when configuration is not loaded or selector throws.</param>
    /// <returns>The selected configuration value, or <paramref name="fallback"/> on error.</returns>
    /// <pre>The selector can evaluate against the loaded configuration when available.</pre>
    /// <post>The selected value or the provided fallback is returned without mutating stored configuration.</post>
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
/// <summary>
/// Top-level configuration container for NeuroMod.
/// </summary>
/// <pre>Configuration sections have been materialized from PLib options or defaults.</pre>
/// <post>Consumers can access grouped configuration areas through strongly typed properties.</post>
[Serializable]
public class ModConfig
{
    /// <summary>
    /// Gets or sets the connection and retry settings for the Neuro endpoint.
    /// </summary>
    /// <pre>The top-level configuration may or may not already include a Neuro section.</pre>
    /// <post>The property stores the Neuro transport settings used by the mod.</post>
    public NeuroConfig Neuro { get; set; } = null!;

    /// <summary>
    /// Gets or sets duplicant-related integration settings.
    /// </summary>
    /// <pre>The top-level configuration may or may not already include a Duplicant section.</pre>
    /// <post>The property stores the duplicant-facing behavior settings used by the mod.</post>
    public DuplicantConfig Duplicant { get; set; } = null!;

    /// <summary>
    /// Gets or sets game-level feature and logging settings.
    /// </summary>
    /// <pre>The top-level configuration may or may not already include a Game section.</pre>
    /// <post>The property stores the game-facing feature and logging settings used by the mod.</post>
    public GameConfig Game { get; set; } = null!;

    /// <summary>
    /// Gets or sets timeout and fallback strategy settings.
    /// </summary>
    /// <pre>The top-level configuration may or may not already include a Timeout section.</pre>
    /// <post>The property stores the timeout and fallback settings used by the mod.</post>
    public TimeoutConfig Timeout { get; set; } = null!;

    /// <summary>
    /// Gets or sets optional performance-related settings.
    /// </summary>
    /// <pre>The top-level configuration may or may not include a Performance section.</pre>
    /// <post>The property stores optional performance tuning settings when present.</post>
    public PerformanceConfig? Performance { get; set; }

    /// <summary>
    /// Gets or sets optional feature flag settings.
    /// </summary>
    /// <pre>The top-level configuration may or may not include a Features section.</pre>
    /// <post>The property stores optional feature-flag settings when present.</post>
    public FeaturesConfig? Features { get; set; }

    /// <summary>
    /// Gets or sets optional notification display settings.
    /// </summary>
    /// <pre>The top-level configuration may or may not include a Notifications section.</pre>
    /// <post>The property stores optional notification settings when present.</post>
    public NotificationsConfig? Notifications { get; set; }
}

/// <summary>
/// Configuration values related to the Neuro connection and transport.
/// </summary>
/// <pre>The mod requires endpoint and retry settings before attempting Neuro communication.</pre>
/// <post>Connection behavior can be configured through these properties.</post>
[Serializable]
public class NeuroConfig
{
    /// <summary>
    /// Gets or sets the WebSocket endpoint URL used to reach Neuro.
    /// </summary>
    /// <pre>The config object represents validated or persisted connection settings.</pre>
    /// <post>The property stores the websocket endpoint URL used for Neuro communication.</post>
    public string EndpointUrl { get; set; } = null!;

    /// <summary>
    /// Gets or sets the timeout, in seconds, for establishing a connection.
    /// </summary>
    /// <pre>The config object represents validated or persisted connection settings.</pre>
    /// <post>The property stores the connection timeout applied during websocket connection attempts.</post>
    public int ConnectionTimeout { get; set; }

    /// <summary>
    /// Gets or sets the timeout, in seconds, for awaiting a Neuro response.
    /// </summary>
    /// <pre>The config object represents validated or persisted connection settings.</pre>
    /// <post>The property stores the response timeout applied to Neuro operations.</post>
    public int ResponseTimeout { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of retry attempts before fallback behavior is used.
    /// </summary>
    /// <pre>The config object represents validated or persisted connection settings.</pre>
    /// <post>The property stores how many retry attempts are allowed before fallback behavior applies.</post>
    public int MaxRetryAttempts { get; set; }

    /// <summary>
    /// Gets or sets the retry delay, in seconds, between connection attempts.
    /// </summary>
    /// <pre>The config object represents validated or persisted connection settings.</pre>
    /// <post>The property stores the delay between retry attempts.</post>
    public int RetryDelay { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the mod should automatically reconnect after disconnection.
    /// </summary>
    /// <pre>The config object represents validated or persisted connection settings.</pre>
    /// <post>The property stores whether automatic reconnect behavior is enabled.</post>
    public bool AutoReconnect { get; set; }
}

/// <summary>
/// Settings that control how Neuro integrates with Duplicant behaviour.
/// </summary>
/// <pre>Duplicant-facing behavior can be customized independently from transport settings.</pre>
/// <post>Callers can inspect or update how the integration presents and recovers for the controlled duplicant.</post>
[Serializable]
public class DuplicantConfig
{
    /// <summary>
    /// Gets or sets the default name assigned to the Neuro-controlled duplicant.
    /// </summary>
    /// <pre>The config object represents duplicant-facing integration settings.</pre>
    /// <post>The property stores the configured name used to identify the Neuro-controlled duplicant.</post>
    public string DefaultName { get; set; } = null!;

    /// <summary>
    /// Gets or sets a value indicating whether renaming the controlled duplicant is allowed.
    /// </summary>
    /// <pre>The config object represents duplicant-facing integration settings.</pre>
    /// <post>The property stores whether renaming the controlled duplicant is permitted.</post>
    public bool AllowRename { get; set; }

    /// <summary>
    /// Gets or sets the fallback behavior used when Neuro is unavailable.
    /// </summary>
    /// <pre>The config object represents duplicant-facing integration settings.</pre>
    /// <post>The property stores the fallback behavior label used when Neuro is unavailable.</post>
    public string FallbackBehavior { get; set; } = null!;

    /// <summary>
    /// Gets or sets a value indicating whether bio monitoring is enabled.
    /// </summary>
    /// <pre>The config object represents duplicant-facing integration settings.</pre>
    /// <post>The property stores whether biodata monitoring is enabled.</post>
    public bool BioMonitoringEnabled { get; set; }

    /// <summary>
    /// Gets or sets the bio data update frequency, in seconds.
    /// </summary>
    /// <pre>The config object represents duplicant-facing integration settings.</pre>
    /// <post>The property stores the interval used for biodata updates.</post>
    public int BioUpdateFrequency { get; set; }
}

/// <summary>
/// Game-level feature toggles and debug settings.
/// </summary>
/// <pre>Game-facing features and diagnostics can be enabled or disabled through this section.</pre>
/// <post>Consumers can tailor runtime integration behavior without changing code.</post>
[Serializable]
public class GameConfig
{
    /// <summary>
    /// Gets or sets a value indicating whether schedule control is enabled.
    /// </summary>
    /// <pre>The config object represents game-level integration settings.</pre>
    /// <post>The property stores whether schedule control features are enabled.</post>
    public bool ScheduleControlEnabled { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether decisions are made in real time.
    /// </summary>
    /// <pre>The config object represents game-level integration settings.</pre>
    /// <post>The property stores whether the integration should prefer real-time decisions.</post>
    public bool RealtimeDecisions { get; set; }

    /// <summary>
    /// Gets or sets the configured command priority label.
    /// </summary>
    /// <pre>The config object represents game-level integration settings.</pre>
    /// <post>The property stores the configured command priority label.</post>
    public string CommandPriority { get; set; } = null!;

    /// <summary>
    /// Gets or sets a value indicating whether debug logging is enabled.
    /// </summary>
    /// <pre>The config object represents game-level integration settings.</pre>
    /// <post>The property stores whether debug logging is enabled.</post>
    public bool DebugLogging { get; set; }

    /// <summary>
    /// Gets or sets the log level label used by the mod.
    /// </summary>
    /// <pre>The config object represents game-level integration settings.</pre>
    /// <post>The property stores the configured log level label.</post>
    public string LogLevel { get; set; } = null!;

    /// <summary>
    /// Gets or sets a value indicating whether performance monitoring is enabled.
    /// </summary>
    /// <pre>The config object represents game-level integration settings.</pre>
    /// <post>The property stores whether performance monitoring is enabled.</post>
    public bool PerformanceMonitoring { get; set; }
}

/// <summary>
/// Timeout and fallback strategy configuration for Neuro operations.
/// </summary>
/// <pre>Timeout behavior has been defined for the supported Neuro operation categories.</pre>
/// <post>Timeout handling components can apply consistent limits and escalation policies.</post>
[Serializable]
public class TimeoutConfig
{
    /// <summary>
    /// Gets or sets the global timeout, in seconds, used when no specific timeout is configured.
    /// </summary>
    /// <pre>The config object represents timeout and fallback settings.</pre>
    /// <post>The property stores the default timeout applied when no operation-specific timeout is configured.</post>
    public int GlobalTimeout { get; set; }

    /// <summary>
    /// Gets or sets the timeout, in seconds, for decision operations.
    /// </summary>
    /// <pre>The config object represents timeout and fallback settings.</pre>
    /// <post>The property stores the timeout applied to decision operations.</post>
    public int DecisionTimeout { get; set; }

    /// <summary>
    /// Gets or sets the timeout, in seconds, for action operations.
    /// </summary>
    /// <pre>The config object represents timeout and fallback settings.</pre>
    /// <post>The property stores the timeout applied to action operations.</post>
    public int ActionTimeout { get; set; }

    /// <summary>
    /// Gets or sets the timeout, in seconds, for query operations.
    /// </summary>
    /// <pre>The config object represents timeout and fallback settings.</pre>
    /// <post>The property stores the timeout applied to query operations.</post>
    public int QueryTimeout { get; set; }

    /// <summary>
    /// Gets or sets the fallback strategy mapping by operation type.
    /// </summary>
    /// <pre>The config object represents timeout and fallback settings.</pre>
    /// <post>The property stores the fallback strategy mapping keyed by operation type.</post>
    public Dictionary<string, string> FallbackStrategies { get; set; } = null!;

    /// <summary>
    /// Gets or sets a value indicating whether timeout warnings should be shown to the player.
    /// </summary>
    /// <pre>The config object represents timeout and fallback settings.</pre>
    /// <post>The property stores whether timeout warnings should be shown to the player.</post>
    public bool ShowTimeoutWarnings { get; set; }

    /// <summary>
    /// Gets or sets the number of timeouts required before escalation occurs.
    /// </summary>
    /// <pre>The config object represents timeout and fallback settings.</pre>
    /// <post>The property stores the timeout count required before escalation behavior applies.</post>
    public int EscalationThreshold { get; set; }

    /// <summary>
    /// Gets or sets the escalation action label to apply after the threshold is reached.
    /// </summary>
    /// <pre>The config object represents timeout and fallback settings.</pre>
    /// <post>The property stores the escalation action label used after the threshold is reached.</post>
    public string EscalationAction { get; set; } = null!;
}

/// <summary>
/// Performance-related configuration values.
/// </summary>
/// <pre>Performance-sensitive systems can consult these values to tune runtime behavior.</pre>
/// <post>Consumers can enforce concurrency, update, and cache policies from configuration.</post>
[Serializable]
public class PerformanceConfig
{
    /// <summary>
    /// Gets or sets the maximum number of concurrent requests.
    /// </summary>
    /// <pre>The config object represents performance-related settings.</pre>
    /// <post>The property stores the concurrency limit used by performance-sensitive systems.</post>
    public int MaxConcurrentRequests { get; set; }

    /// <summary>
    /// Gets or sets update frequencies by subsystem key.
    /// </summary>
    /// <pre>The config object represents performance-related settings.</pre>
    /// <post>The property stores per-subsystem update frequencies keyed by subsystem name.</post>
    public Dictionary<string, int> UpdateFrequencies { get; set; } = null!;

    /// <summary>
    /// Gets or sets a value indicating whether caching is enabled.
    /// </summary>
    /// <pre>The config object represents performance-related settings.</pre>
    /// <post>The property stores whether caching is enabled.</post>
    public bool EnableCaching { get; set; }

    /// <summary>
    /// Gets or sets the cache expiration period.
    /// </summary>
    /// <pre>The config object represents performance-related settings.</pre>
    /// <post>The property stores the cache expiration period used by cached integrations.</post>
    public int CacheExpiration { get; set; }
}

/// <summary>
/// Feature flags that enable experimental or optional behaviour.
/// </summary>
/// <pre>Optional features are represented as switchable flags and related metadata.</pre>
/// <post>Feature-aware systems can selectively enable behaviors from configuration.</post>
[Serializable]
public class FeaturesConfig
{
    /// <summary>
    /// Gets or sets a value indicating whether experimental mode is enabled.
    /// </summary>
    /// <pre>The config object represents feature-flag settings.</pre>
    /// <post>The property stores whether experimental mode is enabled.</post>
    public bool ExperimentalMode { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether multi-duplicant control is enabled.
    /// </summary>
    /// <pre>The config object represents feature-flag settings.</pre>
    /// <post>The property stores whether multi-duplicant control is enabled.</post>
    public bool MultiDuplicantControl { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether voice feedback is enabled.
    /// </summary>
    /// <pre>The config object represents feature-flag settings.</pre>
    /// <post>The property stores whether voice feedback is enabled.</post>
    public bool VoiceFeedback { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether visual indicators are enabled.
    /// </summary>
    /// <pre>The config object represents feature-flag settings.</pre>
    /// <post>The property stores whether visual indicators are enabled.</post>
    public bool VisualIndicators { get; set; }

    /// <summary>
    /// Gets or sets the command prefix used by feature systems.
    /// </summary>
    /// <pre>The config object represents feature-flag settings.</pre>
    /// <post>The property stores the command prefix used by feature-aware systems.</post>
    public string CommandPrefix { get; set; } = null!;
}

/// <summary>
/// Controls which UI notifications are shown to the player.
/// </summary>
/// <pre>Notification presentation settings are available for user-facing messaging systems.</pre>
/// <post>Notification emitters can tailor visibility and duration based on these values.</post>
[Serializable]
public class NotificationsConfig
{
    /// <summary>
    /// Gets or sets a value indicating whether notifications are enabled at all.
    /// </summary>
    /// <pre>The config object represents notification presentation settings.</pre>
    /// <post>The property stores whether user-facing notifications are enabled.</post>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether connection status notifications are shown.
    /// </summary>
    /// <pre>The config object represents notification presentation settings.</pre>
    /// <post>The property stores whether connection status notifications are shown.</post>
    public bool ShowConnectionStatus { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether timeout warnings are shown.
    /// </summary>
    /// <pre>The config object represents notification presentation settings.</pre>
    /// <post>The property stores whether timeout warnings are shown.</post>
    public bool ShowTimeoutWarnings { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether error messages are shown.
    /// </summary>
    /// <pre>The config object represents notification presentation settings.</pre>
    /// <post>The property stores whether error messages are shown.</post>
    public bool ShowErrorMessages { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether success messages are shown.
    /// </summary>
    /// <pre>The config object represents notification presentation settings.</pre>
    /// <post>The property stores whether success messages are shown.</post>
    public bool ShowSuccessMessages { get; set; }

    /// <summary>
    /// Gets or sets the notification display duration.
    /// </summary>
    /// <pre>The config object represents notification presentation settings.</pre>
    /// <post>The property stores the notification display duration.</post>
    public int DisplayDuration { get; set; }
}