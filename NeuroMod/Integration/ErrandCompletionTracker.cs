using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace NeuroMod.Integration;

/// <summary>
/// Tracks the lifecycle of a Neuro-assigned errand from assignment through completion.
/// Provides real-time feedback to Neuro about errand progress, interruptions, and outcomes.
/// Inspired by FinishTasks mod's FinishChoreDetector pattern, extended with completion tracking.
/// </summary>
/// <pre>
/// Callers advance the tracker using a consistent errand lifecycle: assign, start, interrupt, resume, and finish.
/// </pre>
/// <post>
/// The tracker preserves the current or last errand state and emits lifecycle updates through logging and context messages.
/// </post>
public class ErrandCompletionTracker
{
    /// <summary>
    /// Possible states of an errand assignment lifecycle.
    /// </summary>
    /// <pre>Tracked errands advance through a bounded set of lifecycle states.</pre>
    /// <post>Each enum value denotes a distinct phase used by the tracker to enforce valid transitions.</post>
    public enum ErrandState
    {
        /// <summary>No errand is currently being tracked.</summary>
        Idle,
        /// <summary>An errand has been assigned and we are waiting for the duplicant to pick it up.</summary>
        Acquiring,
        /// <summary>The duplicant is actively performing the assigned errand.</summary>
        InProgress,
        /// <summary>The errand was temporarily interrupted (emote, schedule, etc.) and may resume.</summary>
        Interrupted,
        /// <summary>The errand completed successfully.</summary>
        Completed,
        /// <summary>The errand failed or was abandoned.</summary>
        Failed,
        /// <summary>The errand timed out without completion.</summary>
        TimedOut
    }

    /// <summary>
    /// Information about the currently tracked errand.
    /// </summary>
    /// <pre>
    /// Property values describe a single errand lifecycle snapshot.
    /// </pre>
    /// <post>
    /// Instances can summarize the current or completed errand state without external lookups.
    /// </post>
    public class ErrandProgress
    {
        /// <summary>The current state of the errand.</summary>
        /// <pre>The progress object represents one tracked errand lifecycle snapshot.</pre>
        /// <post>The property stores the current lifecycle phase of the tracked errand.</post>
        public ErrandState State { get; set; } = ErrandState.Idle;

        /// <summary>The chore type name being tracked.</summary>
        /// <pre>The progress object represents one tracked errand lifecycle snapshot.</pre>
        /// <post>The property stores the tracked chore type name.</post>
        public string ChoreTypeName { get; set; } = "";

        /// <summary>The chore group name.</summary>
        /// <pre>The progress object represents one tracked errand lifecycle snapshot.</pre>
        /// <post>The property stores the tracked chore group name.</post>
        public string ChoreGroupName { get; set; } = "";

        /// <summary>When the errand was assigned.</summary>
        /// <pre>The progress object represents one tracked errand lifecycle snapshot.</pre>
        /// <post>The property stores the assignment timestamp for the tracked errand.</post>
        public float AssignTime { get; set; }

        /// <summary>When the errand was picked up by the duplicant.</summary>
        /// <pre>The progress object represents one tracked errand lifecycle snapshot.</pre>
        /// <post>The property stores the start timestamp when the errand has begun, or null otherwise.</post>
        public float? StartTime { get; set; }

        /// <summary>When the errand finished (completed, failed, or timed out).</summary>
        /// <pre>The progress object represents one tracked errand lifecycle snapshot.</pre>
        /// <post>The property stores the end timestamp when the errand has finished, or null otherwise.</post>
        public float? EndTime { get; set; }

        /// <summary>Number of times the errand was interrupted.</summary>
        /// <pre>The progress object represents one tracked errand lifecycle snapshot.</pre>
        /// <post>The property stores how many interruptions have been recorded for the errand.</post>
        public int InterruptionCount { get; set; }

        /// <summary>Brief description of what happened.</summary>
        /// <pre>The progress object represents one tracked errand lifecycle snapshot.</pre>
        /// <post>The property stores the latest human-readable status message for the errand.</post>
        public string StatusMessage { get; set; } = "";

        /// <summary>Target location X coordinate.</summary>
        /// <pre>The progress object represents one tracked errand lifecycle snapshot.</pre>
        /// <post>The property stores the target X coordinate for the errand.</post>
        public int TargetX { get; set; }

        /// <summary>Target location Y coordinate.</summary>
        /// <pre>The progress object represents one tracked errand lifecycle snapshot.</pre>
        /// <post>The property stores the target Y coordinate for the errand.</post>
        public int TargetY { get; set; }

