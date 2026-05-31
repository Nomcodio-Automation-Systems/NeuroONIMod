#nullable enable

using JetBrains.Annotations;
using NeuroSdk.Messages.Outgoing;
using NeuroSdk.Websocket;
using NeuroMod.Api;
using NeuroMod;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace NeuroSdk.Actions;

[PublicAPI]
/// <summary>
/// Provides registration and lifecycle coordination for Neuro actions.
/// </summary>
/// <pre>Actions are registered through the shared registration manager and may be resent or unregistered as the SDK lifecycle changes.</pre>
/// <post>Callers can query action registration state and coordinate registration cleanup through the static API surface.</post>
public sealed class NeuroActionHandler : MonoBehaviour
{
    private const string TAG = "NeuroActionHandler";

    /// <summary>
    /// Gets a registered action by name.
    /// </summary>
    /// <param name="name">The action name to look up.</param>
    /// <returns>The registered action, or <see langword="null"/> when not found.</returns>
    /// <pre><paramref name="name"/> identifies an action that may have been registered previously.</pre>
    /// <post>The corresponding registered action is returned when available.</post>
    public static INeuroAction? GetRegistered(string name)
    {
        return RegistrationManager.GetRegistered(name);
    }

    /// <summary>
    /// Determines whether an action name was recently unregistered.
    /// </summary>
    /// <param name="name">The action name to inspect.</param>
    /// <returns><see langword="true"/> when the action was recently unregistered; otherwise, <see langword="false"/>.</returns>
    /// <pre><paramref name="name"/> identifies an action name to check against recent unregister history.</pre>
    /// <post>The method reports whether the name is tracked as recently unregistered.</post>
    public static bool IsRecentlyUnregistered(string name)
    {
        return RegistrationManager.IsRecentlyUnregistered(name);
    }

    private void OnApplicationQuit()
    {
        try
        {
            var all = RegistrationManager.GetAllRegistered();
            NeuroMod.Integration.Api.ApiClient.SendImmediate(new ActionsUnregister(all));
        }
        catch (Exception ex)
        {
            NeuroLogger.LogException(ex, "NeuroActionHandler.OnApplicationQuit", "NeuroActionHandler");
        }
    }

    /// <summary>
    /// Registers a collection of actions.
    /// </summary>
    /// <param name="newActions">The actions to register.</param>
    /// <pre><paramref name="newActions"/> contains the actions that should be made available to the SDK.</pre>
    /// <post>The supplied actions are forwarded to the registration manager and become registered when successful.</post>
    public static void RegisterActions(IReadOnlyCollection<INeuroAction> newActions)
    {
        try
        {
            NeuroLogger.Log($"Registering {newActions.Count} actions", TAG);
            RegistrationManager.RegisterActions(newActions);
        }
        catch (Exception ex)
        {
            NeuroLogger.LogException(ex, "RegisterActions", TAG);
            throw;
        }
    }

    /// <summary>
    /// Registers a parameter array of actions.
    /// </summary>
    /// <param name="newActions">The actions to register.</param>
    /// <pre><paramref name="newActions"/> contains the actions that should be made available to the SDK.</pre>
    /// <post>The supplied actions are forwarded to the registration manager and become registered when successful.</post>
    public static void RegisterActions(params INeuroAction[] newActions)
    {
        try
        {
            NeuroLogger.Log($"Registering {newActions.Length} actions (params)", TAG);
            RegistrationManager.RegisterActions(newActions);
        }
        catch (Exception ex)
        {
            NeuroLogger.LogException(ex, "RegisterActions(params)", TAG);
            throw;
        }
    }

    /// <summary>
    /// Unregisters actions by name.
    /// </summary>
    /// <param name="removeActionsList">The action names to unregister.</param>
    /// <pre><paramref name="removeActionsList"/> identifies registered action names to remove.</pre>
    /// <post>The named actions are forwarded to the registration manager for removal.</post>
    public static void UnregisterActions(IEnumerable<string> removeActionsList)
    {
        try
        {
            NeuroLogger.Log($"Unregistering {removeActionsList.Count()} actions", TAG);
            RegistrationManager.UnregisterActions(removeActionsList);
        }
        catch (Exception ex)
        {
            NeuroLogger.LogException(ex, "UnregisterActions(IEnumerable<string>)", TAG);
            throw;
        }
    }

    /// <summary>
    /// Unregisters actions by instance.
    /// </summary>
    /// <param name="removeActionsList">The actions to unregister.</param>
    /// <pre><paramref name="removeActionsList"/> contains registered action instances to remove.</pre>
    /// <post>The supplied actions are forwarded to the registration manager for removal.</post>
    public static void UnregisterActions(IEnumerable<INeuroAction> removeActionsList)
    {
        try
        {
            NeuroLogger.Log($"Unregistering {removeActionsList.Count()} actions (INeuroAction list)", TAG);
            RegistrationManager.UnregisterActions(removeActionsList);
        }
        catch (Exception ex)
        {
            NeuroLogger.LogException(ex, "UnregisterActions(IEnumerable<INeuroAction>)", TAG);
            throw;
        }
    }

    /// <summary>
    /// Unregisters a parameter array of actions.
    /// </summary>
    /// <param name="removeActionsList">The actions to unregister.</param>
    /// <pre><paramref name="removeActionsList"/> contains registered action instances to remove.</pre>
    /// <post>The supplied actions are forwarded to the registration manager for removal.</post>
    public static void UnregisterActions(params INeuroAction[] removeActionsList)
    {
        try
        {
            NeuroLogger.Log($"Unregistering {removeActionsList.Length} actions (params)", TAG);
            RegistrationManager.UnregisterActions(removeActionsList);
        }
        catch (Exception ex)
        {
            NeuroLogger.LogException(ex, "UnregisterActions(params INeuroAction[])", TAG);
            throw;
        }
    }

    /// <summary>
    /// Unregisters a parameter array of action names.
    /// </summary>
    /// <param name="removeActionNamesList">The action names to unregister.</param>
    /// <pre><paramref name="removeActionNamesList"/> identifies registered action names to remove.</pre>
    /// <post>The named actions are forwarded to the registration manager for removal.</post>
    public static void UnregisterActions(params string[] removeActionNamesList)
    {
        try
        {
            NeuroLogger.Log($"Unregistering {removeActionNamesList.Length} actions (params string[])", TAG);
            RegistrationManager.UnregisterActions(removeActionNamesList);
        }
        catch (Exception ex)
        {
            NeuroLogger.LogException(ex, "UnregisterActions(params string[])", TAG);
            throw;
        }
    }

    /// <summary>
    /// Resends all currently registered actions to the remote endpoint.
    /// </summary>
    /// <pre>The registration manager currently holds action registrations that should be re-announced.</pre>
    /// <post>All tracked registrations are resent through the registration manager.</post>
    public static void ResendRegisteredActions()
    {
        try
        {
            NeuroLogger.Log("Resending registered actions", TAG);
            RegistrationManager.ResendRegisteredActions();
        }
        catch (Exception ex)
        {
            NeuroLogger.LogException(ex, "ResendRegisteredActions", TAG);
            throw;
        }
    }
}