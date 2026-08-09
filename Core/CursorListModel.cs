namespace MouseCursorSupporter.Core;

public sealed class CursorListModel
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public List<string> PackIds { get; set; } = [];
}
