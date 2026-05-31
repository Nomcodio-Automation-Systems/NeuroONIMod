using UnityEngine;

namespace NeuroMod.Integration;

/// <summary>
/// Monitors Neuro-assigned errands to ensure they complete, not just start.
/// Tracks the allowed chore, prevents the duplicant from abandoning it for other work,
/// detects interruptions (emotes, schedule changes), and re-engages after interruption.
/// 
/// Inspired by FinishTasks mod's FinishChoreDetector pattern:
/// - Locks onto a specific chore when assigned
/// - Blocks new work chores while an assigned chore is active (via CAN_DO_NEURO_ASSIGNED)
/// - Allows compulsory chores (emotes) to temporarily interrupt without losing the assignment
/// - Detects completion, timeout, and failure states
/// - Integrates with ErrandCompletionTracker for lifecycle reporting to Neuro
/// </summary>
/// <pre>
/// The component is attached to the Neuro duplicant and can access a live <see cref="ChoreDriver"/>.
/// </pre>
/// <post>
/// The monitor constrains work-chore selection while a Neuro-assigned errand is active and reports lifecycle changes.
/// </post>
public class ErrandMonitor : KMonoBehaviour
{
    /// <summary>
    /// Custom precondition that prevents the Neuro duplicant from starting new work chores
    /// while a Neuro-assigned errand is active. Added to all Work-type chores via Harmony patch.
    /// 
    /// Behavior:
    /// - If no ErrandMonitor on the duplicant: allow all chores (not the Neuro duplicate)
    /// - If monitor has no active assignment: allow all chores
    /// - If monitor is acquiring: allow all valid work chores (so the right one gets picked up)
    /// - If monitor is locked onto a chore: only allow that chore + compulsory chores (emotes)
    /// </summary>
    /// <pre>The precondition is attached to work chores evaluated for the Neuro duplicant.</pre>
    /// <post>The predicate allows only chores consistent with the current Neuro-assigned errand state.</post>
    public static readonly Chore.Precondition CAN_DO_NEURO_ASSIGNED = new Chore.Precondition
    {
        id = "NeuroMod.CanDoNeuroAssigned",
        description = "Schedule disallows new tasks (Neuro-assigned errand active)",
        fn = CheckCanDoAssigned
    };

    private ChoreDriver? _driver;
    private Chore? _allowedChore;
    private Chore? _targetChore;
    private ChoreType? _targetChoreType;
    private bool _isAcquiring;
    private bool _isActive;
    private float _assignmentTime;
    private Chore? _lastSeenChore;
    private ChoreConsumer? _priorityConsumer;
    private ChoreGroup? _boostedChoreGroup;
    private int? _originalPriority;
    private bool _pendingForcedInterrupt;
    private float _lastForcedInterruptTime;
    private const float FORCED_INTERRUPT_RETRY_SECONDS = 0.5f;

    /// <summary>
    /// Whether there is an active Neuro errand assignment (acquiring or locked).
    /// </summary>
    /// <pre>No input is required.</pre>
    /// <post>The returned value reflects whether the monitor is actively tracking or acquiring an assignment.</post>
    public bool HasActiveAssignment => _isActive;

    /// <summary>
    /// The chore the duplicant is locked onto, or null.
    /// </summary>
    /// <pre>No input is required.</pre>
    /// <post>The returned chore is the currently locked assignment, or null while idle or still acquiring.</post>
    public Chore? AllowedChore => _isAcquiring ? null : _allowedChore;

    /// <summary>
    /// The chore currently targeted by the assignment, including the acquisition phase.
    /// </summary>
    /// <pre>No input is required.</pre>
    /// <post>The returned chore is the exact assigned target when one has been selected, or null otherwise.</post>
    public Chore? TargetChore => _targetChore;

    /// <summary>
    /// The chore type currently targeted by the assignment, if any.
    /// </summary>
    /// <pre>No input is required.</pre>
    /// <post>The returned chore type reflects the currently assigned errand type when one exists.</post>
    public ChoreType? TargetChoreType => _targetChoreType;

