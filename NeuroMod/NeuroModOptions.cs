using Newtonsoft.Json;
using PeterHan.PLib.Options;

namespace NeuroMod;

/// <summary>
/// Defines the PLib-backed options surface for NeuroMod.
/// </summary>
/// <pre>PLib attributes are available so the mod options UI can be generated automatically.</pre>
/// <post>Consumers can convert between editor-facing option values and the internal <see cref="ModConfig"/> model.</post>
[JsonObject(MemberSerialization.OptIn)]
[ModInfo("https://github.com/YourUsername/NeuroMod")]
[ConfigFile(IndentOutput: true)]
[RestartRequired]
public class NeuroModOptions
{
    // Neuro Connection Settings
    [Option("WebSocket URL", "Randy server WebSocket endpoint URL", "Connection")]
    [JsonProperty]
    /// <summary>Gets or sets the Randy websocket endpoint URL.</summary>
    /// <pre>The options object represents the current PLib-backed settings surface.</pre>
    /// <post>The property stores the websocket endpoint that will be written into the internal Neuro config.</post>
    public string WebSocketUrl { get; set; } = "ws://localhost:8000";

    [Option("Connection Timeout", "Timeout in seconds for establishing connection", "Connection")]
    [Limit(5, 120)]
    [JsonProperty]
    /// <summary>Gets or sets the connection timeout in seconds.</summary>
    /// <pre>The options object represents the current PLib-backed settings surface.</pre>
    /// <post>The property stores the timeout used when establishing Neuro connections.</post>
    public int ConnectionTimeout { get; set; } = 30;

    [Option("Response Timeout", "Timeout in seconds for action responses", "Connection")]
    [Limit(3, 60)]
    [JsonProperty]
    /// <summary>Gets or sets the action response timeout in seconds.</summary>
    /// <pre>The options object represents the current PLib-backed settings surface.</pre>
    /// <post>The property stores the timeout used for Neuro action responses and derived timeout mappings.</post>
    public int ResponseTimeout { get; set; } = 10;

    [Option("Auto Reconnect", "Automatically reconnect on connection loss", "Connection")]
    [JsonProperty]
    /// <summary>Gets or sets a value indicating whether automatic reconnect is enabled.</summary>
    /// <pre>The options object represents the current PLib-backed settings surface.</pre>
    /// <post>The property stores whether reconnect attempts should occur after connection loss.</post>
    public bool AutoReconnect { get; set; } = true;

    // Duplicant Settings
    [Option("Default Duplicant Name", "Name for the Neuro-controlled Duplicant", "Duplicant")]
    [JsonProperty]
    /// <summary>Gets or sets the default Neuro-controlled duplicant name.</summary>
    /// <pre>The options object represents the current PLib-backed settings surface.</pre>
    /// <post>The property stores the duplicant name used to identify or initialize the Neuro target.</post>
    public string DefaultDuplicantName { get; set; } = "Neuro";

    [Option("Allow Rename", "Allow renaming the controlled Duplicant", "Duplicant")]
    [JsonProperty]
    /// <summary>Gets or sets a value indicating whether renaming the controlled duplicant is allowed.</summary>
    /// <pre>The options object represents the current PLib-backed settings surface.</pre>
    /// <post>The property stores whether rename operations are allowed for the Neuro-controlled duplicant.</post>
    public bool AllowRename { get; set; } = true;

    [Option("Bio Monitoring", "Enable biological data monitoring and transmission", "Duplicant")]
    [JsonProperty]
    /// <summary>Gets or sets a value indicating whether bio monitoring is enabled.</summary>
    /// <pre>The options object represents the current PLib-backed settings surface.</pre>
    /// <post>The property stores whether biodata collection and transmission are enabled.</post>
    public bool BioMonitoringEnabled { get; set; } = true;

    [Option("Bio Update Frequency", "Frequency in seconds for bio data updates", "Duplicant")]
    [Limit(1, 30)]
    [JsonProperty]
    /// <summary>Gets or sets the biodata update frequency in seconds.</summary>
    /// <pre>The options object represents the current PLib-backed settings surface.</pre>
    /// <post>The property stores how often biodata updates are emitted.</post>
    public int BioUpdateFrequency { get; set; } = 5;

