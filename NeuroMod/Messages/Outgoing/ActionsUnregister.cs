#nullable enable

using NeuroSdk.Actions;
using NeuroSdk.Messages.API;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;

namespace NeuroSdk.Messages.Outgoing;

/// <summary>
/// Builds the outbound websocket message that unregisters actions from Neuro.
/// </summary>
/// <pre>Callers provide one or more action names that should no longer be available remotely.</pre>
/// <post>The builder serializes to an <c>actions/unregister</c> payload containing the action names to remove.</post>
public sealed class ActionsUnregister(IEnumerable<string> actionNames) : OutgoingMessageBuilder
{
    /// <summary>
    /// Creates an unregister-actions builder from action instances.
    /// </summary>
    /// <param name="actions">The actions whose names should be unregistered.</param>
    /// <pre><paramref name="actions"/> identifies the action set that should be removed remotely.</pre>
    /// <post>The builder contains the names of the supplied actions.</post>
    public ActionsUnregister(IEnumerable<INeuroAction> actions) : this(actions.Select(a => a.Name))
    {
    }

    /// <summary>
    /// Creates an unregister-actions builder from a params array of action instances.
    /// </summary>
    /// <param name="actions">The actions whose names should be unregistered.</param>
    /// <pre><paramref name="actions"/> identifies the action set that should be removed remotely.</pre>
    /// <post>The builder contains the names of the supplied actions.</post>
    public ActionsUnregister(params INeuroAction[] actions) : this((IEnumerable<INeuroAction>)actions)
    {
    }

    /// <summary>
    /// Creates an unregister-actions builder from a params array of action names.
    /// </summary>
    /// <param name="actionNames">The action names to unregister.</param>
    /// <pre><paramref name="actionNames"/> identifies the remote action names that should be removed.</pre>
    /// <post>The builder contains the supplied action names.</post>
    public ActionsUnregister(params string[] actionNames) : this((IEnumerable<string>)actionNames)
    {
    }

    /// <summary>
    /// Gets the websocket command name for action unregistration.
    /// </summary>
    protected override string Command => "actions/unregister";

    /// <summary>
    /// Gets the action names that will be removed remotely.
    /// </summary>
    [JsonProperty("action_names")]
    public readonly List<string> Names = [.. actionNames];

    /// <summary>
    /// Merges another unregister builder into this one, de-duplicating names.
    /// </summary>
    /// <param name="other">The other outgoing builder.</param>
    /// <returns><see langword="true"/> when the other builder was an <see cref="ActionsUnregister"/> and the merge succeeded.</returns>
    /// <pre><paramref name="other"/> is another outbound builder that may also unregister actions.</pre>
    /// <post>When merged, this builder contains one entry per action name scheduled for unregistration.</post>
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