    /// <summary>
    /// Whether the monitor is looking for the duplicant to pick up the chore.
    /// </summary>
    /// <pre>No input is required.</pre>
    /// <post>The returned value reflects whether the monitor is currently in acquisition mode.</post>
    public bool IsAcquiring => _isAcquiring;

    /// <summary>
    /// Initialize the monitor component.
    /// </summary>
    /// <pre>
    /// The component has just been spawned by Unity.
    /// </pre>
    /// <post>
    /// The monitor is initialized in an idle state and caches the local <see cref="ChoreDriver"/> when available.
    /// </post>
    protected override void OnSpawn()
    {
        base.OnSpawn();
        TryGetComponent(out _driver);

        if (_driver == null)
        {
            NeuroLogger.LogError("ErrandMonitor: No ChoreDriver found", "ErrandMonitor");
        }
        else
        {
            NeuroLogger.Log($"ErrandMonitor initialized for {gameObject.name}", "ErrandMonitor");
        }

        _allowedChore = null;
        _targetChore = null;
        _targetChoreType = null;
        _isAcquiring = false;
        _isActive = false;
        _pendingForcedInterrupt = false;
        _lastForcedInterruptTime = float.MinValue;
    }

    /// <summary>
    /// Start acquiring a specific chore type for Neuro. The monitor will watch for when
    /// the duplicant picks up a chore of the specified type, then lock onto it.
    /// If a previous assignment exists, it will be cancelled first.
    /// </summary>
    /// <param name="choreType">The ChoreType to look for, or null to accept any valid work chore</param>
    /// <pre>
    /// The monitor is attached to a duplicant that can be assigned work chores.
    /// </pre>
    /// <post>
    /// The monitor enters acquisition mode for the requested chore type and clears any previous assignment state.
    /// </post>
    public void StartAcquiring(ChoreType? choreType = null)
    {
        // Cancel any existing assignment
        if (_isActive)
        {
            NeuroLogger.Log("Cancelling previous assignment for new one", "ErrandMonitor");
            ErrandCompletionTracker.Instance.CancelTracking();
            ReleaseReservedChores();
            RestoreBoostedPriority();
        }

        _isAcquiring = true;
        _isActive = true;
        _allowedChore = null;
        _targetChore = null;
        _targetChoreType = choreType;
        _assignmentTime = Time.time;
        _lastSeenChore = null;
        _pendingForcedInterrupt = false;
        _lastForcedInterruptTime = float.MinValue;

        NeuroLogger.Log(
            $"Started acquiring Neuro-assigned chore" +
            (choreType != null ? $" (type: {choreType.Name})" : " (any work chore)"),
            "ErrandMonitor"
        );
    }

    /// <summary>
    /// Start acquiring a specific chore instance for Neuro and remember the temporary priority override.
    /// </summary>
    /// <param name="targetChore">The exact chore Neuro should acquire.</param>
    /// <param name="boostedChoreGroup">The chore group whose priority was temporarily boosted.</param>
    /// <param name="originalPriority">The duplicant's original personal priority for the boosted chore group.</param>
    /// <pre>The exact target chore has been selected during validation and remains available for reservation.</pre>
    /// <post>The monitor enters acquisition mode for the supplied chore, reserves it, stores enough state to restore the temporary priority boost later, and interrupts the dupe's current chore so they immediately re-evaluate and pick it up.</post>
    public void StartAcquiring(Chore targetChore, ChoreGroup? boostedChoreGroup, int originalPriority)
    {
        if (targetChore == null)
        {
            NeuroLogger.LogError("Cannot acquire null chore", "ErrandMonitor");
            return;
        }

        StartAcquiring(targetChore.choreType);

        _targetChore = targetChore;
        _priorityConsumer = GetComponent<ChoreConsumer>();
        _boostedChoreGroup = boostedChoreGroup;
        _originalPriority = originalPriority;
        ErrandReservationHelper.ReserveChore(targetChore);

        NeuroLogger.Log($"Reserved target chore for acquisition: {targetChore.choreType.Name}", "ErrandMonitor");

        RequestForcedInterruptForAssignment();
    }

