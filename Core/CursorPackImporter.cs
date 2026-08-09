using System.IO.Compression;
using System.Text;

namespace MouseCursorSupporter.Core;

public static class CursorPackImporter
{
    public sealed class ImportResult
    {
        public required string PackName { get; init; }
        public required string FolderPath { get; init; }
        public required RoleDetector.DetectionResult Detection { get; init; }
    }

    /// <summary>
    /// Extracts a cursor pack zip into the app's cursor storage directory and runs role
    /// auto-detection over the extracted .cur/.ani files. Does not touch the registry.
    /// </summary>
    public static ImportResult Extract(string zipPath, string? packNameOverride = null)
    {
        var packName = packNameOverride ?? Path.GetFileNameWithoutExtension(zipPath);
        var destFolder = Path.Combine(SettingsStore.CursorPacksDir, MakeSafeFolderName(packName));

        destFolder = EnsureUniqueFolder(destFolder);
        Directory.CreateDirectory(destFolder);

        // Cursor pack zips are commonly authored on Windows with Shift-JIS entry names.
        // ZipFile defaults to UTF-8, which mangles Japanese filenames, so try UTF-8 first
        // and fall back to Shift-JIS if the entry names look garbled.
        Encoding entryEncoding = Encoding.UTF8;
        using (var probe = ZipFile.OpenRead(zipPath))
        {
            if (probe.Entries.Any(e => e.Name.Contains('�')))
            {
                entryEncoding = Encoding.GetEncoding(932); // Shift-JIS
            }
        }

        using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Read, entryEncoding))
        {
            foreach (var entry in archive.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name))
                {
                    continue; // directory entry
                }

                var destPath = Path.Combine(destFolder, entry.Name);
                var destDir = Path.GetDirectoryName(destPath);
                if (!string.IsNullOrEmpty(destDir))
                {
                    Directory.CreateDirectory(destDir);
                }

                entry.ExtractToFile(destPath, overwrite: true);
            }
        }

        var cursorFiles = Directory.EnumerateFiles(destFolder, "*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".cur", StringComparison.OrdinalIgnoreCase)
                        || f.EndsWith(".ani", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var detection = RoleDetector.DetectAll(cursorFiles);

        return new ImportResult
        {
            PackName = packName,
            FolderPath = destFolder,
            Detection = detection,
        };
    }

    private static string EnsureUniqueFolder(string desiredPath)
    {
        if (!Directory.Exists(desiredPath))
        {
            return desiredPath;
        }

        var i = 2;
        string candidate;
        do
        {
            candidate = $"{desiredPath} ({i})";
            i++;
        } while (Directory.Exists(candidate));

        return candidate;
    }

    private static string MakeSafeFolderName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.Select(c => invalid.Contains(c) ? '_' : c).ToArray();
        return new string(chars).Trim();
    }
}
