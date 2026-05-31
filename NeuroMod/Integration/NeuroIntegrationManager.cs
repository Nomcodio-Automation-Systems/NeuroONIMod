using HarmonyLib;
using NeuroSdk.Actions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using UnityEngine;

namespace NeuroMod;

/// <summary>
/// Main integration manager that coordinates between ONI duplicate systems and Neuro SDK
/// Registers all Neuro Actions and manages the integration bridge
/// </summary>
/// <pre>
/// The game has initialized enough colony state for the configured duplicant and SDK action system to be available.
/// </pre>
/// <post>
/// The manager can locate the configured duplicant, register production actions, and keep the integration bridge alive.
/// </post>
public class NeuroIntegrationManager : KMonoBehaviour
{
    public static NeuroIntegrationManager? Instance { get; private set; }

    private NeuroIntegrationBridge? integrationBridge;
    private MinionIdentity? neuroMinion;
    private bool isInitialized = false;

    // Registered Neuro Actions
    private readonly List<INeuroAction> registeredActions = new List<INeuroAction>();

    protected override void OnPrefabInit()
    {
        base.OnPrefabInit();
        Instance = this;
        NeuroLogger.Log("Prefab initialized", "NeuroIntegrationManager");
    }

    protected override void OnSpawn()
    {
        base.OnSpawn();

        // Resend registered actions whenever the WebSocket (re)connects so the Neuro server
        // always has a full action list even if registration fired before the connection was open.
        if (NeuroSdk.Websocket.WebsocketConnection.Instance != null)
        {
            NeuroSdk.Websocket.WebsocketConnection.Instance.onConnected?.AddListener(OnWebSocketConnected);
        }

        // Initialize the integration when the manager spawns
        InitializeIntegration();
    }

    private void OnWebSocketConnected()
    {
        NeuroLogger.Log("WebSocket connected – resending registered actions", "NeuroIntegrationManager");
        NeuroActionHandler.ResendRegisteredActions();
    }