    /// <summary>
    /// Directly lock onto a specific chore instance instead of acquiring by type.
    /// Used when we know exactly which chore to track.
    /// </summary>
    /// <param name="chore">The chore to lock onto</param>
    /// <pre>chore must not be null</pre>
    /// <post>
    /// The monitor becomes active for the supplied chore, reserves it, and exits acquisition mode.
    /// </post>
    public void LockOntoChore(Chore chore)
    {
        if (chore == null)
        {
            NeuroLogger.LogError("Cannot lock onto null chore", "ErrandMonitor");
            return;
        }

        // Cancel any existing assignment
        if (_isActive)
        {
            ErrandCompletionTracker.Instance.CancelTracking();
            ReleaseReservedChores();
            RestoreBoostedPriority();
        }

        _allowedChore = chore;
        _targetChore = chore;
        _targetChoreType = chore.choreType;
        _isAcquiring = false;
        _isActive = true;
        _assignmentTime = Time.time;
        _lastSeenChore = chore;

        // Reserve to prevent others from taking it
        ErrandReservationHelper.ReserveChore(chore);

        NeuroLogger.Log($"Locked onto chore: {chore.choreType.Name}", "ErrandMonitor");
        ErrandCompletionTracker.Instance.OnChoreStarted(chore.choreType.Name);
    }

    /// <summary>
    /// Clear the current assignment and return to idle state.
    /// </summary>
    /// <param name="reason">Why the assignment is being cleared</param>
    /// <pre>
    /// Any currently reserved chore belongs to this monitor instance.
    /// </pre>
    /// <post>
    /// The monitor returns to idle state and releases any reserved chore.
    /// </post>
    public void ClearAssignment(string reason = "Manually cleared")
    {
        ReleaseReservedChores();
        RestoreBoostedPriority();
        _allowedChore = null;
        _targetChore = null;
        _targetChoreType = null;
        _isAcquiring = false;
        _isActive = false;
        _lastSeenChore = null;
        _pendingForcedInterrupt = false;

        NeuroLogger.Log($"Assignment cleared: {reason}", "ErrandMonitor");
    }

    /// <summary>
    /// Main update loop — monitors chore acquisition, completion, and interruptions.
    /// Called every frame by Unity.
    /// </summary>
    /// <pre>
    /// Unity is actively updating the component and the monitor may have an active assignment.
    /// </pre>
    /// <post>
    /// Acquisition, interruption, completion, and timeout transitions are processed as needed.
    /// </post>
    public void Update()
    {
        if (!_isActive || _driver == null) return;

        if (_targetChore != null && !IsChoreAvailable(_targetChore))
        {
            NeuroLogger.Log("Tracked target chore is no longer available", "ErrandMonitor");
            ErrandCompletionTracker.Instance.OnErrandFailed("Target chore is no longer available");
            ClearAssignment("Target chore no longer available");
            return;
        }

        TryApplyPendingForcedInterrupt();

        if (_isAcquiring)
        {
            CheckAcquireChore();
        }
        else
        {
            CheckChoreStatus();
        }

        // Let the completion tracker check its own timeouts
        ErrandCompletionTracker.Instance.CheckTimeouts();

        if (_isActive && !ErrandCompletionTracker.Instance.IsTracking())
        {
            ClearAssignment("Tracking ended");
        }
    }

