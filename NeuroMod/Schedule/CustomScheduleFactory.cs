using System.Collections.Generic;

namespace NeuroMod;

/// <summary>
/// Helper class for creating and managing custom schedules
/// </summary>
public static class CustomScheduleFactory
{
    /// <summary>
    /// Create a work-focused schedule (mostly work time)
    /// </summary>
    /// <param name="name">Name for the schedule</param>
    /// <returns>A new work-focused schedule</returns>
    public static Schedule CreateWorkFocusedSchedule(string name = "Work Focused")
    {
        ScheduleGroup workGroup = Db.Get().ScheduleGroups.Worktime;
        ScheduleGroup sleepGroup = Db.Get().ScheduleGroups.Sleep;
        ScheduleGroup recreationGroup = Db.Get().ScheduleGroups.Recreation;

        List<ScheduleBlock> blocks = [];

        // 20 hours work, 2 hours recreation, 2 hours sleep
        for (int i = 0; i < 20; i++)
        {
            blocks.Add(new ScheduleBlock(workGroup.Name, workGroup.Id));
        }

        for (int i = 0; i < 2; i++)
        {
            blocks.Add(new ScheduleBlock(recreationGroup.Name, recreationGroup.Id));
        }

        for (int i = 0; i < 2; i++)
        {
            blocks.Add(new ScheduleBlock(sleepGroup.Name, sleepGroup.Id));
        }

        return new Schedule(name, blocks, true);
    }

    /// <summary>
    /// Create a balanced schedule
    /// </summary>
    /// <param name="name">Name for the schedule</param>
    /// <returns>A new balanced schedule</returns>
    public static Schedule CreateBalancedSchedule(string name = "Balanced")
    {
        ScheduleGroup workGroup = Db.Get().ScheduleGroups.Worktime;
        ScheduleGroup sleepGroup = Db.Get().ScheduleGroups.Sleep;
        ScheduleGroup recreationGroup = Db.Get().ScheduleGroups.Recreation;

        List<ScheduleBlock> blocks = [];

        // 16 hours work, 4 hours recreation, 4 hours sleep
        for (int i = 0; i < 16; i++)
        {
            blocks.Add(new ScheduleBlock(workGroup.Name, workGroup.Id));
        }

        for (int i = 0; i < 4; i++)
        {
            blocks.Add(new ScheduleBlock(recreationGroup.Name, recreationGroup.Id));
        }

        for (int i = 0; i < 4; i++)
        {
            blocks.Add(new ScheduleBlock(sleepGroup.Name, sleepGroup.Id));
        }

        return new Schedule(name, blocks, true);
    }

    /// <summary>
    /// Create a rest-focused schedule
    /// </summary>
    /// <param name="name">Name for the schedule</param>
    /// <returns>A new rest-focused schedule</returns>
    public static Schedule CreateRestFocusedSchedule(string name = "Rest Focused")
    {
        ScheduleGroup workGroup = Db.Get().ScheduleGroups.Worktime;
        ScheduleGroup sleepGroup = Db.Get().ScheduleGroups.Sleep;
        ScheduleGroup recreationGroup = Db.Get().ScheduleGroups.Recreation;

        List<ScheduleBlock> blocks = [];

        // 12 hours work, 6 hours recreation, 6 hours sleep
        for (int i = 0; i < 12; i++)
        {
            blocks.Add(new ScheduleBlock(workGroup.Name, workGroup.Id));
        }

        for (int i = 0; i < 6; i++)
        {
            blocks.Add(new ScheduleBlock(recreationGroup.Name, recreationGroup.Id));
        }

        for (int i = 0; i < 6; i++)
        {
            blocks.Add(new ScheduleBlock(sleepGroup.Name, sleepGroup.Id));
        }

        return new Schedule(name, blocks, true);
    }

    /// <summary>
    /// Create a bathing/hygiene focused schedule
    /// </summary>
    /// <param name="name">Name for the schedule</param>
    /// <returns>A new bathing-focused schedule</returns>
    public static Schedule CreateBathingFocusedSchedule(string name = "Bathing Focused")
    {
        ScheduleGroup workGroup = Db.Get().ScheduleGroups.Worktime;
        ScheduleGroup sleepGroup = Db.Get().ScheduleGroups.Sleep;
        ScheduleGroup recreationGroup = Db.Get().ScheduleGroups.Recreation;
        ScheduleGroup bathingGroup = Db.Get().ScheduleGroups.Hygene;

        List<ScheduleBlock> blocks = [];

        // 12 hours work, 6 hours bathing, 3 hours recreation, 3 hours sleep
        for (int i = 0; i < 12; i++)
        {
            blocks.Add(new ScheduleBlock(workGroup.Name, workGroup.Id));
        }

        for (int i = 0; i < 6; i++)
        {
            blocks.Add(new ScheduleBlock(bathingGroup.Name, bathingGroup.Id));
        }

        for (int i = 0; i < 3; i++)
        {
            blocks.Add(new ScheduleBlock(recreationGroup.Name, recreationGroup.Id));
        }

        for (int i = 0; i < 3; i++)
        {
            blocks.Add(new ScheduleBlock(sleepGroup.Name, sleepGroup.Id));
        }

        return new Schedule(name, blocks, true);
    }

