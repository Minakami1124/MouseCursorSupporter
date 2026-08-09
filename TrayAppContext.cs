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
            foreach (var pack in _settings.Packs)
            {
                var item = new ToolStripMenuItem(pack.Name) { Checked = pack.Id == _settings.ActivePackId };
                item.Click += (_, _) => _scheduler.ApplyPackDirect(pack);
                designsItem.DropDownItems.Add(item);
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
            Process.Start(new ProcessStartInfo(downloadForm.DownloadedFilePath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"インストーラーの起動に失敗しました。\n{ex.Message}", "アップデート",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        ExitApp();
    }
}
