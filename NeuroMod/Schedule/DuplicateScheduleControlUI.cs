using System.Collections.Generic;

namespace NeuroMod;

/// <summary>
/// Simplified component for controlling duplicate schedules through console commands
/// This avoids UI complications while providing the core functionality
/// </summary>
public class DuplicateScheduleControlUI : KMonoBehaviour
{
    private Schedulable? targetSchedulable;
    private List<Schedule> availableSchedules = [];
    private List<ScheduleBlockType> availableActivities = [];

    protected override void OnSpawn()
    {
        base.OnSpawn();
        RefreshAvailableOptions();
        SetupDebugCommands();
    }

    public void SetTarget(Schedulable? schedulable)
    {
        targetSchedulable = schedulable;
        LogCurrentStatus();
    }

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

    private void SetupDebugCommands()
    {
        // Register debug commands for testing
        Debug.Log("[ScheduleUI] Schedule control UI initialized. Use console commands to control schedules.");
        LogAvailableCommands();
    }

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
        DuplicateScheduleControlPatches.SetCustomSchedule(targetSchedulable, selectedSchedule);
        Debug.Log($"[ScheduleUI] Applied schedule '{selectedSchedule.name}' to {targetSchedulable.GetProperName()}");

        LogCurrentStatus();
    }

    /// <summary>
    /// Force an activity by index for testing
    /// </summary>
    /// <param name="activityIndex">Index of the activity to force</param>
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
        DuplicateScheduleControlPatches.ForceActivity(targetSchedulable, selectedActivity);
        Debug.Log($"[ScheduleUI] Forced activity '{selectedActivity.Name}' for {targetSchedulable.GetProperName()}");

        LogCurrentStatus();
    }

    /// <summary>
    /// Clear all overrides for the current target
    /// </summary>
    public void ClearAllOverrides()
    {
        if (targetSchedulable == null)
        {
            Debug.LogWarning("[ScheduleUI] No target schedulable set");
            return;
        }

        DuplicateScheduleControlPatches.ClearCustomSchedule(targetSchedulable);
        DuplicateScheduleControlPatches.ClearForcedActivity(targetSchedulable);

        Debug.Log($"[ScheduleUI] Cleared all overrides for {targetSchedulable.GetProperName()}");
        LogCurrentStatus();
    }

    /// <summary>
    /// Find and set target by duplicate name
    /// </summary>
    /// <param name="duplicateName">Name or partial name of the duplicate to target</param>
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