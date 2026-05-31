using HarmonyLib;
using NeuroMod.Integration;
using NeuroSdk.Websocket;
using System;

namespace NeuroMod;

/// <summary>
/// Core Harmony patches for NeuroMod initialization and duplicant spawn handling.
/// Split into partial classes across files:
///   - Patches.cs: Db.Initialize, Game.OnSpawn, MinionIdentity.OnSpawn
///   - ActiveControllerPatches.cs: ActiveController state machine patches
///   - ErrandPatches.cs: Chore precondition patches for errand locking
/// </summary>
/// <pre>Harmony patching is active during game startup and duplicant spawn lifecycles.</pre>
/// <post>NeuroMod startup, manager bootstrap, and duplicant-specific integration hooks run at the intended game entry points.</post>
public partial class Patches
{
    /// <summary>
    /// Hooks database initialization to bootstrap NeuroMod systems.
    /// </summary>
    /// <pre>The ONI database initialization lifecycle is about to run.</pre>
    /// <post>Configuration, SDK bootstrapping, and timeout-system initialization are integrated around the database lifecycle.</post>
    [HarmonyPatch(typeof(Db))]
    [HarmonyPatch("Initialize")]
    public class Db_Initialize_Patch
    {
        /// <summary>
        /// Loads configuration and initializes NeuroSdk before database initialization proceeds.
        /// </summary>
        /// <pre>Static game data has not finished initializing yet.</pre>
        /// <post>Configuration has been loaded or defaulted and NeuroSdk startup has been attempted.</post>
        public static void Prefix()
        {
            Debug.Log("I execute before Db.Initialize!");

            // Initialize NeuroMod logging
            NeuroLogger.Log("NeuroMod starting up...", "NeuroMod");
            NeuroLogger.Log($"Console output: {NeuroLogger.EnableConsoleOutput}", "NeuroMod");
            NeuroLogger.Log($"Debug logging: {NeuroLogger.EnableDebugLogging}", "NeuroMod");

            // Load configuration first
            if (!ConfigManager.Instance.LoadConfig())
            {
                NeuroLogger.LogWarning("Configuration loading failed, using defaults", "NeuroMod");
            }

            // Initialize NeuroSdk WebSocket connection
            NeuroLogger.Log("Initializing NeuroSdk WebSocket connection...", "NeuroMod");
            NeuroSdk.NeuroSdkSetup.Initialize("ONI");
            NeuroLogger.Log("NeuroSdk initialization complete", "NeuroMod");
        }

