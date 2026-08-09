namespace MouseCursorSupporter.Core;

public sealed class AppSettings
{
    public List<CursorPack> Packs { get; set; } = [];
    public List<CursorListModel> Lists { get; set; } = [];
    public ScheduleSettings Schedule { get; set; } = new();

    // Id of the pack that is currently applied to Windows, if known.
    public string? ActivePackId { get; set; }
}
