using System;
using System.Linq;

namespace NeuroMod;

/// <summary>
/// Manages Neuro's dedicated schedule instance.
/// Creates and maintains a separate schedule for Neuro that can be controlled independently.
///
/// Pre: Db.Initialize() must have completed before creating schedules
/// Post: Neuro has her own Schedule instance that she is the sole member of
/// </summary>
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
    ///
    /// Pre: Db.Get() must be initialized
    /// Post: _isInitialized is true, schedule is created
    /// </summary>
    public void ManualInitialize()
    {
        // Prevent re-initialization if already done
        if (_isInitialized)
        {
            NeuroLogger.Log("Already initialized, skipping initialization", "NeuroScheduleManager");
            return;
        }

        NeuroLogger.Log("ManualInitialize called - starting initialization", "NeuroScheduleManager");

        // Try to find existing Neuro schedule or create a new one
        NeuroLogger.Log("Calling EnsureNeuroScheduleExists...", "NeuroScheduleManager");
        EnsureNeuroScheduleExists();

        if (_neuroSchedule == null)
        {
            NeuroLogger.LogError("EnsureNeuroScheduleExists failed - _neuroSchedule is still NULL!", "NeuroScheduleManager");
        }
        else
        {
            NeuroLogger.Log($"Schedule created successfully: {_neuroSchedule.name}", "NeuroScheduleManager");
        }

        // Try to find and assign Neuro to her schedule
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
    ///
    /// Pre: Db.Get().ScheduleGroups must be initialized
    /// Post: _neuroSchedule is set to a valid Schedule instance
    /// </summary>
    private void EnsureNeuroScheduleExists()
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

            if (_neuroSchedule == null)
            {
                NeuroLogger.Log($"Creating new schedule: {scheduleName}", "NeuroScheduleManager");

                // Create a balanced default schedule template
                Schedule tempSchedule = CustomScheduleFactory.CreateBalancedSchedule(scheduleName);

                if (tempSchedule != null && ScheduleManager.Instance != null)
                {
                    // Use ScheduleManager.DuplicateSchedule to properly register it
                    // This creates a copy with the same blocks and adds it to the schedules list
                    _neuroSchedule = ScheduleManager.Instance.DuplicateSchedule(tempSchedule);

                    // Update the name to match our configured name
                    _neuroSchedule.name = scheduleName;

                    NeuroLogger.Log($"Successfully created and registered schedule: {scheduleName}", "NeuroScheduleManager");
                }
                else
                {
                    NeuroLogger.LogError("Failed to create duplicant's schedule!", "NeuroScheduleManager");
                }
            }
            else
            {
                NeuroLogger.Log($"Found existing schedule: {scheduleName}", "NeuroScheduleManager");
            }
        }
        catch (Exception ex)
        {
            NeuroLogger.LogException(ex, "creating schedule", "NeuroScheduleManager");
        }
    }

    /// <summary>
    /// Finds Neuro duplicant and assigns her to her dedicated schedule
    ///
    /// Pre: _neuroSchedule must exist
    /// Post: Neuro is assigned to her own schedule instance
    /// </summary>
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
    ///
    /// Pre: _neuroSchedulable and _neuroSchedule must not be null
    /// Post: Neuro is assigned to her schedule and is the only member
    /// </summary>
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
    ///
    /// Pre: schedulable must not be null
    /// Post: schedulable is no longer assigned to any schedule
    /// </summary>
    /// <param name="schedulable">The Schedulable to remove</param>
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
    public Schedule? GetNeuroSchedule()
    {
        return _neuroSchedule;
    }

    /// <summary>
    /// Gets Neuro's Schedulable component
    /// </summary>
    /// <returns>Neuro's Schedulable, or null if not found yet</returns>
    public Schedulable? GetNeuroSchedulable()
    {
        return _neuroSchedulable;
    }

    /// <summary>
    /// Updates Neuro's schedule with a new schedule pattern
    ///
    /// Pre: newSchedule must be a valid Schedule with 24 hours of blocks
    /// Post: Neuro's schedule is updated and she remains assigned
    /// </summary>
    /// <param name="newSchedule">The new schedule to apply</param>
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
    public void RefreshNeuroAssignment()
    {
        FindAndAssignNeuro();
    }
}