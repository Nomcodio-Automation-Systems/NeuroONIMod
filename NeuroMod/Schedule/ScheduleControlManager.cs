using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NeuroMod;

/// <summary>
/// Manager class for handling schedule control operations and persistence
/// </summary>
/// <pre>Schedule overrides belong to the current save and must be matched back to live duplicates after load.</pre>
/// <post>Custom schedules and forced activities are persisted and restored using stable duplicate identity keys rather than transient instance IDs.</post>
public class ScheduleControlManager : KMonoBehaviour, ISaveLoadable
{
    private const string ConfiguredTargetPersistenceKeyPrefix = "configured:";

    public static ScheduleControlManager? Instance { get; private set; }

    // Get debug logging setting from ConfigManager
    private bool EnableDebugLogging => ConfigManager.Instance?.Config?.Game?.DebugLogging ?? false;

    // Persistence data
    private readonly Dictionary<string, string> savedCustomSchedules = [];

    private readonly Dictionary<string, string> savedForcedActivities = [];

    /// <summary>
    /// Initializes the singleton schedule-control manager for the current game session.
    /// </summary>
    /// <pre>The component is attached to a live Unity object during prefab initialization.</pre>
    /// <post>Exactly one persistent manager instance survives and duplicate components destroy themselves.</post>
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

