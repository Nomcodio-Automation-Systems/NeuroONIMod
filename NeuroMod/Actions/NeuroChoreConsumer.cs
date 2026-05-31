using NeuroMod.Integration;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace NeuroMod
{
    /// <summary>
    /// Custom component that forces Neuro to prioritize specific chores.
    /// Works by boosting chore priority modifiers extremely high.
    /// </summary>
    /// <pre>The component is attached to the configured Neuro duplicant and can access its base <see cref="ChoreConsumer"/>.</pre>
    /// <post>The component can temporarily elevate one chore above all others and later restore the original priority state.</post>
    public class NeuroChoreConsumer : MonoBehaviour
    {
        private ChoreConsumer? _originalConsumer;
        private MinionIdentity? _minion;
        private Chore? _forcedChore;
        private bool _isActive = false;
        private PrioritySetting _originalPriority;

        /// <summary>
        /// Initializes the wrapper around the duplicant's underlying chore consumer.
        /// </summary>
        /// <param name="minion">The duplicant that owns the chore consumer to be wrapped.</param>
        /// <pre><paramref name="minion"/> refers to a duplicate that should already have a <see cref="ChoreConsumer"/> component.</pre>
        /// <post>The component is ready to force chores when the underlying consumer exists; otherwise the failure is logged.</post>
        public void Initialize(MinionIdentity minion)
        {
            _minion = minion;
            _originalConsumer = minion.GetComponent<ChoreConsumer>();

            if (_originalConsumer == null)
            {
                NeuroLogger.LogError(
                    "Cannot initialize NeuroChoreConsumer - no ChoreConsumer found",
                    "NeuroChoreConsumer"
                );
                return;
            }

            NeuroLogger.Log("NeuroChoreConsumer initialized", "NeuroChoreConsumer");
        }

        /// <summary>
        /// Forces the duplicant to prioritize a specific chore by temporarily boosting its priority.
        /// </summary>
        /// <param name="targetChore">The chore that should outrank all competing work.</param>
        /// <returns><see langword="true"/> when the chore was successfully forced; otherwise <see langword="false"/>.</returns>
        /// <pre><paramref name="targetChore"/> is a live chore compatible with the wrapped duplicant.</pre>
        /// <post>On success, the target chore is stored as the forced chore and its effective priority has been elevated.</post>
        public bool ForceChore(Chore targetChore)
        {
            if (_originalConsumer == null || targetChore == null)
            {
                return false;
            }

            try
            {
                // Clear any previous forced chore
                ClearForcedChore();

                _forcedChore = targetChore;
                _isActive = true;

                // Save original priority
                _originalPriority = targetChore.masterPriority;

                NeuroLogger.Log($"Forcing chore: {targetChore.choreType.Name}", "NeuroChoreConsumer");
                NeuroLogger.Log(
                    $"Original priority: class={_originalPriority.priority_class}, value={_originalPriority.priority_value}",
                    "NeuroChoreConsumer"
                );

                // Set extremely high priority so this chore beats everything
                targetChore.masterPriority = new PrioritySetting(
                    PriorityScreen.PriorityClass.topPriority,
                    9 // Maximum value
                );

                // Also boost the ChoreGroup priority
                ChoreGroup? choreGroup = GetChoreGroup(targetChore.choreType);
                if (choreGroup != null)
                {
                    _originalConsumer.SetPersonalPriority(choreGroup, 5);
                    NeuroLogger.Log(
                        $"Boosted {choreGroup.Name} priority to 5",
                        "NeuroChoreConsumer"
                    );
                }

                NeuroLogger.Log(
                    "Set masterPriority to topPriority/9, chore should now be top priority",
                    "NeuroChoreConsumer"
                );
                return true;
            }
            catch (Exception ex)
            {
                NeuroLogger.LogError($"Failed to force chore: {ex.Message}", "NeuroChoreConsumer");
                NeuroLogger.LogError($"Stack: {ex.StackTrace}", "NeuroChoreConsumer");
                return false;
            }
        }

        /// <summary>
        /// Clears the forced chore and restores its original priority state.
        /// </summary>
        /// <pre>A forced chore may or may not currently be active.</pre>
        /// <post>No chore remains forced by this component and the active flag has been cleared.</post>
        public void ClearForcedChore()
        {
            if (_forcedChore != null)
            {
                try
                {
                    // Restore original priority
                    _forcedChore.masterPriority = _originalPriority;
                    NeuroLogger.Log(
                        $"Cleared forced chore: {_forcedChore.choreType.Name}, restored priority",
                        "NeuroChoreConsumer"
                    );
                }
                catch (Exception ex)
                {
                    NeuroLogger.LogWarning(
                        $"Error clearing forced chore: {ex.Message}",
                        "NeuroChoreConsumer"
                    );
                }
            }

            _forcedChore = null;
            _isActive = false;
        }

        /// <summary>
        /// Returns the chore currently being forced, if any.
        /// </summary>
        /// <returns>The currently forced chore, or <see langword="null"/> when no chore is active.</returns>
        /// <pre>No input is required.</pre>
        /// <post>The returned value reflects the component's current forced-chore state.</post>
        public Chore? GetForcedChore()
        {
            return _forcedChore;
        }

        /// <summary>
        /// Reports whether the component is actively forcing an incomplete chore.
        /// </summary>
        /// <returns><see langword="true"/> when a forced chore is active and not yet complete.</returns>
        /// <pre>No input is required.</pre>
        /// <post>The result matches the component's current active flag and forced-chore state.</post>
        public bool IsForcingChore()
        {
            return _isActive && _forcedChore != null && !_forcedChore.isComplete;
        }

        /// <summary>
        /// Monitors forced-chore completion and clears it when the work finishes.
        /// </summary>
        /// <pre>The component may or may not currently be forcing a chore.</pre>
        /// <post>If the forced chore completed during this frame, tracking and completion notification have been updated accordingly.</post>
        private void Update()
        {
            if (!_isActive || _forcedChore == null)
            {
                return;
            }

            // Check if forced chore is complete or no longer valid
            if (_forcedChore.isComplete)
            {
                NeuroLogger.Log("Forced chore completed, clearing", "NeuroChoreConsumer");

                // Notify tracker of completion if it was tracking this chore type
                if (ErrandCompletionTracker.Instance.IsTracking() &&
                    ErrandCompletionTracker.Instance.CurrentProgress?.ChoreTypeName == _forcedChore.choreType.Name)
                {
                    ErrandCompletionTracker.Instance.OnChoreCompleted();
                }

                ClearForcedChore();
            }
        }

        /// <summary>
        /// Finds the chore group that owns a given chore type.
        /// </summary>
        /// <param name="choreType">The chore type whose group should be located.</param>
        /// <returns>The matching chore group, or <see langword="null"/> when none contains the type.</returns>
        /// <pre><paramref name="choreType"/> belongs to the current ONI chore database when a non-null result is expected.</pre>
        /// <post>The returned group, when non-null, contains <paramref name="choreType"/>.</post>
        private ChoreGroup? GetChoreGroup(ChoreType choreType)
        {
            if (Db.Get()?.ChoreGroups == null)
            {
                return null;
            }

            foreach (ChoreGroup group in Db.Get().ChoreGroups.resources)
            {
                if (group.choreTypes.Contains(choreType))
                {
                    return group;
                }
            }

            return null;
        }

        /// <summary>
        /// Clears any active forced chore before the component is destroyed.
        /// </summary>
        /// <pre>The component is being torn down.</pre>
        /// <post>No forced-chore state remains after destruction cleanup completes.</post>
        private void OnDestroy()
        {
            ClearForcedChore();
        }
    }
}
