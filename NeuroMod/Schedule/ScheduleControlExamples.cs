using System.Collections.Generic;
using UnityEngine;

namespace NeuroMod;

/// <summary>
/// Example usage and test methods for the duplicate schedule control system
/// </summary>
public static class ScheduleControlExamples
{
    /// <summary>
    /// Example: Set a duplicate to work-focused schedule
    /// </summary>
    /// <param name="duplicateName">Name of the duplicate to modify</param>
    public static void SetDuplicateToWorkFocused(string duplicateName)
    {
        Schedulable? schedulable = FindDuplicateByName(duplicateName);
        if (schedulable == null)
        {
            Debug.LogWarning($"[ScheduleControlExamples] Could not find duplicate: {duplicateName}");
            return;
        }

        Schedule workSchedule = CustomScheduleFactory.CreateWorkFocusedSchedule();
        DuplicateScheduleControlPatches.SetCustomSchedule(schedulable, workSchedule);

        Debug.Log($"[ScheduleControlExamples] Set {duplicateName} to work-focused schedule (20h work, 2h recreation, 2h sleep)");
    }

    /// <summary>
    /// Example: Force a duplicate to only sleep
    /// </summary>
    /// <param name="duplicateName">Name of the duplicate to modify</param>
    public static void ForceDuplicateToSleep(string duplicateName)
    {
        Schedulable? schedulable = FindDuplicateByName(duplicateName);
        if (schedulable == null)
        {
            Debug.LogWarning($"[ScheduleControlExamples] Could not find duplicate: {duplicateName}");
            return;
        }

        ScheduleBlockType sleepActivity = Db.Get().ScheduleBlockTypes.Sleep;
        DuplicateScheduleControlPatches.ForceActivity(schedulable, sleepActivity);

        Debug.Log($"[ScheduleControlExamples] Forced {duplicateName} to sleep until cleared");
    }

    /// <summary>
    /// Example: Force a duplicate to only work
    /// </summary>
    /// <param name="duplicateName">Name of the duplicate to modify</param>
    public static void ForceDuplicateToWork(string duplicateName)
    {
        Schedulable? schedulable = FindDuplicateByName(duplicateName);
        if (schedulable == null)
        {
            Debug.LogWarning($"[ScheduleControlExamples] Could not find duplicate: {duplicateName}");
            return;
        }

        ScheduleBlockType workActivity = Db.Get().ScheduleBlockTypes.Work;
        DuplicateScheduleControlPatches.ForceActivity(schedulable, workActivity);

        Debug.Log($"[ScheduleControlExamples] Forced {duplicateName} to work until cleared");
    }

    /// <summary>
    /// Example: Clear all custom controls for a duplicate
    /// </summary>
    /// <param name="duplicateName">Name of the duplicate to clear controls for</param>
    public static void ClearDuplicateControls(string duplicateName)
    {
        Schedulable? schedulable = FindDuplicateByName(duplicateName);
        if (schedulable == null)
        {
            Debug.LogWarning($"[ScheduleControlExamples] Could not find duplicate: {duplicateName}");
            return;
        }

        DuplicateScheduleControlPatches.ClearCustomSchedule(schedulable);
        DuplicateScheduleControlPatches.ClearForcedActivity(schedulable);

        Debug.Log($"[ScheduleControlExamples] Cleared all custom controls for {duplicateName} - back to default schedule");
    }

    /// <summary>
    /// Example: Apply balanced schedule to all duplicates
    /// </summary>
    public static void SetAllDuplicatesToBalanced()
    {
        Schedulable[] allSchedulables = Object.FindObjectsOfType<Schedulable>();
        Schedule balancedSchedule = CustomScheduleFactory.CreateBalancedSchedule();

        int count = 0;
        foreach (Schedulable schedulable in allSchedulables)
        {
            DuplicateScheduleControlPatches.SetCustomSchedule(schedulable, balancedSchedule);
            count++;
        }

        Debug.Log($"[ScheduleControlExamples] Applied balanced schedule to {count} duplicates (16h work, 4h recreation, 4h sleep)");
    }

    /// <summary>
    /// Example: Create a custom research team
    /// </summary>
    /// <param name="duplicateNames">List of duplicate names to add to research team</param>
    public static void CreateResearchTeam(List<string> duplicateNames)
    {
        Schedule researchSchedule = CustomScheduleFactory.CreateResearchFocusedSchedule();
        int successCount = 0;

        foreach (string name in duplicateNames)
        {
            Schedulable? schedulable = FindDuplicateByName(name);
            if (schedulable != null)
            {
                DuplicateScheduleControlPatches.SetCustomSchedule(schedulable, researchSchedule);
                Debug.Log($"[ScheduleControlExamples] Added {name} to research team");
                successCount++;
            }
            else
            {
                Debug.LogWarning($"[ScheduleControlExamples] Could not find duplicate for research team: {name}");
            }
        }

        Debug.Log($"[ScheduleControlExamples] Research team created with {successCount}/{duplicateNames.Count} members");
    }

