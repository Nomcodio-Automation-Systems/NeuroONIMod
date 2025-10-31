using HarmonyLib;
using NeuroSdk.Actions;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NeuroMod;

/// <summary>
/// Main integration manager that coordinates between ONI duplicate systems and Neuro SDK
/// Registers all Neuro Actions and manages the integration bridge
/// </summary>
public class NeuroIntegrationManager : KMonoBehaviour
{
    public static NeuroIntegrationManager? Instance { get; private set; }

    private NeuroIntegrationBridge? integrationBridge;
    private MinionIdentity? neuroMinion;
    private bool isInitialized = false;

    // Registered Neuro Actions
    private readonly List<INeuroAction> registeredActions = [];

    protected override void OnPrefabInit()
    {
        base.OnPrefabInit();
        Instance = this;
        NeuroLogger.Log("Prefab initialized", "NeuroIntegrationManager");
    }

    protected override void OnSpawn()
    {
        base.OnSpawn();

        // Initialize the integration when the manager spawns
        InitializeIntegration();
    }

    private void InitializeIntegration()
    {
        if (isInitialized)
        {
            return;
        }

        try
        {
            NeuroLogger.Log("Starting integration initialization...", "NeuroIntegrationManager");

            // Find the Neuro duplicate
            FindNeuroMinion();

            if (neuroMinion != null)
            {
                // Create and initialize the integration bridge
                CreateIntegrationBridge();

                // Register all Neuro Actions
                RegisterNeuroActions();

                // Mark as initialized
                isInitialized = true;

                // Send initial context message
                string welcomeMessage = $"Neuro integration system activated! Connected to duplicate '{neuroMinion.GetProperName()}'. " +
                    "AI can now monitor bio data, control tasks, manage schedules, and query status in real-time.";

                // Use safe context sending with fallback
                SafeSendContext(welcomeMessage, true);

                NeuroLogger.Log("Integration initialization completed successfully", "NeuroIntegrationManager");
            }
            else
            {
                NeuroLogger.LogWarning("Neuro duplicate not found. Integration will retry when duplicate is available.", "NeuroIntegrationManager");

                // Retry finding the minion periodically
                Invoke(nameof(RetryInitialization), 5f);
            }
        }
        catch (System.Exception ex)
        {
            NeuroLogger.LogException(ex, "integration initialization", "NeuroIntegrationManager");

            // Retry initialization after a delay
            Invoke(nameof(RetryInitialization), 10f);
        }
    }

    private void RetryInitialization()
    {
        if (!isInitialized)
        {
            NeuroLogger.Log("Retrying integration initialization...", "NeuroIntegrationManager");
            InitializeIntegration();
        }
    }