    private void InitializeIntegration()
    {
        if (isInitialized)
        {
            return;
        }

        try
        {
            Stopwatch sw = Stopwatch.StartNew();
            NeuroLogger.Log("Starting integration initialization...", "NeuroIntegrationManager");

            // Find the Neuro duplicate
            FindNeuroMinion();

            if (neuroMinion != null)
            {
                // Create and initialize the integration bridge
                CreateIntegrationBridge();

                // Register all Neuro Actions
                RegisterNeuroActions();

                // Attach eating tracker so meal history is collected from now on.
                EatingTracker.Attach(neuroMinion);

                // Mark as initialized
                isInitialized = true;

                // Send initial context message
                string welcomeMessage = $"Neuro integration system activated! Connected to duplicate '{neuroMinion.GetProperName()}'. " +
                    "AI can now monitor bio data, control tasks, manage schedules, and query status in real-time.";

                // Use safe context sending with fallback
                SafeSendContext(welcomeMessage, true);

                NeuroLogger.Log($"Integration initialization completed successfully in {sw.ElapsedMilliseconds}ms", "NeuroIntegrationManager");
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
                NeuroLogger.LogError("ConfigManager or DefaultName is null - cannot find duplicant!", "NeuroIntegrationManager");
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
                NeuroLogger.Log($"Found configured duplicate: {neuroMinion.GetProperName()}", "NeuroIntegrationManager");
            }
            else
            {
                NeuroLogger.LogWarning($"No duplicate matching '{configuredName}' found", "NeuroIntegrationManager");

                // Fallback: use the first available minion if configured duplicate doesn't exist
                if (allMinions.Count > 0)
                {
                    neuroMinion = allMinions[0];
                    NeuroLogger.Log($"Using fallback duplicate: {neuroMinion.GetProperName()}", "NeuroIntegrationManager");
                }
            }
        }
        catch (Exception ex)
        {
            NeuroLogger.LogError($"Error finding configured minion: {ex.Message}", "NeuroIntegrationManager");
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
                NeuroLogger.Log("Integration bridge created successfully", "NeuroIntegrationManager");
            }
            else
            {
                NeuroLogger.LogError("Failed to create integration bridge", "NeuroIntegrationManager");
            }
        }
        catch (System.Exception ex)
        {
            NeuroLogger.LogError($"Error creating integration bridge: {ex.Message}", "NeuroIntegrationManager");
        }
    }

    private void RegisterNeuroActions()
    {
        if (neuroMinion == null)
        {
            NeuroLogger.LogError("Cannot register actions - Neuro minion not found", "NeuroIntegrationManager");
            return;
        }

        NeuroLogger.Log("Registering Neuro Actions...", "NeuroIntegrationManager");

        // Clear any existing actions
        registeredActions.Clear();

        // Build each action individually so a single constructor failure does not prevent the
        // remaining actions from being created and registered.
        var factories = new List<(string name, Func<INeuroAction> factory)>
        {
            // Status
            ("GetStatusAction",           () => new GetStatusAction(neuroMinion)),
            // Time / game speed
            ("GetGameSpeedAction",        () => new GetGameSpeedAction()),
            ("SetGameSpeedAction",        () => new SetGameSpeedAction()),
            // Priority actions
            ("ListPrioritiesAction",      () => new ListPrioritiesAction(neuroMinion)),
            ("SetPriorityAction",         () => new SetPriorityAction(neuroMinion)),
            // Schedule actions
            ("GetAvailableSchedulesAction", () => new GetAvailableSchedulesAction()),
            ("GetNeuroScheduleAction",    () => new GetNeuroScheduleAction(neuroMinion)),
            ("SetNeuroScheduleAction",    () => new SetNeuroScheduleAction(neuroMinion)),
            ("SetCustomScheduleAction",   () => new SetCustomScheduleAction(neuroMinion)),
            // Errand actions
            ("ListErrandsAction",         () => new ListErrandsAction(neuroMinion)),
            ("AssignErrandAction",        () => new AssignErrandAction(neuroMinion)),
            ("GetCurrentErrandAction",    () => new GetCurrentErrandAction(neuroMinion)),
            ("GetErrandProgressAction",   () => new GetErrandProgressAction(neuroMinion)),
            ("GetErrandPickupStatusAction", () => new GetErrandPickupStatusAction(neuroMinion)),
            ("ClearCurrentErrandAction",  () => new ClearCurrentErrandAction(neuroMinion)),
            // Colony discovery
            ("ListDuplicantsAction",      () => new ListDuplicantsAction()),
            ("GetDuplicantInfoAction",    () => new GetDuplicantInfoAction()),
            ("GetColonyInfoAction",       () => new GetColonyInfoAction()),
            ("ListResourcesAction",       () => new ListResourcesAction()),
            // Colony world state
            ("GetNotificationsAction",    () => new GetNotificationsAction()),
            ("SetNotificationAction",     () => new SetNotificationAction()),
            ("GetGeysersAction",          () => new GetGeysersAction()),
            ("GetPowerStatusAction",      () => new GetPowerStatusAction()),
            ("GetCurrentResearchAction",  () => new GetCurrentResearchAction()),
            // Morale & thoughts
            ("GetMoraleSourcesAction",    () => new GetMoraleSourcesAction(neuroMinion)),
            ("GetDuplicantThoughtsAction", () => new GetDuplicantThoughtsAction(neuroMinion)),
            // Eating / nutrition
            ("GetEatingInfoAction",       () => new GetEatingInfoAction(neuroMinion)),
            // Announcements
            ("TriggerAnnouncementAction", () => new TriggerAnnouncementAction()),
            // In-game Codex (wiki) reader
            ("ListWikiCategoriesAction",  () => new ListWikiCategoriesAction()),
            ("SearchWikiAction",          () => new SearchWikiAction()),
            ("GetWikiEntryAction",        () => new GetWikiEntryAction()),
        };

        var actionsToRegister = new List<INeuroAction>(factories.Count);
        foreach (var (name, factory) in factories)
        {
            try
            {
                actionsToRegister.Add(factory());
            }
            catch (System.Exception ex)
            {
                NeuroLogger.LogError($"Failed to create action '{name}': {ex.Message}", "NeuroIntegrationManager");
            }
        }

        try
        {
            NeuroActionHandler.RegisterActions(actionsToRegister);
            registeredActions.AddRange(actionsToRegister);

            NeuroLogger.Log($"Successfully registered {registeredActions.Count} of {factories.Count} Neuro Actions", "NeuroIntegrationManager");
            foreach (INeuroAction action in registeredActions)
                NeuroLogger.Log($"Registered action: {action.Name}", "NeuroIntegrationManager");

            // If the WebSocket is already open when registration completes (i.e. the connection
            // was established before InitializeIntegration finished), force-resend the action list
            // now so the Neuro server receives it. The onConnected listener covers future reconnects.
            if (NeuroSdk.Websocket.WebsocketConnection.Instance?.IsConnected == true)
            {
                NeuroLogger.Log("WebSocket already connected – resending actions after registration", "NeuroIntegrationManager");
                NeuroActionHandler.ResendRegisteredActions();
            }
        }
        catch (System.Exception ex)
        {
            NeuroLogger.LogError($"Error registering Neuro Actions: {ex.Message}", "NeuroIntegrationManager");
        }
    }

    // Removed RegisterAction method - now using batch registration to prevent duplicates

    /// <summary>
    /// Force re-initialization of the integration system
    /// Useful when the Neuro duplicate is renamed or changes
    /// </summary>
    /// <pre>
    /// The manager may already hold active bridge and action registrations.
    /// </pre>
    /// <post>
    /// Existing integration state is torn down and rebuilt against the current duplicant selection.
    /// </post>
    public void ForceReinitialize()
    {
        NeuroLogger.Log("Forcing re-initialization...", "NeuroIntegrationManager");

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
                NeuroLogger.Log($"Unregistering {registeredActions.Count} actions: {string.Join(", ", registeredActions.Select(a => a.Name))}", "NeuroIntegrationManager");
                NeuroActionHandler.UnregisterActions(registeredActions.Select(a => a.Name).ToArray());
            }
            registeredActions.Clear();
            NeuroLogger.Log("Unregistered all actions", "NeuroIntegrationManager");
        }
        catch (System.Exception ex)
        {
            NeuroLogger.LogError($"Error unregistering actions: {ex.Message}", "NeuroIntegrationManager");
        }
    }

    /// <summary>
    /// Get the current Neuro duplicate
    /// </summary>
    /// <returns>The current Neuro minion, or null if not found</returns>
    /// <pre>
    /// Initialization may still be in progress or may have failed to find the configured duplicant.
    /// </pre>
    /// <post>
    /// The currently tracked Neuro duplicant reference is returned.
    /// </post>
    public MinionIdentity? GetNeuroMinion()
    {
        return neuroMinion;
    }

    /// <summary>
    /// Check if the integration is active and working
    /// </summary>
    /// <returns>True if integration is fully active</returns>
    /// <pre>
    /// The manager may have partially initialized bridge or action state.
    /// </pre>
    /// <post>
    /// The method reports whether all required integration components are active.
    /// </post>
    public bool IsIntegrationActive()
    {
        return isInitialized && neuroMinion != null && integrationBridge != null;
    }

    /// <summary>
    /// Get status information about the integration
    /// </summary>
    /// <returns>String describing the current integration status</returns>
    /// <pre>
    /// The manager may be initialized, partially initialized, or inactive.
    /// </pre>
    /// <post>
    /// A human-readable status string describing the current integration state is returned.
    /// </post>
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
        EatingTracker.Detach();
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
    /// <pre>
    /// <paramref name="message"/> is intended for the out-of-band context channel.
    /// </pre>
    /// <post>
    /// The integration manager has attempted to deliver the context update through the logger facade.
    /// </post>
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

                    NeuroLogger.Log("NeuroIntegrationManager created and configured", "GameStartPatch");
        }
        catch (Exception ex)
        {
                NeuroLogger.LogError($"Error creating NeuroIntegrationManager: {ex.Message}", "GameStartPatch");
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
                    NeuroLogger.Log($"Detected duplicate rename to '{name}' - checking integration", "MinionRenamePatch");

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