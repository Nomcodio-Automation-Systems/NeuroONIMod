using HarmonyLib;
using Klei.AI;
using System.Collections.Generic;

namespace NeuroMod;

/// <summary>
/// Main class for accessing duplicant bio data through Harmony patches
/// </summary>
[HarmonyPatch]
public static class DuplicateBioDataPatches
{
    // Cache for storing bio data
    private static readonly Dictionary<MinionIdentity, DuplicateBioData> bioDataCache =
        [];

    /// <summary>
    /// Patch Health component to intercept health changes
    /// </summary>
    [HarmonyPatch(typeof(Health), "OnHealthChanged")]
    [HarmonyPostfix]
    public static void Health_OnHealthChanged_Postfix(Health __instance, float delta)
    {
        MinionIdentity minionIdentity = __instance.GetComponent<MinionIdentity>();
        if (minionIdentity != null)
        {
            UpdateBioData(minionIdentity);
        }
    }

    /// <summary>
    /// Patch AmountInstance to intercept amount changes (calories, stamina, etc.)
    /// </summary>
    [HarmonyPatch(typeof(AmountInstance), "SetValue")]
    [HarmonyPostfix]
    public static void AmountInstance_SetValue_Postfix(AmountInstance __instance, float value)
    {
        if (__instance.gameObject != null)
        {
            MinionIdentity minionIdentity = __instance.gameObject.GetComponent<MinionIdentity>();
            if (minionIdentity != null)
            {
                UpdateBioData(minionIdentity);
            }
        }
    }

    /// <summary>
    /// Patch Effects to monitor status effect changes
    /// </summary>
    [HarmonyPatch(typeof(Effects), "Add", [typeof(Effect), typeof(bool)])]
    [HarmonyPostfix]
    public static void Effects_Add_Postfix(Effects __instance, Effect newEffect, bool should_save)
    {
        MinionIdentity minionIdentity = __instance.GetComponent<MinionIdentity>();
        if (minionIdentity != null)
        {
            UpdateBioData(minionIdentity);
            // Removed noisy log: Effect added to duplicant
        }
    }

    /// <summary>
    /// Update bio data for a specific duplicate
    /// </summary>
    private static void UpdateBioData(MinionIdentity minionIdentity)
    {
        if (minionIdentity == null)
        {
            return;
        }

        // Safety check: Don't update bio data if essential components aren't ready yet
        // This prevents null reference exceptions during OnPrefabInit
        Health health = minionIdentity.GetComponent<Health>();
        Effects effects = minionIdentity.GetComponent<Effects>();

        if (health == null || effects == null)
        {
            // Components not ready yet, skip this update
            return;
        }

        try
        {
            DuplicateBioData bioData = GetOrCreateBioData(minionIdentity);
            bioData.UpdateAllData();

            // Trigger event for subscribers
            OnBioDataUpdated?.Invoke(minionIdentity, bioData);
        }
        catch (System.NullReferenceException ex)
        {
            // Components exist but their internal state isn't ready yet
            // This can happen during OnPrefabInit - skip this update
            Debug.LogWarning($"[BioData] Components not fully initialized for {minionIdentity.GetProperName()}: {ex.Message}");
        }
    }

    /// <summary>
    /// Get or create bio data for a minion
    /// </summary>
    private static DuplicateBioData GetOrCreateBioData(MinionIdentity minionIdentity)
    {
        if (!bioDataCache.TryGetValue(minionIdentity, out DuplicateBioData bioData))
        {
            bioData = new DuplicateBioData(minionIdentity);
            bioDataCache[minionIdentity] = bioData;
        }
        return bioData;
    }

    #region Public API

    /// <summary>
    /// Event triggered when bio data is updated
    /// </summary>
    public static System.Action<MinionIdentity, DuplicateBioData>? OnBioDataUpdated;

    /// <summary>
    /// Get bio data for a specific duplicate
    /// </summary>
    public static DuplicateBioData? GetBioData(MinionIdentity minionIdentity)
    {
        if (minionIdentity == null)
        {
            return null;
        }

        // Use performance cache for frequently requested bio data
        string cacheKey = $"biodata_{minionIdentity.GetInstanceID()}";

        return PerformanceCache.Instance.GetOrCompute(cacheKey, () =>
        {
            DuplicateBioData? bioData = GetOrCreateBioData(minionIdentity);
            bioData?.UpdateAllData(); // Ensure fresh data
            return bioData;
        }, 3); // Cache for 3 seconds for bio data (configured in performance settings)
    }

    /// <summary>
    /// Get bio data for all duplicates
    /// </summary>
    public static Dictionary<MinionIdentity, DuplicateBioData> GetAllBioData()
    {
        Dictionary<MinionIdentity, DuplicateBioData> result = [];

        foreach (MinionIdentity? minionIdentity in Components.LiveMinionIdentities.Items)
        {
            if (minionIdentity != null)
            {
                DuplicateBioData? bioData = GetBioData(minionIdentity);
                if (bioData != null)
                {
                    result[minionIdentity] = bioData;
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Clear cached bio data (useful for cleanup)
    /// </summary>
    public static void ClearCache()
    {
        bioDataCache.Clear();
    }

    /// <summary>
    /// Force update all bio data
    /// </summary>
    public static void RefreshAllBioData()
    {
        foreach (MinionIdentity? minionIdentity in Components.LiveMinionIdentities.Items)
        {
            UpdateBioData(minionIdentity);
        }
    }

    /// <summary>
    /// Clean up dead duplicates from cache
    /// </summary>
    public static void CleanupDeadDuplicates()
    {
        List<MinionIdentity> keysToRemove = [];

        foreach (KeyValuePair<MinionIdentity, DuplicateBioData> kvp in bioDataCache)
        {
            if (kvp.Key == null || kvp.Value?.IsDead == true)
            {
                if (kvp.Key != null)
                {
                    keysToRemove.Add(kvp.Key);
                }
            }
        }

        foreach (MinionIdentity key in keysToRemove)
        {
            if (key != null)
            {
                bioDataCache.Remove(key);
            }
        }
    }

    #endregion Public API
}