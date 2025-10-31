#nullable enable

using JetBrains.Annotations;
using NeuroSdk.Messages.API;
using NeuroSdk.Websocket;
using Newtonsoft.Json;

namespace NeuroSdk.Messages.Outgoing;

[PublicAPI]
public sealed class Context(string message, bool silent = false) : OutgoingMessageBuilder
{
    protected override string Command => "context";

    [JsonProperty("message", Order = 0)]
    public readonly string Message = message;

    [JsonProperty("silent", Order = 10)]
    private readonly bool _silent = silent;

    public static void Send(string message, bool silent = false)
    {
        WebsocketConnection.Instance!.Send(new Context(message, silent));
    }
}