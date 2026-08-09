using MouseCursorSupporter.Core;
using MouseCursorSupporter.Forms;

namespace MouseCursorSupporter;

public sealed class TrayAppContext : ApplicationContext
{
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
}
