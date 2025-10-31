using Newtonsoft.Json;
using PeterHan.PLib.Options;

namespace NeuroMod;

/// <summary>
/// PLib-based options for NeuroMod configuration
/// Provides automatic UI generation in the Mods screen
/// </summary>
[JsonObject(MemberSerialization.OptIn)]
[ModInfo("https://github.com/YourUsername/NeuroMod")]
[ConfigFile(IndentOutput: true)]
[RestartRequired]
public class NeuroModOptions
{
    // Neuro Connection Settings
    [Option("WebSocket URL", "Randy server WebSocket endpoint URL", "Connection")]
    [JsonProperty]
    public string WebSocketUrl { get; set; } = "ws://localhost:8000";

    [Option("Connection Timeout", "Timeout in seconds for establishing connection", "Connection")]
    [Limit(5, 120)]
    [JsonProperty]
    public int ConnectionTimeout { get; set; } = 30;

    [Option("Response Timeout", "Timeout in seconds for action responses", "Connection")]
    [Limit(3, 60)]
    [JsonProperty]
    public int ResponseTimeout { get; set; } = 10;

    [Option("Auto Reconnect", "Automatically reconnect on connection loss", "Connection")]
    [JsonProperty]
    public bool AutoReconnect { get; set; } = true;

    // Duplicant Settings
    [Option("Default Duplicant Name", "Name for the Neuro-controlled Duplicant", "Duplicant")]
    [JsonProperty]
    public string DefaultDuplicantName { get; set; } = "Neuro";

    [Option("Allow Rename", "Allow renaming the controlled Duplicant", "Duplicant")]
    [JsonProperty]
    public bool AllowRename { get; set; } = true;

    [Option("Bio Monitoring", "Enable biological data monitoring and transmission", "Duplicant")]
    [JsonProperty]
    public bool BioMonitoringEnabled { get; set; } = true;

    [Option("Bio Update Frequency", "Frequency in seconds for bio data updates", "Duplicant")]
    [Limit(1, 30)]
    [JsonProperty]
    public int BioUpdateFrequency { get; set; } = 5;

    // Game Settings
    [Option("Schedule Control", "Enable Neuro to control Duplicant schedules", "Game")]
    [JsonProperty]
    public bool ScheduleControlEnabled { get; set; } = true;

    [Option("Debug Logging", "Enable detailed debug logging", "Game")]
    [JsonProperty]
    public bool DebugLogging { get; set; } = true;

    [Option("Performance Monitoring", "Monitor and log performance metrics", "Game")]
    [JsonProperty]
    public bool PerformanceMonitoring { get; set; } = true;

    // Timeout & Fallback Settings
    [Option("Auto-Pick Tasks on Timeout", "When Neuro doesn't respond in time, automatically pick tasks for the Duplicant", "Behavior")]
    [JsonProperty]
    public bool AutoPickTasksOnTimeout { get; set; } = true;

    [Option("Max Retry Attempts", "Number of retry attempts before falling back", "Behavior")]
    [Limit(0, 10)]
    [JsonProperty]
    public int MaxRetryAttempts { get; set; } = 3;

    [Option("Retry Delay", "Delay in seconds between retry attempts", "Behavior")]
    [Limit(1, 30)]
    [JsonProperty]
    public int RetryDelay { get; set; } = 5;

    /// <summary>
    /// Converts PLib options to ModConfig format for use with ConfigManager
    /// </summary>
    public ModConfig ToModConfig()
    {
        return new ModConfig
        {
            Neuro = new NeuroConfig
            {
                EndpointUrl = WebSocketUrl,
                ConnectionTimeout = ConnectionTimeout,
                ResponseTimeout = ResponseTimeout,
                MaxRetryAttempts = MaxRetryAttempts,
                RetryDelay = RetryDelay,
                AutoReconnect = AutoReconnect
            },
            Duplicant = new DuplicantConfig
            {
                DefaultName = DefaultDuplicantName,
                AllowRename = AllowRename,
                FallbackBehavior = AutoPickTasksOnTimeout ? "auto_pick_task" : "idle",
                BioMonitoringEnabled = BioMonitoringEnabled,
                BioUpdateFrequency = BioUpdateFrequency
            },
            Game = new GameConfig
            {
                ScheduleControlEnabled = ScheduleControlEnabled,
                RealtimeDecisions = true,
                CommandPriority = "high",
                DebugLogging = DebugLogging,
                LogLevel = DebugLogging ? "debug" : "info",
                PerformanceMonitoring = PerformanceMonitoring
            },
            Timeout = new TimeoutConfig
            {
                GlobalTimeout = ResponseTimeout,
                DecisionTimeout = ResponseTimeout,
                ActionTimeout = ResponseTimeout,
                QueryTimeout = ResponseTimeout / 2,
                FallbackStrategies = new System.Collections.Generic.Dictionary<string, string>
                {
                    { "decision", AutoPickTasksOnTimeout ? "auto_pick_task" : "use_last_known_preference" },
                    { "action", AutoPickTasksOnTimeout ? "auto_pick_task" : "cancel_and_wait" },
                    { "query", "use_cached_data" }
                },
                ShowTimeoutWarnings = true,
                EscalationThreshold = MaxRetryAttempts,
                EscalationAction = AutoPickTasksOnTimeout ? "auto_pick_task" : "switch_to_manual_mode"
            }
        };
    }

    /// <summary>
    /// Creates PLib options from existing ModConfig
    /// </summary>
    public static NeuroModOptions FromModConfig(ModConfig config)
    {
        return new NeuroModOptions
        {
            WebSocketUrl = config.Neuro.EndpointUrl,
            ConnectionTimeout = config.Neuro.ConnectionTimeout,
            ResponseTimeout = config.Neuro.ResponseTimeout,
            AutoReconnect = config.Neuro.AutoReconnect,
            MaxRetryAttempts = config.Neuro.MaxRetryAttempts,
            RetryDelay = config.Neuro.RetryDelay,
            DefaultDuplicantName = config.Duplicant.DefaultName,
            AllowRename = config.Duplicant.AllowRename,
            BioMonitoringEnabled = config.Duplicant.BioMonitoringEnabled,
            BioUpdateFrequency = config.Duplicant.BioUpdateFrequency,
            ScheduleControlEnabled = config.Game.ScheduleControlEnabled,
            DebugLogging = config.Game.DebugLogging,
            PerformanceMonitoring = config.Game.PerformanceMonitoring,
            AutoPickTasksOnTimeout = config.Duplicant.FallbackBehavior == "auto_pick_task"
        };
    }
}