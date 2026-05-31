using System.Collections.Generic;

namespace NeuroMod;

/// <summary>
/// Simplified component for controlling duplicate schedules through console commands
/// This avoids UI complications while providing the core functionality
/// </summary>
/// <pre>The component is attached to a live game object in a running colony and can query schedule data from the game database.</pre>
/// <post>The component exposes a lightweight debug-oriented surface for targeting duplicates and applying schedule overrides.</post>
public class DuplicateScheduleControlUI : KMonoBehaviour
{
    private Schedulable? targetSchedulable;
    private List<Schedule> availableSchedules = [];
    private List<ScheduleBlockType> availableActivities = [];

    /// <summary>
    /// Initializes the available schedule and activity lists when the component spawns.
    /// </summary>
    /// <pre>The game database and schedule block types are available.</pre>
    /// <post>The component has refreshed its available options and logged the supported debug commands.</post>
    protected override void OnSpawn()
    {
        base.OnSpawn();
        RefreshAvailableOptions();
        SetupDebugCommands();
    }

    /// <summary>
    /// Sets the duplicate whose schedule overrides should be inspected and manipulated.
    /// </summary>
    /// <param name="schedulable">The duplicate schedule component to target, or <see langword="null"/> to clear selection.</param>
    /// <pre><paramref name="schedulable"/> is either null or a live duplicate schedule component.</pre>
    /// <post>The current target has been updated and its status has been logged.</post>
    public void SetTarget(Schedulable? schedulable)
    {
        targetSchedulable = schedulable;
        LogCurrentStatus();
    }

    /// <summary>
    /// Refreshes the cached schedule and activity choices exposed by the helper.
    /// </summary>
    /// <pre>The schedule database and standard block types are available.</pre>
    /// <post>The local schedule and activity option lists reflect the current game database.</post>
    private void RefreshAvailableOptions()
    {
        // Get available schedules
        availableSchedules = CustomScheduleFactory.GetAllPredefinedSchedules();

        // Get available activities
        availableActivities =
        [
            Db.Get().ScheduleBlockTypes.Work,
            Db.Get().ScheduleBlockTypes.Sleep,
            Db.Get().ScheduleBlockTypes.Recreation,
            Db.Get().ScheduleBlockTypes.Eat,
            Db.Get().ScheduleBlockTypes.Hygiene
        ];
    }

    /// <summary>
    /// Logs the available debug commands for manual schedule testing.
    /// </summary>
    /// <pre>The component has been initialized and can access its option lists.</pre>
    /// <post>The supported command surface has been written to the debug log.</post>
    private void SetupDebugCommands()
    {
        // Register debug commands for testing
        Debug.Log("[ScheduleUI] Schedule control UI initialized. Use console commands to control schedules.");
        LogAvailableCommands();
    }

    /// <summary>
    /// Logs the currently selected duplicate and any active schedule overrides.
    /// </summary>
    /// <pre>The current target may be null or refer to a live duplicate.</pre>
    /// <post>The debug log reflects the target's current schedule-control state.</post>
    private void LogCurrentStatus()
    {
        if (targetSchedulable == null)
        {
            Debug.Log("[ScheduleUI] No target selected");
            return;
        }

        string status = $"[ScheduleUI] Target: {targetSchedulable.GetProperName()}\n";

        if (DuplicateScheduleControlPatches.HasCustomControl(targetSchedulable))
        {
            ScheduleBlockType? forcedActivity = DuplicateScheduleControlPatches.GetForcedActivity(targetSchedulable);
            if (forcedActivity != null)
            {
                status += $"Status: Forced Activity - {forcedActivity.Name}";
            }
            else
            {
                Schedule? customSchedule = DuplicateScheduleControlPatches.GetEffectiveSchedule(targetSchedulable);
                status += $"Status: Custom Schedule - {customSchedule?.name ?? "Unknown"}";
            }
        }
        else
        {
            status += "Status: Using Default Schedule";
        }

        Debug.Log(status);
    }

    /// <summary>
    /// Logs the available schedule and activity indices for manual testing.
    /// </summary>
    /// <pre>The local schedule and activity option lists have been populated.</pre>
    /// <post>The debug log contains the current command, schedule, and activity choices.</post>
    private void LogAvailableCommands()
    {
        Debug.Log("[ScheduleUI] Available commands:");
        Debug.Log("- Use DuplicateScheduleControlPatches.SetCustomSchedule(schedulable, schedule)");
        Debug.Log("- Use DuplicateScheduleControlPatches.ForceActivity(schedulable, activity)");
        Debug.Log("- Use DuplicateScheduleControlPatches.ClearCustomSchedule(schedulable)");
        Debug.Log("- Use DuplicateScheduleControlPatches.ClearForcedActivity(schedulable)");

        Debug.Log("\nAvailable schedules:");
        for (int i = 0; i < availableSchedules.Count; i++)
        {
            Debug.Log($"- {i}: {availableSchedules[i].name}");
        }

        Debug.Log("\nAvailable activities:");
        for (int i = 0; i < availableActivities.Count; i++)
        {
            Debug.Log($"- {i}: {availableActivities[i].Name}");
        }
    }