        /// <summary>
        /// Gets a human-readable summary of the errand progress.
        /// </summary>
        /// <returns>Formatted errand progress string</returns>
        /// <pre>
        /// The progress object contains at least assignment-time state.
        /// </pre>
        /// <post>
        /// A stable text summary describing lifecycle state, timing, and interruptions is returned.
        /// </post>
        public string GetSummary()
        {
            float elapsed = (EndTime ?? ErrandCompletionTracker.CurrentTime) - AssignTime;
            string result = $"[{State}] {ChoreTypeName} ({ChoreGroupName}) at ({TargetX},{TargetY})";
            result += $" | Elapsed: {elapsed:F1}s";

            if (InterruptionCount > 0)
            {
                result += $" | Interrupted {InterruptionCount}x";
            }

            if (!string.IsNullOrEmpty(StatusMessage))
            {
                result += $" | {StatusMessage}";
            }

            return result;
        }
    }

    private static ErrandCompletionTracker? _instance;

    /// <summary>
    /// Singleton instance of the tracker.
    /// </summary>
    /// <pre>
    /// The process may request tracking services from multiple production call sites.
    /// </pre>
    /// <post>
    /// A shared tracker instance is returned.
    /// </post>
    public static ErrandCompletionTracker Instance => _instance ??= new ErrandCompletionTracker();

    /// <summary>
    /// The current errand progress information.
    /// </summary>
    /// <pre>The tracker may or may not currently be monitoring an active errand.</pre>
    /// <post>The property returns the current tracked errand progress when one is active.</post>
    public ErrandProgress? CurrentProgress { get; private set; }

    /// <summary>
    /// The last completed/failed errand progress (for querying after completion).
    /// </summary>
    /// <pre>The tracker may have archived a prior errand lifecycle.</pre>
    /// <post>The property returns the most recent completed, failed, or timed-out errand snapshot when available.</post>
    public ErrandProgress? LastCompletedProgress { get; private set; }

    /// <summary>
    /// Event fired when an errand state changes.
    /// </summary>
    /// <pre>Subscribers may observe errand lifecycle changes.</pre>
    /// <post>Handlers attached here are invoked whenever the tracker publishes a state update.</post>
    public event Action<ErrandProgress>? OnErrandStateChanged;

    /// <summary>
    /// Maximum time to wait for a duplicant to pick up an assigned chore.
    /// </summary>
    private const float ACQUIRE_TIMEOUT_SECONDS = 60f;

    /// <summary>
    /// Maximum time an errand can be in progress before timing out.
    /// </summary>
    private const float ERRAND_TIMEOUT_SECONDS = 600f;

    /// <summary>
    /// Maximum number of interruptions before abandoning an errand.
    /// </summary>
    private const int MAX_INTERRUPTIONS = 5;

    /// <summary>
    /// Grace period after interruption before declaring the errand failed.
    /// Allows time for emotes/animations to complete and chore to resume.
    /// </summary>
    private const float INTERRUPTION_GRACE_SECONDS = 15f;

    /// <summary>
    /// Time when the last interruption occurred.
    /// </summary>
    /// <pre>The tracker may be monitoring interruption timing for the current errand.</pre>
    /// <post>The field stores the timestamp of the most recent interruption observation.</post>
    private float _lastInterruptionTime;

    /// <summary>
    /// Test-only time provider. When set, bypasses Unity's Time.time.
    /// Set this in unit tests to avoid UnityEngine.CoreModule dependency.
    /// </summary>
    /// <pre>Tests may need deterministic control over the tracker's concept of current time.</pre>
    /// <post>When non-null, the property supplies the current time used by the tracker instead of Unity time.</post>
    internal static Func<float>? TestTimeProvider { get; set; }

    /// <summary>
    /// Gets the current time using the test provider or Unity Time.time.
    /// </summary>
    /// <pre>The tracker may be running in production or in a test context with a custom time provider.</pre>
    /// <post>The returned value is the current tracker time from the test provider when present, otherwise Unity time.</post>
    internal static float CurrentTime => TestTimeProvider?.Invoke() ?? GetUnityTime();

    /// <summary>
    /// Gets Unity's Time.time. Isolated in a separate method so the JIT
    /// only resolves UnityEngine.CoreModule when this method is actually called.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static float GetUnityTime() => Time.time;

    private ErrandCompletionTracker() { }

