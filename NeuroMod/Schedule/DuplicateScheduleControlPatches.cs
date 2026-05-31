using HarmonyLib;

namespace NeuroMod;

/// <summary>
/// Main patches for controlling duplicate schedules
/// </summary>
/// <pre>Schedule override state is maintained through <see cref="ScheduleOverrideApi"/> for live duplicants.</pre>
/// <post>Patched schedule permission checks honor forced activities and detached custom schedules before default game logic.</post>
[HarmonyPatch]
public static class DuplicateScheduleControlPatches
{
    /// <summary>
    /// Patch ScheduleManager.IsAllowed to override schedule checking
    /// This is the core method that determines what duplicates can do
    /// </summary>
    /// <param name="schedulable">Duplicant whose schedule permission is being evaluated.</param>
    /// <param name="schedule_block_type">Schedule block currently being queried.</param>
    /// <param name="__result">Patched permission result when the override handles the query.</param>
    /// <returns><see langword="false"/> when override logic has produced the final answer; otherwise, <see langword="true"/>.</returns>
    /// <pre>The queried duplicant may have forced-activity or custom-schedule override state tracked by <see cref="ScheduleOverrideApi"/>.</pre>
    /// <post>Override state is consulted first and the original permission check is skipped when an override determines the result.</post>
    [HarmonyPatch(typeof(ScheduleManager), "IsAllowed")]
    [HarmonyPrefix]
    public static bool IsAllowed_Prefix(
        Schedulable schedulable,
        ScheduleBlockType schedule_block_type,
        ref bool __result)
    {
        // Check if this duplicate has a forced activity
        if (ScheduleOverrideApi.TryGetForcedActivity(schedulable, out ScheduleBlockType? forcedActivity) &&
            forcedActivity is not null)
        {
            __result = forcedActivity == schedule_block_type;
            Debug.Log($"[ScheduleControl] {schedulable.name} forced activity check: {schedule_block_type.Id} = {__result}");
            return false; // Skip original method
        }

        // Check if this duplicate has a custom schedule
        if (ScheduleOverrideApi.TryGetCustomSchedule(schedulable, out Schedule? customSchedule) &&
            customSchedule is not null)
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
    /// <param name="__instance">Duplicant whose schedule permission is being evaluated.</param>
    /// <param name="schedule_block_type">Schedule block currently being queried.</param>
    /// <param name="__result">Patched permission result when the override handles the query.</param>
    /// <returns><see langword="false"/> when override logic has produced the final answer; otherwise, <see langword="true"/>.</returns>
    /// <pre>The queried duplicant may have forced-activity or custom-schedule override state tracked by <see cref="ScheduleOverrideApi"/>.</pre>
    /// <post>Forced activity, custom schedule, and red-alert override rules are applied before the base game method runs.</post>
    [HarmonyPatch(typeof(Schedulable), "IsAllowed")]
    [HarmonyPrefix]
    public static bool Schedulable_IsAllowed_Prefix(
        Schedulable __instance,
        ScheduleBlockType schedule_block_type,
        ref bool __result)
    {
        // Check for forced activity first
        if (ScheduleOverrideApi.TryGetForcedActivity(__instance, out ScheduleBlockType? forcedActivity) &&
            forcedActivity is not null)
        {
            __result = forcedActivity == schedule_block_type;
            return false; // Skip original method
        }

        // Check for custom schedule
        if (ScheduleOverrideApi.TryGetCustomSchedule(__instance, out Schedule? customSchedule) &&
            customSchedule is not null)
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
    /// <pre><paramref name="schedulable"/> refers to a live duplicate that can accept schedule overrides.</pre>
    /// <post>The duplicate uses the supplied custom schedule through <see cref="ScheduleOverrideApi"/>.</post>
    public static void SetCustomSchedule(Schedulable? schedulable, Schedule? customSchedule)
    {
        ScheduleOverrideApi.SetCustomSchedule(schedulable, customSchedule);
    }

    /// <summary>
    /// Remove custom schedule override for a duplicate
    /// </summary>
    /// <param name="schedulable">The duplicate to clear schedule for</param>
    /// <pre><paramref name="schedulable"/> may currently have a custom schedule override.</pre>
    /// <post>Any stored custom schedule override is cleared for the duplicate.</post>
    public static void ClearCustomSchedule(Schedulable? schedulable)
    {
        ScheduleOverrideApi.ClearCustomSchedule(schedulable);
    }

    /// <summary>
    /// Force a specific activity for a duplicate (overrides schedule)
    /// </summary>
    /// <param name="schedulable">The duplicate to force activity for</param>
    /// <param name="activityType">The activity type to force</param>
    /// <pre><paramref name="schedulable"/> refers to a live duplicate and <paramref name="activityType"/> identifies a valid schedule block.</pre>
    /// <post>The duplicate's effective permission checks now prioritize the supplied forced activity.</post>
    public static void ForceActivity(Schedulable? schedulable, ScheduleBlockType? activityType)
    {
        ScheduleOverrideApi.ForceActivity(schedulable, activityType);
    }

    /// <summary>
    /// Clear forced activity for a duplicate
    /// </summary>
    /// <param name="schedulable">The duplicate to clear forced activity for</param>
    /// <pre><paramref name="schedulable"/> may currently have a forced activity override.</pre>
    /// <post>Any stored forced activity override is cleared for the duplicate.</post>
    public static void ClearForcedActivity(Schedulable? schedulable)
    {
        ScheduleOverrideApi.ClearForcedActivity(schedulable);
    }

    /// <summary>
    /// Get the effective schedule for a duplicate (custom or original)
    /// </summary>
    /// <param name="schedulable">The duplicate to get schedule for</param>
    /// <returns>The effective schedule, or null if not found</returns>
    /// <pre><paramref name="schedulable"/> may or may not have custom schedule state.</pre>
    /// <post>The currently effective schedule reference for the duplicate is returned.</post>
    public static Schedule? GetEffectiveSchedule(Schedulable? schedulable)
    {
        return ScheduleOverrideApi.GetEffectiveSchedule(schedulable);
    }

    /// <summary>
    /// Check if a duplicate has a custom schedule or forced activity
    /// </summary>
    /// <param name="schedulable">The duplicate to check</param>
    /// <returns>True if the duplicate has custom control</returns>
    /// <pre><paramref name="schedulable"/> may have custom control state tracked in <see cref="ScheduleOverrideApi"/>.</pre>
    /// <post>The result reports whether any custom schedule-control override is active.</post>
    public static bool HasCustomControl(Schedulable? schedulable)
    {
        return ScheduleOverrideApi.HasCustomControl(schedulable);
    }

    /// <summary>
    /// Get current forced activity for a duplicate
    /// </summary>
    /// <param name="schedulable">The duplicate to check</param>
    /// <returns>The forced activity, or null if none</returns>
    /// <pre><paramref name="schedulable"/> may currently have a forced activity override.</pre>
    /// <post>The active forced activity is returned when one exists.</post>
    public static ScheduleBlockType? GetForcedActivity(Schedulable? schedulable)
    {
        return ScheduleOverrideApi.GetForcedActivity(schedulable);
    }

    #endregion Public API for Schedule Control
}