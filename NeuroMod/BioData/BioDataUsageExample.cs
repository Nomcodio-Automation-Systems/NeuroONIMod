using System.Collections;
using UnityEngine;

namespace NeuroMod;

/// <summary>
/// Example usage of the bio data system
/// </summary>
public class BioDataUsageExample : KMonoBehaviour
{
    [SerializeField]
    private readonly bool enableExampleLogging = true;

    [SerializeField]
    private readonly float exampleUpdateInterval = 10f;

    protected override void OnSpawn()
    {
        base.OnSpawn();

        // Subscribe to bio data events
        DuplicateBioDataPatches.OnBioDataUpdated += OnDuplicateBioDataUpdated;

        // Subscribe to monitor events
        if (DuplicateBioDataMonitor.Instance != null)
        {
            DuplicateBioDataMonitor.Instance.OnCriticalHealthChange += OnCriticalHealth;
            DuplicateBioDataMonitor.Instance.OnStarvationWarning += OnStarvation;
            DuplicateBioDataMonitor.Instance.OnStressWarning += OnStress;
            DuplicateBioDataMonitor.Instance.OnSicknessDetected += OnSickness;
            DuplicateBioDataMonitor.Instance.OnTemperatureWarning += OnTemperature;
        }

        // Start example usage coroutine
        if (enableExampleLogging)
        {
            StartCoroutine(ExampleUsageCoroutine());
        }
    }

    protected override void OnCleanUp()
    {
        // Unsubscribe from events
        if (DuplicateBioDataPatches.OnBioDataUpdated != null)
        {
            DuplicateBioDataPatches.OnBioDataUpdated -= OnDuplicateBioDataUpdated;
        }

        DuplicateBioDataMonitor? monitor = DuplicateBioDataMonitor.Instance;
        if (monitor != null)
        {
            monitor.OnCriticalHealthChange -= OnCriticalHealth;
            monitor.OnStarvationWarning -= OnStarvation;
            monitor.OnStressWarning -= OnStress;
            monitor.OnSicknessDetected -= OnSickness;
            monitor.OnTemperatureWarning -= OnTemperature;
        }

        base.OnCleanUp();
    }

    private IEnumerator ExampleUsageCoroutine()
    {
        yield return new WaitForSeconds(5f); // Wait for game to initialize

        while (true)
        {
            LogAllDuplicateBioData();
            LogHealthStatusSummary();

            yield return new WaitForSeconds(exampleUpdateInterval);
        }
    }

    /// <summary>
    /// Example: Log bio data for all duplicates
    /// </summary>
    private void LogAllDuplicateBioData()
    {
        System.Collections.Generic.Dictionary<MinionIdentity, DuplicateBioData> allBioData = DuplicateBioDataPatches.GetAllBioData();

        if (allBioData.Count == 0)
        {
            LogExample("No duplicates found");
            return;
        }

        LogExample("=== Duplicate Bio Data Summary ===");

        foreach (System.Collections.Generic.KeyValuePair<MinionIdentity, DuplicateBioData> kvp in allBioData)
        {
            MinionIdentity minionIdentity = kvp.Key;
            DuplicateBioData bioData = kvp.Value;

            LogExample($"{minionIdentity.GetProperName()}:");
            LogExample($"  Health: {bioData.HealthPercentage:P1} ({bioData.HealthState})");
            LogExample($"  Calories: {bioData.CaloriePercentage:P1}");
            LogExample($"  Stamina: {bioData.StaminaPercentage:P1}");
            LogExample($"  Stress: {bioData.StressPercentage:P1}");
            LogExample($"  Needs: {bioData.GetNeedsSummary()}");

            if (bioData.IsSick)
            {
                LogExample($"  Sicknesses: {string.Join(", ", bioData.CurrentSicknesses)}");
            }

            if (bioData.CurrentEffects.Count > 0)
            {
                LogExample($"  Effects: {string.Join(", ", bioData.CurrentEffects)}");
            }
        }
    }

    /// <summary>
    /// Example: Log health status summary
    /// </summary>
    private void LogHealthStatusSummary()
    {
        if (DuplicateBioDataMonitor.Instance == null)
        {
            return;
        }

        System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<MinionIdentity>> statusGroups = DuplicateBioDataMonitor.Instance.GetDuplicatesByHealthStatus();

        LogExample("=== Health Status Summary ===");

        foreach (System.Collections.Generic.KeyValuePair<string, System.Collections.Generic.List<MinionIdentity>> kvp in statusGroups)
        {
            string status = kvp.Key;
            System.Collections.Generic.List<MinionIdentity> duplicates = kvp.Value;

            if (duplicates.Count > 0)
            {
                LogExample($"{status}: {duplicates.Count} duplicates");
                foreach (MinionIdentity minion in duplicates)
                {
                    LogExample($"  - {minion.GetProperName()}");
                }
            }
        }
    }

    /// <summary>
    /// Example: Handle bio data updates
    /// </summary>
    private void OnDuplicateBioDataUpdated(MinionIdentity minionIdentity, DuplicateBioData bioData)
    {
        // Example: React to specific bio data changes
        if (bioData.IsStarving && !bioData.IsDead)
        {
            LogExample($"URGENT: {minionIdentity.GetProperName()} needs food immediately!");
            // You could trigger automatic food delivery here
            HandleStarvingDuplicate(minionIdentity, bioData);
        }

        if (bioData.IsExhausted)
        {
            LogExample($"TIRED: {minionIdentity.GetProperName()} needs rest");
            // You could force sleep schedule here
            HandleExhaustedDuplicate(minionIdentity, bioData);
        }
    }

