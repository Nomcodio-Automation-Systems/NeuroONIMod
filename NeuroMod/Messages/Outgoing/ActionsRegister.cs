#nullable enable

using NeuroSdk.Actions;
using NeuroSdk.Messages.API;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;

namespace NeuroSdk.Messages.Outgoing;

/// <summary>
/// Builds the outbound websocket message that registers actions with Neuro.
/// </summary>
/// <pre>Callers provide one or more action definitions that should be announced remotely.</pre>
/// <post>The builder serializes to an <c>actions/register</c> payload containing websocket-safe action descriptors.</post>
public sealed class ActionsRegister(IEnumerable<INeuroAction> actions) : OutgoingMessageBuilder
{
    /// <summary>
    /// Creates a register-actions builder from a params array of actions.
    /// </summary>
    /// <param name="actions">The actions to register.</param>
    /// <pre><paramref name="actions"/> identifies the action set that should be registered remotely.</pre>
    /// <post>The builder contains websocket descriptors for the supplied actions.</post>
    public ActionsRegister(params INeuroAction[] actions) : this((IEnumerable<INeuroAction>)actions)
    {
    }

    /// <summary>
    /// Gets the websocket command name for action registration.
    /// </summary>
    protected override string Command => "actions/register";

    /// <summary>
    /// Gets the websocket-safe action descriptors that will be serialized.
    /// </summary>
    [JsonProperty("actions")]
    public readonly List<WsAction> Actions = [.. actions.Select(action => action.GetWsAction())];

    /// <summary>
    /// Merges another registration builder into this one, replacing duplicate action names.
    /// </summary>
    /// <param name="other">The other outgoing builder.</param>
    /// <returns><see langword="true"/> when the other builder was an <see cref="ActionsRegister"/> and the merge succeeded.</returns>
    /// <pre><paramref name="other"/> is another outbound builder that may also register actions.</pre>
    /// <post>When merged, this builder contains one descriptor per action name using the newer registration data.</post>
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