    /// <summary>
    /// If still acquiring, check if the duplicant has picked up a valid chore to lock onto.
    /// Similar to FinishTasks' CheckAcquireChore pattern but also checks for the target type.
    /// </summary>
    /// <pre>The monitor is active, in acquisition mode, and has access to the duplicant's current chore driver state.</pre>
    /// <post>When a matching chore is found, the monitor locks onto it, reserves it, and notifies the completion tracker.</post>
    private void CheckAcquireChore()
    {
        if (!_isAcquiring || _driver == null) return;

        if (_targetChore != null && !IsChoreAvailable(_targetChore))
        {
            NeuroLogger.Log("Reserved target chore disappeared before pickup", "ErrandMonitor");
            ErrandCompletionTracker.Instance.OnErrandFailed("Target chore is no longer available");
            ClearAssignment("Target chore disappeared during acquisition");
            return;
        }

        Chore? currentChore = _driver.GetCurrentChore();
        if (currentChore == null)
        {
            RequestForcedInterruptForAssignment();
            return;
        }

        if (!ShouldAllowAcquiringChore(_targetChore, _targetChoreType, currentChore))
        {
            return;
        }

        // Lock onto this chore
        NeuroLogger.Log($"Locked onto chore: {currentChore.choreType.Name}", "ErrandMonitor");
        _isAcquiring = false;
        _allowedChore = currentChore;
        _targetChore = currentChore;
        _lastSeenChore = currentChore;

        // Reserve to prevent others from taking it
        ErrandReservationHelper.ReserveChore(currentChore);

        // Notify the tracker
        ErrandCompletionTracker.Instance.OnChoreStarted(currentChore.choreType.Name);
    }

    /// <summary>
    /// Check if the locked chore is still active, completed, or interrupted.
    /// This is the core "finish task" logic: detect interruptions and allow resumption.
    /// </summary>
    /// <pre>The monitor is active and currently locked onto a chore.</pre>
    /// <post>The current chore state has been reconciled into completion, interruption, failure, or continued tracking as appropriate.</post>
    private void CheckChoreStatus()
    {
        if (_allowedChore == null || _driver == null) return;

        Chore? currentChore = _driver.GetCurrentChore();

        // Case 1: The duplicant is still doing the assigned chore — all good
        if (currentChore == _allowedChore)
        {
            // Check if tracker was in interrupted state and now resumed
            if (ErrandCompletionTracker.Instance.CurrentProgress?.State ==
                ErrandCompletionTracker.ErrandState.Interrupted)
            {
                ErrandCompletionTracker.Instance.OnChoreResumed();
            }
            return;
        }

        // Case 2: The assigned chore is complete (isComplete flag or chore is null/destroyed)
        if (_allowedChore.isComplete)
        {
            NeuroLogger.Log($"Chore completed: {_allowedChore.choreType.Name}", "ErrandMonitor");
            ErrandReservationHelper.ReleaseChore(_allowedChore);
            ErrandCompletionTracker.Instance.OnChoreCompleted();
            ClearAssignment("Chore completed successfully");
            return;
        }

        // Case 3: The duplicant switched to a compulsory chore (emote, bodily need)
        //         This is a temporary interruption — don't abandon the assignment
        if (currentChore != null && IsCompulsoryChore(currentChore))
        {
            // Only notify on first interruption transition
            if (_lastSeenChore != currentChore)
            {
                ErrandCompletionTracker.Instance.OnChoreInterrupted(
                    $"Compulsory: {currentChore.choreType.Name}"
                );
                _lastSeenChore = currentChore;
            }
            return;
        }

        // Case 4: The duplicant switched to a different non-compulsory chore or idled
        //         This means the assigned chore may have been interrupted by something else
        if (currentChore == null)
        {
            // Duplicant is idle — check if the chore is still available
            if (!_allowedChore.isComplete && _allowedChore.target != null && !_allowedChore.isNull)
            {
                // Chore still exists, duplicant just temporarily stopped, mark as interrupted
                if (_lastSeenChore != null)
                {
                    ErrandCompletionTracker.Instance.OnChoreInterrupted("Duplicant became idle");
                    _lastSeenChore = null;
                }
                return;
            }
            else
            {
                // Chore no longer available
                NeuroLogger.Log("Assigned chore is no longer available", "ErrandMonitor");
                ErrandReservationHelper.ReleaseChore(_allowedChore);
                ErrandCompletionTracker.Instance.OnErrandFailed("Chore is no longer available");
                ClearAssignment("Chore no longer available");
                return;
            }
        }

        // Case 5: Duplicant took a different work chore — this shouldn't happen
        //         if the precondition is working, but handle it gracefully
        if (IsValidWorkChore(currentChore) && currentChore != _allowedChore)
        {
            NeuroLogger.LogWarning(
                $"Duplicant switched to different work chore: {currentChore.choreType.Name} " +
                $"(expected: {_allowedChore.choreType.Name})",
                "ErrandMonitor"
            );
            ErrandCompletionTracker.Instance.OnChoreInterrupted(
                $"Switched to: {currentChore.choreType.Name}"
            );
            _lastSeenChore = currentChore;
        }
    }