    /// <summary>
    /// Handle critical health events
    /// </summary>
    private void OnCriticalHealth(MinionIdentity minionIdentity, DuplicateBioData bioData)
    {
        LogExample($"CRITICAL HEALTH: {minionIdentity.GetProperName()} needs immediate medical attention!");

        // Example: Auto-schedule medical care
        HandleCriticalHealth(minionIdentity, bioData);
    }

    /// <summary>
    /// Handle starvation events
    /// </summary>
    private void OnStarvation(MinionIdentity minionIdentity, DuplicateBioData bioData)
    {
        LogExample($"STARVATION WARNING: {minionIdentity.GetProperName()} is starving!");

        // Example: Prioritize food delivery
        HandleStarvingDuplicate(minionIdentity, bioData);
    }

    /// <summary>
    /// Handle stress events
    /// </summary>
    private void OnStress(MinionIdentity minionIdentity, DuplicateBioData bioData)
    {
        LogExample($"HIGH STRESS: {minionIdentity.GetProperName()} is highly stressed!");

        // Example: Schedule recreation time
        HandleStressedDuplicate(minionIdentity, bioData);
    }

    /// <summary>
    /// Handle sickness events
    /// </summary>
    private void OnSickness(MinionIdentity minionIdentity, DuplicateBioData bioData)
    {
        LogExample($"SICKNESS DETECTED: {minionIdentity.GetProperName()} is sick with: {string.Join(", ", bioData.CurrentSicknesses)}");

        // Example: Quarantine or medical treatment
        HandleSickDuplicate(minionIdentity, bioData);
    }

    /// <summary>
    /// Handle temperature events
    /// </summary>
    private void OnTemperature(MinionIdentity minionIdentity, DuplicateBioData bioData)
    {
        string condition = bioData.IsOverheating ? "overheating" : "freezing";
        LogExample($"TEMPERATURE WARNING: {minionIdentity.GetProperName()} is {condition}! ({bioData.BodyTemperature:F1}K)");

        // Example: Move to appropriate temperature zone
        HandleTemperatureIssue(minionIdentity, bioData);
    }

    #region Example Response Handlers

    private void HandleCriticalHealth(MinionIdentity minionIdentity, DuplicateBioData bioData)
    {
        // Example: Force the duplicate to seek medical care
        if (DuplicateScheduleControlPatches.GetForcedActivity(minionIdentity.GetComponent<Schedulable>()) == null)
        {
            // Could force rest to recover health
            Schedulable schedulable = minionIdentity.GetComponent<Schedulable>();
            if (schedulable != null)
            {
                Schedule restSchedule = CustomScheduleFactory.CreateRestFocusedSchedule("Emergency Rest");
                DuplicateScheduleControlPatches.SetCustomSchedule(schedulable, restSchedule);
                LogExample($"Applied emergency rest schedule to {minionIdentity.GetProperName()}");
            }
        }
    }

    private void HandleStarvingDuplicate(MinionIdentity minionIdentity, DuplicateBioData bioData)
    {
        // Example: Force eating activity
        Schedulable schedulable = minionIdentity.GetComponent<Schedulable>();
        if (schedulable != null)
        {
            ScheduleBlockType eatActivity = Db.Get().ScheduleBlockTypes.Eat;
            DuplicateScheduleControlPatches.ForceActivity(schedulable, eatActivity);
            LogExample($"Forced {minionIdentity.GetProperName()} to eat");
        }
    }

    private void HandleExhaustedDuplicate(MinionIdentity minionIdentity, DuplicateBioData bioData)
    {
        // Example: Force sleep activity
        Schedulable schedulable = minionIdentity.GetComponent<Schedulable>();
        if (schedulable != null)
        {
            ScheduleBlockType sleepActivity = Db.Get().ScheduleBlockTypes.Sleep;
            DuplicateScheduleControlPatches.ForceActivity(schedulable, sleepActivity);
            LogExample($"Forced {minionIdentity.GetProperName()} to sleep");
        }
    }

    private void HandleStressedDuplicate(MinionIdentity minionIdentity, DuplicateBioData bioData)
    {
        // Example: Schedule recreation time
        Schedulable schedulable = minionIdentity.GetComponent<Schedulable>();
        if (schedulable != null)
        {
            ScheduleBlockType recreationActivity = Db.Get().ScheduleBlockTypes.Recreation;
            DuplicateScheduleControlPatches.ForceActivity(schedulable, recreationActivity);
            LogExample($"Scheduled recreation for {minionIdentity.GetProperName()}");
        }
    }

    private void HandleSickDuplicate(MinionIdentity minionIdentity, DuplicateBioData bioData)
    {
        // Example: Apply rest schedule for recovery
        Schedulable schedulable = minionIdentity.GetComponent<Schedulable>();
        if (schedulable != null)
        {
            Schedule restSchedule = CustomScheduleFactory.CreateRestFocusedSchedule("Medical Rest");
            DuplicateScheduleControlPatches.SetCustomSchedule(schedulable, restSchedule);
            LogExample($"Applied medical rest schedule to {minionIdentity.GetProperName()}");
        }
    }

    private void HandleTemperatureIssue(MinionIdentity minionIdentity, DuplicateBioData bioData)
    {
        // Example: Could implement logic to move duplicate to appropriate areas
        LogExample($"Temperature issue for {minionIdentity.GetProperName()} requires environmental adjustment");
    }

    #endregion Example Response Handlers

    private void LogExample(string message)
    {
        if (enableExampleLogging)
        {
            Debug.Log($"[BioDataExample] {message}");
        }
    }
}