    /// <summary>
    /// Restores any persisted schedule overrides after the manager spawns.
    /// </summary>
    /// <pre>Persisted schedule data may already have been deserialized for the current save.</pre>
    /// <post>Any matching live duplicates receive their stored schedule or forced-activity overrides.</post>
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
    /// <pre>Live schedulables are discoverable in the current world.</pre>
    /// <post>Only duplicates with active custom schedule state are returned.</post>
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
    /// <pre>The target duplicates are live schedulables and <paramref name="schedule"/> is a valid template.</pre>
    /// <post>Each non-null target duplicate receives the supplied custom schedule override.</post>
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
                ScheduleOverrideApi.SetCustomSchedule(schedulable, schedule);
                LogDebug($"Applied schedule '{schedule.name}' to {schedulable.GetProperName()}");
            }
        }
    }

    /// <summary>
    /// Clear all custom controls from all duplicates
    /// </summary>
    /// <pre>Some live duplicates may currently have custom schedules or forced activities.</pre>
    /// <post>All live custom schedule state tracked by the API is cleared.</post>
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
    /// <pre>Live schedulables can be enumerated from the current world.</pre>
    /// <post>The returned statistics summarize current custom schedule and forced-activity usage.</post>
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

    /// <summary>
    /// Serializes the current schedule-control state for the active save.
    /// </summary>
    /// <param name="writer">Binary writer receiving persisted schedule-control data.</param>
    /// <pre><paramref name="writer"/> is writable and the current world can be queried for active overrides.</pre>
    /// <post>The current override state is written using stable duplicate identity keys.</post>
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

    /// <summary>
    /// Deserializes persisted schedule-control state for the active save.
    /// </summary>
    /// <param name="reader">Binary reader containing previously stored schedule-control data.</param>
    /// <pre><paramref name="reader"/> contains data written by <see cref="Serialize"/> for this save format.</pre>
    /// <post>Stored schedule and forced-activity maps are populated in memory for later restoration.</post>
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

    /// <summary>
    /// Captures the current live override state into the persisted dictionaries.
    /// </summary>
    /// <pre>Live schedulables with active override state are discoverable in the current world.</pre>
    /// <post>The persisted dictionaries mirror the current override state using stable duplicate identity keys.</post>
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

    /// <summary>
    /// Reapplies persisted schedule overrides to matching live duplicates.
    /// </summary>
    /// <pre>Persisted state has already been deserialized and live schedulables are available in the current world.</pre>
    /// <post>Any duplicate whose stable identity matches persisted state regains its custom schedule or forced activity.</post>
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
                    ScheduleOverrideApi.ForceActivity(schedulable, activity);
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
                    ScheduleOverrideApi.SetCustomSchedule(schedulable, schedule);
                    LogDebug($"Restored custom schedule '{schedule.name}' for {schedulable.GetProperName()}");
                }
            }
        }
    }

    /// <summary>
    /// Builds the stable persistence key used to match a live duplicate after save/load.
    /// </summary>
    /// <param name="schedulable">Live duplicate whose schedule-control state may need persistence.</param>
    /// <returns>A stable key based on the configured target name when applicable, otherwise the duplicate's proper name.</returns>
    /// <pre><paramref name="schedulable"/> refers to a live duplicate and may or may not be the configured Neuro target.</pre>
    /// <post>The returned key avoids transient Unity instance IDs and remains stable across save reloads as long as duplicate naming is stable.</post>
    private string GetDuplicateId(Schedulable schedulable)
    {
        MinionIdentity? identity = schedulable.GetComponent<MinionIdentity>();
        if (identity is null)
        {
            return schedulable.name;
        }

        string properName = identity.GetProperName();
        if (GetConfiguredDuplicantName() is string configuredName && !string.IsNullOrWhiteSpace(configuredName))
        {
            if (IsConfiguredTarget(properName, configuredName))
            {
                return ConfiguredTargetPersistenceKeyPrefix + configuredName;
            }
        }

        return properName;
    }

    /// <summary>
    /// Resolves the configured duplicant name from mod settings.
    /// </summary>
    /// <returns>The configured duplicant name, or <see langword="null"/> when settings are unavailable.</returns>
    /// <pre>Configuration may or may not have been loaded successfully for the current session.</pre>
    /// <post>The configured Neuro target name is returned without mutating settings.</post>
    private static string? GetConfiguredDuplicantName()
    {
        return ConfigManager.Instance?.Config?.Duplicant?.DefaultName;
    }

    /// <summary>
    /// Determines whether a live duplicate name should be treated as the configured Neuro target.
    /// </summary>
    /// <param name="properName">Resolved in-game duplicate name.</param>
    /// <param name="configuredName">Configured target name from settings.</param>
    /// <returns><see langword="true"/> when the live duplicate matches the configured target-selection rules.</returns>
    /// <pre>Both names are non-empty duplicate identifiers.</pre>
    /// <post>The result follows the same configured-target matching rules used by the Neuro integration managers.</post>
    private static bool IsConfiguredTarget(string properName, string configuredName)
    {
        return string.Equals(properName, configuredName, StringComparison.OrdinalIgnoreCase) ||
            (configuredName.Length >= 4 && properName.IndexOf(configuredName, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    #endregion Persistence

    #region Utility

    /// <summary>
    /// Emits a debug log message when schedule-control debugging is enabled.
    /// </summary>
    /// <param name="message">Debug message to emit.</param>
    /// <pre><paramref name="message"/> describes schedule-control activity worth surfacing during diagnostics.</pre>
    /// <post>The message is written only when debug logging is enabled in configuration.</post>
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
/// <pre>Schedule-control usage has been sampled from the current live world state.</pre>
/// <post>The object summarizes counts for custom schedules, forced activities, and per-name breakdowns.</post>
public class ScheduleControlStats
{
    public int TotalDuplicates { get; set; }
    public int CustomControlledDuplicates { get; set; }
    public int CustomScheduleDuplicates { get; set; }
    public int ForcedActivityDuplicates { get; set; }
    public Dictionary<string, int> ScheduleCounts { get; set; } = [];
    public Dictionary<string, int> ActivityCounts { get; set; } = [];

    /// <summary>
    /// Formats the collected schedule-control statistics for diagnostics.
    /// </summary>
    /// <returns>A concise textual summary of the current statistics.</returns>
    /// <pre>The statistic properties have been populated from current world state.</pre>
    /// <post>A human-readable summary string is returned without mutating the statistics.</post>
    public override string ToString()
    {
        return $"Schedule Stats: {CustomControlledDuplicates}/{TotalDuplicates} controlled, " +
               $"{CustomScheduleDuplicates} custom schedules, {ForcedActivityDuplicates} forced activities";
    }
}