    /// <summary>
    /// Begin tracking a new errand assignment.
    /// </summary>
    /// <param name="choreTypeName">Name of the chore type</param>
    /// <param name="choreGroupName">Name of the chore group</param>
    /// <param name="targetX">Target X coordinate</param>
    /// <param name="targetY">Target Y coordinate</param>
    /// <pre>
    /// The supplied chore identity and target coordinates describe the newly assigned errand.
    /// </pre>
    /// <post>
    /// <see cref="CurrentProgress"/> enters the acquiring state and any prior progress is archived.
    /// </post>
    public void BeginTracking(string choreTypeName, string choreGroupName, int targetX, int targetY)
    {
        // Archive previous progress if it exists
        if (CurrentProgress != null && CurrentProgress.State != ErrandState.Idle)
        {
            LastCompletedProgress = CurrentProgress;
        }

        CurrentProgress = new ErrandProgress
        {
            State = ErrandState.Acquiring,
            ChoreTypeName = choreTypeName,
            ChoreGroupName = choreGroupName,
            AssignTime = CurrentTime,
            TargetX = targetX,
            TargetY = targetY,
            StatusMessage = "Waiting for duplicant to start errand"
        };

        NeuroLogger.Log($"Begin tracking errand: {choreTypeName} at ({targetX},{targetY})", "ErrandTracker");
        NotifyStateChange();
    }

    /// <summary>
    /// Called when the duplicant picks up the assigned chore.
    /// </summary>
    /// <param name="choreName">Name of the picked up chore (for verification)</param>
    /// <pre>
    /// A matching errand is currently being tracked.
    /// </pre>
    /// <post>
    /// The tracker transitions to in-progress state and records the start time.
    /// </post>
    public void OnChoreStarted(string choreName)
    {
        if (CurrentProgress == null) return;

        CurrentProgress.State = ErrandState.InProgress;
        CurrentProgress.StartTime = CurrentTime;
        CurrentProgress.StatusMessage = $"Duplicant started: {choreName}";

        NeuroLogger.Log($"Errand started: {choreName}", "ErrandTracker");
        SendContext($"Started errand: {choreName} at ({CurrentProgress.TargetX},{CurrentProgress.TargetY})", true);
        NotifyStateChange();
    }

    /// <summary>
    /// Called when the chore is interrupted (e.g., by an emote, schedule change, etc.)
    /// </summary>
    /// <param name="reason">Brief description of why the interruption occurred</param>
    /// <pre>
    /// A tracked errand is active when the interruption occurs.
    /// </pre>
    /// <post>
    /// The tracker transitions to interrupted state and increments the interruption count.
    /// </post>
    public void OnChoreInterrupted(string reason)
    {
        if (CurrentProgress == null) return;

        CurrentProgress.InterruptionCount++;
        CurrentProgress.State = ErrandState.Interrupted;
        CurrentProgress.StatusMessage = $"Interrupted: {reason}";
        _lastInterruptionTime = CurrentTime;

        NeuroLogger.Log($"Errand interrupted ({CurrentProgress.InterruptionCount}x): {reason}", "ErrandTracker");

        // Only notify Neuro on significant interruptions (not every emote)
        if (CurrentProgress.InterruptionCount >= 2)
        {
            SendContext(
                $"Errand '{CurrentProgress.ChoreTypeName}' interrupted ({CurrentProgress.InterruptionCount}x): {reason}",
                false
            );
        }

        NotifyStateChange();
    }

    /// <summary>
    /// Called when the duplicant resumes the interrupted chore.
    /// </summary>
    /// <pre>
    /// The current errand was previously marked interrupted.
    /// </pre>
    /// <post>
    /// The tracker transitions back to in-progress state.
    /// </post>
    public void OnChoreResumed()
    {
        if (CurrentProgress == null) return;

        CurrentProgress.State = ErrandState.InProgress;
        CurrentProgress.StatusMessage = "Resumed after interruption";

        NeuroLogger.Log("Errand resumed after interruption", "ErrandTracker");
        NotifyStateChange();
    }

    /// <summary>
    /// Called when the chore completes successfully.
    /// </summary>
    /// <pre>
    /// A tracked errand is still active and has now completed successfully.
    /// </pre>
    /// <post>
    /// The tracker archives the completed errand, records the end time, and clears the active progress.
    /// </post>
    public void OnChoreCompleted()
    {
        if (CurrentProgress == null) return;

        CurrentProgress.State = ErrandState.Completed;
        CurrentProgress.EndTime = CurrentTime;
        float duration = CurrentProgress.EndTime.Value - CurrentProgress.AssignTime;
        CurrentProgress.StatusMessage = $"Completed in {duration:F1}s";

        NeuroLogger.Log($"Errand completed: {CurrentProgress.ChoreTypeName} in {duration:F1}s", "ErrandTracker");
        SendContext(
            $"Errand COMPLETED: {CurrentProgress.ChoreTypeName} at ({CurrentProgress.TargetX},{CurrentProgress.TargetY}) " +
            $"in {duration:F1}s" +
            (CurrentProgress.InterruptionCount > 0 ? $" (interrupted {CurrentProgress.InterruptionCount}x)" : ""),
            true
        );

        // Archive and clear
        LastCompletedProgress = CurrentProgress;
        NotifyStateChange();
        CurrentProgress = null;
    }

