#nullable enable

using JetBrains.Annotations;
using NeuroSdk.Messages.API;

namespace NeuroSdk.Messages.Outgoing;

[PublicAPI]
/// <summary>
/// Builds the outbound websocket startup message used to announce game startup.
/// </summary>
/// <pre>The game integration is ready to send its startup handshake.</pre>
/// <post>The builder serializes to a <c>startup</c> message with no payload body.</post>
public sealed class Startup : OutgoingMessageBuilder
{
    /// <summary>
    /// Gets the websocket command name for startup messages.
    /// </summary>
    protected override string Command => "startup";

    /// <summary>
    /// Gets the payload for the startup message.
    /// </summary>
    /// <pre>The startup protocol does not require a payload body.</pre>
    /// <post>The returned payload is null.</post>
    protected override object? Data => null;
}