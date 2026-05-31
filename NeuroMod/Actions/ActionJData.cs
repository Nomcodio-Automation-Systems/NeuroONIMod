#nullable enable

using JetBrains.Annotations;
using Newtonsoft.Json.Linq;
using System;

namespace NeuroSdk.Actions;

/// <summary>
/// A wrapper class for the data of an <see cref="NeuroSdk.Messages.Incoming.Action"/> message.
/// </summary>
/// <pre>The SDK has received stringified JSON payload data for an incoming action request.</pre>
/// <post>The payload is exposed as a parsed <see cref="JToken"/> when parsing succeeds.</post>
[PublicAPI]
public sealed class ActionJData
{
    /// <summary>
    /// Gets the parsed JSON payload token, if any.
    /// </summary>
    /// <pre>The incoming action payload has already been parsed or determined to be absent.</pre>
    /// <post>The returned token is the current parsed payload snapshot for this wrapper.</post>
    public JToken? Data { get; private set; }

    private ActionJData()
    {
    }

    /// <summary>
    /// Parses the stored string payload into a JSON token.
    /// </summary>
    /// <param name="stringifiedData">The raw string payload received with the action message.</param>
    /// <pre><paramref name="stringifiedData"/> is either null, empty, or valid JSON text.</pre>
    /// <post><see cref="Data"/> is null for empty input or contains the parsed JSON token for valid input.</post>
    private void DeserializeFromJson(string? stringifiedData)
    {
        if (stringifiedData is null or "")
        {
            Data = null;
            return;
        }

        Data = JToken.Parse(stringifiedData);
    }

    /// <summary>
    /// Attempts to parse stringified action payload data into an <see cref="ActionJData"/> wrapper.
    /// </summary>
    /// <param name="stringifiedData">The raw string payload received with the action message.</param>
    /// <param name="actionJData">Receives the parsed wrapper when parsing succeeds.</param>
    /// <returns><see langword="true"/> when parsing succeeded; otherwise <see langword="false"/>.</returns>
    /// <pre><paramref name="stringifiedData"/> is either null, empty, or JSON that should be parseable by <see cref="JToken.Parse(string)"/>.</pre>
    /// <post>On success <paramref name="actionJData"/> contains the parsed wrapper; on failure it is null and the parse error has been logged.</post>
    internal static bool TryParse(string? stringifiedData, out ActionJData? actionJData)
    {
        try
        {
            actionJData = new ActionJData();
            actionJData.DeserializeFromJson(stringifiedData);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to deserialize ActionJData from string.");
            Debug.LogError(e);
            actionJData = null;
            return false;
        }
    }
}