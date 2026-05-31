using System.Collections.Generic;
using UnityEngine;

namespace NeuroMod;

/// <summary>
/// Monitors live duplicate bio data and raises alerts when configured thresholds are crossed.
/// </summary>
/// <pre>The component is spawned once and remains subscribed to the bio-data patch stream while active.</pre>
/// <post>Threshold-based warning events are raised without crashing the monitor when subscribers throw.</post>
public class DuplicateBioDataMonitor : KMonoBehaviour, ISim1000ms
{
    private static DuplicateBioDataMonitor? instance;
    public static DuplicateBioDataMonitor? Instance => instance;

    private readonly Dictionary<MinionIdentity, DuplicateBioData> lastKnownData =
        [];

    // Wrapped delegate reference for safe subscription
    private System.Action<MinionIdentity, DuplicateBioData>? _onBioDataUpdatedWrapped;

    [SerializeField]
    private bool enableDebugLogging = true;

    [SerializeField]
    private bool enableCriticalAlerts = true;

    [SerializeField]
    private float criticalHealthThreshold = 0.2f;

    [SerializeField]
    private float starvationThreshold = 0.15f;

    [SerializeField]
    private float highStressThreshold = 0.8f;

    // Events for critical conditions
    public System.Action<MinionIdentity, DuplicateBioData>? OnCriticalHealthChange;

    public System.Action<MinionIdentity, DuplicateBioData>? OnStarvationWarning;
    public System.Action<MinionIdentity, DuplicateBioData>? OnStressWarning;
    public System.Action<MinionIdentity, DuplicateBioData>? OnSicknessDetected;
    public System.Action<MinionIdentity, DuplicateBioData>? OnTemperatureWarning;

