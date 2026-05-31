using System;
using System.Collections;
using UnityEngine;

namespace NeuroMod;

/// <summary>
/// Example usage of the bio data system
/// </summary>
/// <pre>The bio-data patch and monitor systems are available in a running game session.</pre>
/// <post>The component demonstrates how to subscribe to bio-data events and react with schedule-control helpers.</post>
public class BioDataUsageExample : KMonoBehaviour
{
    [SerializeField]
    private readonly bool enableExampleLogging = true;

    [SerializeField]
    private readonly float exampleUpdateInterval = 10f;

    // Wrapped delegate references so we can unsubscribe correctly
    private Action<MinionIdentity, DuplicateBioData>? _onDuplicateBioDataUpdatedWrapped;
    private Action<MinionIdentity, DuplicateBioData>? _onCriticalHealthWrapped;
    private Action<MinionIdentity, DuplicateBioData>? _onStarvationWrapped;
    private Action<MinionIdentity, DuplicateBioData>? _onStressWrapped;
    private Action<MinionIdentity, DuplicateBioData>? _onSicknessWrapped;
    private Action<MinionIdentity, DuplicateBioData>? _onTemperatureWrapped;

    /// <summary>
    /// Subscribes to bio-data events and starts periodic example logging when enabled.
    /// </summary>
    /// <pre>The bio-data systems and optional monitor singleton have been initialized.</pre>
    /// <post>The component is subscribed to available events and periodic example logging is running when configured.</post>
    protected override void OnSpawn()
    {
        base.OnSpawn();

        // Subscribe to bio data events (wrapped for debug + anti-spam)
        _onDuplicateBioDataUpdatedWrapped = NeuroMod.Api.EventSubscriber.Wrap<MinionIdentity, DuplicateBioData>("BioDataUsageExample.OnDuplicateBioDataUpdated", OnDuplicateBioDataUpdated);
        DuplicateBioDataPatches.OnBioDataUpdated += _onDuplicateBioDataUpdatedWrapped;

        // Subscribe to monitor events
        if (DuplicateBioDataMonitor.Instance != null)
        {
            _onCriticalHealthWrapped = NeuroMod.Api.EventSubscriber.Wrap<MinionIdentity, DuplicateBioData>("BioDataUsageExample.OnCriticalHealth", OnCriticalHealth);
            _onStarvationWrapped = NeuroMod.Api.EventSubscriber.Wrap<MinionIdentity, DuplicateBioData>("BioDataUsageExample.OnStarvation", OnStarvation);
            _onStressWrapped = NeuroMod.Api.EventSubscriber.Wrap<MinionIdentity, DuplicateBioData>("BioDataUsageExample.OnStress", OnStress);
            _onSicknessWrapped = NeuroMod.Api.EventSubscriber.Wrap<MinionIdentity, DuplicateBioData>("BioDataUsageExample.OnSickness", OnSickness);
            _onTemperatureWrapped = NeuroMod.Api.EventSubscriber.Wrap<MinionIdentity, DuplicateBioData>("BioDataUsageExample.OnTemperature", OnTemperature);

            DuplicateBioDataMonitor.Instance.OnCriticalHealthChange += _onCriticalHealthWrapped;
            DuplicateBioDataMonitor.Instance.OnStarvationWarning += _onStarvationWrapped;
            DuplicateBioDataMonitor.Instance.OnStressWarning += _onStressWrapped;
            DuplicateBioDataMonitor.Instance.OnSicknessDetected += _onSicknessWrapped;
            DuplicateBioDataMonitor.Instance.OnTemperatureWarning += _onTemperatureWrapped;
        }

        // Start example usage coroutine
        if (enableExampleLogging)
        {
            StartCoroutine(ExampleUsageCoroutine());
        }
    }

