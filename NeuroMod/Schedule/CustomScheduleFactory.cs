using System.Collections.Generic;

namespace NeuroMod;

/// <summary>
/// Helper class for creating and managing custom schedules
/// </summary>
/// <pre>
/// The schedule database has been initialized and the required schedule groups are available.
/// </pre>
/// <post>
/// Factory methods return detached schedule templates with exactly 24 hourly blocks when successful.
/// </post>
public static class CustomScheduleFactory
{
    private static Schedule CreateSchedule(string name, params (ScheduleGroup Group, int Hours)[] segments)
    {
        List<ScheduleBlock> blocks = [];

        foreach ((ScheduleGroup group, int hours) in segments)
        {
            for (int i = 0; i < hours; i++)
            {
                blocks.Add(new ScheduleBlock(group.Name, group.Id));
            }
        }

        return new Schedule(name, blocks, true);
    }

    /// <summary>
    /// Create a work-focused schedule (mostly work time)
    /// </summary>
    /// <param name="name">Name for the schedule</param>
    /// <returns>A new work-focused schedule</returns>
    /// <pre>
    /// Work, sleep, and recreation schedule groups are available in the database.
    /// </pre>
    /// <post>
    /// A 24-block schedule favoring work time is returned.
    /// </post>
    public static Schedule CreateWorkFocusedSchedule(string name = "Work Focused")
    {
        ScheduleGroup workGroup = Db.Get().ScheduleGroups.Worktime;
        ScheduleGroup sleepGroup = Db.Get().ScheduleGroups.Sleep;
        ScheduleGroup recreationGroup = Db.Get().ScheduleGroups.Recreation;

        return CreateSchedule(name, (workGroup, 20), (recreationGroup, 2), (sleepGroup, 2));
    }

    /// <summary>
    /// Create a balanced schedule
    /// </summary>
    /// <param name="name">Name for the schedule</param>
    /// <returns>A new balanced schedule</returns>
    /// <pre>
    /// Work, sleep, and recreation schedule groups are available in the database.
    /// </pre>
    /// <post>
    /// A 24-block schedule with standard work, recreation, and sleep distribution is returned.
    /// </post>
    public static Schedule CreateBalancedSchedule(string name = "Balanced")
    {
        ScheduleGroup workGroup = Db.Get().ScheduleGroups.Worktime;
        ScheduleGroup sleepGroup = Db.Get().ScheduleGroups.Sleep;
        ScheduleGroup recreationGroup = Db.Get().ScheduleGroups.Recreation;

        return CreateSchedule(name, (workGroup, 16), (recreationGroup, 4), (sleepGroup, 4));
    }

    /// <summary>
    /// Create a rest-focused schedule
    /// </summary>
    /// <param name="name">Name for the schedule</param>
    /// <returns>A new rest-focused schedule</returns>
    /// <pre>
    /// Work, sleep, and recreation schedule groups are available in the database.
    /// </pre>
    /// <post>
    /// A 24-block schedule emphasizing sleep and recreation is returned.
    /// </post>
    public static Schedule CreateRestFocusedSchedule(string name = "Rest Focused")
    {
        ScheduleGroup workGroup = Db.Get().ScheduleGroups.Worktime;
        ScheduleGroup sleepGroup = Db.Get().ScheduleGroups.Sleep;
        ScheduleGroup recreationGroup = Db.Get().ScheduleGroups.Recreation;

        return CreateSchedule(name, (workGroup, 12), (recreationGroup, 6), (sleepGroup, 6));
    }

    /// <summary>
    /// Create a bathing/hygiene focused schedule
    /// </summary>
    /// <param name="name">Name for the schedule</param>
    /// <returns>A new bathing-focused schedule</returns>
    /// <pre>
    /// Work, sleep, recreation, and hygiene schedule groups are available in the database.
    /// </pre>
    /// <post>
    /// A 24-block schedule with dedicated hygiene time is returned.
    /// </post>
    public static Schedule CreateBathingFocusedSchedule(string name = "Bathing Focused")
    {
        ScheduleGroup workGroup = Db.Get().ScheduleGroups.Worktime;
        ScheduleGroup sleepGroup = Db.Get().ScheduleGroups.Sleep;
        ScheduleGroup recreationGroup = Db.Get().ScheduleGroups.Recreation;
        ScheduleGroup bathingGroup = Db.Get().ScheduleGroups.Hygene;

        return CreateSchedule(name, (workGroup, 12), (bathingGroup, 6), (recreationGroup, 3), (sleepGroup, 3));
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
    /// <pre>
    /// Hour allocations must describe a full 24-hour cycle.
    /// </pre>
    /// <post>
    /// A custom 24-block schedule is returned when the inputs are valid; otherwise <see langword="null"/> is returned.
    /// </post>
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

        return CreateSchedule(name,
            (workGroup, workHours),
            (recreationGroup, recreationHours),
            (sleepGroup, sleepHours),
            (bathingGroup, bathingHours));
    }

