using System.Collections.Generic;

namespace Jennifer.Wpf.Session;

/// <summary>
/// Describes how Jennifer should send a force-action request.
/// </summary>
/// <post>The plan indicates the transport, normalized actions, and any payload or error text required for execution.</post>
public sealed class JenniferForceRequestPlan
{
    /// <summary>
    /// Gets or sets the transport mode Jennifer should use.
    /// </summary>
    public JenniferForceRequestMode Mode { get; set; }

    /// <summary>
    /// Gets or sets the normalized action names.
    /// </summary>
    public IReadOnlyList<string> ActionNames { get; set; } = [];

    /// <summary>
    /// Gets or sets the normalized priority.
    /// </summary>
    public string Priority { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the websocket payload when websocket transport is selected.
    /// </summary>
    public string? WebSocketPayload { get; set; }

    /// <summary>
    /// Gets or sets the compatibility TCP message when compatibility transport is selected.
    /// </summary>
    public string? CompatibilityMessage { get; set; }

    /// <summary>
    /// Gets or sets the user-facing log message for rejected requests.
    /// </summary>
    public string? LogMessage { get; set; }
}

/// <summary>
/// The supported Jennifer transports for force-action requests.
/// </summary>
public enum JenniferForceRequestMode
{
    /// <summary>
    /// No request should be sent.
    /// </summary>
    None,

    /// <summary>
    /// Send the request through the websocket connection.
    /// </summary>
    WebSocket,

    /// <summary>
    /// Send the request through the compatibility TCP fallback.
    /// </summary>
    CompatibilityTcp,
}