using System;
using System.Linq;

namespace NeuroMod;

/// <summary>
/// Manages Neuro's dedicated schedule instance.
/// Creates and maintains a separate schedule for Neuro that can be controlled independently.
/// </summary>
/// <pre>
/// The schedule database and live duplicant list are available when initialization runs.
/// </pre>
/// <post>
/// Neuro has a dedicated schedule instance and can be re-bound to it whenever the schedule pattern changes.
/// </post>
public class NeuroScheduleManager : KMonoBehaviour, ISaveLoadable
{
    public static NeuroScheduleManager? Instance { get; private set; }

    private Schedule? _neuroSchedule;
    private Schedulable? _neuroSchedulable;
    private bool _isInitialized = false;

    // Get schedule name from configured duplicant name
    private string GetScheduleName()
    {
        string duplicantName = ConfigManager.Instance?.Config?.Duplicant?.DefaultName ?? "Duplicant";
        return $"{duplicantName}'s Schedule";
    }

    protected override void OnPrefabInit()
    {
        base.OnPrefabInit();

        // Prevent multiple instances
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[NeuroScheduleManager] Multiple instances detected! Destroying duplicate.");
            Destroy(this);
            return;
        }

        Instance = this;
        Debug.Log("[NeuroScheduleManager] Instance created");
    }

    protected override void OnSpawn()
    {
        base.OnSpawn();
        NeuroLogger.Log("OnSpawn called", "NeuroScheduleManager");
        ManualInitialize();
    }

    /// <summary>
    /// Manually initializes the NeuroScheduleManager.
    /// This is called either from OnSpawn (normal Unity lifecycle)
    /// or manually after AddComponent (when added after Game.OnSpawn).
    /// </summary>
    /// <pre>
    /// The schedule database has been initialized and the manager has not already completed setup.
    /// </pre>
    /// <post>
    /// The manager caches its schedule state, attempts duplicant assignment, and marks itself initialized.
    /// </post>
    public void ManualInitialize()
    {
        // Prevent re-initialization if already done
        if (_isInitialized)
        {
            NeuroLogger.Log("Already initialized, skipping initialization", "NeuroScheduleManager");
            return;
        }

        NeuroLogger.Log("ManualInitialize called - starting initialization", "NeuroScheduleManager");

        // Find the Neuro duplicant first so we can (optionally) clone their current schedule.
        MinionIdentity? neuro = FindNeuroDuplicant();
        Schedule? existingSchedule = null;

        if (neuro == null)
        {
            _isInitialized = true;
            NeuroLogger.Log("No configured duplicant found during startup - skipping dedicated schedule initialization until one is available", "NeuroScheduleManager");
            return;
        }

        bool scheduleControlEnabled = ConfigManager.Instance?.Config?.Game?.ScheduleControlEnabled ?? true;
        if (scheduleControlEnabled && neuro != null)
        {
            string scheduleName = GetScheduleName();
            bool dedicatedAlreadyExists = ScheduleManager.Instance?.GetSchedules()
                ?.Any(s => s.name == scheduleName) ?? false;

            if (dedicatedAlreadyExists)
            {
                NeuroLogger.Log("Dedicated schedule already exists – skipping clone", "NeuroScheduleManager");
            }
            else
            {
                Schedulable? schedulable = neuro.GetComponent<Schedulable>();
                existingSchedule = schedulable?.GetSchedule();
                if (existingSchedule != null)
                    NeuroLogger.Log($"Found existing schedule '{existingSchedule.name}' on duplicant – will clone it", "NeuroScheduleManager");
            }
        }
        else if (!scheduleControlEnabled)
        {
            NeuroLogger.Log("ScheduleControlEnabled is disabled – balanced template will be used", "NeuroScheduleManager");
        }

        // Create (or locate) Neuro's dedicated schedule.
        // Pass the duplicant's current schedule so it can be used as the clone source.
        NeuroLogger.Log("Calling EnsureNeuroScheduleExists...", "NeuroScheduleManager");
        EnsureNeuroScheduleExists(existingSchedule);

        if (_neuroSchedule == null)
        {
            NeuroLogger.LogError("EnsureNeuroScheduleExists failed - _neuroSchedule is still NULL!", "NeuroScheduleManager");
        }
        else
        {
            NeuroLogger.Log($"Schedule ready: {_neuroSchedule.name}", "NeuroScheduleManager");
        }

        // Assign Neuro to her dedicated schedule.
        NeuroLogger.Log("Calling FindAndAssignNeuro...", "NeuroScheduleManager");
        FindAndAssignNeuro();

        if (_neuroSchedulable == null)
        {
            NeuroLogger.LogWarning("FindAndAssignNeuro failed - _neuroSchedulable is still NULL (Neuro might not be spawned yet)", "NeuroScheduleManager");
        }
        else
        {
            NeuroLogger.Log($"Neuro found and assigned: {_neuroSchedulable.GetProperName()}", "NeuroScheduleManager");
        }

        _isInitialized = true;
        NeuroLogger.Log($"Initialization complete - initialized: {_isInitialized}, schedule: {_neuroSchedule != null}, schedulable: {_neuroSchedulable != null}", "NeuroScheduleManager");
    }

    /// <summary>
    /// Returns the <see cref="MinionIdentity"/> for the configured Neuro duplicant, or <c>null</c> if not yet spawned.
    /// </summary>
    /// <returns>The matching <see cref="MinionIdentity"/>, or <c>null</c>.</returns>
    /// <pre>The configured duplicant name has been set in <see cref="ConfigManager"/>.</pre>
    /// <post>Returns the first duplicant whose name matches the configuration, or <c>null</c>.</post>
    private static MinionIdentity? FindNeuroDuplicant()
    {
        string? configuredName = ConfigManager.Instance?.Config?.Duplicant?.DefaultName;
        if (string.IsNullOrWhiteSpace(configuredName)) return null;

        System.Collections.Generic.List<MinionIdentity> minions = Components.LiveMinionIdentities.Items;
        if (minions == null) return null;

        foreach (MinionIdentity? minion in minions)
        {
            if (minion == null) continue;
            string name = minion.GetProperName();
            if (string.Equals(name, configuredName, StringComparison.OrdinalIgnoreCase)
             || (configuredName!.Length >= 4 && name.IndexOf(configuredName, StringComparison.OrdinalIgnoreCase) >= 0))
                return minion;
        }
        return null;
    }

    protected override void OnCleanUp()
    {
        base.OnCleanUp();

        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// Ensures Neuro's dedicated schedule exists in the game
    /// </summary>
    /// <pre>
    /// Schedule groups and the schedule manager are available.
    /// </pre>
    /// <post>
    /// <see cref="_neuroSchedule"/> refers to an existing or newly registered dedicated schedule when successful.
    /// </post>
    /// <summary>
    /// Ensures Neuro's dedicated schedule exists in the game.
    /// If a <paramref name="sourceSchedule"/> is supplied the new schedule is cloned from it,
    /// preserving whatever block layout the duplicant already had.
    /// Falls back to the balanced template when no source is available.
    /// </summary>
    /// <param name="sourceSchedule">
    /// The schedule currently assigned to the Neuro duplicant, or <c>null</c> to use the balanced template.
    /// </param>
    /// <pre>
    /// Schedule groups and the schedule manager are available.
    /// </pre>
    /// <post>
    /// <see cref="_neuroSchedule"/> refers to an existing or newly registered dedicated schedule when successful.
    /// </post>
    private void EnsureNeuroScheduleExists(Schedule? sourceSchedule = null)
    {
        try
        {
            string scheduleName = GetScheduleName();

            // Check if schedule already exists (by looking for it in game schedules)
            if (ScheduleManager.Instance != null)
            {
                System.Collections.Generic.List<Schedule> existingSchedules = ScheduleManager.Instance.GetSchedules();
                _neuroSchedule = existingSchedules?.FirstOrDefault(s => s.name == scheduleName);
            }

            if (_neuroSchedule != null)
            {
                NeuroLogger.Log($"Found existing schedule: {scheduleName}", "NeuroScheduleManager");
                return;
            }

            NeuroLogger.Log($"Creating new schedule: {scheduleName}", "NeuroScheduleManager");

            if (ScheduleManager.Instance == null)
            {
                NeuroLogger.LogError("Failed to create duplicant's schedule: ScheduleManager.Instance is null!", "NeuroScheduleManager");
                return;
            }

            // Determine the clone source: prefer the duplicant's current schedule,
            // then any registered schedule, and finally a detached balanced template.
            System.Collections.Generic.List<Schedule> registered = ScheduleManager.Instance.GetSchedules();
            Schedule? cloneSource = sourceSchedule
                ?? (registered?.Count > 0 ? registered[0] : null);

            if (cloneSource == null)
            {
                cloneSource = CustomScheduleFactory.CreateBalancedSchedule(scheduleName);
                NeuroLogger.Log("No registered schedules available - using detached balanced template as duplication source", "NeuroScheduleManager");
            }

            _neuroSchedule = ScheduleManager.Instance.DuplicateSchedule(cloneSource);

            // When no duplicant schedule was available, overwrite with the balanced template.
            if (sourceSchedule == null)
            {
                NeuroLogger.Log("No duplicant schedule found – applying balanced template", "NeuroScheduleManager");
                Schedule balancedTemplate = CustomScheduleFactory.CreateBalancedSchedule(scheduleName);
                System.Collections.Generic.List<ScheduleBlock>? templateBlocks = balancedTemplate.GetBlocks();
                if (templateBlocks != null && templateBlocks.Count == 24)
                {
                    for (int i = 0; i < 24; i++)
                    {
                        ScheduleGroup? group = Db.Get().ScheduleGroups.FindGroupForScheduleTypes(templateBlocks[i].allowed_types);
                        if (group != null)
                            _neuroSchedule.SetBlockGroup(i, group);
                    }
                }
            }
            else
            {
                NeuroLogger.Log($"Cloned schedule from '{cloneSource.name}' – preserving existing blocks", "NeuroScheduleManager");
            }

            _neuroSchedule.name = scheduleName;
            NeuroLogger.Log($"Successfully created and registered schedule: {scheduleName}", "NeuroScheduleManager");
        }
        catch (Exception ex)
        {
            NeuroLogger.LogException(ex, "creating schedule", "NeuroScheduleManager");
        }
    }

    /// <summary>
    /// Finds Neuro duplicant and assigns her to her dedicated schedule
    /// </summary>
    /// <pre>
    /// <see cref="_neuroSchedule"/> exists and the configured duplicant name can be resolved.
    /// </pre>
    /// <post>
    /// <see cref="_neuroSchedulable"/> is set when the configured duplicant is found and assigned to the dedicated schedule.
    /// </post>
    private void FindAndAssignNeuro()
    {
        try
        {
            if (_neuroSchedule == null)
            {
                NeuroLogger.LogWarning("Cannot assign Neuro: schedule doesn't exist!", "NeuroScheduleManager");
                return;
            }

            // Get configured duplicant name from settings - NO hardcoded defaults
            if (ConfigManager.Instance?.Config?.Duplicant?.DefaultName == null)
            {
                NeuroLogger.LogError("ConfigManager or DefaultName is null - cannot find duplicant without configured name!", "NeuroScheduleManager");
                return;
            }

            string configuredName = ConfigManager.Instance.Config.Duplicant.DefaultName;
            NeuroLogger.Log($"Using configured duplicant name: '{configuredName}'", "NeuroScheduleManager");

            // Find all minions
            System.Collections.Generic.List<MinionIdentity> minions = Components.LiveMinionIdentities.Items;

            if (minions == null || minions.Count == 0)
            {
                Debug.Log("[NeuroScheduleManager] No minions found yet");
                return;
            }

            foreach (MinionIdentity? minion in minions)
            {
                if (minion == null)
                {
                    continue;
                }

                string minionName = minion.GetProperName();

                // Match strategy:
                // 1. Exact match with configured name (case-insensitive)
                // 2. If configured name length >= 4, match any duplicant containing configured name
                // This handles cases where config says "NeuroBot" but actual duplicant is "Neuro"
                bool isNeuro = string.Equals(minionName, configuredName, StringComparison.OrdinalIgnoreCase);

                if (!isNeuro && configuredName.Length >= 4)
                {
                    isNeuro = minionName.ToLower().Contains(configuredName.ToLower());
                }

                if (isNeuro)
                {
                    _neuroSchedulable = minion.GetComponent<Schedulable>();

                    if (_neuroSchedulable != null)
                    {
                        AssignNeuroToSchedule();
                        NeuroLogger.Log($"Found and assigned '{minionName}' (configured as '{configuredName}') to her dedicated schedule", "NeuroScheduleManager");
                        return;
                    }
                    else
                    {
                        Debug.LogWarning($"[NeuroScheduleManager] Found '{minionName}' but Schedulable component is null!");
                    }
                }
            }

            NeuroLogger.Log($"Neuro (configured as '{configuredName}') not found yet, will retry later", "NeuroScheduleManager");
        }
        catch (Exception ex)
        {
            NeuroLogger.LogException(ex, "finding Neuro", "NeuroScheduleManager");
        }
    }

    /// <summary>
    /// Assigns Neuro's Schedulable to her dedicated schedule
    /// </summary>
    /// <pre>
    /// <see cref="_neuroSchedulable"/> and <see cref="_neuroSchedule"/> both reference live game objects.
    /// </pre>
    /// <post>
    /// The duplicant is unassigned from any previous schedule and assigned to the dedicated Neuro schedule.
    /// </post>
    private void AssignNeuroToSchedule()
    {
        if (_neuroSchedulable == null || _neuroSchedule == null)
        {
            NeuroLogger.LogError("Cannot assign: schedulable or schedule is null!", "NeuroScheduleManager");
            return;
        }

        try
        {
            // Remove Neuro from any existing schedule
            RemoveFromCurrentSchedule(_neuroSchedulable);

            // Assign Neuro to her dedicated schedule
            _neuroSchedule.Assign(_neuroSchedulable);

            NeuroLogger.Log($"Successfully assigned {_neuroSchedulable.GetProperName()} to {_neuroSchedule.name}", "NeuroScheduleManager");
        }
        catch (Exception ex)
        {
            NeuroLogger.LogException(ex, "assigning to schedule", "NeuroScheduleManager");
        }
    }

    /// <summary>
    /// Removes a schedulable from their current schedule
    /// </summary>
    /// <param name="schedulable">The Schedulable to remove</param>
    /// <pre>
    /// <paramref name="schedulable"/> may currently be assigned to one or more schedule instances.
    /// </pre>
    /// <post>
    /// The schedulable is no longer assigned to any schedule found in the schedule manager list.
    /// </post>
    private void RemoveFromCurrentSchedule(Schedulable schedulable)
    {
        if (schedulable == null)
        {
            return;
        }

        try
        {
            System.Collections.Generic.List<Schedule>? allSchedules = ScheduleManager.Instance?.GetSchedules();
            if (allSchedules == null)
            {
                return;
            }

            foreach (Schedule? schedule in allSchedules)
            {
                if (schedule.IsAssigned(schedulable))
                {
                    schedule.Unassign(schedulable);
                    NeuroLogger.Log($"Removed {schedulable.GetProperName()} from schedule: {schedule.name}", "NeuroScheduleManager");
                }
            }
        }
        catch (Exception ex)
        {
            NeuroLogger.LogException(ex, "removing from current schedule", "NeuroScheduleManager");
        }
    }

    /// <summary>
    /// Gets Neuro's dedicated schedule instance
    /// </summary>
    /// <returns>Neuro's Schedule, or null if not created yet</returns>
    /// <pre>
    /// Initialization may still be pending or may not yet have produced a dedicated schedule.
    /// </pre>
    /// <post>
    /// The current dedicated schedule reference is returned.
    /// </post>
    public Schedule? GetNeuroSchedule()
    {
        return _neuroSchedule;
    }

    /// <summary>
    /// Gets Neuro's Schedulable component
    /// </summary>
    /// <returns>Neuro's Schedulable, or null if not found yet</returns>
    /// <pre>
    /// The configured duplicant may or may not have been found in the live colony list.
    /// </pre>
    /// <post>
    /// The currently tracked schedulable reference is returned.
    /// </post>
    public Schedulable? GetNeuroSchedulable()
    {
        return _neuroSchedulable;
    }

    /// <summary>
    /// Updates Neuro's schedule with a new schedule pattern
    /// </summary>
    /// <param name="newSchedule">The new schedule to apply</param>
    /// <pre>
    /// <paramref name="newSchedule"/> contains a valid 24-block template and the manager can resolve the dedicated schedule and duplicant.
    /// </pre>
    /// <post>
    /// Neuro's dedicated schedule blocks mirror the supplied template while preserving registration and assignment.
    /// </post>
    public void UpdateNeuroSchedule(Schedule newSchedule)
    {
        NeuroLogger.Log("========== UpdateNeuroSchedule START ==========", "NeuroScheduleManager");

        if (newSchedule == null)
        {
            NeuroLogger.LogError("newSchedule parameter is NULL!", "NeuroScheduleManager");
            return;
        }

        NeuroLogger.Log($"New schedule name: {newSchedule.name}", "NeuroScheduleManager");

        try
        {
            // Step 1: Verify schedule exists and is registered in ScheduleManager
            if (_neuroSchedule == null)
            {
                NeuroLogger.LogError("_neuroSchedule is NULL - attempting to create schedule", "NeuroScheduleManager");
                EnsureNeuroScheduleExists();

                if (_neuroSchedule == null)
                {
                    NeuroLogger.LogError("Failed to create _neuroSchedule - aborting", "NeuroScheduleManager");
                    return;
                }
                NeuroLogger.Log("Schedule created successfully", "NeuroScheduleManager");
            }

            // Verify the schedule is in ScheduleManager's list
            if (ScheduleManager.Instance != null)
            {
                System.Collections.Generic.List<Schedule> registeredSchedules = ScheduleManager.Instance.GetSchedules();
                bool isRegistered = registeredSchedules?.Contains(_neuroSchedule) ?? false;

                if (!isRegistered)
                {
                    NeuroLogger.LogError("Neuro's schedule is NOT registered in ScheduleManager! This will cause UI issues.", "NeuroScheduleManager");
                    NeuroLogger.Log("Attempting to re-create schedule through ScheduleManager...", "NeuroScheduleManager");

                    // Force re-creation through proper channels
                    _neuroSchedule = null;
                    EnsureNeuroScheduleExists();

                    if (_neuroSchedule == null)
                    {
                        NeuroLogger.LogError("Failed to re-register schedule - aborting", "NeuroScheduleManager");
                        return;
                    }
                }
                else
                {
                    NeuroLogger.Log($"Schedule '{_neuroSchedule.name}' is properly registered in ScheduleManager", "NeuroScheduleManager");
                }
            }

            // Step 2: Find Neuro if not found yet
            if (_neuroSchedulable == null)
            {
                NeuroLogger.LogError("_neuroSchedulable is NULL - attempting to find duplicant", "NeuroScheduleManager");

                // Get configured duplicant name from settings - NO hardcoded defaults
                if (ConfigManager.Instance?.Config?.Duplicant?.DefaultName == null)
                {
                    NeuroLogger.LogError("ConfigManager or DefaultName is null - cannot search for duplicant!", "NeuroScheduleManager");
                    return;
                }

                string configuredName = ConfigManager.Instance.Config.Duplicant.DefaultName;
                NeuroLogger.Log($"Searching for duplicant configured as: '{configuredName}'", "NeuroScheduleManager");

                System.Collections.Generic.List<MinionIdentity>? minions = Components.LiveMinionIdentities.Items;
                NeuroLogger.Log($"Total minions found: {minions?.Count ?? 0}", "NeuroScheduleManager");

                if (minions != null)
                {
                    foreach (MinionIdentity? minion in minions)
                    {
                        if (minion == null)
                        {
                            continue;
                        }

                        string minionName = minion.GetProperName();
                        NeuroLogger.Log($"  - Checking minion: '{minionName}'", "NeuroScheduleManager");

                        // Match strategy:
                        // 1. Exact match with configured name (case-insensitive)
                        // 2. If configured name length >= 4, match any duplicant containing configured name
                        // This handles cases where config says "NeuroBot" but actual duplicant is "Neuro"
                        bool isNeuro = string.Equals(minionName, configuredName, StringComparison.OrdinalIgnoreCase);

                        if (!isNeuro && configuredName.Length >= 4)
                        {
                            isNeuro = minionName.ToLower().Contains(configuredName.ToLower());
                        }

                        if (isNeuro)
                        {
                            NeuroLogger.Log($"FOUND Neuro: '{minionName}' (configured as '{configuredName}')", "NeuroScheduleManager");
                            _neuroSchedulable = minion.GetComponent<Schedulable>();

                            if (_neuroSchedulable != null)
                            {
                                NeuroLogger.Log("Schedulable component retrieved successfully", "NeuroScheduleManager");
                                break;
                            }
                            else
                            {
                                NeuroLogger.LogError($"Found {minionName} but Schedulable component is NULL!", "NeuroScheduleManager");
                            }
                        }
                    }
                }

                if (_neuroSchedulable == null)
                {
                    NeuroLogger.LogError("Failed to find Neuro - aborting schedule update", "NeuroScheduleManager");
                    return;
                }
            }

            // Step 3: Log current state
            NeuroLogger.Log($"Current state:", "NeuroScheduleManager");
            NeuroLogger.Log($"  - Current schedule: {_neuroSchedule.name}", "NeuroScheduleManager");
            NeuroLogger.Log($"  - Schedulable: {_neuroSchedulable.GetProperName()}", "NeuroScheduleManager");
            NeuroLogger.Log($"  - Is assigned to current schedule: {_neuroSchedule.IsAssigned(_neuroSchedulable)}", "NeuroScheduleManager");

            // Step 4: Copy blocks from source schedule to existing schedule
            NeuroLogger.Log("Step 1: Copying schedule blocks...", "NeuroScheduleManager");
            System.Collections.Generic.List<ScheduleBlock>? sourceBlocks = newSchedule.GetBlocks();
            if (sourceBlocks == null || sourceBlocks.Count != 24)
            {
                NeuroLogger.LogError($"Source schedule has invalid blocks (count: {sourceBlocks?.Count ?? 0})", "NeuroScheduleManager");
                return;
            }

            NeuroLogger.Log($"  - Source schedule: {newSchedule.name} ({sourceBlocks.Count} blocks)", "NeuroScheduleManager");
            NeuroLogger.Log($"  - Target schedule: {_neuroSchedule.name}", "NeuroScheduleManager");

            // Get the internal blocks list and update it directly to avoid 24 Changed() calls
            System.Collections.Generic.List<ScheduleBlock> targetBlocks = _neuroSchedule.GetBlocks();
            if (targetBlocks == null || targetBlocks.Count != 24)
            {
                NeuroLogger.LogError("Target schedule blocks are invalid!", "NeuroScheduleManager");
                return;
            }

            // Copy blocks directly - this modifies the schedule's internal list
            for (int i = 0; i < 24; i++)
            {
                ScheduleBlock sourceBlock = sourceBlocks[i];
                ScheduleGroup group = Db.Get().ScheduleGroups.FindGroupForScheduleTypes(sourceBlock.allowed_types);
                if (group != null)
                {
                    // Use SetBlockGroup which properly triggers Changed() and updates UI
                    _neuroSchedule.SetBlockGroup(i, group);
                }
            }

            NeuroLogger.Log($"  - Successfully copied all 24 blocks", "NeuroScheduleManager");

            // Verify the blocks were actually updated
            System.Collections.Generic.List<ScheduleBlock> updatedBlocks = _neuroSchedule.GetBlocks();
            NeuroLogger.Log($"  - Verification: First block is now '{updatedBlocks[0].name}' (was expecting '{sourceBlocks[0].name}')", "NeuroScheduleManager");
            NeuroLogger.Log($"  - Verification: Last block is now '{updatedBlocks[23].name}' (was expecting '{sourceBlocks[23].name}')", "NeuroScheduleManager");

            // Force UI refresh if ScheduleScreen is open
            if (ScheduleScreen.Instance != null)
            {
                NeuroLogger.Log("  - ScheduleScreen IS OPEN, forcing UI refresh", "NeuroScheduleManager");

                // The schedule's Changed() was already called 24 times by SetBlockGroup
                // Ensure the duplicant also gets notified
                if (_neuroSchedulable != null)
                {
                    _neuroSchedulable.OnScheduleChanged(_neuroSchedule);
                    NeuroLogger.Log("  - Called OnScheduleChanged on Neuro's schedulable", "NeuroScheduleManager");
                }
            }
            else
            {
                NeuroLogger.Log("  - ScheduleScreen is NOT OPEN (no immediate UI to update)", "NeuroScheduleManager");
            }

            NeuroLogger.Log($"  - Schedule name: {_neuroSchedule.name}", "NeuroScheduleManager");
            NeuroLogger.Log($"  - Is Neuro still assigned: {_neuroSchedule.IsAssigned(_neuroSchedulable)}", "NeuroScheduleManager");

            NeuroLogger.Log("========== UpdateNeuroSchedule SUCCESS ==========", "NeuroScheduleManager");
        }
        catch (Exception ex)
        {
            NeuroLogger.LogError($"========== UpdateNeuroSchedule FAILED ==========", "NeuroScheduleManager");
            NeuroLogger.LogError($"Exception: {ex.Message}", "NeuroScheduleManager");
            NeuroLogger.LogError($"Stack trace: {ex.StackTrace}", "NeuroScheduleManager");
        }
    }

    /// <summary>
    /// Public method to manually trigger finding and assigning Neuro
    /// Call this after Neuro spawns in the game
    /// </summary>
    /// <pre>
    /// The dedicated schedule already exists or can be resolved.
    /// </pre>
    /// <post>
    /// The manager re-runs duplicant discovery and schedule assignment.
    /// </post>
    public void RefreshNeuroAssignment()
    {
        if (_neuroSchedule == null)
        {
            MinionIdentity? neuro = FindNeuroDuplicant();
            Schedulable? schedulable = neuro?.GetComponent<Schedulable>();
            Schedule? currentSchedule = schedulable?.GetSchedule();

            if (neuro != null)
            {
                EnsureNeuroScheduleExists(currentSchedule);
            }
        }

        FindAndAssignNeuro();
    }
}