    /// <summary>
    /// Create a custom schedule with specific hour allocations including bathing
    /// </summary>
    /// <param name="name">Name for the schedule</param>
    /// <param name="workHours">Number of work hours (0-24)</param>
    /// <param name="recreationHours">Number of recreation hours (0-24)</param>
    /// <param name="sleepHours">Number of sleep hours (0-24)</param>
    /// <param name="bathingHours">Number of bathing/hygiene hours (0-24)</param>
    /// <returns>A new custom schedule, or null if parameters are invalid</returns>
    public static Schedule? CreateCustomSchedule(
        string name,
        int workHours,
        int recreationHours,
        int sleepHours,
        int bathingHours = 0)
    {
        if (workHours + recreationHours + sleepHours + bathingHours != 24)
        {
            Debug.LogError($"[CustomScheduleFactory] Schedule hours must add up to 24! Got: {workHours + recreationHours + sleepHours + bathingHours}");
            return null;
        }

        ScheduleGroup workGroup = Db.Get().ScheduleGroups.Worktime;
        ScheduleGroup sleepGroup = Db.Get().ScheduleGroups.Sleep;
        ScheduleGroup recreationGroup = Db.Get().ScheduleGroups.Recreation;
        ScheduleGroup bathingGroup = Db.Get().ScheduleGroups.Hygene;

        List<ScheduleBlock> blocks = [];

        for (int i = 0; i < workHours; i++)
        {
            blocks.Add(new ScheduleBlock(workGroup.Name, workGroup.Id));
        }

        for (int i = 0; i < recreationHours; i++)
        {
            blocks.Add(new ScheduleBlock(recreationGroup.Name, recreationGroup.Id));
        }

        for (int i = 0; i < sleepHours; i++)
        {
            blocks.Add(new ScheduleBlock(sleepGroup.Name, sleepGroup.Id));
        }

        for (int i = 0; i < bathingHours; i++)
        {
            blocks.Add(new ScheduleBlock(bathingGroup.Name, bathingGroup.Id));
        }

        return new Schedule(name, blocks, true);
    }

    /// <summary>
    /// Create a schedule optimized for research
    /// </summary>
    /// <param name="name">Name for the schedule</param>
    /// <returns>A new research-focused schedule</returns>
    public static Schedule CreateResearchFocusedSchedule(string name = "Research Focused")
    {
        ScheduleGroup workGroup = Db.Get().ScheduleGroups.Worktime;
        ScheduleGroup sleepGroup = Db.Get().ScheduleGroups.Sleep;
        ScheduleGroup recreationGroup = Db.Get().ScheduleGroups.Recreation;

        List<ScheduleBlock> blocks = [];

        // 18 hours work, 3 hours recreation, 3 hours sleep
        for (int i = 0; i < 18; i++)
        {
            blocks.Add(new ScheduleBlock(workGroup.Name, workGroup.Id));
        }

        for (int i = 0; i < 3; i++)
        {
            blocks.Add(new ScheduleBlock(recreationGroup.Name, recreationGroup.Id));
        }

        for (int i = 0; i < 3; i++)
        {
            blocks.Add(new ScheduleBlock(sleepGroup.Name, sleepGroup.Id));
        }

        return new Schedule(name, blocks, true);
    }

    /// <summary>
    /// Create a night shift schedule
    /// </summary>
    /// <param name="name">Name for the schedule</param>
    /// <returns>A new night shift schedule</returns>
    public static Schedule CreateNightShiftSchedule(string name = "Night Shift")
    {
        // Similar to balanced but shifted for night work
        return CreateBalancedSchedule(name);
    }

    /// <summary>
    /// Get all predefined schedule types
    /// </summary>
    /// <returns>List of all predefined schedules</returns>
    public static List<Schedule> GetAllPredefinedSchedules()
    {
        return
        [
            CreateWorkFocusedSchedule(),
            CreateBalancedSchedule(),
            CreateRestFocusedSchedule(),
            CreateResearchFocusedSchedule(),
            CreateNightShiftSchedule()
        ];
    }
}