    /// <summary>
    /// Unsubscribes from all wrapped event handlers before cleanup completes.
    /// </summary>
    /// <pre>The component may currently hold subscriptions to bio-data patch or monitor events.</pre>
    /// <post>All event subscriptions created by this component have been removed.</post>
    protected override void OnCleanUp()
    {
        // Unsubscribe from events
        if (_onDuplicateBioDataUpdatedWrapped != null)
        {
            DuplicateBioDataPatches.OnBioDataUpdated -= _onDuplicateBioDataUpdatedWrapped;
            _onDuplicateBioDataUpdatedWrapped = null;
        }

        DuplicateBioDataMonitor? monitor = DuplicateBioDataMonitor.Instance;
        if (monitor != null)
        {
            if (_onCriticalHealthWrapped != null) monitor.OnCriticalHealthChange -= _onCriticalHealthWrapped;
            if (_onStarvationWrapped != null) monitor.OnStarvationWarning -= _onStarvationWrapped;
            if (_onStressWrapped != null) monitor.OnStressWarning -= _onStressWrapped;
            if (_onSicknessWrapped != null) monitor.OnSicknessDetected -= _onSicknessWrapped;
            if (_onTemperatureWrapped != null) monitor.OnTemperatureWarning -= _onTemperatureWrapped;

            _onCriticalHealthWrapped = null;
            _onStarvationWrapped = null;
            _onStressWrapped = null;
            _onSicknessWrapped = null;
            _onTemperatureWrapped = null;
        }

        base.OnCleanUp();
    }

    /// <summary>
    /// Periodically logs example bio-data summaries after the game has initialized.
    /// </summary>
    /// <returns>An enumerator that repeatedly logs summaries at the configured interval.</returns>
    /// <pre>The component has spawned and example logging is enabled.</pre>
    /// <post>Each loop iteration logs the current bio-data and health-status summaries before waiting again.</post>
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
    /// <pre>The bio-data patch layer is available for querying current duplicate snapshots.</pre>
    /// <post>The debug log contains either a no-duplicates message or a snapshot summary for every discovered duplicate.</post>
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
    /// <pre>The bio-data monitor singleton may or may not currently be available.</pre>
    /// <post>When the monitor exists, the debug log contains a grouped health-status summary.</post>
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
    /// <param name="minionIdentity">The duplicate whose bio data changed.</param>
    /// <param name="bioData">The refreshed bio-data snapshot.</param>
    /// <pre><paramref name="bioData"/> reflects the duplicate's latest known state.</pre>
    /// <post>Relevant starvation or exhaustion handlers have been invoked for matching conditions.</post>
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
    /// <param name="minionIdentity">The duplicate with critical health.</param>
    /// <param name="bioData">The refreshed bio-data snapshot.</param>
    /// <pre><paramref name="bioData"/> indicates a critical-health condition.</pre>
    /// <post>The example critical-health response handler has been invoked.</post>
    private void OnCriticalHealth(MinionIdentity minionIdentity, DuplicateBioData bioData)
    {
        LogExample($"CRITICAL HEALTH: {minionIdentity.GetProperName()} needs immediate medical attention!");

        // Example: Auto-schedule medical care
        HandleCriticalHealth(minionIdentity, bioData);
    }

    /// <summary>
    /// Handle starvation events
    /// </summary>
    /// <param name="minionIdentity">The duplicate with a starvation warning.</param>
    /// <param name="bioData">The refreshed bio-data snapshot.</param>
    /// <pre><paramref name="bioData"/> indicates a starvation warning.</pre>
    /// <post>The example starvation response handler has been invoked.</post>
    private void OnStarvation(MinionIdentity minionIdentity, DuplicateBioData bioData)
    {
        LogExample($"STARVATION WARNING: {minionIdentity.GetProperName()} is starving!");

        // Example: Prioritize food delivery
        HandleStarvingDuplicate(minionIdentity, bioData);
    }

    /// <summary>
    /// Handle stress events
    /// </summary>
    /// <param name="minionIdentity">The duplicate with a stress warning.</param>
    /// <param name="bioData">The refreshed bio-data snapshot.</param>
    /// <pre><paramref name="bioData"/> indicates a high-stress condition.</pre>
    /// <post>The example stress response handler has been invoked.</post>
    private void OnStress(MinionIdentity minionIdentity, DuplicateBioData bioData)
    {
        LogExample($"HIGH STRESS: {minionIdentity.GetProperName()} is highly stressed!");

        // Example: Schedule recreation time
        HandleStressedDuplicate(minionIdentity, bioData);
    }

    /// <summary>
    /// Handle sickness events
    /// </summary>
    /// <param name="minionIdentity">The duplicate for which sickness was detected.</param>
    /// <param name="bioData">The refreshed bio-data snapshot.</param>
    /// <pre><paramref name="bioData"/> contains the current sickness list.</pre>
    /// <post>The example sickness response handler has been invoked.</post>
    private void OnSickness(MinionIdentity minionIdentity, DuplicateBioData bioData)
    {
        LogExample($"SICKNESS DETECTED: {minionIdentity.GetProperName()} is sick with: {string.Join(", ", bioData.CurrentSicknesses)}");

        // Example: Quarantine or medical treatment
        HandleSickDuplicate(minionIdentity, bioData);
    }

