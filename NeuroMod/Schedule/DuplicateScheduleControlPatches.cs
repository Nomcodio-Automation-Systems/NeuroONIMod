using HarmonyLib;
using System.Collections.Generic;

namespace NeuroMod;

/// <summary>
/// Main patches for controlling duplicate schedules
/// </summary>
[HarmonyPatch]
public static class DuplicateScheduleControlPatches
{
    // Dictionary to store custom schedule overrides per duplicate
    private static readonly Dictionary<Schedulable, Schedule> customScheduleOverrides =
        [];

    private static readonly Dictionary<Schedulable, ScheduleBlockType> forcedCurrentActivity =
        [];

    /// <summary>
    /// Patch ScheduleManager.IsAllowed to override schedule checking
    /// This is the core method that determines what duplicates can do
    /// </summary>
    [HarmonyPatch(typeof(ScheduleManager), "IsAllowed")]
    [HarmonyPrefix]
    public static bool IsAllowed_Prefix(
        Schedulable schedulable,
        ScheduleBlockType schedule_block_type,
        ref bool __result)
    {
        // Check if this duplicate has a forced activity
        if (forcedCurrentActivity.TryGetValue(schedulable, out ScheduleBlockType forcedActivity))
        {
            __result = forcedActivity == schedule_block_type;
            Debug.Log($"[ScheduleControl] {schedulable.name} forced activity check: {schedule_block_type.Id} = {__result}");
            return false; // Skip original method
        }

        // Check if this duplicate has a custom schedule
        if (customScheduleOverrides.TryGetValue(schedulable, out Schedule customSchedule))
        {
            __result = customSchedule.GetCurrentScheduleBlock().IsAllowed(schedule_block_type);
            Debug.Log($"[ScheduleControl] {schedulable.name} custom schedule check: {schedule_block_type.Id} = {__result}");
            return false; // Skip original method
        }

        // Use default behavior
        return true;
    }

    /// <summary>
    /// Patch Schedulable.IsAllowed to add our custom logic
    /// </summary>
    [HarmonyPatch(typeof(Schedulable), "IsAllowed")]
    [HarmonyPrefix]
    public static bool Schedulable_IsAllowed_Prefix(
        Schedulable __instance,
        ScheduleBlockType schedule_block_type,
        ref bool __result)
    {
        // Check for forced activity first
        if (forcedCurrentActivity.TryGetValue(__instance, out ScheduleBlockType forcedActivity))
        {
            __result = forcedActivity == schedule_block_type;
            return false; // Skip original method
        }

        // Check for custom schedule
        if (customScheduleOverrides.TryGetValue(__instance, out Schedule customSchedule))
        {
            WorldContainer? myWorld = __instance.gameObject.GetMyWorld();
            if (myWorld == null)
            {
                Debug.LogWarning($"Schedulable {__instance.name} is not on a valid world");
                __result = false;
                return false;
            }

            // Red alert overrides everything
            if (myWorld.AlertManager.IsRedAlert())
            {
                __result = true;
                return false;
            }

            __result = customSchedule.GetCurrentScheduleBlock().IsAllowed(schedule_block_type);
            return false; // Skip original method
        }

        // Use default behavior
        return true;
    }

    #region Public API for Schedule Control

    /// <summary>
    /// Set a custom schedule for a specific duplicate
    /// </summary>
    /// <param name="schedulable">The duplicate to set schedule for</param>
    /// <param name="customSchedule">The custom schedule to apply</param>
    public static void SetCustomSchedule(Schedulable? schedulable, Schedule? customSchedule)
    {
        if (schedulable == null || customSchedule == null)
        {
            Debug.LogWarning("[ScheduleControl] Cannot set custom schedule: schedulable or customSchedule is null");
            return;
        }

        customScheduleOverrides[schedulable] = customSchedule;

        // Clear any forced activity
        forcedCurrentActivity.Remove(schedulable);

        Debug.Log($"[ScheduleControl] Set custom schedule for {schedulable.name}: {customSchedule.name}");

        // Trigger schedule change events
        schedulable.OnScheduleChanged(customSchedule);
    }

