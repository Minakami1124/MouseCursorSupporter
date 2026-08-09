using System.Diagnostics;
using System.Reflection;
using MouseCursorSupporter.Core;
using MouseCursorSupporter.Forms;

namespace MouseCursorSupporter;

public sealed class TrayAppContext : ApplicationContext
{
    private static readonly Version CurrentVersion = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);

    private readonly AppSettings _settings;
    private readonly SchedulerEngine _scheduler;
    private readonly NotifyIcon _notifyIcon;
    private SettingsForm? _settingsForm;

    public TrayAppContext()
    {
        _settings = SettingsStore.Load();
        _scheduler = new SchedulerEngine(_settings, SaveSettings);
        _scheduler.PackApplied += _ => RebuildMenu();

        _notifyIcon = new NotifyIcon
        {
            Icon = TrayIconFactory.CreateArrowIcon(),
            Visible = true,
            Text = "マウスカーソル自動切替",
        };
        _notifyIcon.DoubleClick += (_, _) => OpenSettings();

        RebuildMenu();
        _scheduler.Start();

        _ = CheckForUpdatesAsync(auto: true);
    }

    // WinForms context menus can get clipped by the screen edge/taskbar once they have enough
    // items (observed with large pack collections), so the tray submenu is capped and the full,
    // properly-scrollable list lives in Settings > パック管理 instead.
    private const int MaxDesignsInTrayMenu = 15;

    private void SaveSettings() => SettingsStore.Save(_settings);

    private void RebuildMenu()
    {
        var menu = new ContextMenuStrip();

        var activePack = _settings.Packs.FirstOrDefault(p => p.Id == _settings.ActivePackId);
        var header = new ToolStripMenuItem($"現在: {(activePack?.Name ?? "未設定")}") { Enabled = false };
        menu.Items.Add(header);
        menu.Items.Add(new ToolStripSeparator());

        var designsItem = new ToolStripMenuItem("デザインを選択");
        if (_settings.Packs.Count == 0)
        {
            designsItem.DropDownItems.Add(new ToolStripMenuItem("(未登録)") { Enabled = false });
        }
        else
        {
            // Always keep the active pack visible even if it would otherwise fall outside the cap.
            var visiblePacks = _settings.Packs.Take(MaxDesignsInTrayMenu).ToList();
            if (activePack is not null && !visiblePacks.Contains(activePack))
            {
                visiblePacks[^1] = activePack;
            }

            foreach (var pack in visiblePacks)
            {
                var item = new ToolStripMenuItem(pack.Name) { Checked = pack.Id == _settings.ActivePackId };
                item.Click += (_, _) => _scheduler.ApplyPackDirect(pack);
                designsItem.DropDownItems.Add(item);
            }

            var hiddenCount = _settings.Packs.Count - visiblePacks.Count;
            if (hiddenCount > 0)
            {
                designsItem.DropDownItems.Add(new ToolStripSeparator());
                var moreItem = new ToolStripMenuItem($"他 {hiddenCount} 件は設定から選択...");
                moreItem.Click += (_, _) => OpenSettings();
                designsItem.DropDownItems.Add(moreItem);
            }
        }
        menu.Items.Add(designsItem);

        var switchNowItem = new ToolStripMenuItem("次のデザインに切替");
        switchNowItem.Click += (_, _) => _scheduler.SwitchNow();
        menu.Items.Add(switchNowItem);

        menu.Items.Add(new ToolStripSeparator());

        var settingsItem = new ToolStripMenuItem("設定...");
        settingsItem.Click += (_, _) => OpenSettings();
        menu.Items.Add(settingsItem);

        var updateItem = new ToolStripMenuItem("更新を確認...");
        updateItem.Click += (_, _) => _ = CheckForUpdatesAsync(auto: false);
        menu.Items.Add(updateItem);

        var exitItem = new ToolStripMenuItem("終了");
        exitItem.Click += (_, _) => ExitApp();
        menu.Items.Add(exitItem);

        _notifyIcon.ContextMenuStrip = menu;
    }

    private void OpenSettings()
    {
        if (_settingsForm is { IsDisposed: false })
        {
            _settingsForm.Activate();
            return;
        }

        _settingsForm = new SettingsForm(_settings, SaveSettings, _scheduler);
        _settingsForm.FormClosed += (_, _) => RebuildMenu();
        _settingsForm.Show();
        _settingsForm.Activate();
    }

    private void ExitApp()
    {
        _notifyIcon.Visible = false;
        _scheduler.Stop();
        _scheduler.Dispose();
        _notifyIcon.Dispose();
        Application.Exit();
    }

    /// <summary>
    /// Checks GitHub Releases for a newer version. When <paramref name="auto"/> is true (startup
    /// check) this stays silent unless an update is found; a manual check always reports back.
    /// </summary>
    private async Task CheckForUpdatesAsync(bool auto)
    {
        if (auto && !_settings.CheckForUpdatesOnStartup)
        {
            return;
        }

        var update = await UpdateChecker.CheckForUpdateAsync(CurrentVersion);
        if (update is null)
        {
            if (!auto)
            {
                MessageBox.Show("現在お使いのバージョンが最新です。", "アップデートの確認",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            return;
        }

        if (auto && update.Version.ToString() == _settings.SkippedUpdateVersion)
        {
            return;
        }

        using var prompt = new UpdateAvailableForm(CurrentVersion, update);
        prompt.ShowDialog();

        switch (prompt.Choice)
        {
            case UpdateChoice.Skip:
                _settings.SkippedUpdateVersion = update.Version.ToString();
                SaveSettings();
                break;

            case UpdateChoice.UpdateNow:
                await DownloadAndLaunchInstallerAsync(update);
                break;
        }
    }

    private async Task DownloadAndLaunchInstallerAsync(UpdateInfo update)
    {
        using var downloadForm = new UpdateDownloadForm(update);
        downloadForm.ShowDialog();

        if (downloadForm.Error is not null)
        {
            MessageBox.Show($"ダウンロードに失敗しました。\n{downloadForm.Error.Message}", "アップデート",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        if (downloadForm.DownloadedFilePath is null)
        {
            return; // user cancelled the download
        }

        try
        {
            // /VERYSILENT skips the installer wizard entirely - from the user's perspective this
            // is an in-place self-update, not "installing a different version". installer.iss's
            // [Run] postinstall entry (no skipifsilent) relaunches the app once files are swapped.
            Process.Start(new ProcessStartInfo(downloadForm.DownloadedFilePath)
            {
                Arguments = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART",
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"インストーラーの起動に失敗しました。\n{ex.Message}", "アップデート",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        _notifyIcon.ShowBalloonTip(3000, "アップデート", "更新を適用しています。まもなく再起動します...", ToolTipIcon.Info);
        await Task.Delay(1500); // give the balloon tip a moment to actually appear before we hide the icon
        ExitApp();
    }
}
