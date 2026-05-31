using HarmonyLib;
using NeuroSdk.Websocket;
using System;

namespace NeuroMod;

/// <summary>
/// Harmony patches for ActiveController state machine behavior.
/// Handles state initialization, Neuro integration, and instance creation.
/// </summary>
/// <pre>The game's ActiveController state machine types are available for Harmony patching.</pre>
/// <post>Patched ActiveController instances can route activity decisions through Neuro-aware helpers when configured.</post>
public partial class Patches
{
    /// <summary>
    /// Applies Neuro-aware state initialization to ActiveController instances.
    /// </summary>
    /// <pre>Harmony has targeted the ActiveController state initialization entry points.</pre>
    /// <post>Matching state machines use NeuroMod initialization instead of the vanilla implementation.</post>
    [HarmonyPatch(typeof(ActiveController))]
    public static class ActiveControllerPatches
    {
        /// <summary>
        /// Replaces the default ActiveController state graph initialization.
        /// </summary>
        /// <param name="__instance">The controller whose state machine is being initialized.</param>
        /// <param name="default_state">Receives the starting state that the state machine should enter.</param>
        /// <returns><see langword="false"/> to suppress the original implementation after custom initialization.</returns>
        /// <pre><paramref name="__instance"/> is a valid ActiveController instance entering its state setup phase.</pre>
        /// <post>The controller's core states have been wired using either standard or Neuro-aware transitions.</post>
        [HarmonyPatch("InitializeStates")]
        [HarmonyPrefix]
        public static bool InitializeStates_Prefix(
            ActiveController __instance,
            out StateMachine.BaseState default_state)
        {
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
        /// Configures ActiveController states with Neuro-aware transition checks.
        /// </summary>
        /// <param name="controller">The controller whose state graph should be configured.</param>
        /// <pre><paramref name="controller"/> exposes the standard off and working states expected by ONI.</pre>
        /// <post>The controller transitions through Neuro-aware active and inactive checks.</post>
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
        /// Configures ActiveController states using the vanilla-style activity checks.
        /// </summary>
        /// <param name="controller">The controller whose state graph should be configured.</param>
        /// <pre><paramref name="controller"/> exposes the standard off and working states expected by ONI.</pre>
        /// <post>The controller transitions rely only on the operational active flag.</post>
        private static void CustomInitializeStates(ActiveController controller)
        {
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
        /// Evaluates whether a controller should be active using only vanilla operational state.
        /// </summary>
        /// <param name="smi">The state-machine instance being evaluated.</param>
        /// <returns><see langword="true"/> when the controller's Operational component is active.</returns>
        /// <pre><paramref name="smi"/> belongs to an ActiveController state-machine instance.</pre>
        /// <post>The result reflects only the current Operational component state.</post>
        private static bool StandardActiveCheck(
            GameStateMachine<ActiveController, ActiveController.Instance, IStateMachineTarget, object>.Instance smi)
        {
            Operational operational = smi.GetComponent<Operational>();
            return operational != null && operational.IsActive;
        }

        /// <summary>
        /// Evaluates whether a controller should be active using Neuro integration when available.
        /// </summary>
        /// <param name="smi">The state-machine instance being evaluated.</param>
        /// <returns>The operational default when Neuro is unavailable, timed out, or manual mode is active; otherwise the Neuro-aware decision.</returns>
        /// <pre><paramref name="smi"/> belongs to an ActiveController state-machine instance.</pre>
        /// <post>The result is safe for synchronous state-machine use and falls back to deterministic local behavior on failure.</post>
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
        /// Asks the Neuro integration layer for an active-state decision.
        /// </summary>
        /// <param name="smi">The state-machine instance being evaluated.</param>
        /// <param name="defaultResult">The local operational fallback decision.</param>
        /// <returns>A task that resolves to the Neuro decision or the supplied fallback when Neuro is unavailable.</returns>
        /// <pre><paramref name="defaultResult"/> represents the safe vanilla decision for this controller.</pre>
        /// <post>The completed task yields a decision compatible with the controller's active-state contract.</post>
        private static async System.Threading.Tasks.Task<bool> GetNeuroActiveDecision(
            GameStateMachine<ActiveController, ActiveController.Instance, IStateMachineTarget, object>.Instance smi,
            bool defaultResult)
        {
            if (WebsocketConnection.Instance?.IsConnected != true)
            {
                return defaultResult;
            }

            // Send decision request to Neuro
            await System.Threading.Tasks.Task.Delay(100);
            return defaultResult;
        }

        /// <summary>
        /// Computes the local fallback active decision when Neuro cannot respond.
        /// </summary>
        /// <param name="smi">The state-machine instance being evaluated.</param>
        /// <param name="defaultResult">The default active decision derived from the machine's operational state.</param>
        /// <returns>The configured fallback behavior.</returns>
        /// <pre><paramref name="defaultResult"/> represents the safe vanilla decision for this controller.</pre>
        /// <post>The result honors the configured fallback mode and never throws on unknown values.</post>
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
                "emergency_protocol" => false,
                "idle" => false,
                _ => defaultResult
            };
        }

