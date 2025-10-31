#nullable enable

using NeuroSdk.Actions;
using NeuroSdk.Messages.API;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;

namespace NeuroSdk.Messages.Outgoing;

public sealed class ActionsForce(string query, string? state, bool? ephemeralContext, IEnumerable<INeuroAction> actions) : OutgoingMessageBuilder
{
    public ActionsForce(string query, string? state, bool? ephemeralContext, params INeuroAction[] actions)
        : this(query, state, ephemeralContext, (IEnumerable<INeuroAction>)actions)
    {
    }

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