using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace MouseCursorSupporter.Core;

/// <summary>
/// Reads/writes the same registry locations Windows' own "Mouse Properties > Pointers" dialog
/// uses (HKCU\Control Panel\Cursors and ...\Cursors\Schemes), and asks the shell to reload
/// cursors immediately so a switch is visible without logging off.
/// </summary>
public static class CursorRegistryService
{
    private const string CursorsKeyPath = @"Control Panel\Cursors";
    private const string SchemesKeyPath = @"Control Panel\Cursors\Schemes";

    private const uint SPI_SETCURSORS = 0x0057;
    private const uint SPIF_SENDCHANGE = 0x02;

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);

    /// <summary>
    /// Builds the comma separated 17-field string Windows stores under
    /// HKCU\Control Panel\Cursors\Schemes\&lt;name&gt; and registers it there, so the pack shows
    /// up as a selectable scheme in the standard Mouse Properties > Pointers dialog.
    /// </summary>
    public static void RegisterScheme(CursorPack pack)
    {
        var fields = CursorRoleInfo.SchemeOrder.Select(role =>
            pack.RoleFiles.TryGetValue(role, out var path) ? path : "");
        var schemeValue = string.Join(",", fields);

        using var schemesKey = Registry.CurrentUser.CreateSubKey(SchemesKeyPath, writable: true);
        schemesKey.SetValue(pack.SchemeName, schemeValue, RegistryValueKind.String);
    }

    public static void RemoveScheme(string schemeName)
    {
        using var schemesKey = Registry.CurrentUser.CreateSubKey(SchemesKeyPath, writable: true);
        if (schemesKey.GetValue(schemeName) is not null)
        {
            schemesKey.DeleteValue(schemeName);
        }
    }

    /// <summary>
    /// Applies a pack as the live, active cursor set and asks the shell to reload cursors.
    /// </summary>
    public static void ApplyPack(CursorPack pack)
    {
        using var cursorsKey = Registry.CurrentUser.CreateSubKey(CursorsKeyPath, writable: true);
        cursorsKey.SetValue("", pack.Name, RegistryValueKind.String);

        foreach (var role in CursorRoleInfo.SchemeOrder)
        {
            var valueName = CursorRoleInfo.RegistryValueName[role];
            var path = pack.RoleFiles.TryGetValue(role, out var p) ? p : "";
            cursorsKey.SetValue(valueName, path, RegistryValueKind.String);
        }

        SystemParametersInfo(SPI_SETCURSORS, 0, IntPtr.Zero, SPIF_SENDCHANGE);
    }
}
