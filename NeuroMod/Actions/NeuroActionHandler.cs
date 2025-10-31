#nullable enable

using JetBrains.Annotations;
using NeuroSdk.Messages.Outgoing;
using NeuroSdk.Websocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace NeuroSdk.Actions;

[PublicAPI]
public sealed class NeuroActionHandler : MonoBehaviour
{
    private static List<INeuroAction> _currentlyRegisteredActions = [];
    private static readonly List<INeuroAction> _dyingActions = [];

    public static INeuroAction? GetRegistered(string name)
    {
        return _currentlyRegisteredActions.FirstOrDefault(a => a.Name == name);
    }

    public static bool IsRecentlyUnregistered(string name)
    {
        return _dyingActions.Any(a => a.Name == name);
    }

    private void OnApplicationQuit()
    {
        // Only send if WebSocket connection is available
        WebsocketConnection.Instance?.SendImmediate(new ActionsUnregister(_currentlyRegisteredActions));
        _currentlyRegisteredActions = null!;
    }

    public static void RegisterActions(IReadOnlyCollection<INeuroAction> newActions)
    {
        // Log what we're trying to register
        Debug.Log($"[NeuroActionHandler] Registering {newActions.Count} actions: {string.Join(", ", newActions.Select(a => a.Name))}");

        // Remove any existing actions with the same names
        List<INeuroAction> existingActionsToRemove = _currentlyRegisteredActions.Where(oldAction =>
            newActions.Any(newAction => oldAction.Name == newAction.Name)).ToList();

        if (existingActionsToRemove.Any())
        {
            Debug.Log($"[NeuroActionHandler] Removing {existingActionsToRemove.Count} existing actions: {string.Join(", ", existingActionsToRemove.Select(a => a.Name))}");
            _currentlyRegisteredActions.RemoveAll(existingActionsToRemove.Contains);
        }

        _dyingActions.RemoveAll(oldAction => newActions.Any(newAction => oldAction.Name == newAction.Name));

        // Add the new actions
        _currentlyRegisteredActions.AddRange(newActions);

        Debug.Log($"[NeuroActionHandler] Total registered actions: {_currentlyRegisteredActions.Count}");

        // Only send to WebSocket if connection is available
        WebsocketConnection.Instance?.Send(new ActionsRegister(newActions));
    }

    public static void RegisterActions(params INeuroAction[] newActions)
    {
        RegisterActions((IReadOnlyCollection<INeuroAction>)newActions);
    }

    public static void UnregisterActions(IEnumerable<string> removeActionsList)
    {
        INeuroAction[] actionsToRemove = [.. _currentlyRegisteredActions.Where(oldAction => removeActionsList.Any(removeAction => oldAction.Name == removeAction))];

        _currentlyRegisteredActions.RemoveAll(actionsToRemove.Contains);
        _dyingActions.AddRange(actionsToRemove);
        _ = Task.Run(async () => await removeActions());

        // Only send if WebSocket connection is available
        WebsocketConnection.Instance?.Send(new ActionsUnregister(removeActionsList));

        return;

        async Task removeActions()
        {
            await Task.Delay(TimeSpan.FromMilliseconds(10000));
            _dyingActions.RemoveAll(actionsToRemove.Contains);
        }
    }

    public static void UnregisterActions(IEnumerable<INeuroAction> removeActionsList)
    {
        UnregisterActions(removeActionsList.Select(a => a.Name));
    }

    public static void UnregisterActions(params INeuroAction[] removeActionsList)
    {
        UnregisterActions((IReadOnlyCollection<INeuroAction>)removeActionsList);
    }

    public static void UnregisterActions(params string[] removeActionNamesList)
    {
        UnregisterActions((IReadOnlyCollection<string>)removeActionNamesList);
    }

    public static void ResendRegisteredActions()
    {
        // Only send if WebSocket connection is available
        WebsocketConnection.Instance?.Send(new ActionsRegister(_currentlyRegisteredActions));
    }
}