    /// <summary>
    /// Check if a chore is a valid work chore (not idle or compulsory like emotes).
    /// </summary>
    /// <param name="chore">The chore to check</param>
    /// <returns>True if it's a standard work chore</returns>
    /// <pre>
    /// <paramref name="chore"/> references a live chore with a valid priority classification.
    /// </pre>
    /// <post>
    /// The method reports whether the chore belongs to the normal work range.
    /// </post>
    public static bool IsValidWorkChore(Chore chore)
    {
        PriorityScreen.PriorityClass pc = chore.masterPriority.priority_class;
        return pc > PriorityScreen.PriorityClass.idle &&
               pc < PriorityScreen.PriorityClass.personalNeeds;
    }

    /// <summary>
    /// Determines whether a chore reference still points to a live, available errand instance.
    /// </summary>
    /// <param name="chore">The chore reference to validate.</param>
    /// <returns><see langword="true"/> when the chore is still present and not already complete; otherwise <see langword="false"/>.</returns>
    /// <pre>The supplied chore reference may refer to a live chore, a completed chore, or a stale destroyed Unity object.</pre>
    /// <post>The return value indicates whether the monitor can continue tracking or reserving the supplied chore.</post>
    internal static bool IsChoreAvailable(Chore? chore)
    {
        return chore != null && !chore.isNull && !chore.isComplete && chore.target != null;
    }

    /// <summary>
    /// Determines whether a candidate chore is eligible while the monitor is acquiring an assigned errand.
    /// </summary>
    /// <param name="targetChore">The exact target chore when the assignment selected a specific chore instance.</param>
    /// <param name="targetChoreType">The required chore type when the assignment is type-based.</param>
    /// <param name="candidateChore">The chore the duplicant is attempting to pick up.</param>
    /// <returns><see langword="true"/> when the candidate is a valid work chore and satisfies the current acquisition target.</returns>
    /// <pre>The acquisition flow has identified the current target chore and or target chore type for the active assignment.</pre>
    /// <post>The return value indicates whether the candidate chore matches the acquisition constraints for the active assignment.</post>
    internal static bool ShouldAllowAcquiringChore(Chore? targetChore, ChoreType? targetChoreType, Chore candidateChore)
    {
        return ShouldAllowAcquiringCandidate(
            IsValidWorkChore(candidateChore),
            targetChore != null,
            targetChore == candidateChore,
            targetChore != null && MatchesEquivalentTargetChore(targetChore, candidateChore),
            targetChoreType != null,
            candidateChore.choreType == targetChoreType);
    }

    /// <summary>
    /// Determines whether an acquisition candidate satisfies the current target selection constraints.
    /// </summary>
    /// <param name="isValidWorkChore">Whether the candidate belongs to the normal work-chore range.</param>
    /// <param name="hasExactTarget">Whether acquisition is locked to one exact chore instance.</param>
    /// <param name="matchesExactTarget">Whether the candidate is that exact target chore.</param>
    /// <param name="matchesEquivalentTarget">Whether the candidate is an equivalent replacement for the exact target chore.</param>
    /// <param name="hasTargetType">Whether acquisition is constrained to a specific chore type.</param>
    /// <param name="matchesTargetType">Whether the candidate matches that target chore type.</param>
    /// <returns><see langword="true"/> when the candidate satisfies the acquisition constraints.</returns>
    /// <pre>The supplied flags were computed from one acquisition decision for one candidate chore.</pre>
    /// <post>The return value indicates whether the candidate should be allowed during acquisition.</post>
    internal static bool ShouldAllowAcquiringCandidate(
        bool isValidWorkChore,
        bool hasExactTarget,
        bool matchesExactTarget,
        bool matchesEquivalentTarget,
        bool hasTargetType,
        bool matchesTargetType)
    {
        if (!isValidWorkChore)
        {
            return false;
        }

        if (hasExactTarget)
        {
            return matchesExactTarget || matchesEquivalentTarget;
        }

        if (hasTargetType)
        {
            return matchesTargetType;
        }

        return true;
    }

