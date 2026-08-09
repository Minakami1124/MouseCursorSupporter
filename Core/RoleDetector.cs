using System.Text;

namespace MouseCursorSupporter.Core;

public static class RoleDetector
{
    // Checked first, in order; longer/more specific tokens are listed first so e.g.
    // "斜めに拡大・縮小1" is matched before a bare "斜め" would be.
    private static readonly (CursorRole Role, string[] Keywords)[] StrongRules =
    [
        (CursorRole.SizeNWSE, ["斜めに拡大・縮小1", "斜めに拡大縮小1", "斜めに拡大縮小１", "斜め1", "斜め１", "diagonal resize 1", "diagonal1", "sizenwse", "nwse"]),
        (CursorRole.SizeNESW, ["斜めに拡大・縮小2", "斜めに拡大縮小2", "斜めに拡大縮小２", "斜め2", "斜め２", "diagonal resize 2", "diagonal2", "sizenesw", "nesw"]),
        (CursorRole.SizeNS, ["上下に拡大・縮小", "上下に拡大縮小", "上下", "vertical resize", "sizens"]),
        (CursorRole.SizeWE, ["左右に拡大・縮小", "左右に拡大縮小", "左右", "horizontal resize", "sizewe"]),
        (CursorRole.AppStarting, ["バックグラウンドで作業中", "バックグラウンド", "working in background", "appstarting", "background"]),
        (CursorRole.Wait, ["待ち状態", "待機", "busy", "wait"]),
        (CursorRole.Help, ["ヘルプの選択", "ヘルプ", "help select", "help"]),
        (CursorRole.Crosshair, ["領域選択", "精密選択", "precision select", "crosshair", "cross"]),
        (CursorRole.IBeam, ["テキスト選択", "テキスト", "text select", "ibeam", "beam"]),
        (CursorRole.NWPen, ["手書き", "handwriting", "pen"]),
        (CursorRole.No, ["利用不可", "unavailable", "not allowed", "no drop"]),
        (CursorRole.SizeAll, ["移動", "move", "size all", "sizeall"]),
        (CursorRole.UpArrow, ["代替選択", "alternate select", "up arrow", "uparrow"]),
        (CursorRole.Hand, ["リンクの選択", "リンク", "link select", "hand"]),
        (CursorRole.Pin, ["場所の選択", "ピンの選択", "location select", "pin"]),
        (CursorRole.Person, ["個人設定の選択", "person select", "person"]),
        (CursorRole.Arrow, ["通常の選択", "通常", "normal select", "standard select", "arrow", "pointer"]),
    ];

    // Checked only if nothing above matched, so an unambiguous keyword never loses to a vaguer
    // one. "斜め" alone (no digit) means SizeNWSE here, for packs that number their second
    // diagonal cursor ("斜め2") but leave the first one unnumbered - it must run after
    // StrongRules or it would wrongly swallow "斜め2" filenames too.
    private static readonly (CursorRole Role, string[] Keywords)[] FallbackRules =
    [
        (CursorRole.SizeNWSE, ["斜め"]),
        (CursorRole.Crosshair, ["領域"]),
    ];

    /// <summary>
    /// Guesses the cursor role for a file based on its name. Returns null when no rule matches.
    /// </summary>
    public static CursorRole? Detect(string fileNameWithoutExtension)
    {
        // Cursor packs are frequently distributed as zips authored on macOS, whose filesystem
        // stores filenames with combining marks decomposed (NFD) - e.g. "バ" as "ハ" + a
        // separate voiced-sound-mark codepoint. Our keyword literals are precomposed (NFC), so
        // without normalizing both sides here, otherwise-correct keywords like "バックグラウンド"
        // silently fail to match. Normalizing to NFC makes the comparison encoding-agnostic.
        var name = fileNameWithoutExtension.Normalize(NormalizationForm.FormC).ToLowerInvariant();

        foreach (var (role, keywords) in StrongRules)
        {
            foreach (var keyword in keywords)
            {
                if (name.Contains(keyword.Normalize(NormalizationForm.FormC).ToLowerInvariant()))
                {
                    return role;
                }
            }
        }

        foreach (var (role, keywords) in FallbackRules)
        {
            foreach (var keyword in keywords)
            {
                if (name.Contains(keyword.Normalize(NormalizationForm.FormC).ToLowerInvariant()))
                {
                    return role;
                }
            }
        }

        return null;
    }

    public sealed class DetectionResult
    {
        // Best-guess file chosen for each role that had at least one match.
        public Dictionary<CursorRole, string> Assigned { get; } = new();

        // All candidate files per role, in case the auto-picked one is wrong.
        public Dictionary<CursorRole, List<string>> Candidates { get; } = new();

        public List<string> Unmatched { get; } = [];
    }

    public static DetectionResult DetectAll(IEnumerable<string> cursorFilePaths)
    {
        var result = new DetectionResult();

        foreach (var path in cursorFilePaths)
        {
            var nameOnly = Path.GetFileNameWithoutExtension(path);
            var role = Detect(nameOnly);
            if (role is null)
            {
                result.Unmatched.Add(path);
                continue;
            }

            if (!result.Candidates.TryGetValue(role.Value, out var list))
            {
                list = [];
                result.Candidates[role.Value] = list;
            }
            list.Add(path);

            // Prefer animated cursors over static ones when multiple files match the same role;
            // otherwise keep the first match found.
            if (!result.Assigned.TryGetValue(role.Value, out var existing))
            {
                result.Assigned[role.Value] = path;
            }
            else if (!Path.GetExtension(existing).Equals(".ani", StringComparison.OrdinalIgnoreCase)
                     && Path.GetExtension(path).Equals(".ani", StringComparison.OrdinalIgnoreCase))
            {
                result.Assigned[role.Value] = path;
            }
        }

        return result;
    }
}
