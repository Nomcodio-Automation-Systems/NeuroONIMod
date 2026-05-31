using System;
using System.Collections.Generic;

namespace NeuroMod;

/// <summary>
/// Central API for schedule overrides and forced activities.
/// Ensures callers work with detached schedule copies instead of shared live schedules.
/// </summary>
/// <pre>
/// Callers operate on live <see cref="Schedulable"/> instances and avoid mutating shared schedules in place.
/// </pre>
/// <post>
/// Per-duplicant overrides are stored separately from game-managed schedule definitions.
/// </post>
public static class ScheduleOverrideApi
{
    private static readonly Dictionary<Schedulable, Schedule> _customScheduleOverrides = [];
    private static readonly Dictionary<Schedulable, ScheduleBlockType> _forcedCurrentActivity = [];

    /// <summary>
    /// Creates a detached schedule copy for safe per-duplicant overrides.
    /// </summary>
    /// <param name="sourceSchedule">The source schedule to copy.</param>
    /// <returns>A detached schedule copy with duplicated blocks and matching metadata.</returns>
    /// <pre>
    /// <paramref name="sourceSchedule"/> is a valid schedule template to clone.
    /// </pre>
    /// <post>
    /// A detached schedule instance is returned that can be modified without affecting the source schedule.
    /// </post>
    public static Schedule CreateDetachedCopy(Schedule sourceSchedule)
    {
        if (sourceSchedule is null)
        {
            throw new ArgumentNullException(nameof(sourceSchedule));
        }

        Schedule detachedSchedule = new(sourceSchedule.name, sourceSchedule.GetBlocks(), sourceSchedule.alarmActivated)
        {
            ProgressTimetableIdx = sourceSchedule.ProgressTimetableIdx,
            isDefaultForBionics = sourceSchedule.isDefaultForBionics
        };

        return detachedSchedule;
    }

    /// <summary>
    /// Sets a custom schedule for a specific duplicant using a detached copy.
    /// </summary>
    /// <param name="schedulable">The duplicant to override.</param>
    /// <param name="sourceSchedule">The source schedule to clone and apply.</param>
    /// <pre>
    /// Both parameters refer to live game objects and the source schedule can be cloned safely.
    /// </pre>
    /// <post>
    /// The duplicant now uses a detached override schedule and any forced activity is cleared.
    /// </post>
    public static void SetCustomSchedule(Schedulable? schedulable, Schedule? sourceSchedule)
    {
        if (schedulable is null || sourceSchedule is null)
        {
            Debug.LogWarning("[ScheduleControl] Cannot set custom schedule: schedulable or sourceSchedule is null");
            return;
        }

        Schedule scheduleCopy = CreateDetachedCopy(sourceSchedule);
        _customScheduleOverrides[schedulable] = scheduleCopy;
        _forcedCurrentActivity.Remove(schedulable);

        Debug.Log($"[ScheduleControl] Set custom schedule for {schedulable.name}: {scheduleCopy.name} (detached copy)");
        schedulable.OnScheduleChanged(scheduleCopy);
    }

    /// <summary>
    /// Clears the custom schedule override for a duplicant.
    /// </summary>
    /// <param name="schedulable">The duplicant to restore.</param>
    /// <pre>
    /// The duplicant may currently have a custom schedule override or forced activity.
    /// </pre>
    /// <post>
    /// The stored override state is removed and the duplicant is re-bound to the manager-provided schedule when available.
    /// </post>
    public static void ClearCustomSchedule(Schedulable? schedulable)
    {
        if (schedulable is null)
        {
            Debug.LogWarning("[ScheduleControl] Cannot clear custom schedule: schedulable is null");
            return;
        }

        _customScheduleOverrides.Remove(schedulable);
        _forcedCurrentActivity.Remove(schedulable);

        Debug.Log($"[ScheduleControl] Cleared custom schedule for {schedulable.name}");

        Schedule? originalSchedule = ScheduleManager.Instance?.GetSchedule(schedulable);
        if (originalSchedule is not null)
        {
            schedulable.OnScheduleChanged(originalSchedule);
        }
    }