    /// <summary>
    /// Determines whether the candidate chore is an equivalent replacement for the originally targeted chore.
    /// </summary>
    /// <param name="targetChore">The originally selected target chore.</param>
    /// <param name="candidateChore">The candidate chore the duplicant is about to pick up.</param>
    /// <returns><see langword="true"/> when the candidate appears to represent the same errand at the same location.</returns>
    /// <pre>The game may recreate an errand as a different chore instance while acquisition is still in progress.</pre>
    /// <post>The return value indicates whether the candidate should be treated as the same errand for acquisition purposes.</post>
    internal static bool MatchesEquivalentTargetChore(Chore targetChore, Chore candidateChore)
    {
        if (!IsChoreAvailable(targetChore) || !IsChoreAvailable(candidateChore))
        {
            return false;
        }

        if (targetChore.choreType != candidateChore.choreType)
        {
            return false;
        }

        Vector3 targetPosition = targetChore.target.transform.position;
        Vector3 candidatePosition = candidateChore.target.transform.position;
        return (int)targetPosition.x == (int)candidatePosition.x &&
               (int)targetPosition.y == (int)candidatePosition.y;
    }

    /// <summary>
    /// Check if a chore is compulsory (emote, bodily need, etc.) 
    /// Compulsory chores should temporarily interrupt but not cancel the assignment.
    /// </summary>
    /// <param name="chore">The chore to check</param>
    /// <returns>True if it's a compulsory chore</returns>
    /// <pre><paramref name="chore"/> references a live chore with a valid priority classification.</pre>
    /// <post>The method reports whether the chore belongs to the compulsory or personal-needs range.</post>
    private static bool IsCompulsoryChore(Chore chore)
    {
        return chore.masterPriority.priority_class == PriorityScreen.PriorityClass.compulsory ||
               chore.masterPriority.priority_class >= PriorityScreen.PriorityClass.personalNeeds;
    }

    /// <summary>
    /// Static precondition checker for the CAN_DO_NEURO_ASSIGNED precondition.
    /// Prevents Neuro from starting new work chores while an assigned errand is active.
    /// Allows: the assigned chore itself, compulsory chores, and chores when no assignment is active.
    /// </summary>
    /// <param name="context">The chore precondition context</param>
    /// <param name="data">Unused</param>
    /// <returns>True if the chore is allowed, false if it should be blocked</returns>
    /// <pre>
    /// <paramref name="context"/> describes the candidate chore currently being evaluated by the game.
    /// </pre>
    /// <post>
    /// The method returns whether the duplicant may start the candidate chore under the active Neuro assignment rules.
    /// </post>
    private static bool CheckCanDoAssigned(ref Chore.Precondition.Context context, object data)
    {
        ChoreDriver? driver = context.consumerState?.choreDriver;
        if (driver == null) return true;

        // Check if this duplicant has an ErrandMonitor with an active assignment
        if (!driver.TryGetComponent(out ErrandMonitor monitor)) return true;
        if (!monitor.HasActiveAssignment) return true;

        // Always allow compulsory chores (emotes, bathroom, etc.)
        PriorityScreen.PriorityClass pc = context.chore.masterPriority.priority_class;
        if (pc == PriorityScreen.PriorityClass.compulsory ||
            pc >= PriorityScreen.PriorityClass.personalNeeds)
        {
            return true;
        }

        // During acquisition, allow all valid work chores so the right one can be picked up
        if (monitor.IsAcquiring)
        {
            return ShouldAllowAcquiringChore(monitor.TargetChore, monitor.TargetChoreType, context.chore);
        }

        // If locked onto a specific chore, only allow that chore
        Chore? allowedChore = monitor.AllowedChore;
        if (allowedChore == null) return true;

        // Allow the assigned chore itself
        return allowedChore == context.chore;
    }

