#nullable enable

using NeuroSdk.Actions;
using NeuroSdk.Messages.API;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;

namespace NeuroSdk.Messages.Outgoing;

public sealed class ActionsRegister(IEnumerable<INeuroAction> actions) : OutgoingMessageBuilder
{
    public ActionsRegister(params INeuroAction[] actions) : this((IEnumerable<INeuroAction>)actions)
    {
    }

    protected override string Command => "actions/register";

    [JsonProperty("actions")]
    public readonly List<WsAction> Actions = [.. actions.Select(action => action.GetWsAction())];

    public override bool Merge(OutgoingMessageBuilder other)
    {
        if (other is ActionsRegister actionsRegister)
        {
            Actions.RemoveAll(existingWsa => actionsRegister.Actions.Any(newWsa => newWsa.Name == existingWsa.Name));
            Actions.AddRange(actionsRegister.Actions);
            return true;
        }

        return false;
    }
}