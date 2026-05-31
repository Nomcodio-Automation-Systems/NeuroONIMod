using HarmonyLib;
using Klei.AI;
using System.Collections.Generic;

namespace NeuroMod;

/// <summary>
/// Maintains the live duplicate bio-data cache by reacting to Harmony patch callbacks.
/// </summary>
/// <pre>Harmony has patched the relevant game callbacks before the public API is used.</pre>
/// <post>Cached bio data is refreshed lazily and update notifications are emitted for successful refreshes.</post>
[HarmonyPatch]
public static class DuplicateBioDataPatches
{
    // Cache for storing bio data
    private static readonly Dictionary<MinionIdentity, DuplicateBioData> bioDataCache =
        [];

    /// <summary>
    /// Refreshes cached bio data after a duplicate health change callback.
    /// </summary>
    /// <param name="__instance">The health component whose owning duplicate may need a refreshed snapshot.</param>
    /// <param name="delta">The health delta reported by the game callback.</param>
    /// <pre><paramref name="__instance"/> belongs to a live duplicate when a refresh is expected.</pre>
    /// <post>If the owning duplicate can be resolved and its required components are initialized, the cached snapshot has been refreshed.</post>
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
    /// Refreshes cached bio data after a tracked amount value changes.
    /// </summary>
    /// <param name="__instance">The amount instance whose owning duplicate may need a refreshed snapshot.</param>
    /// <param name="value">The new amount value reported by the game callback.</param>
    /// <pre><paramref name="__instance"/> belongs to a live duplicate when a refresh is expected.</pre>
    /// <post>If the owning duplicate can be resolved and its required components are initialized, the cached snapshot has been refreshed.</post>
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
    /// Refreshes cached bio data after a status effect is added to a duplicate.
    /// </summary>
    /// <param name="__instance">The effects component whose owning duplicate may need a refreshed snapshot.</param>
    /// <param name="newEffect">The effect that has been applied.</param>
    /// <param name="should_save">Whether the effect should be persisted by the game.</param>
    /// <pre><paramref name="__instance"/> belongs to a live duplicate when a refresh is expected.</pre>
    /// <post>If the owning duplicate can be resolved and its required components are initialized, the cached snapshot has been refreshed.</post>
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
    /// Recomputes cached bio data for a specific duplicate when its components are ready.
    /// </summary>
    /// <param name="minionIdentity">The duplicate whose snapshot should be refreshed.</param>
    /// <pre><paramref name="minionIdentity"/> may be null or refer to a duplicate that is still initializing.</pre>
    /// <post>When required components are available, the cached snapshot has been updated and subscribers have been notified.</post>
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
    /// Returns the cached bio-data wrapper for a duplicate, creating it on first access.
    /// </summary>
    /// <param name="minionIdentity">The duplicate whose wrapper should be returned.</param>
    /// <returns>The cached or newly created wrapper for the duplicate.</returns>
    /// <pre><paramref name="minionIdentity"/> is expected to be non-null and stable for dictionary lookup.</pre>
    /// <post>The cache contains an entry for <paramref name="minionIdentity"/>.</post>
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
    /// Raised after a duplicate bio-data snapshot has been refreshed successfully.
    /// </summary>
    public static System.Action<MinionIdentity, DuplicateBioData>? OnBioDataUpdated;

    /// <summary>
    /// Gets the bio data snapshot for a specific duplicate.
    /// </summary>
    /// <param name="minionIdentity">The duplicate whose snapshot should be returned.</param>
    /// <returns>The current snapshot for the duplicate, or <c>null</c> when the duplicate reference is unavailable.</returns>
    /// <pre><paramref name="minionIdentity"/> refers to a live duplicate when a non-null result is expected.</pre>
    /// <post>The returned snapshot has been refreshed recently or computed on demand and may be served from the performance cache.</post>
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
    /// Gets bio data for all currently live duplicates.
    /// </summary>
    /// <returns>A new dictionary snapshot keyed by the live duplicates found at call time.</returns>
    /// <pre>The live minion component registry is available.</pre>
    /// <post>The returned dictionary contains only duplicates for which bio data could be resolved.</post>
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
    /// Clears the internal bio-data cache.
    /// </summary>
    /// <pre>Callers accept that subsequent lookups will rebuild the cache lazily.</pre>
    /// <post>The patch-layer cache is empty until new lookups or updates repopulate it.</post>
    public static void ClearCache()
    {
        bioDataCache.Clear();
    }

    /// <summary>
    /// Forces a refresh attempt for all currently live duplicates.
    /// </summary>
    /// <pre>The live minion component registry is available.</pre>
    /// <post>Every live duplicate has been offered a refresh attempt, subject to component readiness checks.</post>
    public static void RefreshAllBioData()
    {
        foreach (MinionIdentity? minionIdentity in Components.LiveMinionIdentities.Items)
        {
            UpdateBioData(minionIdentity);
        }
    }

    /// <summary>
    /// Removes dead or invalid duplicate entries from the cache.
    /// </summary>
    /// <pre>The cache may contain entries for duplicates that have died or been destroyed.</pre>
    /// <post>Any cache entry whose key is invalid or whose bio-data snapshot is dead has been removed.</post>
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