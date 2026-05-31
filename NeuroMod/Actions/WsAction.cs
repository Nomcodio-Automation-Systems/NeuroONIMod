#nullable enable

using NeuroSdk.Json;
using Newtonsoft.Json;

namespace NeuroSdk.Actions;

/// <summary>
/// Represents an action that can be executed via WebSocket communication.
/// </summary>
/// <param name="name">The unique name identifier for the action</param>
/// <param name="description">Human-readable description of what the action does</param>
/// <param name="schema">Optional JSON schema for validating action parameters</param>
/// <pre>The caller provides stable action metadata suitable for remote registration.</pre>
/// <post>An immutable serializable action descriptor is available for outbound WebSocket messages.</post>
public readonly struct WsAction(string name, string description, JsonSchema? schema)
{
    /// <summary>
    /// Gets the unique name identifier for this action.
    /// </summary>
    /// <pre>The descriptor was constructed with a stable action name.</pre>
    /// <post>The returned value is the name that will be serialized for registration.</post>
    [JsonProperty("name", Order = 0)]
    public readonly string Name = name;

    /// <summary>
    /// Gets the human-readable description of what this action does.
    /// </summary>
    /// <pre>The descriptor was constructed with a human-readable description.</pre>
    /// <post>The returned value is the description that will be serialized for registration.</post>
    [JsonProperty("description", Order = 10)]
    public readonly string Description = description;

    /// <summary>
    /// Gets the optional JSON schema for validating action parameters.
    /// </summary>
    /// <pre>The descriptor may or may not carry a JSON schema.</pre>
    /// <post>The returned schema is the one that will be serialized for registration, or null when the action takes no structured payload.</post>
    [JsonProperty("schema", Order = 20)]
    public readonly JsonSchema? Schema = schema;
}