    private void FindNeuroMinion()
    {
        try
        {
            // Get configured duplicant name from settings - NO hardcoded values
            if (ConfigManager.Instance?.Config?.Duplicant?.DefaultName == null)
            {
                Debug.LogError("[NeuroIntegrationManager] ConfigManager or DefaultName is null - cannot find duplicant!");
                return;
            }

            string configuredName = ConfigManager.Instance.Config.Duplicant.DefaultName;
            List<MinionIdentity> allMinions = Components.MinionIdentities.Items;

            // First try exact match with configured name (case-insensitive)
            neuroMinion = allMinions.FirstOrDefault(minion =>
                minion != null &&
                string.Equals(minion.GetProperName(), configuredName, StringComparison.OrdinalIgnoreCase));

            // If no exact match and configured name contains identifying text, search with partial match
            if (neuroMinion == null && configuredName.Length >= 4)
            {
                string lowerConfigName = configuredName.ToLower();
                neuroMinion = allMinions.FirstOrDefault(minion =>
                    minion != null &&
                    minion.GetProperName().ToLower().Contains(lowerConfigName));
            }

            if (neuroMinion != null)
            {
                Debug.Log($"[NeuroIntegrationManager] Found configured duplicate: {neuroMinion.GetProperName()}");
            }
            else
            {
                Debug.LogWarning($"[NeuroIntegrationManager] No duplicate matching '{configuredName}' found");

                // Fallback: use the first available minion if configured duplicate doesn't exist
                if (allMinions.Count > 0)
                {
                    neuroMinion = allMinions[0];
                    Debug.Log($"[NeuroIntegrationManager] Using fallback duplicate: {neuroMinion.GetProperName()}");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[NeuroIntegrationManager] Error finding configured minion: {ex.Message}");
        }
    }

    private void CreateIntegrationBridge()
    {
        try
        {
            // Create the integration bridge
            integrationBridge = gameObject.AddComponent<NeuroIntegrationBridge>();

            if (integrationBridge != null)
            {
                Debug.Log("[NeuroIntegrationManager] Integration bridge created successfully");
            }
            else
            {
                Debug.LogError("[NeuroIntegrationManager] Failed to create integration bridge");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[NeuroIntegrationManager] Error creating integration bridge: {ex.Message}");
        }
    }

    private void RegisterNeuroActions()
    {
        if (neuroMinion == null)
        {
            Debug.LogError("[NeuroIntegrationManager] Cannot register actions - Neuro minion not found");
            return;
        }

        try
        {
            Debug.Log("[NeuroIntegrationManager] Registering Neuro Actions...");

            // Clear any existing actions
            registeredActions.Clear();

            // Create all actions first
            List<INeuroAction> actionsToRegister = new()
            {
                new GetStatusAction(neuroMinion),
                new ClearCurrentErrandAction(neuroMinion),
                new GetNeuroScheduleAction(neuroMinion),
                new SetPriorityAction(neuroMinion),
                new SetNeuroScheduleAction(neuroMinion),
                new SetCustomScheduleAction(neuroMinion),
                new ListPrioritiesAction(neuroMinion),
                new GetAvailableSchedulesAction(),
                new GetBioDataAction(),
                // Errand actions (actual chores in the world)
                new ListErrandsAction(neuroMinion),
                new GetCurrentErrandAction(neuroMinion),
                new AssignErrandAction(neuroMinion)
            };

            // Register all actions at once to prevent duplicates
            NeuroActionHandler.RegisterActions(actionsToRegister);
            registeredActions.AddRange(actionsToRegister);

            Debug.Log($"[NeuroIntegrationManager] Successfully registered {registeredActions.Count} Neuro Actions");

            // Log all registered actions
            foreach (INeuroAction action in registeredActions)
            {
                Debug.Log($"[NeuroIntegrationManager] Registered action: {action.Name}");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[NeuroIntegrationManager] Error registering Neuro Actions: {ex.Message}");
        }
    }

    // Removed RegisterAction method - now using batch registration to prevent duplicates

    /// <summary>
    /// Force re-initialization of the integration system
    /// Useful when the Neuro duplicate is renamed or changes
    /// </summary>
    public void ForceReinitialize()
    {
        Debug.Log("[NeuroIntegrationManager] Forcing re-initialization...");

        isInitialized = false;
        neuroMinion = null;

        // Unregister existing actions
        UnregisterAllActions();

        // Destroy existing bridge
        if (integrationBridge != null)
        {
            Destroy(integrationBridge);
            integrationBridge = null;
        }

        // Re-initialize
        InitializeIntegration();
    }

    private void UnregisterAllActions()
    {
        try
        {
            if (registeredActions.Any())
            {
                Debug.Log($"[NeuroIntegrationManager] Unregistering {registeredActions.Count} actions: {string.Join(", ", registeredActions.Select(a => a.Name))}");
                NeuroActionHandler.UnregisterActions(registeredActions.Select(a => a.Name).ToArray());
            }
            registeredActions.Clear();
            Debug.Log("[NeuroIntegrationManager] Unregistered all actions");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[NeuroIntegrationManager] Error unregistering actions: {ex.Message}");
        }
    }

    /// <summary>
    /// Get the current Neuro duplicate
    /// </summary>
    /// <returns>The current Neuro minion, or null if not found</returns>
    public MinionIdentity? GetNeuroMinion()
    {
        return neuroMinion;
    }

    /// <summary>
    /// Check if the integration is active and working
    /// </summary>
    /// <returns>True if integration is fully active</returns>
    public bool IsIntegrationActive()
    {
        return isInitialized && neuroMinion != null && integrationBridge != null;
    }

    /// <summary>
    /// Get status information about the integration
    /// </summary>
    /// <returns>String describing the current integration status</returns>
    public string GetIntegrationStatus()
    {
        return !isInitialized
            ? "Integration not initialized"
            : neuroMinion == null
            ? "Neuro duplicate not found"
            : integrationBridge == null
            ? "Integration bridge not active"
            : $"Integration active - Connected to '{neuroMinion.GetProperName()}' with {registeredActions.Count} actions registered";
    }

    protected override void OnCleanUp()
    {
        // Clean up when the manager is destroyed
        UnregisterAllActions();

        if (integrationBridge != null)
        {
            Destroy(integrationBridge);
        }

        if (Instance == this)
        {
            Instance = null;
        }

        base.OnCleanUp();
    }

    /// <summary>
    /// Safely sends context message with fallback to Debug.Log if NeuroSdk context isn't available
    /// </summary>
    private void SafeSendContext(string message, bool isHighPriority)
    {
        NeuroLogger.SendContext(message, isHighPriority, "NeuroIntegrationManager");
    }
}

/// <summary>
/// Harmony patch to create the NeuroIntegrationManager when the game starts
/// </summary>
[HarmonyPatch(typeof(Game), "OnPrefabInit")]
public static class GameStartPatch
{
    private static void Postfix(Game __instance)
    {
        try
        {
            // Create the integration manager
            GameObject managerGO = new("NeuroIntegrationManager");
            managerGO.AddComponent<NeuroIntegrationManager>();

            // Don't destroy on load so it persists across scenes
            UnityEngine.Object.DontDestroyOnLoad(managerGO);

            Debug.Log("[GameStartPatch] NeuroIntegrationManager created and configured");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[GameStartPatch] Error creating NeuroIntegrationManager: {ex.Message}");
        }
    }
}

/// <summary>
/// Harmony patch to handle duplicate renaming and re-initialize if needed
/// </summary>
[HarmonyPatch(typeof(MinionIdentity), "SetName")]
public static class MinionRenamePatch
{
    private static void Postfix(MinionIdentity __instance, string name)
    {
        try
        {
            // Check if duplicate was renamed to configured name or if current configured duplicate was renamed
            if (NeuroIntegrationManager.Instance != null &&
                ConfigManager.Instance?.Config?.Duplicant?.DefaultName != null)
            {
                string configuredName = ConfigManager.Instance.Config.Duplicant.DefaultName;
                MinionIdentity? currentNeuro = NeuroIntegrationManager.Instance.GetNeuroMinion();

                // Check if this matches configured duplicant name (exact or contains) or is the current one
                bool nameMatches = string.Equals(name, configuredName, StringComparison.OrdinalIgnoreCase) ||
                                   (configuredName.Length >= 4 && name.ToLower().Contains(configuredName.ToLower()));

                if (nameMatches || __instance == currentNeuro)
                {
                    Debug.Log($"[MinionRenamePatch] Detected duplicate rename to '{name}' - checking integration");

                    // Delay re-initialization to allow the rename to complete
                    NeuroIntegrationManager.Instance.Invoke(nameof(NeuroIntegrationManager.ForceReinitialize), 1f);
                }
            }
        }
        catch (Exception ex)
        {
            NeuroLogger.LogException(ex, "duplicate rename handling", "MinionRenamePatch");
        }
    }
}