    /// <summary>
    /// Clean up resources when component is destroyed.
    /// </summary>
    /// <pre>
    /// The monitor is being removed from the duplicant or the world is unloading.
    /// </pre>
    /// <post>
    /// Reserved chores and local monitor state are released before base cleanup runs.
    /// </post>
    protected override void OnCleanUp()
    {
        ReleaseReservedChores();
        RestoreBoostedPriority();
        _allowedChore = null;
        _targetChore = null;
        _targetChoreType = null;
        _isAcquiring = false;
        _isActive = false;
        _lastSeenChore = null;
        _pendingForcedInterrupt = false;
        _lastForcedInterruptTime = float.MinValue;
        base.OnCleanUp();
    }

    /// <summary>
    /// Determines whether the monitor should interrupt the current chore immediately when an errand is assigned.
    /// </summary>
    /// <param name="hasDriver">Whether the duplicant currently has a chore driver available.</param>
    /// <param name="isGamePaused">Whether the simulation is currently paused.</param>
    /// <returns><see langword="true"/> when it is safe to interrupt immediately; otherwise <see langword="false"/>.</returns>
    /// <pre>The caller has already resolved whether a chore driver exists and whether the simulation is paused.</pre>
    /// <post>The return value indicates whether a forced chore stop should happen now instead of being deferred.</post>
    internal static bool ShouldInterruptCurrentChoreImmediately(bool hasDriver, bool isGamePaused)
    {
        return hasDriver && !isGamePaused;
    }

    /// <summary>
    /// Requests a forced re-evaluation of the current chore for a newly assigned errand.
    /// </summary>
    /// <pre>The monitor has just reserved a target chore for the active assignment.</pre>
    /// <post>The current chore is interrupted immediately when safe, otherwise the interruption is deferred until the simulation resumes.</post>
    private void RequestForcedInterruptForAssignment()
    {
        if (!ShouldRetryForcedInterrupt(CurrentTime, _lastForcedInterruptTime, _pendingForcedInterrupt))
        {
            return;
        }

        if (!ShouldInterruptCurrentChoreImmediately(_driver != null, IsGamePaused()))
        {
            _pendingForcedInterrupt = _driver != null;
            if (_pendingForcedInterrupt)
            {
                NeuroLogger.Log(
                    "Deferring forced chore interruption until the simulation is running",
                    "ErrandMonitor"
                );
            }

            return;
        }

        ForceInterruptCurrentChore();
    }

    /// <summary>
    /// Determines whether the monitor should attempt another forced chore interruption while acquiring an errand.
    /// </summary>
    /// <param name="currentTime">The current monitor time.</param>
    /// <param name="lastForcedInterruptTime">The time the previous forced interruption attempt was issued.</param>
    /// <param name="hasPendingForcedInterrupt">Whether a deferred interruption is already queued.</param>
    /// <returns><see langword="true"/> when another forced interruption attempt should be issued.</returns>
    /// <pre>The monitor is evaluating whether acquisition needs another scheduler nudge.</pre>
    /// <post>The return value throttles repeated interruption requests while still allowing retries after the configured cooldown.</post>
    internal static bool ShouldRetryForcedInterrupt(float currentTime, float lastForcedInterruptTime, bool hasPendingForcedInterrupt)
    {
        if (hasPendingForcedInterrupt)
        {
            return false;
        }

        return currentTime - lastForcedInterruptTime >= FORCED_INTERRUPT_RETRY_SECONDS;
    }

    /// <summary>
    /// Applies a deferred forced interruption once the simulation is no longer paused.
    /// </summary>
    /// <pre>A prior assignment request may have deferred the chore interruption because the simulation was paused.</pre>
    /// <post>If the game is running and a driver is available, the duplicant's current chore is interrupted exactly once.</post>
    private void TryApplyPendingForcedInterrupt()
    {
        if (!_pendingForcedInterrupt)
        {
            return;
        }

        if (!ShouldInterruptCurrentChoreImmediately(_driver != null, IsGamePaused()))
        {
            return;
        }

        ForceInterruptCurrentChore();
    }

