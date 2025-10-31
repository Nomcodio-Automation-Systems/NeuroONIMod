#nullable enable

using JetBrains.Annotations;
using NeuroSdk.Messages.API;
using NeuroSdk.Websocket;
using Newtonsoft.Json;

namespace NeuroSdk.Messages.Outgoing;

[PublicAPI]
public sealed class ActionResult(string id, ExecutionResult result) : OutgoingMessageBuilder
{
    protected override string Command => "action/result";

    [JsonProperty("id", Order = 0)]
    private readonly string _id = id;

    [JsonProperty("success", Order = 10)]
    private readonly bool _success = result.Successful;

    [JsonProperty("message", Order = 20)]
    private readonly string? _message = result.Message;
}