    /// <summary>
    /// Apply a schedule by index for testing
    /// </summary>
    /// <param name="scheduleIndex">Index of the schedule to apply</param>
    /// <pre>A target duplicate has been selected and <paramref name="scheduleIndex"/> refers to the cached schedule list.</pre>
    /// <post>When the index is valid, the selected schedule override has been applied and the new status has been logged.</post>
    public void ApplyScheduleByIndex(int scheduleIndex)
    {
        if (targetSchedulable == null)
        {
            Debug.LogWarning("[ScheduleUI] No target schedulable set");
            return;
        }

        if (scheduleIndex < 0 || scheduleIndex >= availableSchedules.Count)
        {
            Debug.LogWarning($"[ScheduleUI] Invalid schedule index: {scheduleIndex}");
            return;
        }

        Schedule selectedSchedule = availableSchedules[scheduleIndex];
        ScheduleOverrideApi.SetCustomSchedule(targetSchedulable, selectedSchedule);
        Debug.Log($"[ScheduleUI] Applied schedule '{selectedSchedule.name}' to {targetSchedulable.GetProperName()}");

        LogCurrentStatus();
    }

    /// <summary>
    /// Force an activity by index for testing
    /// </summary>
    /// <param name="activityIndex">Index of the activity to force</param>
    /// <pre>A target duplicate has been selected and <paramref name="activityIndex"/> refers to the cached activity list.</pre>
    /// <post>When the index is valid, the selected activity override has been applied and the new status has been logged.</post>
    public void ForceActivityByIndex(int activityIndex)
    {
        if (targetSchedulable == null)
        {
            Debug.LogWarning("[ScheduleUI] No target schedulable set");
            return;
        }

        if (activityIndex < 0 || activityIndex >= availableActivities.Count)
        {
            Debug.LogWarning($"[ScheduleUI] Invalid activity index: {activityIndex}");
            return;
        }

        ScheduleBlockType selectedActivity = availableActivities[activityIndex];
        ScheduleOverrideApi.ForceActivity(targetSchedulable, selectedActivity);
        Debug.Log($"[ScheduleUI] Forced activity '{selectedActivity.Name}' for {targetSchedulable.GetProperName()}");

        LogCurrentStatus();
    }

    /// <summary>
    /// Clear all overrides for the current target
    /// </summary>
    /// <pre>A target duplicate may or may not currently be selected.</pre>
    /// <post>When a target exists, both custom schedule and forced activity overrides have been cleared.</post>
    public void ClearAllOverrides()
    {
        if (targetSchedulable == null)
        {
            Debug.LogWarning("[ScheduleUI] No target schedulable set");
            return;
        }

        ScheduleOverrideApi.ClearCustomSchedule(targetSchedulable);
        ScheduleOverrideApi.ClearForcedActivity(targetSchedulable);

        Debug.Log($"[ScheduleUI] Cleared all overrides for {targetSchedulable.GetProperName()}");
        LogCurrentStatus();
    }

    /// <summary>
    /// Find and set target by duplicate name
    /// </summary>
    /// <param name="duplicateName">Name or partial name of the duplicate to target</param>
    /// <pre><paramref name="duplicateName"/> contains a non-empty duplicate name fragment.</pre>
    /// <post>When a match is found, the current target has been updated; otherwise a warning has been logged.</post>
    public void SetTargetByName(string duplicateName)
    {
        if (string.IsNullOrWhiteSpace(duplicateName))
        {
            Debug.LogWarning("[ScheduleUI] Duplicate name cannot be null or empty");
            return;
        }

        Schedulable[] allSchedulables = FindObjectsOfType<Schedulable>();

        foreach (Schedulable schedulable in allSchedulables)
        {
            if (schedulable.GetProperName().Contains(duplicateName))
            {
                SetTarget(schedulable);
                Debug.Log($"[ScheduleUI] Found and set target: {schedulable.GetProperName()}");
                return;
            }
        }

        Debug.LogWarning($"[ScheduleUI] Could not find duplicate with name containing: {duplicateName}");
    }

    /// <summary>
    /// List all duplicates for easy targeting
    /// </summary>
    /// <pre>The colony contains zero or more live schedulable duplicates.</pre>
    /// <post>The debug log lists all currently discovered duplicates and whether they are under custom control.</post>
    public void ListAllDuplicates()
    {
        Schedulable[] allSchedulables = FindObjectsOfType<Schedulable>();

        Debug.Log("[ScheduleUI] All duplicates:");
        for (int i = 0; i < allSchedulables.Length; i++)
        {
            Schedulable schedulable = allSchedulables[i];
            string status = DuplicateScheduleControlPatches.HasCustomControl(schedulable) ? " (CONTROLLED)" : "";
            Debug.Log($"- {i}: {schedulable.GetProperName()}{status}");
        }
    }
}