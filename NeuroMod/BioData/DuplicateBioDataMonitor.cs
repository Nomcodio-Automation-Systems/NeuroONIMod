using System.Collections.Generic;
using UnityEngine;

namespace NeuroMod;

/// <summary>
/// Component for real-time monitoring of duplicate bio data
/// </summary>
public class DuplicateBioDataMonitor : KMonoBehaviour, ISim1000ms
{
    private static DuplicateBioDataMonitor? instance;
    public static DuplicateBioDataMonitor? Instance => instance;

    private readonly Dictionary<MinionIdentity, DuplicateBioData> lastKnownData =
        [];

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

        // Subscribe to bio data updates
        DuplicateBioDataPatches.OnBioDataUpdated += OnBioDataUpdated;

        LogDebug("[BioMonitor] Bio data monitor initialized");
    }

    protected override void OnCleanUp()
    {
        if (DuplicateBioDataPatches.OnBioDataUpdated != null)
        {
            DuplicateBioDataPatches.OnBioDataUpdated -= OnBioDataUpdated;
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
    /// Get current bio data for all duplicates
    /// </summary>
    public Dictionary<MinionIdentity, DuplicateBioData> GetAllCurrentBioData()
    {
        return DuplicateBioDataPatches.GetAllBioData();
    }

    /// <summary>
    /// Get duplicates in critical condition
    /// </summary>
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
    /// Check if a duplicate is in critical condition
    /// </summary>
    public bool IsCritical(DuplicateBioData bioData)
    {
        return bioData.HealthPercentage < criticalHealthThreshold ||
               bioData.CaloriePercentage < starvationThreshold ||
               bioData.StressPercentage > highStressThreshold ||
               bioData.IsSick ||
               bioData.IsOverheating ||
               bioData.IsFreezing;
    }

    /// <summary>
    /// Get duplicates by health status
    /// </summary>
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
    /// Log bio data for all duplicates
    /// </summary>
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
    /// Log critical duplicates summary
    /// </summary>
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

    public void SetCriticalHealthThreshold(float threshold)
    {
        criticalHealthThreshold = Mathf.Clamp01(threshold);
    }

    public void SetStarvationThreshold(float threshold)
    {
        starvationThreshold = Mathf.Clamp01(threshold);
    }

    public void SetHighStressThreshold(float threshold)
    {
        highStressThreshold = Mathf.Clamp01(threshold);
    }

    public void EnableCriticalAlerts(bool enable)
    {
        enableCriticalAlerts = enable;
    }

    public void EnableDebugLogging(bool enable)
    {
        enableDebugLogging = enable;
    }

    #endregion Configuration
}