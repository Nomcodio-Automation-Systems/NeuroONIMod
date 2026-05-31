using System.Text.Json.Serialization;

namespace Jennifer.Wpf.Config;

/// <summary>
/// Represents the persisted configuration for a Jennifer session.
/// All fields have sensible defaults so an absent settings file does not cause failures.
/// </summary>
/// <post>Default values mirror the previous hardcoded constants so existing users experience no change on first run.</post>
public sealed class JenniferSettings
{
    // ── Connection ──────────────────────────────────────────────────────────

    /// <summary>Gets or sets the WebSocket endpoint Jennifer connects to (e.g. Randy or a Neuro mod).</summary>
    public string Endpoint { get; set; } = "ws://localhost:8000";

    /// <summary>
    /// Gets or sets the optional game name sent in startup/register payloads.
    /// Empty means the field is omitted from the Neuro protocol message.
    /// </summary>
    public string GameName { get; set; } = string.Empty;

    /// <summary>Gets or sets whether Jennifer should attempt to connect automatically on startup.</summary>
    public bool AutoConnect { get; set; } = true;

    // ── TCP compatibility listener ───────────────────────────────────────────

    /// <summary>Gets or sets the local port Jennifer opens for legacy TCP compatibility clients.</summary>
    public int TcpListenerPort { get; set; } = 8081;

    // ── Built-in TestServer ──────────────────────────────────────────────────

    /// <summary>Gets or sets whether the built-in TestServer WebSocket broker is enabled.</summary>
    public bool TestServerEnabled { get; set; } = true;

    /// <summary>Gets or sets the port the built-in TestServer WebSocket broker listens on.</summary>
    public int TestServerWsPort { get; set; } = 8000;

    /// <summary>Gets or sets the port the built-in TestServer HTTP helper listens on.</summary>
    public int TestServerHttpPort { get; set; } = 1337;

    // ── Action source discovery ──────────────────────────────────────────────

    /// <summary>
    /// Gets or sets the directory Jennifer scans for Neuro action source files (.cs).
    /// When empty Jennifer falls back to auto-discovering a sibling NeuroMod folder.
    /// Set this to the <c>Actions</c> folder of any mod to use Jennifer with other projects.
    /// </summary>
    public string ActionSourceDirectory { get; set; } = string.Empty;

    // ── Force defaults ───────────────────────────────────────────────────────

    /// <summary>Gets or sets the default force priority shown in the UI on startup.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ForcePriority DefaultPriority { get; set; } = ForcePriority.Medium;

    /// <summary>Gets or sets the default ephemeral flag shown in the UI on startup.</summary>
    public bool DefaultEphemeral { get; set; } = true;

    // ── UI ───────────────────────────────────────────────────────────────────

    /// <summary>Gets or sets whether the log panel should auto-scroll to the latest entry.</summary>
    public bool AutoScrollLog { get; set; } = true;

    /// <summary>Gets or sets the maximum number of log lines kept in the response box before trimming.</summary>
    public int MaxLogLines { get; set; } = 1000;
}

/// <summary>Priority levels for Neuro <c>actions/force</c> requests.</summary>
public enum ForcePriority
{
    Low,
    Medium,
    High,
    Critical,
}
