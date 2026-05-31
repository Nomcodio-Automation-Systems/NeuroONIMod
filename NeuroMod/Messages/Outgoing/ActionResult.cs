#nullable enable

using JetBrains.Annotations;
using NeuroSdk.Messages.API;
using NeuroSdk.Websocket;
using Newtonsoft.Json;

namespace NeuroSdk.Messages.Outgoing;

[PublicAPI]
/// <summary>
/// Builds the outbound websocket message that reports an action execution result.
/// </summary>
/// <pre>The action protocol has produced an action id and final <see cref="ExecutionResult"/>.</pre>
/// <post>The builder serializes to an <c>action/result</c> payload that echoes the original action id.</post>
public sealed class ActionResult(string id, ExecutionResult result) : OutgoingMessageBuilder
{
    /// <summary>
    /// Gets the websocket command name for action results.
    /// </summary>
    protected override string Command => "action/result";

    [JsonProperty("id", Order = 0)]
    private readonly string _id = id;

    [JsonProperty("success", Order = 10)]
    private readonly bool _success = result.Successful;

    [JsonProperty("message", Order = 20)]
    private readonly string? _message = result.Message;
}