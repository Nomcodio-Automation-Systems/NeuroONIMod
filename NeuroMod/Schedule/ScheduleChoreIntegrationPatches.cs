using HarmonyLib;
using System;
using System.Linq;

namespace NeuroMod;

/// <summary>
/// Patches to integrate schedule control with the chore system
/// </summary>
[HarmonyPatch]
public static class ScheduleChoreIntegrationPatches
{
    /// <summary>
    /// Patch ChoreConsumer.IsPermittedByUser to respect schedule overrides
    /// </summary>
    [HarmonyPatch(typeof(ChoreConsumer), "IsPermittedByUser")]
    [HarmonyPrefix]
    public static bool IsPermittedByUser_Prefix(
        ChoreConsumer __instance,
        ChoreGroup chore_group,
        ref bool __result)
    {
        Schedulable? schedulable = __instance.GetComponent<Schedulable>();
        if (schedulable == null)
        {
            return true; // Use default behavior
        }

        ScheduleBlockType? forcedActivity = DuplicateScheduleControlPatches.GetForcedActivity(schedulable);
        if (forcedActivity != null)
        {
            // If activity is forced, only allow matching chore groups
            __result = ChoreGroupMatchesActivity(chore_group, forcedActivity);
            Debug.Log($"[ChoreIntegration] {schedulable.GetProperName()} forced activity check: {chore_group.Id} = {__result}");
            return false; // Skip original method
        }

        return true; // Use default behavior
    }

    /// <summary>
    /// Patch Chore.Precondition.Context constructor to add priority bonuses for forced activities
    /// </summary>
    [HarmonyPatch(typeof(Chore.Precondition.Context), MethodType.Constructor, new Type[] { typeof(Chore), typeof(ChoreConsumerState), typeof(bool), typeof(object) })]
    [HarmonyPostfix]
    public static void Context_Constructor_Postfix(
        ref Chore.Precondition.Context __instance,
        Chore chore,
        ChoreConsumerState consumer_state,
        bool is_attempting_override,
        object data)
    {
        if (consumer_state?.consumer == null)
        {
            return;
        }

        Schedulable? schedulable = consumer_state.consumer.GetComponent<Schedulable>();
        if (schedulable == null)
        {
            return;
        }

        ScheduleBlockType? forcedActivity = DuplicateScheduleControlPatches.GetForcedActivity(schedulable);
        if (forcedActivity != null && ChoreMatchesActivity(chore, forcedActivity))
        {
            // Boost priority for forced activities
            __instance.priority += 10000;
            Debug.Log($"[ChoreIntegration] Boosted priority for forced activity: {chore.choreType.Id}");
        }
    }

    private static bool ChoreGroupMatchesActivity(ChoreGroup? choreGroup, ScheduleBlockType? activityType)
    {
        if (choreGroup == null || activityType == null)
        {
            return false;
        }

        // Work activities - most chore groups are work-related
        if (activityType == Db.Get().ScheduleBlockTypes.Work)
        {
            return choreGroup == Db.Get().ChoreGroups.Build ||
                   choreGroup == Db.Get().ChoreGroups.Dig ||
                   choreGroup == Db.Get().ChoreGroups.Hauling ||
                   choreGroup == Db.Get().ChoreGroups.Cook ||
                   choreGroup == Db.Get().ChoreGroups.Research ||
                   choreGroup == Db.Get().ChoreGroups.MedicalAid ||
                   choreGroup == Db.Get().ChoreGroups.Basekeeping ||
                   choreGroup == Db.Get().ChoreGroups.Ranching;
        }

        // Recreation activities
        if (activityType == Db.Get().ScheduleBlockTypes.Recreation)
        {
            return choreGroup == Db.Get().ChoreGroups.Art ||
                   choreGroup == Db.Get().ChoreGroups.Recreation;
        }

        // Other activities (Sleep, Hygiene, Eat) typically don't involve chore groups
        // as they're handled by individual chore types, not groups
        return false;
    }

    private static bool ChoreMatchesActivity(Chore? chore, ScheduleBlockType? activityType)
    {
        if (chore == null || activityType == null)
        {
            return false;
        }

        // Map chore types to schedule block types
        ChoreType choreType = chore.choreType;

        // Work activities
        if (activityType == Db.Get().ScheduleBlockTypes.Work)
        {
            return IsWorkChore(choreType);
        }

        // Sleep activities
        if (activityType == Db.Get().ScheduleBlockTypes.Sleep)
        {
            return choreType == Db.Get().ChoreTypes.Sleep ||
                   choreType == Db.Get().ChoreTypes.Narcolepsy;
        }

        // Recreation activities
        if (activityType == Db.Get().ScheduleBlockTypes.Recreation)
        {
            return IsRecreationChore(choreType);
        }

        // Hygiene activities
        if (activityType == Db.Get().ScheduleBlockTypes.Hygiene)
        {
            return choreType == Db.Get().ChoreTypes.Shower ||
                   choreType == Db.Get().ChoreTypes.WashHands ||
                   choreType == Db.Get().ChoreTypes.Pee;
        }

        // Eat activities
        return activityType == Db.Get().ScheduleBlockTypes.Eat && choreType == Db.Get().ChoreTypes.Eat;
    }

    private static bool IsWorkChore(ChoreType choreType)
    {
        // Check if choreType is in work-related groups
        ChoreGroup[] workGroups =
        [
            Db.Get().ChoreGroups.Hauling,
            Db.Get().ChoreGroups.Dig,
            Db.Get().ChoreGroups.Build,
            Db.Get().ChoreGroups.Cook,
            Db.Get().ChoreGroups.Art,
            Db.Get().ChoreGroups.Research,
            Db.Get().ChoreGroups.Farming,
            Db.Get().ChoreGroups.Ranching,
            Db.Get().ChoreGroups.MachineOperating
        ];

        foreach (ChoreGroup group in workGroups)
        {
            if (choreType.groups.Contains(group))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsRecreationChore(ChoreType choreType)
    {
        return choreType.groups.Contains(Db.Get().ChoreGroups.Recreation);
    }

    /// <summary>
    /// Simple helper to check if a duplicate should prioritize certain chores
    /// </summary>
    /// <param name="consumer">The chore consumer to check</param>
    /// <param name="chore">The chore to evaluate</param>
    /// <returns>True if the chore should be prioritized</returns>
    public static bool ShouldPrioritizeChore(ChoreConsumer consumer, Chore chore)
    {
        Schedulable? schedulable = consumer.GetComponent<Schedulable>();
        if (schedulable == null)
        {
            return false;
        }

        ScheduleBlockType? forcedActivity = DuplicateScheduleControlPatches.GetForcedActivity(schedulable);
        return forcedActivity != null && ChoreMatchesActivity(chore, forcedActivity);
    }

    /// <summary>
    /// Get priority bonus for chores that match forced activities
    /// </summary>
    /// <param name="consumer">The chore consumer</param>
    /// <param name="chore">The chore to evaluate</param>
    /// <returns>Priority bonus value (0 if no bonus)</returns>
    public static int GetForcedActivityPriorityBonus(ChoreConsumer consumer, Chore chore)
    {
        if (ShouldPrioritizeChore(consumer, chore))
        {
            return 10000; // High priority bonus for forced activities
        }

        return 0;
    }
}