    /// <summary>
    /// Forces a duplicant into a specific activity.
    /// </summary>
    /// <param name="schedulable">The duplicant to affect.</param>
    /// <param name="activityType">The activity to force.</param>
    /// <pre>
    /// The duplicant has an effective schedule that can be refreshed after the forced activity is applied.
    /// </pre>
    /// <post>
    /// The forced activity is stored and the duplicant's schedule blocks are refreshed.
    /// </post>
    public static void ForceActivity(Schedulable? schedulable, ScheduleBlockType? activityType)
    {
        if (schedulable is null || activityType is null)
        {
            Debug.LogWarning("[ScheduleControl] Cannot force activity: schedulable or activityType is null");
            return;
        }

        _forcedCurrentActivity[schedulable] = activityType;

        Debug.Log($"[ScheduleControl] Forced activity for {schedulable.name}: {activityType.Id}");

        Schedule? currentSchedule = GetEffectiveSchedule(schedulable);
        if (currentSchedule is not null)
        {
            schedulable.OnScheduleBlocksChanged(currentSchedule);
        }
    }

    /// <summary>
    /// Clears a forced activity override for a duplicant.
    /// </summary>
    /// <param name="schedulable">The duplicant to affect.</param>
    /// <pre>
    /// The duplicant may currently have a forced activity override.
    /// </pre>
    /// <post>
    /// Any stored forced activity is removed and schedule blocks are refreshed when possible.
    /// </post>
    public static void ClearForcedActivity(Schedulable? schedulable)
    {
        if (schedulable is null)
        {
            Debug.LogWarning("[ScheduleControl] Cannot clear forced activity: schedulable is null");
            return;
        }

        _forcedCurrentActivity.Remove(schedulable);

        Debug.Log($"[ScheduleControl] Cleared forced activity for {schedulable.name}");

        Schedule? currentSchedule = GetEffectiveSchedule(schedulable);
        if (currentSchedule is not null)
        {
            schedulable.OnScheduleBlocksChanged(currentSchedule);
        }
    }

    /// <summary>
    /// Gets the effective schedule reference for a duplicant.
    /// </summary>
    /// <param name="schedulable">The duplicant to inspect.</param>
    /// <returns>The detached override schedule or the original game-managed schedule.</returns>
    /// <pre>
    /// <paramref name="schedulable"/> may or may not currently have a detached override.
    /// </pre>
    /// <post>
    /// The currently effective schedule reference for the duplicant is returned.
    /// </post>
    public static Schedule? GetEffectiveSchedule(Schedulable? schedulable)
    {
        return schedulable is null
            ? null
            : _customScheduleOverrides.TryGetValue(schedulable, out Schedule? customSchedule)
                ? customSchedule
                : ScheduleManager.Instance?.GetSchedule(schedulable);
    }

    /// <summary>
    /// Checks whether a duplicant has any custom schedule control.
    /// </summary>
    /// <param name="schedulable">The duplicant to inspect.</param>
    /// <returns>True when a custom schedule or forced activity is active.</returns>
    /// <pre>
    /// <paramref name="schedulable"/> may have override and forced-activity state tracked in this API.
    /// </pre>
    /// <post>
    /// The method reports whether any custom schedule-control state is active for the duplicant.
    /// </post>
    public static bool HasCustomControl(Schedulable? schedulable)
    {
        return schedulable is not null && (_customScheduleOverrides.ContainsKey(schedulable) ||
            _forcedCurrentActivity.ContainsKey(schedulable));
    }

    /// <summary>
    /// Gets the currently forced activity for a duplicant.
    /// </summary>
    /// <param name="schedulable">The duplicant to inspect.</param>
    /// <returns>The forced activity, or null when none is active.</returns>
    /// <pre>
    /// <paramref name="schedulable"/> may currently have a forced activity override.
    /// </pre>
    /// <post>
    /// The stored forced activity is returned when one exists.
    /// </post>
    public static ScheduleBlockType? GetForcedActivity(Schedulable? schedulable)
    {
        if (schedulable is null)
        {
            return null;
        }

        _forcedCurrentActivity.TryGetValue(schedulable, out ScheduleBlockType? activity);
        return activity;
    }

    internal static bool TryGetForcedActivity(Schedulable? schedulable, out ScheduleBlockType? activity)
    {
        if (schedulable is null)
        {
            activity = null;
            return false;
        }

        return _forcedCurrentActivity.TryGetValue(schedulable, out activity);
    }

    internal static bool TryGetCustomSchedule(Schedulable? schedulable, out Schedule? customSchedule)
    {
        if (schedulable is null)
        {
            customSchedule = null;
            return false;
        }

        return _customScheduleOverrides.TryGetValue(schedulable, out customSchedule);
    }
}
