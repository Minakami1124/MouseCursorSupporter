namespace MouseCursorSupporter.Core;

public sealed class CursorPack
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";

    // Folder under the app's cursor storage directory where the extracted files live.
    public string FolderPath { get; set; } = "";

    // The registry scheme name this pack is published under (HKCU\Control Panel\Cursors\Schemes).
    public string SchemeName { get; set; } = "";

    // Absolute file path (.cur/.ani) for each assigned role. Missing roles are omitted.
    public Dictionary<CursorRole, string> RoleFiles { get; set; } = new();
}