    /// <summary>
    /// Create a schedule optimized for research
    /// </summary>
    /// <param name="name">Name for the schedule</param>
    /// <returns>A new research-focused schedule</returns>
    /// <pre>
    /// Work, sleep, and recreation schedule groups are available in the database.
    /// </pre>
    /// <post>
    /// A 24-block schedule emphasizing work time for research tasks is returned.
    /// </post>
    public static Schedule CreateResearchFocusedSchedule(string name = "Research Focused")
    {
        ScheduleGroup workGroup = Db.Get().ScheduleGroups.Worktime;
        ScheduleGroup sleepGroup = Db.Get().ScheduleGroups.Sleep;
        ScheduleGroup recreationGroup = Db.Get().ScheduleGroups.Recreation;

        return CreateSchedule(name, (workGroup, 18), (recreationGroup, 3), (sleepGroup, 3));
    }

    /// <summary>
    /// Create a night shift schedule
    /// </summary>
    /// <param name="name">Name for the schedule</param>
    /// <returns>A new night shift schedule</returns>
    /// <pre>
    /// Work, sleep, and recreation schedule groups are available in the database.
    /// </pre>
    /// <post>
    /// A 24-block schedule is returned that sleeps earlier in the cycle and shifts work into later hours.
    /// </post>
    public static Schedule CreateNightShiftSchedule(string name = "Night Shift")
    {
        ScheduleGroup workGroup = Db.Get().ScheduleGroups.Worktime;
        ScheduleGroup sleepGroup = Db.Get().ScheduleGroups.Sleep;
        ScheduleGroup recreationGroup = Db.Get().ScheduleGroups.Recreation;

        return CreateSchedule(name, (sleepGroup, 4), (recreationGroup, 4), (workGroup, 16));
    }

    /// <summary>
    /// Create an early-bird schedule that front-loads work before the normal balanced template winds down.
    /// </summary>
    /// <param name="name">Name for the schedule.</param>
    /// <returns>A new early-bird schedule.</returns>
    /// <pre>
    /// Work, sleep, and recreation schedule groups are available in the database.
    /// </pre>
    /// <post>
    /// A 24-block schedule is returned that starts work early, sleeps earlier than the balanced template, and resumes work at the end of the cycle.
    /// </post>
    public static Schedule CreateEarlyBirdSchedule(string name = "Early Bird")
    {
        ScheduleGroup workGroup = Db.Get().ScheduleGroups.Worktime;
        ScheduleGroup sleepGroup = Db.Get().ScheduleGroups.Sleep;
        ScheduleGroup recreationGroup = Db.Get().ScheduleGroups.Recreation;

        return CreateSchedule(name, (workGroup, 8), (recreationGroup, 4), (sleepGroup, 4), (workGroup, 8));
    }

    /// <summary>
    /// Creates a fully custom schedule from an explicit per-hour activity list.
    /// Each of the 24 entries specifies the activity for that hour of the cycle.
    /// </summary>
    /// <param name="name">Name for the schedule.</param>
    /// <param name="hourActivities">
    /// Exactly 24 activity strings, one per hour (0–23).
    /// Accepted values: <c>"work"</c>, <c>"sleep"</c>, <c>"recreation"</c>, <c>"bathing"</c>.
    /// </param>
    /// <returns>A new schedule, or <see langword="null"/> when the input list does not have exactly 24 entries.</returns>
    /// <pre>The schedule database has been initialized and the required schedule groups are available.</pre>
    /// <post>A 24-block schedule reflecting the requested per-hour activities is returned when the input is valid.</post>
    public static Schedule? CreateHourlySchedule(string name, IReadOnlyList<string> hourActivities)
    {
        if (hourActivities == null || hourActivities.Count != 24)
        {
            UnityEngine.Debug.LogError($"[CustomScheduleFactory] CreateHourlySchedule requires exactly 24 hour entries. Got: {hourActivities?.Count ?? 0}");
            return null;
        }

        ScheduleGroup workGroup       = Db.Get().ScheduleGroups.Worktime;
        ScheduleGroup sleepGroup      = Db.Get().ScheduleGroups.Sleep;
        ScheduleGroup recreationGroup = Db.Get().ScheduleGroups.Recreation;
        ScheduleGroup bathingGroup    = Db.Get().ScheduleGroups.Hygene;

        List<ScheduleBlock> blocks = new(24);
        for (int i = 0; i < 24; i++)
        {
            ScheduleGroup group = (hourActivities[i]?.ToLowerInvariant()) switch
            {
                "sleep"      => sleepGroup,
                "recreation" => recreationGroup,
                "bathing"    => bathingGroup,
                _            => workGroup,  // default to work for "work" or unrecognised values
            };
            blocks.Add(new ScheduleBlock(group.Name, group.Id));
        }

        return new Schedule(name, blocks, true);
    }

    /// <summary>
    /// Get all predefined schedule types
    /// </summary>
    /// <returns>List of all predefined schedules</returns>
    /// <pre>
    /// The schedule factory can create all advertised built-in schedule templates.
    /// </pre>
    /// <post>
    /// A list of built-in schedule templates is returned.
    /// </post>
    public static List<Schedule> GetAllPredefinedSchedules()
    {
        return
        [
            CreateWorkFocusedSchedule(),
            CreateBalancedSchedule(),
            CreateRestFocusedSchedule(),
            CreateResearchFocusedSchedule(),
            CreateNightShiftSchedule(),
            CreateEarlyBirdSchedule(),
            CreateBathingFocusedSchedule()
        ];
    }
}