using System;

namespace Jennifer.Wpf.Session;

/// <summary>
/// Represents an incoming action request tracked by Jennifer.
/// </summary>
/// <post>The action can be displayed in the UI, matched against automation, and completed with a result.</post>
public sealed class JenniferIncomingAction
{
    /// <summary>
    /// Gets or sets the action correlation id.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the action name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the raw action data payload.
    /// </summary>
    public string? Data { get; set; }

    /// <summary>
    /// Gets or sets the raw incoming message payload.
    /// </summary>
    public string Raw { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the receive timestamp.
    /// </summary>
    public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.Now;

    /// <summary>
    /// Gets the display name shown in Jennifer's incoming action list.
    /// </summary>
    public string DisplayName => $"{Name} [{ReceivedAt:HH:mm:ss}]";
}