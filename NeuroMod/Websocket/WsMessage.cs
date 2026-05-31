#nullable enable

using Newtonsoft.Json;
using System;

namespace NeuroSdk.Websocket;

/// <summary>
/// Represents a WebSocket message sent to the Neuro SDK server
/// </summary>
/// <pre>
/// The command, game, and optional data payload already conform to the outbound websocket protocol.
/// </pre>
/// <post>
/// Instances can be serialized as stable websocket payloads for outbound SDK communication.
/// </post>
/// <remarks>
/// Initializes a new instance of the WsMessage class
/// </remarks>
/// <param name="command">The command to execute</param>
/// <param name="data">Optional data payload for the command</param>
/// <param name="game">The game identifier</param>
public class WsMessage(string command, object? data, string game)
{
    /// <summary>
    /// The command to be executed on the server
    /// </summary>
    [JsonProperty("command", Order = 0)]
    public string Command { get; } = command ?? throw new ArgumentNullException(nameof(command));

    /// <summary>
    /// The game identifier for this message
    /// </summary>
    [JsonProperty("game", Order = 10)]
    public string Game { get; } = game ?? throw new ArgumentNullException(nameof(game));

    /// <summary>
    /// Optional data payload associated with the command
    /// </summary>
    [JsonProperty("data", Order = 20)]
    public object? Data { get; } = data;

    /// <summary>
    /// Determines whether the specified object is equal to the current object
    /// </summary>
    /// <param name="obj">The object to compare with the current object</param>
    /// <returns>True if the specified object is equal to the current object; otherwise, false</returns>
    /// <pre>
    /// <paramref name="obj"/> may or may not be another websocket message instance.
    /// </pre>
    /// <post>
    /// The method reports value equality across command, game, and data payload.
    /// </post>
    public override bool Equals(object? obj)
    {
        return obj is not WsMessage other
            ? false
            : Command == other.Command &&
               Game == other.Game &&
               Equals(Data, other.Data);
    }

    /// <summary>
    /// Serves as the default hash function
    /// </summary>
    /// <returns>A hash code for the current object</returns>
    /// <pre>
    /// The websocket message has immutable command, game, and data fields.
    /// </pre>
    /// <post>
    /// A hash code consistent with <see cref="Equals(object?)"/> is returned.
    /// </post>
    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = (hash * 23) + (Command?.GetHashCode() ?? 0);
            hash = (hash * 23) + (Game?.GetHashCode() ?? 0);
            hash = (hash * 23) + (Data?.GetHashCode() ?? 0);
            return hash;
        }
    }

    /// <summary>
    /// Returns a string that represents the current object
    /// </summary>
    /// <returns>A string that represents the current object</returns>
    /// <pre>
    /// The message fields have already been initialized.
    /// </pre>
    /// <post>
    /// A diagnostic string representation of the websocket message is returned.
    /// </post>
    public override string ToString()
    {
        return $"WsMessage {{ Command = {Command}, Game = {Game}, Data = {Data} }}";
    }
}