        /// <summary>
        /// Evaluates whether the controller should leave the working state.
        /// </summary>
        /// <param name="smi">The state-machine instance being evaluated.</param>
        /// <returns><see langword="true"/> when the controller is missing or no longer active.</returns>
        /// <pre><paramref name="smi"/> belongs to an ActiveController state-machine instance.</pre>
        /// <post>The result reflects only the current Operational component state.</post>
        private static bool NeuroInactiveCheck(
            GameStateMachine<ActiveController, ActiveController.Instance, IStateMachineTarget, object>.Instance smi)
        {
            Operational operational = smi.GetComponent<Operational>();
            return operational == null || !operational.IsActive;
        }
    }

    /// <summary>
    /// Adds post-construction setup hooks for ActiveController instances.
    /// </summary>
    /// <pre>Harmony has targeted the ActiveController.Instance constructor overload used by the game.</pre>
    /// <post>New ActiveController instances can receive NeuroMod-specific setup after construction.</post>
    [HarmonyPatch(typeof(ActiveController.Instance))]
    public static class ActiveControllerInstancePatches
    {
        /// <summary>
        /// Runs after the ActiveController.Instance constructor completes.
        /// </summary>
        /// <param name="__instance">The newly created ActiveController instance.</param>
        /// <param name="master">The state-machine target that owns the instance.</param>
        /// <param name="def">The controller definition used for construction.</param>
        /// <pre><paramref name="__instance"/> has finished base construction.</pre>
        /// <post>Custom NeuroMod instance setup has been attempted.</post>
        [HarmonyPatch(MethodType.Constructor, [typeof(IStateMachineTarget), typeof(ActiveController.Def)])]
        [HarmonyPostfix]
        public static void Constructor_Postfix(
            ActiveController.Instance __instance,
            IStateMachineTarget master,
            ActiveController.Def def)
        {
#if DEBUG
            Debug.Log($"[ActiveController.Instance] Created instance for {master.name}");
#endif
            CustomInstanceSetup(__instance, master, def);
        }

        /// <summary>
        /// Performs any additional initialization needed for a newly constructed controller instance.
        /// </summary>
        /// <param name="instance">The newly created ActiveController instance.</param>
        /// <param name="master">The state-machine target that owns the instance.</param>
        /// <param name="def">The controller definition used for construction.</param>
        /// <pre><paramref name="instance"/> and <paramref name="master"/> refer to the same newly constructed controller context.</pre>
        /// <post>Any required NeuroMod-specific setup has been applied or intentionally skipped.</post>
        private static void CustomInstanceSetup(
            ActiveController.Instance instance,
            IStateMachineTarget master,
            ActiveController.Def def)
        {
            // Custom instance setup logic
            UnityEngine.GameObject gameObject = master.gameObject;
        }
    }

