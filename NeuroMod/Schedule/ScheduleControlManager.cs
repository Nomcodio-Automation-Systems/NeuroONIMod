using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NeuroMod;

/// <summary>
/// Manager class for handling schedule control operations and persistence
/// </summary>
public class ScheduleControlManager : KMonoBehaviour, ISaveLoadable
{
    public static ScheduleControlManager? Instance { get; private set; }

    // Get debug logging setting from ConfigManager
    private bool EnableDebugLogging => ConfigManager.Instance?.Config?.Game?.DebugLogging ?? false;

    // Persistence data
    private readonly Dictionary<string, string> savedCustomSchedules = [];

    private readonly Dictionary<string, string> savedForcedActivities = [];

    protected override void OnPrefabInit()
    {
        base.OnPrefabInit();

        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            DestroyImmediate(gameObject);
        }
    }

    protected override void OnSpawn()
    {
        base.OnSpawn();
        RestoreSavedSchedules();
    }

    #region Public API

    /// <summary>
    /// Get all duplicates that have custom schedule controls
    /// </summary>
    /// <returns>List of schedulables with custom controls</returns>
    public List<Schedulable> GetControlledDuplicates()
    {
        Schedulable[] allSchedulables = FindObjectsOfType<Schedulable>();
        return [.. allSchedulables.Where(s => DuplicateScheduleControlPatches.HasCustomControl(s))];
    }

    /// <summary>
    /// Apply a schedule template to multiple duplicates
    /// </summary>
    /// <param name="schedulables">List of schedulables to apply schedule to</param>
    /// <param name="schedule">Schedule to apply</param>
    public void ApplyScheduleToMultiple(List<Schedulable> schedulables, Schedule schedule)
    {
        if (schedulables == null || schedule == null)
        {
            LogDebug("Cannot apply schedule: schedulables or schedule is null");
            return;
        }

        foreach (Schedulable schedulable in schedulables)
        {
            if (schedulable != null)
            {
                DuplicateScheduleControlPatches.SetCustomSchedule(schedulable, schedule);
                LogDebug($"Applied schedule '{schedule.name}' to {schedulable.GetProperName()}");
            }
        }
    }

    /// <summary>
    /// Clear all custom controls from all duplicates
    /// </summary>
    public void ClearAllCustomControls()
    {
        List<Schedulable> controlledDuplicates = GetControlledDuplicates();

        foreach (Schedulable schedulable in controlledDuplicates)
        {
            DuplicateScheduleControlPatches.ClearCustomSchedule(schedulable);
            DuplicateScheduleControlPatches.ClearForcedActivity(schedulable);
        }

        LogDebug($"Cleared custom controls from {controlledDuplicates.Count} duplicates");
    }

    /// <summary>
    /// Get statistics about current schedule usage
    /// </summary>
    /// <returns>Statistics about schedule usage</returns>
    public ScheduleControlStats GetScheduleStats()
    {
        Schedulable[] allSchedulables = FindObjectsOfType<Schedulable>();
        ScheduleControlStats stats = new();

        foreach (Schedulable schedulable in allSchedulables)
        {
            stats.TotalDuplicates++;

            if (DuplicateScheduleControlPatches.HasCustomControl(schedulable))
            {
                stats.CustomControlledDuplicates++;

                ScheduleBlockType? forcedActivity = DuplicateScheduleControlPatches.GetForcedActivity(schedulable);
                if (forcedActivity != null)
                {
                    stats.ForcedActivityDuplicates++;

                    if (!stats.ActivityCounts.ContainsKey(forcedActivity.Name))
                    {
                        stats.ActivityCounts[forcedActivity.Name] = 0;
                    }

                    stats.ActivityCounts[forcedActivity.Name]++;
                }
                else
                {
                    Schedule? customSchedule = DuplicateScheduleControlPatches.GetEffectiveSchedule(schedulable);
                    if (customSchedule != null)
                    {
                        stats.CustomScheduleDuplicates++;

                        if (!stats.ScheduleCounts.ContainsKey(customSchedule.name))
                        {
                            stats.ScheduleCounts[customSchedule.name] = 0;
                        }

                        stats.ScheduleCounts[customSchedule.name]++;
                    }
                }
            }
        }

        return stats;
    }

    #endregion Public API

    #region Persistence

    public void Serialize(BinaryWriter writer)
    {
        SaveCurrentState();

        writer.Write(savedCustomSchedules.Count);
        foreach (KeyValuePair<string, string> kvp in savedCustomSchedules)
        {
            writer.Write(kvp.Key);
            writer.Write(kvp.Value);
        }

        writer.Write(savedForcedActivities.Count);
        foreach (KeyValuePair<string, string> kvp in savedForcedActivities)
        {
            writer.Write(kvp.Key);
            writer.Write(kvp.Value);
        }
    }

    public void Deserialize(BinaryReader reader)
    {
        savedCustomSchedules.Clear();
        int scheduleCount = reader.ReadInt32();
        for (int i = 0; i < scheduleCount; i++)
        {
            string key = reader.ReadString();
            string value = reader.ReadString();
            savedCustomSchedules[key] = value;
        }

        savedForcedActivities.Clear();
        int activityCount = reader.ReadInt32();
        for (int i = 0; i < activityCount; i++)
        {
            string key = reader.ReadString();
            string value = reader.ReadString();
            savedForcedActivities[key] = value;
        }
    }

    private void SaveCurrentState()
    {
        savedCustomSchedules.Clear();
        savedForcedActivities.Clear();

        Schedulable[] allSchedulables = FindObjectsOfType<Schedulable>();

        foreach (Schedulable schedulable in allSchedulables)
        {
            if (DuplicateScheduleControlPatches.HasCustomControl(schedulable))
            {
                string duplicateId = GetDuplicateId(schedulable);

                ScheduleBlockType? forcedActivity = DuplicateScheduleControlPatches.GetForcedActivity(schedulable);
                if (forcedActivity != null)
                {
                    savedForcedActivities[duplicateId] = forcedActivity.Id;
                }
                else
                {
                    Schedule? customSchedule = DuplicateScheduleControlPatches.GetEffectiveSchedule(schedulable);
                    if (customSchedule != null)
                    {
                        savedCustomSchedules[duplicateId] = customSchedule.name;
                    }
                }
            }
        }
    }

    private void RestoreSavedSchedules()
    {
        Schedulable[] allSchedulables = FindObjectsOfType<Schedulable>();

        foreach (Schedulable schedulable in allSchedulables)
        {
            string duplicateId = GetDuplicateId(schedulable);

            // Restore forced activities
            if (savedForcedActivities.TryGetValue(duplicateId, out string? activityId))
            {
                ScheduleBlockType? activity = Db.Get().ScheduleBlockTypes.TryGet(activityId);
                if (activity != null)
                {
                    DuplicateScheduleControlPatches.ForceActivity(schedulable, activity);
                    LogDebug($"Restored forced activity '{activity.Name}' for {schedulable.GetProperName()}");
                }
            }
            // Restore custom schedules
            else if (savedCustomSchedules.TryGetValue(duplicateId, out string? scheduleName))
            {
                List<Schedule> availableSchedules = CustomScheduleFactory.GetAllPredefinedSchedules();
                Schedule? schedule = availableSchedules.FirstOrDefault(s => s.name == scheduleName);

                if (schedule != null)
                {
                    DuplicateScheduleControlPatches.SetCustomSchedule(schedulable, schedule);
                    LogDebug($"Restored custom schedule '{schedule.name}' for {schedulable.GetProperName()}");
                }
            }
        }
    }

    private string GetDuplicateId(Schedulable schedulable)
    {
        // Use a combination of name and instance ID for unique identification
        MinionIdentity? identity = schedulable.GetComponent<MinionIdentity>();
        return identity != null ? $"{identity.name}_{identity.GetInstanceID()}" : $"{schedulable.name}_{schedulable.GetInstanceID()}";
    }

    #endregion Persistence

    #region Utility

    private void LogDebug(string message)
    {
        if (EnableDebugLogging)
        {
            Debug.Log($"[ScheduleControlManager] {message}");
        }
    }

    #endregion Utility
}

/// <summary>
/// Statistics about schedule control usage
/// </summary>
public class ScheduleControlStats
{
    public int TotalDuplicates { get; set; }
    public int CustomControlledDuplicates { get; set; }
    public int CustomScheduleDuplicates { get; set; }
    public int ForcedActivityDuplicates { get; set; }
    public Dictionary<string, int> ScheduleCounts { get; set; } = [];
    public Dictionary<string, int> ActivityCounts { get; set; } = [];

    public override string ToString()
    {
        return $"Schedule Stats: {CustomControlledDuplicates}/{TotalDuplicates} controlled, " +
               $"{CustomScheduleDuplicates} custom schedules, {ForcedActivityDuplicates} forced activities";
    }
}