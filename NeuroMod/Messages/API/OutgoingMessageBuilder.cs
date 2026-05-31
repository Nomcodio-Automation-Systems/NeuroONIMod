#nullable enable

using NeuroSdk.Websocket;
using System;

namespace NeuroSdk.Messages.API;

/// <summary>
/// Base type for objects that serialize into outbound websocket messages.
/// </summary>
/// <pre>Derived types provide a command name and the payload data to send.</pre>
/// <post>Instances can build <see cref="WsMessage"/> envelopes and optionally merge with compatible outbound messages.</post>
public abstract class OutgoingMessageBuilder
{
    /// <summary>
    /// Gets the websocket command name for the outgoing message.
    /// </summary>
    /// <pre>Derived types expose the protocol command they represent.</pre>
    /// <post>The returned command name is used in the outgoing websocket envelope.</post>
    protected abstract string Command { get; }

    /// <summary>
    /// Gets the payload object to serialize into the outgoing websocket message.
    /// </summary>
    /// <pre>Derived types may override this when the serialized payload differs from the builder instance itself.</pre>
    /// <post>The returned object is what will be placed into the message envelope's data field.</post>
    protected virtual object? Data => this;

    /// <summary>
    /// Attempts to merge another builder into this builder to reduce outbound message duplication.
    /// </summary>
    /// <param name="other">The other outgoing builder.</param>
    /// <returns><see langword="true"/> when the merge succeeded; otherwise <see langword="false"/>.</returns>
    /// <pre><paramref name="other"/> is another outbound builder of the same protocol family.</pre>
    /// <post>When <see langword="true"/>, this instance contains the merged outbound state of both builders.</post>
    public virtual bool Merge(OutgoingMessageBuilder other)
    {
        return false;
    }

    /// <summary>
    /// Builds the websocket envelope for this outgoing message.
    /// </summary>
    /// <returns>The websocket message ready for serialization and sending.</returns>
    /// <pre>The builder exposes a valid command name and payload object for the outbound protocol.</pre>
    /// <post>The returned message contains this builder's command, payload, and the current game identifier when available.</post>
    public WsMessage GetWsMessage()
    {
        // Allow tests and non-Unity contexts to build messages even when no
        // WebsocketConnection singleton is present. Use empty game id as a
        // safe default to avoid throwing during test discovery/serialization.
        string gameId = WebsocketConnection.Instance?.game ?? string.Empty;
        return new(Command, Data, gameId);
    }
}