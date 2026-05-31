using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Jennifer.Wpf.Config.ActionInjection;

/// <summary>
/// Root model for the Jennifer action-injection configuration file
/// (<c>action_injection.json</c> in the Jennifer AppData folder).
///
/// Jennifer reads this file on startup and merges the declared actions into the
/// <c>actions/register</c> payload it sends to the internal server so that the
/// full action list is available to Jennifer without source-code parsing.
/// </summary>
/// <post>All collections are non-null; missing files return a default empty instance.</post>
public sealed class JenniferActionInjectionConfig
{
    /// <summary>
    /// Gets or sets the optional game name override.
    /// When non-empty this overrides the game name field in every registration payload.
    /// </summary>
    public string? GameName { get; set; }

    /// <summary>
    /// Gets or sets the list of additional actions to inject into the registration payload.
    /// </summary>
    public List<InjectedAction> Actions { get; set; } = [];
}

/// <summary>
/// Describes a single action that Jennifer injects into the server-side registration payload.
/// </summary>
public sealed class InjectedAction
{
    /// <summary>Gets or sets the unique action name (snake_case, matches the game-side action).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the human-readable description forwarded in the registration payload.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the raw JSON schema object string for this action's parameters.
    /// <c>null</c> or empty means the action takes no parameters.
    /// </summary>
    [JsonPropertyName("schema")]
    public string? SchemaJson { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this action should also appear as a quick-action button in the Jennifer UI.
    /// </summary>
    public bool ShowQuickButton { get; set; } = false;
}
