using HarmonyLib;
using NeuroMod.Integration;
using System;

namespace NeuroMod;

/// <summary>
/// Harmony patches for errand-related chore preconditions.
/// Ensures assigned errands are completed before the duplicant picks up other work.
/// Also prevents other duplicants from stealing reserved chores.
/// </summary>
/// <pre>Harmony patching is active for chore precondition registration.</pre>
/// <post>Reserved Neuro errands receive additional runtime guards during chore evaluation.</post>
public partial class Patches
{
    /// <summary>
    /// Precondition that prevents non-Neuro duplicants from taking reserved chores.
    /// When a chore is reserved via ErrandReservationHelper, only Neuro can pick it up.
    /// </summary>
    private static readonly Chore.Precondition IS_NOT_RESERVED_FOR_NEURO = new Chore.Precondition
    {
        id = "NeuroMod.IsNotReservedForNeuro",
        description = "This errand is reserved for Neuro",
        fn = CheckReservation
    };

    /// <summary>
    /// Checks whether a chore is reserved and whether the consumer is the Neuro duplicant.
    /// If reserved and consumer is NOT Neuro, blocks the chore.
    /// </summary>
    /// <param name="context">The chore precondition context</param>
    /// <param name="data">Unused</param>
    /// <returns>True if the chore is allowed, false if reserved for Neuro</returns>
    /// <pre><paramref name="context"/> contains the chore and consumer currently being evaluated.</pre>
    /// <post>The result is <see langword="false"/> only when another duplicant tries to take a chore reserved for Neuro.</post>
    private static bool CheckReservation(ref Chore.Precondition.Context context, object data)
    {
        // If the chore isn't reserved, allow it
        if (!ErrandReservationHelper.IsReserved(context.chore))
        {
            return true;
        }

        // Chore is reserved — check if this consumer is the Neuro duplicant
        // Neuro has an ErrandMonitor component, others don't
        if (context.consumerState?.choreDriver != null &&
            context.consumerState.choreDriver.TryGetComponent(out ErrandMonitor _))
        {
            return true; // Neuro can always take reserved chores
        }

        // Another duplicant trying to take a reserved chore — block it
        return false;
    }

    /// <summary>
    /// Patch to add the CAN_DO_NEURO_ASSIGNED precondition to Work-type chores.
    /// When a Neuro errand is assigned, this precondition blocks the duplicant from
    /// starting other work chores — ensuring the assigned errand gets finished.
    /// Also adds reservation precondition to prevent other dupes from stealing reserved chores.
    /// Similar to FinishTasks' StandardChoreBase_AddPrecondition_Patch pattern.
    /// </summary>
    /// <pre>Work-type chores are being configured through <see cref="Chore.AddPrecondition"/>.</pre>
    /// <post>Matching work chores also carry Neuro-specific lock and reservation preconditions.</post>
    [HarmonyPatch(typeof(StandardChoreBase), nameof(Chore.AddPrecondition))]
    public static class StandardChoreBase_NeuroAssigned_Patch
    {
        /// <summary>
        /// Cache the IsScheduledTime precondition ID for comparison.
        /// </summary>
        private static string? _isScheduledTimeId;

        /// <summary>
        /// Runs after AddPrecondition — if this is a Work-type chore, adds the Neuro preconditions.
        /// </summary>
        /// <param name="__instance">The chore being configured</param>
        /// <param name="precondition">The precondition being added</param>
        /// <param name="data">The data associated with the precondition</param>
        /// <pre><paramref name="__instance"/> is undergoing precondition registration for a work chore candidate.</pre>
        /// <post>Matching work chores receive Neuro assignment and reservation guards exactly once through normal chore setup.</post>
        internal static void Postfix(Chore __instance, Chore.Precondition precondition, object data)
        {
            try
            {
                // Cache the scheduled time ID on first use
                _isScheduledTimeId ??= ChorePreconditions.instance?.IsScheduledTime.id;
                if (string.IsNullOrEmpty(_isScheduledTimeId)) return;

                // Only add to Work-type chores (identified by IsScheduledTime + Work block type)
                if (precondition.id != _isScheduledTimeId) return;
                if (data is not ScheduleBlockType blockType) return;

                ScheduleBlockType? workType = Db.Get()?.ScheduleBlockTypes?.Work;
                if (workType == null || blockType != workType) return;

                // Add Neuro's lock precondition (prevents Neuro from abandoning assigned errand)
                __instance.AddPrecondition(ErrandMonitor.CAN_DO_NEURO_ASSIGNED, __instance);

                // Add reservation precondition (prevents other dupes from stealing reserved chores)
                __instance.AddPrecondition(IS_NOT_RESERVED_FOR_NEURO, __instance);
            }
            catch (Exception ex)
            {
                NeuroLogger.LogException(ex, "adding Neuro preconditions to chore", "ErrandPatch");
            }
        }
    }
}
