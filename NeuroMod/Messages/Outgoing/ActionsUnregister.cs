#nullable enable

using NeuroSdk.Actions;
using NeuroSdk.Messages.API;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;

namespace NeuroSdk.Messages.Outgoing;

public sealed class ActionsUnregister(IEnumerable<string> actionNames) : OutgoingMessageBuilder
{
    public ActionsUnregister(IEnumerable<INeuroAction> actions) : this(actions.Select(a => a.Name))
    {
    }

    public ActionsUnregister(params INeuroAction[] actions) : this((IEnumerable<INeuroAction>)actions)
    {
    }

    public ActionsUnregister(params string[] actionNames) : this((IEnumerable<string>)actionNames)
    {
    }

    protected override string Command => "actions/unregister";

    [JsonProperty("action_names")]
    public readonly List<string> Names = [.. actionNames];

    public override bool Merge(OutgoingMessageBuilder other)
    {
        if (other is ActionsUnregister actionsUnregister)
        {
            Names.RemoveAll(existingName => actionsUnregister.Names.Contains(existingName));
            Names.AddRange(actionsUnregister.Names);
            return true;
        }

        return false;
    }
}