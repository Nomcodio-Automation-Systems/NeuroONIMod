using HarmonyLib;
using NeuroSdk.Websocket;
using System;

namespace NeuroMod;

public class Patches
{
    [HarmonyPatch(typeof(Db))]
    [HarmonyPatch("Initialize")]
    public class Db_Initialize_Patch
    {
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

        public static void Postfix()
        {
            Debug.Log("I execute after Db.Initialize!");

            try
            {
                // Get configuration for initialization
                ModConfig config = ConfigManager.Instance.Config;

                // Initialize systems based on configuration
                if (config.Game.ScheduleControlEnabled)
                {
                    Debug.Log("[NeuroMod] Schedule control system loaded!");
                }

                if (config.Duplicant.BioMonitoringEnabled)
                {
                    Debug.Log("[NeuroMod] Bio data monitoring system loaded!");
                }

                // Initialize Neuro SDK with configured settings
                InitializeNeuroSdk(config);

                // Initialize timeout management
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
        /// Initializes Neuro SDK with configuration settings
        /// </summary>
        private static void InitializeNeuroSdk(ModConfig config)
        {
            try
            {
                Debug.Log("[NeuroMod] Initializing Neuro SDK system...");

                // Initialize WebSocket connection with configured endpoint
                if (WebsocketConnection.Instance != null)
                {
                    Debug.Log("[NeuroMod] WebSocket connection instance found");

                    // Note: WebsocketConnection doesn't expose ConfigureConnection method
                    // Configuration should be handled through NeuroSdk initialization

                    // Auto-connect functionality would be handled by the SDK
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
        /// Initializes timeout management system
        /// </summary>
        private static void InitializeTimeoutManager(ModConfig config)
        {
            try
            {
                Debug.Log("[NeuroMod] Initializing timeout management system...");

                // Reset any existing timeouts
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
    /// Patch to instantiate NeuroScheduleManager when the game spawns
    /// OnSpawn is called after OnPrefabInit, ensuring all game systems are ready
    /// </summary>
    [HarmonyPatch(typeof(Game))]
    [HarmonyPatch("OnSpawn")]
    public class Game_OnSpawn_Patch
    {
        public static void Postfix(Game __instance)
        {
            if (__instance == null || __instance.gameObject == null)
            {
                Debug.LogError("[NeuroMod] Game instance or gameObject is null in OnSpawn patch!");
                return;
            }

            NeuroLogger.Log("Game.OnSpawn - Setting up NeuroScheduleManager", "ONIMod");

            // Check if manager already exists (prevent duplicates)
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
    /// Patch to ensure Neuro gets assigned to her schedule after minions spawn
    /// </summary>
    [HarmonyPatch(typeof(MinionIdentity))]
    [HarmonyPatch("OnSpawn")]
    public class MinionIdentity_OnSpawn_Patch
    {
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
                    // Can't check without config, skip
                    return;
                }

                string configuredName = ConfigManager.Instance.Config.Duplicant.DefaultName;

                if (__instance.GetProperName() == configuredName)
                {
                    NeuroLogger.Log($"Configured duplicant '{configuredName}' spawned! Assigning to dedicated schedule...", "ONIMod");

                    // Wait a frame for all components to initialize, then assign
                    __instance.StartCoroutine(AssignNeuroToScheduleDelayed());
                }
            }
            catch (Exception ex)
            {
                NeuroLogger.LogException(ex, "MinionIdentity_OnSpawn_Patch", "ONIMod");
            }
        }

        private static System.Collections.IEnumerator AssignNeuroToScheduleDelayed()
        {
            // Wait one frame to ensure all components are initialized
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

        /// <summary>
        /// Initializes Neuro SDK with configuration settings
        /// </summary>
        private static void InitializeNeuroSdk(ModConfig config)
        {
            try
            {
                Debug.Log("[NeuroMod] Initializing Neuro SDK system...");

                // Initialize WebSocket connection with configured endpoint
                if (WebsocketConnection.Instance != null)
                {
                    Debug.Log("[NeuroMod] WebSocket connection instance found");

                    // Note: WebsocketConnection doesn't expose ConfigureConnection method
                    // Configuration should be handled through NeuroSdk initialization

                    // Auto-connect functionality would be handled by the SDK
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
        /// Initializes timeout management system
        /// </summary>
        private static void InitializeTimeoutManager(ModConfig config)
        {
            try
            {
                Debug.Log("[NeuroMod] Initializing timeout management system...");

                // Reset any existing timeouts
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
    /// Enhanced ActiveController patches with configuration integration
    /// </summary>
    [HarmonyPatch(typeof(ActiveController))]
    public static class ActiveControllerPatches
    {
        /// <summary>
        /// Patch to override the InitializeStates method
        /// This is where the state machine behavior is defined
        /// </summary>
        [HarmonyPatch("InitializeStates")]
        [HarmonyPrefix]
        public static bool InitializeStates_Prefix(
            ActiveController __instance,
            out StateMachine.BaseState default_state)
        {
            // Your custom initialization logic here
            default_state = __instance.off;

            // Use configured behavior if available
            ModConfig config = ConfigManager.Instance.Config;
            if (config?.Game?.RealtimeDecisions == true)
            {
                CustomInitializeStatesWithNeuro(__instance);
            }
            else
            {
                CustomInitializeStates(__instance);
            }

            // Return false to skip the original method
            return false;
        }

        /// <summary>
        /// Custom initialization with Neuro integration and timeout handling
        /// </summary>
        private static void CustomInitializeStatesWithNeuro(ActiveController controller)
        {
            ModConfig config = ConfigManager.Instance.Config;

            controller.off
                .PlayAnim("off")
                .EventTransition(GameHashes.ActiveChanged, controller.working_pre,
                    (smi) => NeuroActiveCheck(smi));

            controller.working_pre
                .PlayAnim("working_pre")
                .OnAnimQueueComplete(controller.working_loop);

            controller.working_loop
                .PlayAnim("working_loop", KAnim.PlayMode.Loop)
                .EventTransition(GameHashes.ActiveChanged, controller.working_pst,
                    (smi) => NeuroInactiveCheck(smi));

            controller.working_pst
                .PlayAnim("working_pst")
                .OnAnimQueueComplete(controller.off);
        }

        /// <summary>
        /// Standard initialization without Neuro integration
        /// </summary>
        private static void CustomInitializeStates(ActiveController controller)
        {
            // Define basic state machine behavior
            controller.off
                .PlayAnim("off")
                .EventTransition(GameHashes.ActiveChanged, controller.working_pre,
                    (smi) => StandardActiveCheck(smi));

            controller.working_pre
                .PlayAnim("working_pre")
                .OnAnimQueueComplete(controller.working_loop);

            controller.working_loop
                .PlayAnim("working_loop")
                .EventTransition(GameHashes.ActiveChanged, controller.working_pst,
                    (smi) => !StandardActiveCheck(smi));

            controller.working_pst
                .PlayAnim("working_pst")
                .OnAnimQueueComplete(controller.off);
        }

        /// <summary>
        /// Standard active check without Neuro integration
        /// </summary>
        private static bool StandardActiveCheck(
            GameStateMachine<ActiveController, ActiveController.Instance, IStateMachineTarget, object>.Instance smi)
        {
            Operational operational = smi.GetComponent<Operational>();
            return operational != null && operational.IsActive;
        }

        /// <summary>
        /// Enhanced active check with Neuro integration and timeout handling
        /// </summary>
        private static bool NeuroActiveCheck(
            GameStateMachine<ActiveController, ActiveController.Instance, IStateMachineTarget, object>.Instance smi)
        {
            Operational operational = smi.GetComponent<Operational>();
            if (operational == null)
            {
                return false;
            }

            bool defaultResult = operational.IsActive;

            // Skip Neuro integration if in manual mode
            if (TimeoutManager.Instance.IsManualModeActive)
            {
                return defaultResult;
            }

            try
            {
                // Use timeout manager for Neuro decision
                System.Threading.Tasks.Task<bool> task = TimeoutManager.Instance.ExecuteWithTimeout(
                    "decision",
                    async () => await GetNeuroActiveDecision(smi, defaultResult),
                    () => GetFallbackActiveDecision(smi, defaultResult)
                );

                // For synchronous context, we use the default or wait briefly
                if (task.IsCompleted)
                {
                    return task.Result;
                }
                else
                {
                    // Return default immediately, Neuro decision will apply later
                    return defaultResult;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ActiveController] Neuro active check failed: {ex.Message}");
                return defaultResult;
            }
        }

        /// <summary>
        /// Gets Neuro's decision on whether the machine should be active
        /// </summary>
        private static async System.Threading.Tasks.Task<bool> GetNeuroActiveDecision(
            GameStateMachine<ActiveController, ActiveController.Instance, IStateMachineTarget, object>.Instance smi,
            bool defaultResult)
        {
            if (WebsocketConnection.Instance?.IsConnected != true)
            {
                return defaultResult;
            }

            // Send decision request to Neuro
            // This is where you'd integrate with your Neuro SDK
            // For now, return default
            await System.Threading.Tasks.Task.Delay(100); // Simulate async operation
            return defaultResult;
        }

        /// <summary>
        /// Fallback decision when Neuro times out or is unavailable
        /// </summary>
        private static bool GetFallbackActiveDecision(
            GameStateMachine<ActiveController, ActiveController.Instance, IStateMachineTarget, object>.Instance smi,
            bool defaultResult)
        {
            ModConfig config = ConfigManager.Instance.Config;
            string fallbackBehavior = config?.Duplicant?.FallbackBehavior ?? "idle";

            Debug.Log($"[ActiveController] Using fallback behavior: {fallbackBehavior}");

            return fallbackBehavior.ToLower() switch
            {
                "continue_task" => defaultResult,
                "emergency_protocol" => false, // Stop all operations
                "idle" => false, // Default to idle
                _ => defaultResult
            };
        }

        private static bool NeuroInactiveCheck(
            GameStateMachine<ActiveController, ActiveController.Instance, IStateMachineTarget, object>.Instance smi)
        {
            Operational operational = smi.GetComponent<Operational>();
            return operational == null || !operational.IsActive;
        }
    }

    /// <summary>
    /// Patches for ActiveController.Instance
    /// </summary>
    [HarmonyPatch(typeof(ActiveController.Instance))]
    public static class ActiveControllerInstancePatches
    {
        /// <summary>
        /// Patch the constructor if you need to modify instance creation
        /// </summary>
        [HarmonyPatch(MethodType.Constructor, [typeof(IStateMachineTarget), typeof(ActiveController.Def)])]
        [HarmonyPostfix]
        public static void Constructor_Postfix(
            ActiveController.Instance __instance,
            IStateMachineTarget master,
            ActiveController.Def def)
        {
            // Custom initialization for instances
            Debug.Log($"[ActiveController.Instance] Created instance for {master.name}");

            // Add custom components or setup here
            CustomInstanceSetup(__instance, master, def);
        }

        private static void CustomInstanceSetup(
            ActiveController.Instance instance,
            IStateMachineTarget master,
            ActiveController.Def def)
        {
            // Your custom instance setup logic
            UnityEngine.GameObject gameObject = master.gameObject;

            // Example: Add custom components
            // if (gameObject.GetComponent<YourCustomComponent>() == null)
            // {
            //     gameObject.AddComponent<YourCustomComponent>();
            // }

            // Example: Subscribe to custom events
            // instance.Subscribe(-1, OnCustomEvent);
        }
    }

    /// <summary>
    /// Advanced patches for additional state behavior
    /// </summary>
    [HarmonyPatch(typeof(ActiveController))]
    public static class ActiveControllerAdvancedPatches
    {
        /// <summary>
        /// Intercept state transitions to add custom behavior
        /// </summary>
        [HarmonyPatch("InitializeStates")]
        [HarmonyPostfix]
        public static void InitializeStates_Postfix(ActiveController __instance)
        {
            // Add custom behavior to existing states
            AddCustomStateHandlers(__instance);
        }

        private static void AddCustomStateHandlers(ActiveController controller)
        {
            // Add custom enter/exit behaviors to states
            controller.off
                .Enter((smi) => OnEnterOffState(smi))
                .Exit((smi) => OnExitOffState(smi));

            controller.working_pre
                .Enter((smi) => OnEnterWorkingPreState(smi))
                .Exit((smi) => OnExitWorkingPreState(smi));

            controller.working_loop
                .Enter((smi) => OnEnterWorkingLoopState(smi))
                .Exit((smi) => OnExitWorkingLoopState(smi))
                .Update((smi, dt) => OnUpdateWorkingLoopState(smi, dt), UpdateRate.SIM_200ms);

            controller.working_pst
                .Enter((smi) => OnEnterWorkingPstState(smi))
                .Exit((smi) => OnExitWorkingPstState(smi));
        }

        private static void OnEnterOffState(
            GameStateMachine<ActiveController, ActiveController.Instance, IStateMachineTarget, object>.Instance smi)
        {
            Debug.Log($"[ActiveController] {GetTargetName(smi)} entered OFF state");
            // Your custom logic when entering off state
        }

        private static void OnExitOffState(
            GameStateMachine<ActiveController, ActiveController.Instance, IStateMachineTarget, object>.Instance smi)
        {
            Debug.Log($"[ActiveController] {GetTargetName(smi)} exited OFF state");
            // Your custom logic when exiting off state
        }

        private static void OnEnterWorkingPreState(
            GameStateMachine<ActiveController, ActiveController.Instance, IStateMachineTarget, object>.Instance smi)
        {
            Debug.Log($"[ActiveController] {GetTargetName(smi)} entered WORKING_PRE state");
            // Your custom logic when starting work preparation
        }

        private static void OnExitWorkingPreState(
            GameStateMachine<ActiveController, ActiveController.Instance, IStateMachineTarget, object>.Instance smi)
        {
            Debug.Log($"[ActiveController] {GetTargetName(smi)} exited WORKING_PRE state");
            // Your custom logic when finishing work preparation
        }

        private static void OnEnterWorkingLoopState(
            GameStateMachine<ActiveController, ActiveController.Instance, IStateMachineTarget, object>.Instance smi)
        {
            Debug.Log($"[ActiveController] {GetTargetName(smi)} entered WORKING_LOOP state");
            // Your custom logic when starting main work loop
        }

        private static void OnUpdateWorkingLoopState(
            GameStateMachine<ActiveController, ActiveController.Instance, IStateMachineTarget, object>.Instance smi,
            float dt)
        {
            // Your custom logic that runs every 200ms during working loop
            // Example: Check custom conditions, modify behavior, etc.
        }

        private static void OnExitWorkingLoopState(
            GameStateMachine<ActiveController, ActiveController.Instance, IStateMachineTarget, object>.Instance smi)
        {
            Debug.Log($"[ActiveController] {GetTargetName(smi)} exited WORKING_LOOP state");
            // Your custom logic when stopping main work loop
        }

        private static void OnEnterWorkingPstState(
            GameStateMachine<ActiveController, ActiveController.Instance, IStateMachineTarget, object>.Instance smi)
        {
            Debug.Log($"[ActiveController] {GetTargetName(smi)} entered WORKING_PST state");
            // Your custom logic when starting work cleanup
        }

        private static void OnExitWorkingPstState(
            GameStateMachine<ActiveController, ActiveController.Instance, IStateMachineTarget, object>.Instance smi)
        {
            Debug.Log($"[ActiveController] {GetTargetName(smi)} exited WORKING_PST state");
            // Your custom logic when finishing work cleanup
        }

        private static string GetTargetName(
            GameStateMachine<ActiveController, ActiveController.Instance, IStateMachineTarget, object>.Instance smi)
        {
            try
            {
                return smi.gameObject?.name ?? "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }
    }
}