#nullable enable

using NeuroSdk.Json;
using Newtonsoft.Json;

namespace NeuroSdk.Actions;

/// <summary>
/// Represents an action that can be executed via WebSocket communication
/// </summary>
/// <param name="name">The unique name identifier for the action</param>
/// <param name="description">Human-readable description of what the action does</param>
/// <param name="schema">Optional JSON schema for validating action parameters</param>
public readonly struct WsAction(string name, string description, JsonSchema? schema)
{
    /// <summary>
    /// The unique name identifier for this action
    /// </summary>
    [JsonProperty("name", Order = 0)]
    public readonly string Name = name;

    /// <summary>
    /// Human-readable description of what this action does
    /// </summary>
    [JsonProperty("description", Order = 10)]
    public readonly string Description = description;

    /// <summary>
    /// Optional JSON schema for validating action parameters
    /// </summary>
    [JsonProperty("schema", Order = 20)]
    public readonly JsonSchema? Schema = schema;
}