    // Game Settings
    [Option("Schedule Control",
            "Enable Neuro to control the duplicant's schedule. " +
            "When enabled, a dedicated schedule is created on world load by cloning the duplicant's existing schedule " +
            "(or the balanced template when no schedule exists yet).",
            "Game")]
    [JsonProperty]
    /// <summary>Gets or sets a value indicating whether schedule control is enabled.</summary>
    /// <pre>The options object represents the current PLib-backed settings surface.</pre>
    /// <post>The property stores whether Neuro-managed schedule control (and initial schedule cloning) is enabled.</post>
    public bool ScheduleControlEnabled { get; set; } = true;

    [Option("Debug Logging", "Enable detailed debug logging", "Game")]
    [JsonProperty]
    /// <summary>Gets or sets a value indicating whether debug logging is enabled.</summary>
    /// <pre>The options object represents the current PLib-backed settings surface.</pre>
    /// <post>The property stores whether detailed logging should be emitted by the mod.</post>
    public bool DebugLogging { get; set; } = true;

    [Option("Performance Monitoring", "Monitor and log performance metrics", "Game")]
    [JsonProperty]
    /// <summary>Gets or sets a value indicating whether performance monitoring is enabled.</summary>
    /// <pre>The options object represents the current PLib-backed settings surface.</pre>
    /// <post>The property stores whether performance metrics should be observed and logged.</post>
    public bool PerformanceMonitoring { get; set; } = true;

    // Timeout & Fallback Settings
    [Option("Auto-Pick Tasks on Timeout", "When Neuro doesn't respond in time, automatically pick tasks for the Duplicant", "Behavior")]
    [JsonProperty]
    /// <summary>Gets or sets a value indicating whether timeout fallback auto-picks tasks.</summary>
    /// <pre>The options object represents the current PLib-backed settings surface.</pre>
    /// <post>The property stores whether timeout fallback should auto-pick tasks instead of leaving the duplicant idle or manual.</post>
    public bool AutoPickTasksOnTimeout { get; set; } = true;

    [Option("Max Retry Attempts", "Number of retry attempts before falling back", "Behavior")]
    [Limit(0, 10)]
    [JsonProperty]
    /// <summary>Gets or sets the maximum retry attempts before fallback.</summary>
    /// <pre>The options object represents the current PLib-backed settings surface.</pre>
    /// <post>The property stores the escalation threshold used by retry and timeout handling.</post>
    public int MaxRetryAttempts { get; set; } = 3;

    [Option("Retry Delay", "Delay in seconds between retry attempts", "Behavior")]
    [Limit(1, 30)]
    [JsonProperty]
    /// <summary>Gets or sets the retry delay in seconds.</summary>
    /// <pre>The options object represents the current PLib-backed settings surface.</pre>
    /// <post>The property stores the delay used between retry attempts.</post>
    public int RetryDelay { get; set; } = 5;

    /// <summary>
    /// Converts the current PLib-backed options into the internal <see cref="ModConfig"/> representation.
    /// </summary>
    /// <returns>A new <see cref="ModConfig"/> populated from these options.</returns>
    /// <pre>The current option properties contain the values selected in the PLib options UI.</pre>
    /// <post>A new <see cref="ModConfig"/> instance is returned with values mapped from the option set.</post>
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
                PerformanceMonitoring = PerformanceMonitoring,
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
    /// Builds a <see cref="NeuroModOptions"/> instance from an existing <see cref="ModConfig"/>.
    /// </summary>
    /// <param name="config">Source configuration to convert.</param>
    /// <returns>A <see cref="NeuroModOptions"/> instance with values mapped from <paramref name="config"/>.</returns>
    /// <pre><paramref name="config"/> contains a valid internal configuration model.</pre>
    /// <post>A new option object is returned with PLib-facing values mapped from the supplied configuration.</post>
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