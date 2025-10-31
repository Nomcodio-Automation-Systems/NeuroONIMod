#nullable enable

using JetBrains.Annotations;
using NeuroSdk.Messages.API;
using NeuroSdk.Messages.Outgoing;
using System.Collections.Generic;
using UnityEngine;

namespace NeuroSdk.Websocket;

[PublicAPI]
public class MessageQueue : MonoBehaviour
{
    protected readonly List<OutgoingMessageBuilder> Messages = [new Startup()];

    public virtual int Count
    {
        get
        {
            lock (Messages)
            {
                return Messages.Count;
            }
        }
    }

    public virtual void Enqueue(OutgoingMessageBuilder message)
    {
        lock (Messages)
        {
            foreach (OutgoingMessageBuilder existingMessage in Messages)
            {
                if (existingMessage.Merge(message))
                {
                    return;
                }
            }

            Messages.Add(message);
        }
    }

    public virtual OutgoingMessageBuilder? Dequeue()
    {
        lock (Messages)
        {
            if (Messages.Count == 0)
            {
                return null;
            }

            OutgoingMessageBuilder message = Messages[0];
            Messages.RemoveAt(0);

            return message;
        }
    }
}