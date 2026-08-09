using System.Net.Http;
using System.Text.Json;

namespace MouseCursorSupporter.Core;

public sealed record UpdateInfo(Version Version, string DownloadUrl, string ReleaseUrl, string AssetFileName);

public static class UpdateChecker
{
    private const string RepoApiUrl = "https://api.github.com/repos/Minakami1124/MouseCursorSupporter/releases/latest";

    /// <summary>
    /// Checks the GitHub Releases API for a newer version than <paramref name="currentVersion"/>.
    /// Returns null on any failure (offline, rate limited, no matching asset, up to date, etc.) -
    /// an update check must never block or crash normal startup.
    /// </summary>
    public static async Task<UpdateInfo?> CheckForUpdateAsync(Version currentVersion, CancellationToken ct = default)
    {
        try
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("MouseCursorSupporter-UpdateChecker");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

            using var response = await client.GetAsync(RepoApiUrl, ct);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var root = doc.RootElement;

            var tagName = root.GetProperty("tag_name").GetString() ?? "";
            var versionText = tagName.StartsWith('v') ? tagName[1..] : tagName;
            if (!Version.TryParse(versionText, out var latestVersion))
            {
                return null;
            }

            if (latestVersion <= currentVersion)
            {
                return null;
            }

            if (!root.TryGetProperty("assets", out var assets))
            {
                return null;
            }

            string? downloadUrl = null;
            string? assetName = null;
            foreach (var asset in assets.EnumerateArray())
            {
                var name = asset.GetProperty("name").GetString() ?? "";
                if (!name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                downloadUrl = asset.GetProperty("browser_download_url").GetString();
                assetName = name;
                if (name.Contains("Setup", StringComparison.OrdinalIgnoreCase))
                {
                    break; // prefer the installer asset if there happen to be multiple .exe assets
                }
            }

            if (downloadUrl is null || assetName is null)
            {
                return null;
            }

            var releaseUrl = root.TryGetProperty("html_url", out var htmlUrl) ? htmlUrl.GetString() ?? "" : "";
            return new UpdateInfo(latestVersion, downloadUrl, releaseUrl, assetName);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Downloads an update asset to <paramref name="destinationPath"/>, reporting 0-100 progress.</summary>
    public static async Task DownloadAsync(string url, string destinationPath, IProgress<int>? progress, CancellationToken ct)
    {
        using var client = new HttpClient();
        client.Timeout = Timeout.InfiniteTimeSpan;
        client.DefaultRequestHeaders.UserAgent.ParseAdd("MouseCursorSupporter-UpdateChecker");

        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1L;
        await using var httpStream = await response.Content.ReadAsStreamAsync(ct);
        await using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);

        var buffer = new byte[81920];
        long readTotal = 0;
        int read;
        while ((read = await httpStream.ReadAsync(buffer, ct)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, read), ct);
            readTotal += read;
            if (totalBytes > 0)
            {
                progress?.Report((int)(readTotal * 100 / totalBytes));
            }
        }
    }
}