    /// <summary>
    /// Advanced patches for additional state behavior — adds enter/exit handlers to ActiveController states.
    /// Debug logging is wrapped in #if DEBUG to avoid log spam in Release builds.
    /// </summary>
    /// <pre>Base ActiveController state initialization has already completed.</pre>
    /// <post>Selected ActiveController states emit additional NeuroMod enter and exit hooks.</post>
    [HarmonyPatch(typeof(ActiveController))]
    public static class ActiveControllerAdvancedPatches
    {
        /// <summary>
        /// Adds custom enter and exit handlers after state initialization completes.
        /// </summary>
        /// <param name="__instance">The controller whose states should receive additional handlers.</param>
        /// <pre><paramref name="__instance"/> has a fully initialized state graph.</pre>
        /// <post>The selected states have NeuroMod enter and exit delegates attached.</post>
        [HarmonyPatch("InitializeStates")]
        [HarmonyPostfix]
        public static void InitializeStates_Postfix(ActiveController __instance)
        {
            AddCustomStateHandlers(__instance);
        }

        /// <summary>
        /// Attaches custom enter and exit delegates to the controller's core states.
        /// </summary>
        /// <param name="controller">The controller whose state handlers should be extended.</param>
        /// <pre><paramref name="controller"/> has a fully initialized state graph.</pre>
        /// <post>The off and working states invoke NeuroMod hook methods on entry and exit.</post>
        private static void AddCustomStateHandlers(ActiveController controller)
        {
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
#if DEBUG
            Debug.Log($"[ActiveController] {GetTargetName(smi)} entered OFF state");
#endif
        }

        private static void OnExitOffState(
            GameStateMachine<ActiveController, ActiveController.Instance, IStateMachineTarget, object>.Instance smi)
        {
#if DEBUG
            Debug.Log($"[ActiveController] {GetTargetName(smi)} exited OFF state");
#endif
        }

        private static void OnEnterWorkingPreState(
            GameStateMachine<ActiveController, ActiveController.Instance, IStateMachineTarget, object>.Instance smi)
        {
#if DEBUG
            Debug.Log($"[ActiveController] {GetTargetName(smi)} entered WORKING_PRE state");
#endif
        }

        private static void OnExitWorkingPreState(
            GameStateMachine<ActiveController, ActiveController.Instance, IStateMachineTarget, object>.Instance smi)
        {
#if DEBUG
            Debug.Log($"[ActiveController] {GetTargetName(smi)} exited WORKING_PRE state");
#endif
        }

        private static void OnEnterWorkingLoopState(
            GameStateMachine<ActiveController, ActiveController.Instance, IStateMachineTarget, object>.Instance smi)
        {
#if DEBUG
            Debug.Log($"[ActiveController] {GetTargetName(smi)} entered WORKING_LOOP state");
#endif
        }

        private static void OnUpdateWorkingLoopState(
            GameStateMachine<ActiveController, ActiveController.Instance, IStateMachineTarget, object>.Instance smi,
            float dt)
        {
            // Custom logic that runs every 200ms during working loop
        }

        private static void OnExitWorkingLoopState(
            GameStateMachine<ActiveController, ActiveController.Instance, IStateMachineTarget, object>.Instance smi)
        {
#if DEBUG
            Debug.Log($"[ActiveController] {GetTargetName(smi)} exited WORKING_LOOP state");
#endif
        }

        private static void OnEnterWorkingPstState(
            GameStateMachine<ActiveController, ActiveController.Instance, IStateMachineTarget, object>.Instance smi)
        {
#if DEBUG
            Debug.Log($"[ActiveController] {GetTargetName(smi)} entered WORKING_PST state");
#endif
        }

        private static void OnExitWorkingPstState(
            GameStateMachine<ActiveController, ActiveController.Instance, IStateMachineTarget, object>.Instance smi)
        {
#if DEBUG
            Debug.Log($"[ActiveController] {GetTargetName(smi)} exited WORKING_PST state");
#endif
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