    /// <summary>
    /// Handle temperature events
    /// </summary>
    /// <param name="minionIdentity">The duplicate with the temperature warning.</param>
    /// <param name="bioData">The refreshed bio-data snapshot.</param>
    /// <pre><paramref name="bioData"/> indicates either overheating or freezing.</pre>
    /// <post>The example temperature response handler has been invoked.</post>
    private void OnTemperature(MinionIdentity minionIdentity, DuplicateBioData bioData)
    {
        string condition = bioData.IsOverheating ? "overheating" : "freezing";
        LogExample($"TEMPERATURE WARNING: {minionIdentity.GetProperName()} is {condition}! ({bioData.BodyTemperature:F1}K)");

        // Example: Move to appropriate temperature zone
        HandleTemperatureIssue(minionIdentity, bioData);
    }

    #region Example Response Handlers

    /// <summary>
    /// Demonstrates a critical-health response by applying an emergency rest schedule.
    /// </summary>
    /// <param name="minionIdentity">The duplicate needing critical-health intervention.</param>
    /// <param name="bioData">The refreshed bio-data snapshot.</param>
    /// <pre><paramref name="bioData"/> indicates critical health and the duplicate may expose a schedulable component.</pre>
    /// <post>When possible, the duplicate receives an emergency rest schedule.</post>
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

    /// <summary>
    /// Demonstrates a starvation response by forcing the duplicate to eat.
    /// </summary>
    /// <param name="minionIdentity">The duplicate needing food.</param>
    /// <param name="bioData">The refreshed bio-data snapshot.</param>
    /// <pre><paramref name="bioData"/> indicates starvation and the duplicate may expose a schedulable component.</pre>
    /// <post>When possible, the duplicate is forced onto the Eat activity.</post>
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

    /// <summary>
    /// Demonstrates an exhaustion response by forcing the duplicate to sleep.
    /// </summary>
    /// <param name="minionIdentity">The exhausted duplicate.</param>
    /// <param name="bioData">The refreshed bio-data snapshot.</param>
    /// <pre><paramref name="bioData"/> indicates exhaustion and the duplicate may expose a schedulable component.</pre>
    /// <post>When possible, the duplicate is forced onto the Sleep activity.</post>
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

    /// <summary>
    /// Demonstrates a stress response by forcing recreation.
    /// </summary>
    /// <param name="minionIdentity">The stressed duplicate.</param>
    /// <param name="bioData">The refreshed bio-data snapshot.</param>
    /// <pre><paramref name="bioData"/> indicates high stress and the duplicate may expose a schedulable component.</pre>
    /// <post>When possible, the duplicate is forced onto the Recreation activity.</post>
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

    /// <summary>
    /// Demonstrates a sickness response by applying a medical rest schedule.
    /// </summary>
    /// <param name="minionIdentity">The sick duplicate.</param>
    /// <param name="bioData">The refreshed bio-data snapshot.</param>
    /// <pre><paramref name="bioData"/> indicates sickness and the duplicate may expose a schedulable component.</pre>
    /// <post>When possible, the duplicate receives a rest-focused recovery schedule.</post>
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

    /// <summary>
    /// Demonstrates a temperature-warning response.
    /// </summary>
    /// <param name="minionIdentity">The duplicate with the environmental temperature issue.</param>
    /// <param name="bioData">The refreshed bio-data snapshot.</param>
    /// <pre><paramref name="bioData"/> indicates overheating or freezing.</pre>
    /// <post>A diagnostic message describing the required environmental adjustment has been logged.</post>
    private void HandleTemperatureIssue(MinionIdentity minionIdentity, DuplicateBioData bioData)
    {
        // Example: Could implement logic to move duplicate to appropriate areas
        LogExample($"Temperature issue for {minionIdentity.GetProperName()} requires environmental adjustment");
    }

    #endregion Example Response Handlers

    /// <summary>
    /// Writes an example message to the debug log when logging is enabled.
    /// </summary>
    /// <param name="message">The message to write.</param>
    /// <pre><paramref name="message"/> contains the example text to emit.</pre>
    /// <post>The message has been logged when example logging is enabled.</post>
    private void LogExample(string message)
    {
        if (enableExampleLogging)
        {
            Debug.Log($"[BioDataExample] {message}");
        }
    }
}