        /// <summary>
        /// Finishes NeuroMod subsystem initialization after the game database is ready.
        /// </summary>
        /// <pre>The game database and related singleton registries are available.</pre>
        /// <post>NeuroMod subsystems have been initialized or an initialization failure has been logged.</post>
        public static void Postfix()
        {
            Debug.Log("I execute after Db.Initialize!");

            try
            {
                ModConfig config = ConfigManager.Instance.Config;

                if (config.Game.ScheduleControlEnabled)
                {
                    Debug.Log("[NeuroMod] Schedule control system loaded!");
                }

                if (config.Duplicant.BioMonitoringEnabled)
                {
                    Debug.Log("[NeuroMod] Bio data monitoring system loaded!");
                }

                InitializeNeuroSdk(config);
                InitializeTimeoutManager(config);

                Debug.Log("[NeuroMod] All systems initialized successfully!");
                Debug.Log($"[NeuroMod] Duplicant name: {config.Duplicant.DefaultName}");
                Debug.Log($"[NeuroMod] Neuro endpoint: {config.Neuro.EndpointUrl}");
                Debug.Log("[NeuroMod] Use NeuroTestCommands.ListAvailableCommands() for testing");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NeuroMod] Initialization failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Applies configuration-dependent Neuro SDK startup behavior.
        /// </summary>
        /// <param name="config">The loaded mod configuration</param>
        /// <pre><paramref name="config"/> contains the runtime settings selected for the mod.</pre>
        /// <post>SDK-related initialization decisions have been logged and any available connection instance has been inspected.</post>
        private static void InitializeNeuroSdk(ModConfig config)
        {
            try
            {
                Debug.Log("[NeuroMod] Initializing Neuro SDK system...");

                if (WebsocketConnection.Instance != null)
                {
                    Debug.Log("[NeuroMod] WebSocket connection instance found");

                    if (config.Neuro.AutoReconnect)
                    {
                        Debug.Log("[NeuroMod] Auto-reconnect enabled - handled by NeuroSdk");
                    }
                }

                Debug.Log("[NeuroMod] Neuro SDK system ready for initialization!");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NeuroMod] Neuro SDK initialization failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Applies configuration-dependent timeout manager startup behavior.
        /// </summary>
        /// <param name="config">The loaded mod configuration</param>
        /// <pre><paramref name="config"/> contains the runtime timeout settings selected for the mod.</pre>
        /// <post>The timeout manager has been reset and the effective settings have been logged.</post>
        private static void InitializeTimeoutManager(ModConfig config)
        {
            try
            {
                Debug.Log("[NeuroMod] Initializing timeout management system...");

                TimeoutManager.Instance.ResetTimeoutCount();

                Debug.Log($"[NeuroMod] Timeout settings - Global: {config.Timeout.GlobalTimeout}s, " +
                         $"Decision: {config.Timeout.DecisionTimeout}s, " +
                         $"Action: {config.Timeout.ActionTimeout}s");

                Debug.Log("[NeuroMod] Timeout management system ready!");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NeuroMod] Timeout manager initialization failed: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Patch to instantiate NeuroScheduleManager when the game spawns.
    /// OnSpawn is called after OnPrefabInit, ensuring all game systems are ready.
    /// </summary>
    /// <pre>The main Game object is entering its spawn lifecycle.</pre>
    /// <post>A NeuroScheduleManager component exists on the Game object and has been initialized when possible.</post>
    [HarmonyPatch(typeof(Game))]
    [HarmonyPatch("OnSpawn")]
    public class Game_OnSpawn_Patch
    {
        /// <summary>
        /// Ensures the Game object owns a NeuroScheduleManager component.
        /// </summary>
        /// <param name="__instance">The Game instance</param>
        /// <pre><paramref name="__instance"/> is the spawned Game singleton.</pre>
        /// <post>The Game object has a NeuroScheduleManager component or a failure has been logged.</post>
        public static void Postfix(Game __instance)
        {
            if (__instance == null || __instance.gameObject == null)
            {
                Debug.LogError("[NeuroMod] Game instance or gameObject is null in OnSpawn patch!");
                return;
            }

            NeuroLogger.Log("Game.OnSpawn - Setting up NeuroScheduleManager", "ONIMod");

            NeuroScheduleManager existingManager = __instance.gameObject.GetComponent<NeuroScheduleManager>();
            if (existingManager == null)
            {
                NeuroScheduleManager manager = __instance.gameObject.AddComponent<NeuroScheduleManager>();
                NeuroLogger.Log("NeuroScheduleManager component added to Game object", "ONIMod");

                // Manually trigger initialization since OnSpawn won't be called automatically
                // when adding a component after the GameObject has already spawned
                manager.ManualInitialize();
            }
            else
            {
                Debug.Log("[NeuroMod] NeuroScheduleManager already exists, skipping creation");
            }
        }
    }

    /// <summary>
    /// Patch to ensure Neuro gets assigned to her schedule and ErrandMonitor after minions spawn.
    /// Combines schedule assignment and ErrandMonitor attachment in a single patch.
    /// </summary>
    /// <pre>MinionIdentity spawn events are firing for newly spawned duplicates.</pre>
    /// <post>The configured Neuro duplicant receives schedule assignment refresh and ErrandMonitor attachment when identified.</post>
    [HarmonyPatch(typeof(MinionIdentity))]
    [HarmonyPatch("OnSpawn")]
    public class MinionIdentity_OnSpawn_Patch
    {
        /// <summary>
        /// Identifies the configured Neuro duplicant, attaches monitoring, and refreshes schedule assignment.
        /// </summary>
        /// <param name="__instance">The spawned MinionIdentity</param>
        /// <pre><paramref name="__instance"/> is the duplicate whose spawn has just completed.</pre>
        /// <post>If the duplicate matches the configured Neuro target, monitoring and schedule-refresh work has been scheduled.</post>
        public static void Postfix(MinionIdentity __instance)
        {
            try
            {
                if (__instance == null)
                {
                    Debug.LogWarning("[NeuroMod] MinionIdentity instance is null in OnSpawn patch");
                    return;
                }

                // Get configured duplicant name - NO hardcoded defaults
                if (ConfigManager.Instance?.Config?.Duplicant?.DefaultName == null)
                {
                    return;
                }

                string configuredName = ConfigManager.Instance.Config.Duplicant.DefaultName;

                string minionName = __instance.GetProperName();
                bool isNeuro = string.Equals(minionName, configuredName, StringComparison.OrdinalIgnoreCase) ||
                               (configuredName.Length >= 4 && minionName.ToLower().Contains(configuredName.ToLower()));

                if (isNeuro)
                {
                    NeuroLogger.Log($"Configured duplicant '{configuredName}' spawned! Assigning to dedicated schedule...", "ONIMod");

                    // Attach ErrandMonitor if not already present
                    if (__instance.GetComponent<ErrandMonitor>() == null)
                    {
                        __instance.gameObject.AddComponent<ErrandMonitor>();
                        NeuroLogger.Log($"Attached ErrandMonitor to Neuro duplicant: {minionName}", "ErrandPatch");
                    }

                    // Wait a frame for all components to initialize, then assign
                    __instance.StartCoroutine(AssignNeuroToScheduleDelayed());
                }
            }
            catch (Exception ex)
            {
                NeuroLogger.LogException(ex, "MinionIdentity_OnSpawn_Patch", "ONIMod");
            }
        }

        /// <summary>
        /// Delays schedule assignment until the next frame so dependent components are ready.
        /// </summary>
        /// <returns>An enumerator that waits one frame before refreshing the Neuro assignment.</returns>
        /// <pre>The configured Neuro duplicant has just spawned and the schedule manager may not yet be fully ready.</pre>
        /// <post>After one frame, the Neuro schedule assignment has been refreshed when the manager is available.</post>
        private static System.Collections.IEnumerator AssignNeuroToScheduleDelayed()
        {
            yield return null;

            if (NeuroScheduleManager.Instance != null)
            {
                NeuroScheduleManager.Instance.RefreshNeuroAssignment();
            }
            else
            {
                NeuroLogger.LogWarning("NeuroScheduleManager not found when trying to assign Neuro!", "ONIMod");
            }
        }
    }
}