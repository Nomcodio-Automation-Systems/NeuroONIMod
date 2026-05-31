using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using NeuroSdk.Actions;
using NeuroSdk.Messages.Outgoing;
using NeuroSdk.Websocket;

namespace NeuroMod.Api
{
    /// <summary>
    /// Centralized registration API for actions and event-like subscriptions.
    /// Adds aggressive debug logging and anti-spam for repeated messages.
    /// Keep this thin and testable; existing callers call NeuroActionHandler which delegates here.
    /// </summary>
    /// <pre>Incoming action sets are authoritative for the names they contain.</pre>
    /// <post>Registration state remains internally consistent even when websocket forwarding fails.</post>
    public static class RegistrationManager
    {
        private static readonly List<INeuroAction> _registered = new();
        private static readonly List<INeuroAction> _recentlyUnregistered = new();

        private static readonly TimeSpan LogThrottle = TimeSpan.FromSeconds(2);

        /// <summary>
        /// Gets the currently registered action with the specified name.
        /// </summary>
        /// <param name="name">Action name to resolve.</param>
        /// <returns>The registered action, or <see langword="null"/> when none is present.</returns>
        /// <pre><paramref name="name"/> identifies an action name used in the registration cache.</pre>
        /// <post>The registration cache is not modified by the lookup.</post>
        public static INeuroAction? GetRegistered(string name)
        {
            try
            {
                return _registered.FirstOrDefault(a => a.Name == name);
            }
            catch (Exception ex)
            {
                NeuroLogger.LogException(ex, "RegistrationManager.GetRegistered", "RegistrationManager");
                return null;
            }
        }

        /// <summary>
        /// Determines whether an action name was recently unregistered and is still inside the grace window.
        /// </summary>
        /// <param name="name">Action name to query.</param>
        /// <returns><see langword="true"/> when the action name is still tracked as recently removed; otherwise, <see langword="false"/>.</returns>
        /// <pre><paramref name="name"/> identifies an action name that may have been unregistered recently.</pre>
        /// <post>The registration state is not mutated by the query.</post>
        public static bool IsRecentlyUnregistered(string name)
        {
            try
            {
                return _recentlyUnregistered.Any(a => a.Name == name);
            }
            catch (Exception ex)
            {
                NeuroLogger.LogException(ex, "RegistrationManager.IsRecentlyUnregistered", "RegistrationManager");
                return false;
            }
        }

        /// <summary>
        /// Registers or replaces the supplied actions and forwards the result to the websocket layer.
        /// </summary>
        /// <param name="newActions">Actions that should be active after registration completes.</param>
        /// <pre><paramref name="newActions"/> contains the full replacement definitions for any duplicate names it includes.</pre>
        /// <post>The internal registry contains the supplied actions and no stale entries with the same names.</post>
        public static void RegisterActions(IReadOnlyCollection<INeuroAction> newActions)
        {
            if (newActions == null || newActions.Count == 0)
                return;

            try
            {
                var names = string.Join(", ", newActions.Select(a => a.Name));
                if (LogThrottler.ShouldLog("RegisterActions:" + names, LogThrottle))
                    NeuroLogger.LogDebug($"Registering {newActions.Count} actions: {names}", "RegistrationManager");

                // Remove duplicates
                var existingToRemove = _registered.Where(old => newActions.Any(n => n.Name == old.Name)).ToList();
                if (existingToRemove.Any())
                {
                    if (LogThrottler.ShouldLog("RemoveExisting:" + string.Join(", ", existingToRemove.Select(a => a.Name)), LogThrottle))
                        NeuroLogger.LogDebug($"Removing {existingToRemove.Count} existing actions: {string.Join(", ", existingToRemove.Select(a => a.Name))}", "RegistrationManager");
                    _registered.RemoveAll(existingToRemove.Contains);
                }

                _recentlyUnregistered.RemoveAll(old => newActions.Any(n => n.Name == old.Name));

                _registered.AddRange(newActions);

                if (LogThrottler.ShouldLog("TotalRegistered", LogThrottle))
                    NeuroLogger.LogDebug($"Total registered actions: {_registered.Count}", "RegistrationManager");

                // Send to websocket when available, but catch errors so registration isn't fragile
                try
                {
                    NeuroMod.Integration.Api.ApiClient.Send(new ActionsRegister(newActions));
                }
                catch (Exception ex)
                {
                    NeuroLogger.LogException(ex, "RegistrationManager.SendRegister", "RegistrationManager");
                }
            }
            catch (Exception ex)
            {
                NeuroLogger.LogException(ex, "RegistrationManager.RegisterActions", "RegistrationManager");
            }
        }

