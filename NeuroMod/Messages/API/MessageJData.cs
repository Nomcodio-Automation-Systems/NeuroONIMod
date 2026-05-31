#nullable enable

using Newtonsoft.Json.Linq;

namespace NeuroSdk.Messages.API;

/// <summary>
/// Wraps the raw JSON payload token extracted from an incoming websocket message.
/// </summary>
/// <pre>The websocket layer has already parsed the envelope into a <see cref="JToken"/>.</pre>
/// <post>The payload token is exposed immutably for message-handler validation.</post>
public readonly struct MessageJData(JToken? data)
{
    /// <summary>
    /// Gets the raw payload token supplied with the incoming message.
    /// </summary>
    /// <pre>The incoming message may or may not have a data payload.</pre>
    /// <post>The value is the exact token extracted from the incoming envelope, or null when no data was supplied.</post>
    public readonly JToken? Data = data;
};