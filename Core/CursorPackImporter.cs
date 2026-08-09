using System.IO.Compression;
using System.Text;

namespace MouseCursorSupporter.Core;

public static class CursorPackImporter
{
    static CursorPackImporter()
    {
        // Required for Encoding.GetEncoding(932) (Shift-JIS) below - .NET no longer registers
        // legacy codepages by default.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

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
        var destFolder = EnsureUniqueFolder(Path.Combine(SettingsStore.CursorPacksDir, MakeSafeFolderName(packName)));
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

                // Zips authored on macOS store filenames with combining marks decomposed (NFD -
                // e.g. "バ" as "ハ" + a separate voiced-sound-mark codepoint). Normalizing to NFC
                // here keeps on-disk names consistent and matches what RoleDetector expects.
                var entryName = entry.Name.Normalize(NormalizationForm.FormC);
                var destPath = Path.Combine(destFolder, entryName);
                var destDir = Path.GetDirectoryName(destPath);
                if (!string.IsNullOrEmpty(destDir))
                {
                    Directory.CreateDirectory(destDir);
                }

                entry.ExtractToFile(destPath, overwrite: true);
            }
        }

        return BuildResult(packName, destFolder);
    }

    /// <summary>
    /// Builds a pack from an already-extracted folder of .cur/.ani files (recursively), copying
    /// them into the app's managed storage so the pack keeps working even if the original folder
    /// is later moved or deleted.
    /// </summary>
    public static ImportResult ImportFromFolder(string sourceFolder, string? packNameOverride = null)
    {
        var packName = packNameOverride ?? new DirectoryInfo(sourceFolder).Name;
        var sourceFiles = Directory.EnumerateFiles(sourceFolder, "*", SearchOption.AllDirectories)
            .Where(IsCursorFile);
        return ImportFileList(packName, sourceFiles);
    }

    /// <summary>Builds a pack from a set of individually selected .cur/.ani files.</summary>
    public static ImportResult ImportFromFiles(IEnumerable<string> filePaths, string packNameOverride)
    {
        return ImportFileList(packNameOverride, filePaths.Where(IsCursorFile));
    }

    private static ImportResult ImportFileList(string packName, IEnumerable<string> sourceFiles)
    {
        var destFolder = EnsureUniqueFolder(Path.Combine(SettingsStore.CursorPacksDir, MakeSafeFolderName(packName)));
        Directory.CreateDirectory(destFolder);

        foreach (var src in sourceFiles)
        {
            var fileName = Path.GetFileName(src).Normalize(NormalizationForm.FormC);
            var destPath = EnsureUniqueFile(Path.Combine(destFolder, fileName));
            File.Copy(src, destPath);
        }

        return BuildResult(packName, destFolder);
    }

    private static ImportResult BuildResult(string packName, string destFolder)
    {
        var cursorFiles = Directory.EnumerateFiles(destFolder, "*", SearchOption.AllDirectories)
            .Where(IsCursorFile)
            .ToList();

        return new ImportResult
        {
            PackName = packName,
            FolderPath = destFolder,
            Detection = RoleDetector.DetectAll(cursorFiles),
        };
    }

    private static bool IsCursorFile(string path) =>
        path.EndsWith(".cur", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".ani", StringComparison.OrdinalIgnoreCase);

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

    private static string EnsureUniqueFile(string desiredPath)
    {
        if (!File.Exists(desiredPath))
        {
            return desiredPath;
        }

        var dir = Path.GetDirectoryName(desiredPath)!;
        var name = Path.GetFileNameWithoutExtension(desiredPath);
        var ext = Path.GetExtension(desiredPath);

        var i = 2;
        string candidate;
        do
        {
            candidate = Path.Combine(dir, $"{name} ({i}){ext}");
            i++;
        } while (File.Exists(candidate));

        return candidate;
    }

    private static string MakeSafeFolderName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.Select(c => invalid.Contains(c) ? '_' : c).ToArray();
        return new string(chars).Trim();
    }
}
