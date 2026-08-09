using System.Text.Json;

namespace MouseCursorSupporter.Core;

public static class SettingsStore
{
    private static readonly string AppDataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MouseCursorSupporter");

    public static readonly string CursorPacksDir = Path.Combine(AppDataDir, "CursorPacks");

    private static readonly string SettingsPath = Path.Combine(AppDataDir, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    public static AppSettings Load()
    {
        Directory.CreateDirectory(AppDataDir);
        Directory.CreateDirectory(CursorPacksDir);

        if (!File.Exists(SettingsPath))
        {
            return new AppSettings();
        }

        try
        {
            var json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        }
        catch (JsonException)
        {
            // Corrupt settings file: back it up and start fresh rather than crashing on launch.
            var backupPath = SettingsPath + ".bak";
            File.Copy(SettingsPath, backupPath, overwrite: true);
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings)
    {
        Directory.CreateDirectory(AppDataDir);
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        var tempPath = SettingsPath + ".tmp";
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, SettingsPath, overwrite: true);
    }
}
