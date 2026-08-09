namespace MouseCursorSupporter.Core;

public enum CursorRole
{
    Arrow,
    Help,
    AppStarting,
    Wait,
    Crosshair,
    IBeam,
    NWPen,
    No,
    SizeNS,
    SizeWE,
    SizeNWSE,
    SizeNESW,
    SizeAll,
    UpArrow,
    Hand,
    Pin,
    Person,
}

public static class CursorRoleInfo
{
    // Order here matches the field order Windows uses for the comma separated
    // "HKCU\Control Panel\Cursors\Schemes" value. Confirmed against this machine's
    // existing scheme entries (registry names double as scheme field order).
    public static readonly CursorRole[] SchemeOrder =
    [
        CursorRole.Arrow,
        CursorRole.Help,
        CursorRole.AppStarting,
        CursorRole.Wait,
        CursorRole.Crosshair,
        CursorRole.IBeam,
        CursorRole.NWPen,
        CursorRole.No,
        CursorRole.SizeNS,
        CursorRole.SizeWE,
        CursorRole.SizeNWSE,
        CursorRole.SizeNESW,
        CursorRole.SizeAll,
        CursorRole.UpArrow,
        CursorRole.Hand,
        CursorRole.Pin,
        CursorRole.Person,
    ];

    public static readonly IReadOnlyDictionary<CursorRole, string> RegistryValueName = new Dictionary<CursorRole, string>
    {
        [CursorRole.Arrow] = "Arrow",
        [CursorRole.Help] = "Help",
        [CursorRole.AppStarting] = "AppStarting",
        [CursorRole.Wait] = "Wait",
        [CursorRole.Crosshair] = "Crosshair",
        [CursorRole.IBeam] = "IBeam",
        [CursorRole.NWPen] = "NWPen",
        [CursorRole.No] = "No",
        [CursorRole.SizeNS] = "SizeNS",
        [CursorRole.SizeWE] = "SizeWE",
        [CursorRole.SizeNWSE] = "SizeNWSE",
        [CursorRole.SizeNESW] = "SizeNESW",
        [CursorRole.SizeAll] = "SizeAll",
        [CursorRole.UpArrow] = "UpArrow",
        [CursorRole.Hand] = "Hand",
        [CursorRole.Pin] = "Pin",
        [CursorRole.Person] = "Person",
    };

    public static readonly IReadOnlyDictionary<CursorRole, string> DisplayNameJa = new Dictionary<CursorRole, string>
    {
        [CursorRole.Arrow] = "通常の選択",
        [CursorRole.Help] = "ヘルプの選択",
        [CursorRole.AppStarting] = "バックグラウンドで作業中",
        [CursorRole.Wait] = "待ち状態",
        [CursorRole.Crosshair] = "領域選択",
        [CursorRole.IBeam] = "テキスト選択",
        [CursorRole.NWPen] = "手書き",
        [CursorRole.No] = "利用不可",
        [CursorRole.SizeNS] = "上下に拡大/縮小",
        [CursorRole.SizeWE] = "左右に拡大/縮小",
        [CursorRole.SizeNWSE] = "斜めに拡大/縮小 1",
        [CursorRole.SizeNESW] = "斜めに拡大/縮小 2",
        [CursorRole.SizeAll] = "移動",
        [CursorRole.UpArrow] = "代替選択",
        [CursorRole.Hand] = "リンクの選択",
        [CursorRole.Pin] = "場所の選択",
        [CursorRole.Person] = "個人設定の選択",
    };
}
