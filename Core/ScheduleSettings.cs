namespace MouseCursorSupporter.Core;

public enum ScheduleMode
{
    Manual,
    Interval,
    TimeOfDay,
}

public enum CursorSelectionMode
{
    Sequential,
    Random,
}

public sealed class TimeSlotEntry
{
    // Minutes since midnight (0-1439) the slot starts at.
    public int StartMinutes { get; set; }
    public string PackId { get; set; } = "";
}

public sealed class ScheduleSettings
{
    public ScheduleMode Mode { get; set; } = ScheduleMode.Manual;
    public CursorSelectionMode SelectionMode { get; set; } = CursorSelectionMode.Sequential;
    public int IntervalMinutes { get; set; } = 30;
    public bool SwitchOnStartup { get; set; }

    // Null/empty = use every registered pack as the candidate pool.
    public string? ActiveListId { get; set; }

    public List<TimeSlotEntry> TimeSlots { get; set; } = [];

    // Persisted rotation cursor so sequential mode continues where it left off across restarts.
    public int LastSequentialIndex { get; set; } = -1;
}