        /// <summary>
        /// Registers the supplied actions and forwards to <see cref="RegisterActions(IReadOnlyCollection{INeuroAction})"/>.
        /// </summary>
        /// <param name="newActions">Actions that should be active after registration completes.</param>
        /// <pre><paramref name="newActions"/> contains action definitions that may replace existing actions with the same names.</pre>
        /// <post>The registration state matches the supplied action definitions for the provided names.</post>
        public static void RegisterActions(params INeuroAction[] newActions)
        {
            RegisterActions((IReadOnlyCollection<INeuroAction>)newActions);
        }

        /// <summary>
        /// Unregisters actions by name and records them as recently removed for a short grace period.
        /// </summary>
        /// <param name="removeActionNames">Names of actions to remove.</param>
        /// <pre><paramref name="removeActionNames"/> identifies currently registered actions or benign misses.</pre>
        /// <post>Matching actions are removed from the registry and mirrored to websocket consumers when possible.</post>
        public static void UnregisterActions(IEnumerable<string> removeActionNames)
        {
            if (removeActionNames == null) return;

            try
            {
                var removeList = removeActionNames.ToArray();
                var toRemove = _registered.Where(r => removeList.Any(n => n == r.Name)).ToArray();
                if (!toRemove.Any())
                {
                    if (LogThrottler.ShouldLog("UnregisterNone:" + string.Join(", ", removeList), LogThrottle))
                        NeuroLogger.LogDebug($"Unregister requested but no matching registered actions found: {string.Join(", ", removeList)}", "RegistrationManager");
                    return;
                }

                foreach (var a in toRemove)
                {
                    _registered.Remove(a);
                    _recentlyUnregistered.Add(a);
                }

                // schedule clearing of recently unregistered entries
                _ = Task.Run(async () =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(10));
                    foreach (var a in toRemove) _recentlyUnregistered.RemoveAll(x => x.Name == a.Name);
                });

                try
                {
                    NeuroMod.Integration.Api.ApiClient.Send(new ActionsUnregister(removeList));
                }
                catch (Exception ex)
                {
                    NeuroLogger.LogException(ex, "RegistrationManager.SendUnregister", "RegistrationManager");
                }
            }
            catch (Exception ex)
            {
                NeuroLogger.LogException(ex, "RegistrationManager.UnregisterActions", "RegistrationManager");
            }
        }

        /// <summary>
        /// Unregisters the supplied action objects by their names.
        /// </summary>
        /// <param name="removeActions">Actions whose names should be removed from the registry.</param>
        /// <pre><paramref name="removeActions"/> contains actions with stable names that can be removed from registration state.</pre>
        /// <post>Any matching registered names are removed and tracked as recently unregistered.</post>
        public static void UnregisterActions(IEnumerable<INeuroAction> removeActions)
        {
            UnregisterActions(removeActions.Select(a => a.Name));
        }

        /// <summary>
        /// Unregisters the supplied action objects by their names.
        /// </summary>
        /// <param name="removeActions">Actions whose names should be removed from the registry.</param>
        /// <pre><paramref name="removeActions"/> contains action names that may currently exist in the registry.</pre>
        /// <post>Any matching registered names are removed and tracked as recently unregistered.</post>
        public static void UnregisterActions(params INeuroAction[] removeActions)
        {
            UnregisterActions((IEnumerable<INeuroAction>)removeActions);
        }

        /// <summary>
        /// Unregisters the supplied action names.
        /// </summary>
        /// <param name="removeActionNames">Names to remove from the registry.</param>
        /// <pre><paramref name="removeActionNames"/> contains action names that may currently be registered.</pre>
        /// <post>Any matching registered names are removed and tracked as recently unregistered.</post>
        public static void UnregisterActions(params string[] removeActionNames)
        {
            UnregisterActions((IEnumerable<string>)removeActionNames);
        }

        /// <summary>
        /// Re-sends the current registration snapshot to websocket consumers.
        /// </summary>
        /// <pre>The internal registry already contains the authoritative action snapshot to mirror.</pre>
        /// <post>Websocket consumers receive a fresh registration payload when sending succeeds.</post>
        public static void ResendRegisteredActions()
        {
            try
            {
                NeuroMod.Integration.Api.ApiClient.Send(new ActionsRegister(_registered));
                NeuroLogger.LogDebug($"Resent {_registered.Count} registered actions", "RegistrationManager");
            }
            catch (Exception ex)
            {
                NeuroLogger.LogException(ex, "RegistrationManager.ResendRegisteredActions", "RegistrationManager");
            }
        }

        /// <summary>
        /// Returns snapshot of all currently registered actions.
        /// </summary>
        /// <returns>A snapshot of the current registration state.</returns>
        /// <pre>The internal registry may be mutated by registration calls while the snapshot is being requested.</pre>
        /// <post>A detached collection is returned so callers cannot mutate the internal registry directly.</post>
        public static IReadOnlyCollection<INeuroAction> GetAllRegistered()
        {
            return _registered.ToArray();
        }
    }
}
