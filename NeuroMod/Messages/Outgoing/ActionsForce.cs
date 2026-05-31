#nullable enable

using NeuroSdk.Actions;
using NeuroSdk.Messages.API;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;

namespace NeuroSdk.Messages.Outgoing;

/// <summary>
/// Builds the outbound websocket message that force-prompts Neuro with a specific action subset.
/// </summary>
/// <pre>Callers provide a query plus the action set that Neuro may choose from.</pre>
/// <post>The builder serializes to an <c>actions/force</c> payload containing the query and selected action names.</post>
public sealed class ActionsForce(string query, string? state, bool? ephemeralContext, IEnumerable<INeuroAction> actions) : OutgoingMessageBuilder
{
    /// <summary>
    /// Creates a force-actions builder from a params array of actions.
    /// </summary>
    /// <param name="query">The prompt or instruction to send.</param>
    /// <param name="state">Optional serialized state to send with the query.</param>
    /// <param name="ephemeralContext">Whether the associated context should be treated as ephemeral.</param>
    /// <param name="actions">The actions that Neuro may choose from.</param>
    /// <pre><paramref name="actions"/> identifies the action set that should be exposed to the forced prompt.</pre>
    /// <post>The builder targets exactly the supplied actions.</post>
    public ActionsForce(string query, string? state, bool? ephemeralContext, params INeuroAction[] actions)
        : this(query, state, ephemeralContext, (IEnumerable<INeuroAction>)actions)
    {
    }

    /// <summary>
    /// Gets the websocket command name for force-action prompts.
    /// </summary>
    protected override string Command => "actions/force";

    [JsonProperty("state", Order = 0)]
    private readonly string? _state = state;

    [JsonProperty("query", Order = 10)]
    private readonly string _query = query;

    [JsonProperty("ephemeral_context", Order = 20)]
    private readonly bool? _ephemeralContext = ephemeralContext;

    [JsonProperty("action_names", Order = 30)]
    private readonly string[] _actionNames = [.. actions.Select(a => a.Name)];
}