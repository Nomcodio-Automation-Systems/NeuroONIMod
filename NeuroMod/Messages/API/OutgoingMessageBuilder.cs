#nullable enable

using NeuroSdk.Websocket;
using System;

namespace NeuroSdk.Messages.API;

public abstract class OutgoingMessageBuilder
{
    protected abstract string Command { get; }

    protected virtual object? Data => this;

    public virtual bool Merge(OutgoingMessageBuilder other)
    {
        return false;
    }

    public WsMessage GetWsMessage()
    {
        return new(Command, Data, WebsocketConnection.Instance?.game ?? throw new InvalidOperationException("Cannot get WsMessage without a WebsocketConnection instance."));
    }
}