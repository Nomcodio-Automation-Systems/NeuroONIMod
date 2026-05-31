using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Jennifer.Wpf.Automation;

/// <summary>
/// Represents a Jennifer automation plan loaded from JSON.
/// </summary>
/// <post>The plan can be applied to the Jennifer UI and executed or matched against incoming actions.</post>
public sealed class JenniferAutomationPlan
{
    /// <summary>
    /// Gets or sets the user-facing plan name.
    /// </summary>
    public string Name { get; set; } = "Untitled Jennifer plan";

    /// <summary>
    /// Gets or sets the optional plan description.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional WebSocket endpoint Jennifer should use for this plan.
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the preferred game name for this plan.
    /// </summary>
    public string GameName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the legacy game alias accepted from JSON.
    /// </summary>
    [JsonPropertyName("game")]
    public string Game { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether Jennifer should auto-reply to matching incoming actions.
    /// </summary>
    public bool AutoRespond { get; set; }

    /// <summary>
    /// Gets or sets the ordered automation steps.
    /// </summary>
    public List<JenniferAutomationStep> Steps { get; set; } = new();
}

/// <summary>
/// Represents a single Jennifer automation step.
/// </summary>
/// <post>The step can be executed manually or used as an auto-reply rule for incoming actions.</post>
public sealed class JenniferAutomationStep
{
    /// <summary>
    /// Gets or sets the user-facing step name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the target action name.
    /// </summary>
    public string ActionName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the legacy action alias accepted from JSON.
    /// </summary>
    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional state payload.
    /// </summary>
    public string State { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional Neuro query text.
    /// </summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the step priority.
    /// </summary>
    public string Priority { get; set; } = "low";

    /// <summary>
    /// Gets or sets a value indicating whether the step uses ephemeral context.
    /// </summary>
    public bool Ephemeral { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the automatic result should report success.
    /// </summary>
    public bool ResultSuccess { get; set; } = true;

    /// <summary>
    /// Gets or sets the optional result message for auto-replies.
    /// </summary>
    public string ResultMessage { get; set; } = string.Empty;

    /// <summary>
    /// Gets the text shown in the Jennifer automation step list.
    /// </summary>
    [JsonIgnore]
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? ActionName : $"{Name} [{ActionName}]";
}