    /// <summary>
    /// Remove custom schedule override for a duplicate
    /// </summary>
    /// <param name="schedulable">The duplicate to clear schedule for</param>
    public static void ClearCustomSchedule(Schedulable? schedulable)
    {
        if (schedulable == null)
        {
            Debug.LogWarning("[ScheduleControl] Cannot clear custom schedule: schedulable is null");
            return;
        }

        customScheduleOverrides.Remove(schedulable);
        forcedCurrentActivity.Remove(schedulable);

        Debug.Log($"[ScheduleControl] Cleared custom schedule for {schedulable.name}");

        // Get the original schedule and trigger change
        Schedule? originalSchedule = ScheduleManager.Instance?.GetSchedule(schedulable);
        if (originalSchedule != null)
        {
            schedulable.OnScheduleChanged(originalSchedule);
        }
    }

    /// <summary>
    /// Force a specific activity for a duplicate (overrides schedule)
    /// </summary>
    /// <param name="schedulable">The duplicate to force activity for</param>
    /// <param name="activityType">The activity type to force</param>
    public static void ForceActivity(Schedulable? schedulable, ScheduleBlockType? activityType)
    {
        if (schedulable == null || activityType == null)
        {
            Debug.LogWarning("[ScheduleControl] Cannot force activity: schedulable or activityType is null");
            return;
        }

        forcedCurrentActivity[schedulable] = activityType;

        Debug.Log($"[ScheduleControl] Forced activity for {schedulable.name}: {activityType.Id}");

        // Trigger schedule change to update behaviors
        Schedule? currentSchedule = GetEffectiveSchedule(schedulable);
        if (currentSchedule != null)
        {
            schedulable.OnScheduleBlocksChanged(currentSchedule);
        }
    }

    /// <summary>
    /// Clear forced activity for a duplicate
    /// </summary>
    /// <param name="schedulable">The duplicate to clear forced activity for</param>
    public static void ClearForcedActivity(Schedulable? schedulable)
    {
        if (schedulable == null)
        {
            Debug.LogWarning("[ScheduleControl] Cannot clear forced activity: schedulable is null");
            return;
        }

        forcedCurrentActivity.Remove(schedulable);

        Debug.Log($"[ScheduleControl] Cleared forced activity for {schedulable.name}");

        // Trigger schedule change
        Schedule? currentSchedule = GetEffectiveSchedule(schedulable);
        if (currentSchedule != null)
        {
            schedulable.OnScheduleBlocksChanged(currentSchedule);
        }
    }

    /// <summary>
    /// Get the effective schedule for a duplicate (custom or original)
    /// </summary>
    /// <param name="schedulable">The duplicate to get schedule for</param>
    /// <returns>The effective schedule, or null if not found</returns>
    public static Schedule? GetEffectiveSchedule(Schedulable? schedulable)
    {
        return schedulable == null
            ? null
            : customScheduleOverrides.TryGetValue(schedulable, out Schedule customSchedule)
            ? customSchedule
            : ScheduleManager.Instance?.GetSchedule(schedulable);
    }

    /// <summary>
    /// Check if a duplicate has a custom schedule or forced activity
    /// </summary>
    /// <param name="schedulable">The duplicate to check</param>
    /// <returns>True if the duplicate has custom control</returns>
    public static bool HasCustomControl(Schedulable? schedulable)
    {
        return schedulable != null && (customScheduleOverrides.ContainsKey(schedulable) ||
               forcedCurrentActivity.ContainsKey(schedulable));
    }

    /// <summary>
    /// Get current forced activity for a duplicate
    /// </summary>
    /// <param name="schedulable">The duplicate to check</param>
    /// <returns>The forced activity, or null if none</returns>
    public static ScheduleBlockType? GetForcedActivity(Schedulable? schedulable)
    {
        if (schedulable == null)
        {
            return null;
        }

        forcedCurrentActivity.TryGetValue(schedulable, out ScheduleBlockType? activity);
        return activity;
    }

    #endregion Public API for Schedule Control
}