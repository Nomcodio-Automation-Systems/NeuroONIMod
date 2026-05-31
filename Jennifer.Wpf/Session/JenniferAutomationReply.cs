namespace Jennifer.Wpf.Session;

/// <summary>
/// Represents an automatic Jennifer response generated from a matching automation step.
/// </summary>
/// <post>The reply contains the result values Jennifer should send for an incoming action.</post>
public sealed class JenniferAutomationReply
{
    /// <summary>
    /// Gets or sets the matched action name.
    /// </summary>
    public string ActionName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the result success flag.
    /// </summary>
    public bool ResultSuccess { get; set; }

    /// <summary>
    /// Gets or sets the result message.
    /// </summary>
    public string ResultMessage { get; set; } = string.Empty;
}