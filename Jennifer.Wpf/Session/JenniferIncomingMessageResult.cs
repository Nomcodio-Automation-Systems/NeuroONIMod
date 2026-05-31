using System.Collections.Generic;
using Jennifer.Wpf.Parsing;

namespace Jennifer.Wpf.Session;

/// <summary>
/// Represents Jennifer's interpretation of an incoming websocket or TCP message.
/// </summary>
/// <post>The result captures the message kind, user-facing log text, and any parsed incoming action.</post>
public sealed class JenniferIncomingMessageResult
{
    /// <summary>
    /// Gets or sets the parsed message kind.
    /// </summary>
    public JenniferWsMessageKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the parsed incoming action when the message is an action.
    /// </summary>
    public JenniferIncomingAction? IncomingAction { get; set; }

    /// <summary>
    /// Gets or sets the user-facing log message Jennifer should append.
    /// </summary>
    public string LogMessage { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the action names the game declared in an <c>actions/register</c> message.
    /// Empty when the message is not an <c>actions/register</c>.
    /// </summary>
    public IReadOnlyList<string> GameRegisteredActionNames { get; set; } = [];

    /// <summary>
    /// Gets or sets the action names the game removed in an <c>actions/unregister</c> message.
    /// Empty when the message is not an <c>actions/unregister</c>.
    /// </summary>
    public IReadOnlyList<string> GameUnregisteredActionNames { get; set; } = [];

    /// <summary>
    /// Gets or sets the action name selected from an <c>actions/force</c> request that Jennifer should dispatch back to the game.
    /// <c>null</c> when the message is not an <c>actions/force</c> or no action could be selected.
    /// </summary>
    public string? ForceSelectedActionName { get; set; }

    /// <summary>
    /// Gets or sets all candidate action names from an <c>actions/force</c> request.
    /// Empty when the message is not an <c>actions/force</c>.
    /// </summary>
    public IReadOnlyList<string> ForceCandidateNames { get; set; } = [];

    /// <summary>
    /// Gets or sets the action identifier from an <c>action/result</c> response.
    /// </summary>
    public string? ActionResultId { get; set; }

    /// <summary>
    /// Gets or sets whether the <c>action/result</c> response reports success.
    /// </summary>
    public bool ActionResultSuccess { get; set; }

    /// <summary>
    /// Gets or sets the human-readable message from an <c>action/result</c> response.
    /// </summary>
    public string? ActionResultMessage { get; set; }

    /// <summary>
    /// Gets or sets the game name announced by the sender in an <c>actions/register</c> message.
    /// <c>null</c> when the message type does not carry a game name.
    /// </summary>
    public string? GameName { get; set; }
}