    /// <summary>
    /// Called when the errand fails or is abandoned.
    /// </summary>
    /// <param name="reason">Reason for failure</param>
    /// <pre>
    /// A tracked errand is active when the failure is detected.
    /// </pre>
    /// <post>
    /// The tracker archives the failed errand and clears the active progress.
    /// </post>
    public void OnErrandFailed(string reason)
    {
        if (CurrentProgress == null) return;

        CurrentProgress.State = ErrandState.Failed;
        CurrentProgress.EndTime = CurrentTime;
        CurrentProgress.StatusMessage = $"Failed: {reason}";

        NeuroLogger.Log($"Errand failed: {CurrentProgress.ChoreTypeName} - {reason}", "ErrandTracker");
        SendContext(
            $"Errand FAILED: {CurrentProgress.ChoreTypeName} - {reason}. " +
            "You can try assigning a different errand or the same one again.",
            true
        );

        LastCompletedProgress = CurrentProgress;
        NotifyStateChange();
        CurrentProgress = null;
    }

    /// <summary>
    /// Called when the errand times out without completion.
    /// </summary>
    /// <pre>
    /// A tracked errand has exceeded the configured completion timeout.
    /// </pre>
    /// <post>
    /// The tracker archives the timed-out errand and clears the active progress.
    /// </post>
    public void OnErrandTimedOut()
    {
        if (CurrentProgress == null) return;

        CurrentProgress.State = ErrandState.TimedOut;
        CurrentProgress.EndTime = CurrentTime;
        CurrentProgress.StatusMessage = "Timed out";

        NeuroLogger.Log($"Errand timed out: {CurrentProgress.ChoreTypeName}", "ErrandTracker");
        SendContext(
            $"Errand TIMED OUT: {CurrentProgress.ChoreTypeName} at ({CurrentProgress.TargetX},{CurrentProgress.TargetY}). " +
            "The duplicant took too long. Try a closer task or check for path issues.",
            true
        );

        LastCompletedProgress = CurrentProgress;
        NotifyStateChange();
        CurrentProgress = null;
    }

    /// <summary>
    /// Check for timeout conditions. Called periodically by ErrandMonitor.Update().
    /// </summary>
    /// <pre>
    /// <see cref="CurrentProgress"/> may contain an acquiring, in-progress, or interrupted errand.
    /// </pre>
    /// <post>
    /// Timeout or failure callbacks may be triggered when the lifecycle exceeds configured limits.
    /// </post>
    public void CheckTimeouts()
    {
        if (CurrentProgress == null) return;

        float elapsed = CurrentTime - CurrentProgress.AssignTime;

        switch (CurrentProgress.State)
        {
            case ErrandState.Acquiring:
                if (elapsed > ACQUIRE_TIMEOUT_SECONDS)
                {
                    OnErrandFailed("Duplicant did not pick up the errand in time");
                }
                break;

            case ErrandState.InProgress:
                if (elapsed > ERRAND_TIMEOUT_SECONDS)
                {
                    OnErrandTimedOut();
                }
                break;

            case ErrandState.Interrupted:
                if (CurrentProgress.InterruptionCount >= MAX_INTERRUPTIONS)
                {
                    OnErrandFailed($"Too many interruptions ({MAX_INTERRUPTIONS})");
                }
                else if (CurrentTime - _lastInterruptionTime > INTERRUPTION_GRACE_SECONDS)
                {
                    // Grace period expired without resumption — the chore was likely abandoned
                    OnErrandFailed("Chore not resumed after interruption");
                }
                break;
        }
    }

    /// <summary>
    /// Check if there is an active tracked errand.
    /// </summary>
    /// <returns>True if currently tracking an errand</returns>
    public bool IsTracking()
    {
        return CurrentProgress != null &&
               CurrentProgress.State is ErrandState.Acquiring or ErrandState.InProgress or ErrandState.Interrupted;
    }

    /// <summary>
    /// Cancel the current errand tracking without sending failure notifications.
    /// Used when a new errand is assigned, replacing the old one.
    /// </summary>
    public void CancelTracking()
    {
        if (CurrentProgress != null)
        {
            NeuroLogger.Log($"Cancelled tracking for: {CurrentProgress.ChoreTypeName}", "ErrandTracker");
            LastCompletedProgress = CurrentProgress;
            CurrentProgress = null;
        }
    }

    /// <summary>
    /// Sends a context message to Neuro via the SDK.
    /// </summary>
    private static void SendContext(string message, bool isHighPriority)
    {
        NeuroLogger.SendContext(message, isHighPriority, "ErrandTracker");
    }

    /// <summary>
    /// Notifies listeners of an errand state change.
    /// </summary>
    private void NotifyStateChange()
    {
        if (CurrentProgress != null)
        {
            OnErrandStateChanged?.Invoke(CurrentProgress);
        }
    }
}