    protected override void OnPrefabInit()
    {
        base.OnPrefabInit();

        if (instance == null)
        {
            instance = this;
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

        // Subscribe to bio data updates (wrapped to protect monitor from subscriber exceptions)
        _onBioDataUpdatedWrapped = NeuroMod.Api.EventSubscriber.Wrap<MinionIdentity, DuplicateBioData>("DuplicateBioDataMonitor.OnBioDataUpdated", OnBioDataUpdated);
        DuplicateBioDataPatches.OnBioDataUpdated += _onBioDataUpdatedWrapped;

        LogDebug("[BioMonitor] Bio data monitor initialized (wrapped)");
    }

    protected override void OnCleanUp()
    {
        if (_onBioDataUpdatedWrapped != null)
        {
            DuplicateBioDataPatches.OnBioDataUpdated -= _onBioDataUpdatedWrapped;
            _onBioDataUpdatedWrapped = null;
        }
        base.OnCleanUp();
    }

    public void Sim1000ms(float dt)
    {
        // Update all bio data every second
        foreach (MinionIdentity? minionIdentity in Components.LiveMinionIdentities.Items)
        {
            DuplicateBioData? bioData = DuplicateBioDataPatches.GetBioData(minionIdentity);
            if (bioData != null)
            {
                CheckForCriticalConditions(minionIdentity, bioData);
            }
        }
    }

    private void OnBioDataUpdated(MinionIdentity minionIdentity, DuplicateBioData bioData)
    {
        lastKnownData[minionIdentity] = bioData;

        if (enableCriticalAlerts)
        {
            CheckForCriticalConditions(minionIdentity, bioData);
        }
    }

    private void CheckForCriticalConditions(MinionIdentity minionIdentity, DuplicateBioData bioData)
    {
        if (!enableCriticalAlerts || bioData.IsDead)
        {
            return;
        }

        // Check for critical health
        if (bioData.HealthPercentage < criticalHealthThreshold)
        {
            OnCriticalHealthChange?.Invoke(minionIdentity, bioData);
            LogWarning($"{minionIdentity.GetProperName()} has critical health: {bioData.HealthPercentage:P1}");
        }

        // Check for starvation
        if (bioData.CaloriePercentage < starvationThreshold)
        {
            OnStarvationWarning?.Invoke(minionIdentity, bioData);
            LogWarning($"{minionIdentity.GetProperName()} is starving: {bioData.CaloriePercentage:P1}");
        }

        // Check for high stress
        if (bioData.StressPercentage > highStressThreshold)
        {
            OnStressWarning?.Invoke(minionIdentity, bioData);
            LogWarning($"{minionIdentity.GetProperName()} is highly stressed: {bioData.StressPercentage:P1}");
        }

        // Check for sickness
        if (bioData.IsSick)
        {
            OnSicknessDetected?.Invoke(minionIdentity, bioData);
            LogWarning($"{minionIdentity.GetProperName()} is sick: {string.Join(", ", bioData.CurrentSicknesses)}");
        }

        // Check for temperature issues
        if (bioData.IsOverheating || bioData.IsFreezing)
        {
            OnTemperatureWarning?.Invoke(minionIdentity, bioData);
            LogWarning($"{minionIdentity.GetProperName()} temperature issue: {bioData.BodyTemperature:F1}K");
        }
    }

    /// <summary>
    /// Gets the current bio data snapshot for every live duplicate.
    /// </summary>
    /// <pre>The patch layer is initialized and able to resolve bio data for live minions.</pre>
    /// <post>Returns a new dictionary snapshot keyed by the live minions found at call time.</post>
    public Dictionary<MinionIdentity, DuplicateBioData> GetAllCurrentBioData()
    {
        return DuplicateBioDataPatches.GetAllBioData();
    }

    /// <summary>
    /// Gets the duplicates that currently satisfy the monitor's critical-condition contract.
    /// </summary>
    /// <pre>Configured thresholds describe the urgent conditions that should be treated as critical.</pre>
    /// <post>The returned list excludes non-critical warnings such as ordinary sickness or temperature discomfort.</post>
    public List<MinionIdentity> GetCriticalDuplicates()
    {
        List<MinionIdentity> criticalDupes = [];
        Dictionary<MinionIdentity, DuplicateBioData> allData = GetAllCurrentBioData();

        foreach (KeyValuePair<MinionIdentity, DuplicateBioData> kvp in allData)
        {
            DuplicateBioData bioData = kvp.Value;
            if (IsCritical(bioData))
            {
                criticalDupes.Add(kvp.Key);
            }
        }

        return criticalDupes;
    }

    /// <summary>
    /// Determines whether the provided snapshot represents a critical condition.
    /// </summary>
    /// <param name="bioData">The duplicate snapshot to evaluate.</param>
    /// <returns><c>true</c> when the duplicate is in an urgent state that warrants critical handling; otherwise <c>false</c>.</returns>
    /// <pre><paramref name="bioData"/> is a non-null snapshot for a live or recently tracked duplicate.</pre>
    /// <post>Only severe health, starvation, oxygen, or high-stress states are classified as critical.</post>
    public bool IsCritical(DuplicateBioData bioData)
    {
        return bioData.HealthPercentage < criticalHealthThreshold ||
               bioData.CaloriePercentage < starvationThreshold ||
               bioData.StressPercentage > highStressThreshold ||
               bioData.NeedsOxygen;
    }

    /// <summary>
    /// Groups duplicates into one primary health-status bucket each.
    /// </summary>
    /// <pre>The current bio data snapshot is available for the tracked duplicates.</pre>
    /// <post>Each duplicate appears in at most one returned status bucket.</post>
    public Dictionary<string, List<MinionIdentity>> GetDuplicatesByHealthStatus()
    {
        Dictionary<string, List<MinionIdentity>> statusGroups = new()
        {
            ["Healthy"] = [],
            ["Critical"] = [],
            ["Stressed"] = [],
            ["Hungry"] = [],
            ["Tired"] = [],
            ["Sick"] = []
        };

        Dictionary<MinionIdentity, DuplicateBioData> allData = GetAllCurrentBioData();

        foreach (KeyValuePair<MinionIdentity, DuplicateBioData> kvp in allData)
        {
            DuplicateBioData bioData = kvp.Value;
            MinionIdentity minion = kvp.Key;

            if (IsCritical(bioData))
            {
                statusGroups["Critical"].Add(minion);
            }
            else if (bioData.IsStressed)
            {
                statusGroups["Stressed"].Add(minion);
            }
            else if (bioData.IsHungry)
            {
                statusGroups["Hungry"].Add(minion);
            }
            else if (bioData.IsTired)
            {
                statusGroups["Tired"].Add(minion);
            }
            else if (bioData.IsSick)
            {
                statusGroups["Sick"].Add(minion);
            }
            else
            {
                statusGroups["Healthy"].Add(minion);
            }
        }

        return statusGroups;
    }

    /// <summary>
    /// Logs the current bio data snapshot for every live duplicate.
    /// </summary>
    /// <pre>Debug logging may be disabled, in which case no messages are emitted.</pre>
    /// <post>One summary header plus one line per duplicate is written when logging is enabled.</post>
    public void LogAllBioData()
    {
        Dictionary<MinionIdentity, DuplicateBioData> allData = GetAllCurrentBioData();
        LogDebug("=== Bio Data Summary ===");

        foreach (KeyValuePair<MinionIdentity, DuplicateBioData> kvp in allData)
        {
            LogDebug($"{kvp.Value}");
        }
    }

    /// <summary>
    /// Logs a summary of duplicates that are currently in critical condition.
    /// </summary>
    /// <pre>The critical-condition contract is defined by <see cref="IsCritical(DuplicateBioData)"/>.</pre>
    /// <post>The log contains either the critical duplicate summary or a single healthy-state message.</post>
    public void LogCriticalSummary()
    {
        List<MinionIdentity> criticalDupes = GetCriticalDuplicates();

        if (criticalDupes.Count > 0)
        {
            LogWarning($"=== {criticalDupes.Count} Duplicates in Critical Condition ===");
            foreach (MinionIdentity minion in criticalDupes)
            {
                DuplicateBioData? bioData = DuplicateBioDataPatches.GetBioData(minion);
                LogWarning($"{bioData}");
            }
        }
        else
        {
            LogDebug("All duplicates are in good condition");
        }
    }

    #region Utility Methods

    private void LogDebug(string message)
    {
        if (enableDebugLogging)
        {
            Debug.Log($"[BioMonitor] {message}");
        }
    }

    private void LogWarning(string message)
    {
        Debug.LogWarning($"[BioMonitor] {message}");
    }

    #endregion Utility Methods

    #region Configuration

    /// <summary>
    /// Sets the health percentage below which a duplicate is treated as critical.
    /// </summary>
    /// <param name="threshold">The desired normalized threshold.</param>
    /// <pre><paramref name="threshold"/> is expressed as a normalized percentage.</pre>
    /// <post>The stored threshold is clamped to the inclusive range [0, 1].</post>
    public void SetCriticalHealthThreshold(float threshold)
    {
        criticalHealthThreshold = Mathf.Clamp01(threshold);
    }

    /// <summary>
    /// Sets the calorie percentage below which a duplicate is treated as starving.
    /// </summary>
    /// <param name="threshold">The desired normalized threshold.</param>
    /// <pre><paramref name="threshold"/> is expressed as a normalized percentage.</pre>
    /// <post>The stored threshold is clamped to the inclusive range [0, 1].</post>
    public void SetStarvationThreshold(float threshold)
    {
        starvationThreshold = Mathf.Clamp01(threshold);
    }

    /// <summary>
    /// Sets the stress percentage above which a duplicate is treated as highly stressed.
    /// </summary>
    /// <param name="threshold">The desired normalized threshold.</param>
    /// <pre><paramref name="threshold"/> is expressed as a normalized percentage.</pre>
    /// <post>The stored threshold is clamped to the inclusive range [0, 1].</post>
    public void SetHighStressThreshold(float threshold)
    {
        highStressThreshold = Mathf.Clamp01(threshold);
    }

    /// <summary>
    /// Enables or disables threshold-based critical alerts.
    /// </summary>
    /// <param name="enable"><c>true</c> to emit alerts; otherwise <c>false</c>.</param>
    /// <pre>The monitor is already initialized.</pre>
    /// <post>Subsequent updates either emit alerts or remain silent based on <paramref name="enable"/>.</post>
    public void EnableCriticalAlerts(bool enable)
    {
        enableCriticalAlerts = enable;
    }

    /// <summary>
    /// Enables or disables debug log output for the monitor.
    /// </summary>
    /// <param name="enable"><c>true</c> to emit debug logs; otherwise <c>false</c>.</param>
    /// <pre>The monitor exists and may already be processing updates.</pre>
    /// <post>Future debug messages honor the updated logging flag.</post>
    public void EnableDebugLogging(bool enable)
    {
        enableDebugLogging = enable;
    }

    #endregion Configuration
}