    /// <summary>
    /// Example: Get statistics about current schedule usage
    /// </summary>
    public static void PrintScheduleStatistics()
    {
        if (ScheduleControlManager.Instance == null)
        {
            Debug.LogWarning("[ScheduleControlExamples] ScheduleControlManager not available");
            return;
        }

        ScheduleControlStats stats = ScheduleControlManager.Instance.GetScheduleStats();
        Debug.Log($"[ScheduleControlExamples] === Schedule Control Statistics ===");
        Debug.Log($"[ScheduleControlExamples] Total Duplicates: {stats.TotalDuplicates}");
        Debug.Log($"[ScheduleControlExamples] Custom Controlled: {stats.CustomControlledDuplicates}");
        Debug.Log($"[ScheduleControlExamples] Custom Schedules: {stats.CustomScheduleDuplicates}");
        Debug.Log($"[ScheduleControlExamples] Forced Activities: {stats.ForcedActivityDuplicates}");

        if (stats.ScheduleCounts.Count > 0)
        {
            Debug.Log("[ScheduleControlExamples] Schedule Usage:");
            foreach (KeyValuePair<string, int> kvp in stats.ScheduleCounts)
            {
                Debug.Log($"[ScheduleControlExamples]   {kvp.Key}: {kvp.Value} duplicates");
            }
        }

        if (stats.ActivityCounts.Count > 0)
        {
            Debug.Log("[ScheduleControlExamples] Forced Activity Usage:");
            foreach (KeyValuePair<string, int> kvp in stats.ActivityCounts)
            {
                Debug.Log($"[ScheduleControlExamples]   {kvp.Key}: {kvp.Value} duplicates");
            }
        }
    }

    /// <summary>
    /// Example: List all duplicates and their current status
    /// </summary>
    public static void ListAllDuplicatesWithStatus()
    {
        Schedulable[] allSchedulables = Object.FindObjectsOfType<Schedulable>();

        Debug.Log("[ScheduleControlExamples] === All Duplicates Status ===");
        foreach (Schedulable schedulable in allSchedulables)
        {
            string status = "Default Schedule";

            if (DuplicateScheduleControlPatches.HasCustomControl(schedulable))
            {
                ScheduleBlockType? forcedActivity = DuplicateScheduleControlPatches.GetForcedActivity(schedulable);
                if (forcedActivity != null)
                {
                    status = $"FORCED: {forcedActivity.Name}";
                }
                else
                {
                    Schedule? customSchedule = DuplicateScheduleControlPatches.GetEffectiveSchedule(schedulable);
                    status = $"CUSTOM: {customSchedule?.name ?? "Unknown"}";
                }
            }

            Debug.Log($"[ScheduleControlExamples] {schedulable.GetProperName()}: {status}");
        }
    }

    /// <summary>
    /// Helper method to find a duplicate by name
    /// </summary>
    /// <param name="name">Name to search for (partial matches allowed)</param>
    /// <returns>The schedulable for the duplicate, or null if not found</returns>
    private static Schedulable? FindDuplicateByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            Debug.LogWarning("[ScheduleControlExamples] Cannot find duplicate with null or empty name");
            return null;
        }

        Schedulable[] allSchedulables = Object.FindObjectsOfType<Schedulable>();

        foreach (Schedulable schedulable in allSchedulables)
        {
            if (schedulable.GetProperName().Contains(name))
            {
                return schedulable;
            }
        }

        return null;
    }

    /// <summary>
    /// Example: Demonstration of different schedule types
    /// </summary>
    public static void DemonstrateScheduleTypes()
    {
        Debug.Log("[ScheduleControlExamples] === Available Schedule Types ===");

        List<Schedule> schedules = CustomScheduleFactory.GetAllPredefinedSchedules();
        foreach (Schedule schedule in schedules)
        {
            Debug.Log($"[ScheduleControlExamples] Schedule: {schedule.name}");
            Debug.Log($"[ScheduleControlExamples]   Blocks: {schedule.GetBlocks().Count}");
            // Additional schedule info could be logged here
        }
    }

    /// <summary>
    /// Validate that all required systems are available
    /// </summary>
    /// <returns>True if all systems are ready for schedule control</returns>
    public static bool ValidateSystemsReady()
    {
        bool allReady = true;

        if (Db.Get()?.ScheduleBlockTypes == null)
        {
            Debug.LogError("[ScheduleControlExamples] Schedule block types not available");
            allReady = false;
        }

        if (ScheduleControlManager.Instance == null)
        {
            Debug.LogWarning("[ScheduleControlExamples] ScheduleControlManager not available");
            // This is a warning, not an error, as it's not required for basic functionality
        }

        Debug.Log($"[ScheduleControlExamples] Systems validation: {(allReady ? "PASSED" : "FAILED")}");
        return allReady;
    }
}