    /// <summary>
    /// Stops the duplicant's current chore so the reserved errand can be reconsidered immediately.
    /// </summary>
    /// <pre>A chore driver exists and the current simulation state allows safe interruption.</pre>
    /// <post>The current chore has been stopped and any deferred interruption flag has been cleared.</post>
    private void ForceInterruptCurrentChore()
    {
        if (_driver == null)
        {
            _pendingForcedInterrupt = false;
            return;
        }

        _driver.StopChore();
        _pendingForcedInterrupt = false;
        _lastForcedInterruptTime = CurrentTime;
        NeuroLogger.Log("Interrupted current chore to force re-evaluation of assignment", "ErrandMonitor");
    }

    /// <summary>
    /// Gets the current time for monitor retry decisions.
    /// </summary>
    /// <pre>The monitor may be running in production or in a unit test with a custom time provider.</pre>
    /// <post>The returned value is suitable for throttling reacquire retries without forcing direct Unity time access in tests.</post>
    private static float CurrentTime => ErrandCompletionTracker.CurrentTime;

    /// <summary>
    /// Determines whether the simulation is currently paused.
    /// </summary>
    /// <returns><see langword="true"/> when the game is paused; otherwise <see langword="false"/>.</returns>
    /// <pre>The game speed controller is available when the world is active.</pre>
    /// <post>The returned value reflects whether forced chore interruption should be deferred for pause safety.</post>
    private static bool IsGamePaused()
    {
        return SpeedControlScreen.Instance != null && SpeedControlScreen.Instance.IsPaused;
    }

    /// <summary>
    /// Releases any chore reservations held by the monitor.
    /// </summary>
    /// <pre>The monitor may be holding a reserved target chore, a locked chore, or both references to the same chore.</pre>
    /// <post>All reservations associated with the current assignment have been released.</post>
    private void ReleaseReservedChores()
    {
        if (_allowedChore != null)
        {
            ErrandReservationHelper.ReleaseChore(_allowedChore);
        }

        if (_targetChore != null && _targetChore != _allowedChore)
        {
            ErrandReservationHelper.ReleaseChore(_targetChore);
        }
    }

    /// <summary>
    /// Restores the duplicated priority override applied during errand assignment when it is still in effect.
    /// </summary>
    /// <pre>The monitor may be carrying the original personal priority captured when the assignment was created.</pre>
    /// <post>The temporary errand-specific priority boost has been reverted when it was still active.</post>
    private void RestoreBoostedPriority()
    {
        if (_priorityConsumer != null && _boostedChoreGroup != null && _originalPriority.HasValue)
        {
            try
            {
                int currentPriority = _priorityConsumer.GetPersonalPriority(_boostedChoreGroup);
                if (ShouldRestoreBoostedPriority(currentPriority, _originalPriority.Value))
                {
                    _priorityConsumer.SetPersonalPriority(_boostedChoreGroup, _originalPriority.Value);
                    NeuroLogger.Log(
                        $"Restored {_boostedChoreGroup.Name} priority from 5 to {_originalPriority.Value}",
                        "ErrandMonitor"
                    );
                }
            }
            catch (System.Exception ex)
            {
                NeuroLogger.LogError($"Failed to restore boosted priority: {ex.Message}", "ErrandMonitor");
            }
        }

        _priorityConsumer = null;
        _boostedChoreGroup = null;
        _originalPriority = null;
    }

    /// <summary>
    /// Determines whether a temporary errand-specific priority boost should be reverted.
    /// </summary>
    /// <param name="currentPriority">The duplicant's current priority for the boosted chore group.</param>
    /// <param name="originalPriority">The priority captured before the errand boost was applied.</param>
    /// <returns><see langword="true"/> when the current value still reflects the temporary max-priority boost and should be restored.</returns>
    /// <pre>The current and original priority values came from the same chore group for one errand assignment.</pre>
    /// <post>The return value indicates whether the monitor should restore the original priority instead of leaving the current value unchanged.</post>
    internal static bool ShouldRestoreBoostedPriority(int currentPriority, int originalPriority)
    {
        return currentPriority == 5 && currentPriority != originalPriority;
    }
}
