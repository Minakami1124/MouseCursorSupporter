using MouseCursorSupporter.Core;

namespace MouseCursorSupporter.Forms;

public sealed class SettingsForm : Form
{
    private readonly AppSettings _settings;
    private readonly Action _saveSettings;
    private readonly SchedulerEngine _scheduler;

    // Packs tab
    private readonly ListBox _packListBox = new() { Dock = DockStyle.Fill };

    // Lists tab
    private readonly ListBox _listListBox = new() { Dock = DockStyle.Fill };
    private readonly CheckedListBox _listMembersBox = new() { Dock = DockStyle.Fill, CheckOnClick = true };

    // Schedule tab
    private readonly RadioButton _modeManual = new() { Text = "手動切替のみ", AutoSize = true };
    private readonly RadioButton _modeInterval = new() { Text = "一定間隔で自動切替", AutoSize = true };
    private readonly RadioButton _modeTimeOfDay = new() { Text = "時間帯で自動切替", AutoSize = true };
    private readonly NumericUpDown _intervalMinutes = new() { Minimum = 1, Maximum = 1440, Width = 70 };
    private readonly CheckBox _switchOnStartup = new() { Text = "Windows起動/ログオン時にも切り替える", AutoSize = true };
    private readonly RadioButton _selectSequential = new() { Text = "順番にローテーション", AutoSize = true };
    private readonly RadioButton _selectRandom = new() { Text = "ランダム", AutoSize = true };
    private readonly ComboBox _activeListCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
    private readonly DataGridView _timeSlotGrid = new() { Dock = DockStyle.Fill, AllowUserToAddRows = false };

    // General tab
    private readonly CheckBox _runAtStartupBox = new() { Text = "Windowsログオン時に自動起動する", AutoSize = true };
    private readonly CheckBox _checkForUpdatesBox = new() { Text = "起動時に更新を自動確認する", AutoSize = true };

    private bool _suppressEvents;

    public SettingsForm(AppSettings settings, Action saveSettings, SchedulerEngine scheduler)
    {
        _settings = settings;
        _saveSettings = saveSettings;
        _scheduler = scheduler;

        Text = "マウスカーソル自動切替 - 設定";
        Width = 640;
        Height = 560;
        StartPosition = FormStartPosition.CenterScreen;

        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(BuildPacksTab());
        tabs.TabPages.Add(BuildListsTab());
        tabs.TabPages.Add(BuildScheduleTab());
        tabs.TabPages.Add(BuildGeneralTab());
        Controls.Add(tabs);

        RefreshPackList();
        RefreshListList();
        LoadScheduleUi();
    }

    // ----- Packs tab -----------------------------------------------------

    private TabPage BuildPacksTab()
    {
        var page = new TabPage("パック管理");
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(10) };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _packListBox.DisplayMember = "Name";
        layout.Controls.Add(_packListBox, 0, 0);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        var addZipButton = new Button { Text = "ZIPから追加...", AutoSize = true };
        addZipButton.Click += OnAddPackFromZipClicked;
        var addFolderButton = new Button { Text = "フォルダから追加...", AutoSize = true };
        addFolderButton.Click += OnAddPackFromFolderClicked;
        var addFilesButton = new Button { Text = "ファイルから追加...", AutoSize = true };
        addFilesButton.Click += OnAddPackFromFilesClicked;
        var addZipFolderButton = new Button { Text = "フォルダ内ZIPを一括インポート...", AutoSize = true };
        addZipFolderButton.Click += OnAddPacksFromZipFolderClicked;
        var editButton = new Button { Text = "内容を編集...", AutoSize = true };
        editButton.Click += OnEditPackClicked;
        var renameButton = new Button { Text = "名前を変更", AutoSize = true };
        renameButton.Click += OnRenamePackClicked;
        var removeButton = new Button { Text = "削除", AutoSize = true };
        removeButton.Click += OnRemovePackClicked;
        buttons.Controls.Add(addZipButton);
        buttons.Controls.Add(addFolderButton);
        buttons.Controls.Add(addFilesButton);
        buttons.Controls.Add(addZipFolderButton);
        buttons.Controls.Add(editButton);
        buttons.Controls.Add(renameButton);
        buttons.Controls.Add(removeButton);
        layout.Controls.Add(buttons, 0, 1);

        page.Controls.Add(layout);
        return page;
    }

    private void RefreshPackList()
    {
        var selected = _packListBox.SelectedItem as CursorPack;
        _packListBox.DataSource = null;
        _packListBox.DataSource = _settings.Packs;
        _packListBox.DisplayMember = "Name";
        if (selected is not null)
        {
            var again = _settings.Packs.FirstOrDefault(p => p.Id == selected.Id);
            _packListBox.SelectedItem = again;
        }

        RefreshListMembersSource();
        RefreshActiveListCombo();
        RefreshTimeSlotPackColumn();
    }

    private void OnAddPackFromZipClicked(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "マウスカーソルのZIPファイルを選択(複数選択可)",
            Filter = "ZIPファイル (*.zip)|*.zip",
            Multiselect = true,
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        ImportZipsSequentially(dialog.FileNames);
    }

    private void OnAddPacksFromZipFolderClicked(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "ZIPファイルが入ったフォルダを選択してください(そのフォルダ直下のZIPをまとめて読み込みます)",
            UseDescriptionForTitle = true,
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var zipPaths = Directory.GetFiles(dialog.SelectedPath, "*.zip");
        if (zipPaths.Length == 0)
        {
            MessageBox.Show(this, "選択したフォルダにZIPファイルが見つかりませんでした。", "確認", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ImportZipsSequentially(zipPaths);
    }

    /// <summary>
    /// Imports several zips one at a time, showing the usual role-mapping confirmation for each
    /// so a bad auto-detection on one pack doesn't silently taint a bulk import. After each
    /// successful import, asks whether to delete the source zip; when importing more than one,
    /// the user can apply that choice to the rest instead of being asked every time.
    /// </summary>
    private void ImportZipsSequentially(IReadOnlyList<string> zipPaths)
    {
        var importedCount = 0;
        var allowAbort = zipPaths.Count > 1;
        bool? rememberedDeleteChoice = null;

        foreach (var zipPath in zipPaths)
        {
            var before = _settings.Packs.Count;
            var aborted = ImportAndConfirmPack(() => CursorPackImporter.Extract(zipPath), $"ZIPの展開に失敗しました。\n({Path.GetFileName(zipPath)})", allowAbort);
            if (aborted)
            {
                break;
            }
            if (_settings.Packs.Count <= before)
            {
                continue; // user skipped this one; nothing to clean up
            }
            importedCount++;

            bool shouldDelete;
            if (rememberedDeleteChoice.HasValue)
            {
                shouldDelete = rememberedDeleteChoice.Value;
            }
            else
            {
                var (delete, applyToRest) = AskDeleteZip(this, Path.GetFileName(zipPath), zipPaths.Count > 1);
                shouldDelete = delete;
                if (applyToRest)
                {
                    rememberedDeleteChoice = delete;
                }
            }

            if (shouldDelete)
            {
                try { File.Delete(zipPath); }
                catch (Exception ex)
                {
                    MessageBox.Show(this, $"ZIPファイルの削除に失敗しました。\n{ex.Message}", "確認", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        if (zipPaths.Count > 1)
        {
            MessageBox.Show(this, $"{zipPaths.Count}個中{importedCount}個のデザインを登録しました。", "一括インポート完了",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    /// <summary>Asks whether to delete an imported zip. When <paramref name="showApplyToRest"/> is
    /// true, offers a checkbox to apply the same answer to the rest of the batch.</summary>
    private static (bool Delete, bool ApplyToRest) AskDeleteZip(IWin32Window owner, string zipFileName, bool showApplyToRest)
    {
        using var dialog = new Form
        {
            Text = "ZIPファイルの削除",
            Width = 440,
            Height = showApplyToRest ? 190 : 160,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            MaximizeBox = false,
        };

        var label = new Label
        {
            Text = $"「{zipFileName}」を削除しますか?\nカーソルファイルは既に読み込み済みのため、ZIP自体は不要です。",
            AutoSize = false,
            Left = 12,
            Top = 12,
            Width = 400,
            Height = 50,
        };
        dialog.Controls.Add(label);

        var applyToRestBox = new CheckBox
        {
            Text = "残りのZIPファイルにもこの選択を適用する",
            AutoSize = true,
            Left = 12,
            Top = 66,
            Visible = showApplyToRest,
        };
        dialog.Controls.Add(applyToRestBox);

        var buttonTop = showApplyToRest ? 100 : 70;
        var deleteButton = new Button { Text = "削除する", DialogResult = DialogResult.Yes, Left = 150, Top = buttonTop, Width = 100 };
        var keepButton = new Button { Text = "削除しない", DialogResult = DialogResult.No, Left = 260, Top = buttonTop, Width = 100 };
        dialog.Controls.Add(deleteButton);
        dialog.Controls.Add(keepButton);
        dialog.AcceptButton = keepButton;
        dialog.CancelButton = keepButton;

        var result = dialog.ShowDialog(owner);
        return (result == DialogResult.Yes, applyToRestBox.Checked);
    }

    private void OnAddPackFromFolderClicked(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "カーソルファイル(.cur/.ani)が入ったフォルダを選択してください",
            UseDescriptionForTitle = true,
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        ImportAndConfirmPack(() => CursorPackImporter.ImportFromFolder(dialog.SelectedPath), "フォルダの読み込みに失敗しました。");
    }

    private void OnAddPackFromFilesClicked(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "カーソルファイルを選択(複数選択可)",
            Filter = "カーソルファイル (*.cur;*.ani)|*.cur;*.ani",
            Multiselect = true,
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var suggestedName = PromptText(this, "デザイン名を入力してください:", "新しいデザイン");
        if (string.IsNullOrWhiteSpace(suggestedName))
        {
            return;
        }

        ImportAndConfirmPack(() => CursorPackImporter.ImportFromFiles(dialog.FileNames, suggestedName), "ファイルの読み込みに失敗しました。");
    }

    /// <summary>
    /// Shared tail end of every "add pack" flow (ZIP/folder/files): run the importer, let the
    /// user review/fix the auto-detected role mapping, then register the resulting pack.
    /// Returns true if the user asked to abort the rest of a batch import.
    /// </summary>
    private bool ImportAndConfirmPack(Func<CursorPackImporter.ImportResult> import, string failureMessage, bool allowAbortBatch = false)
    {
        CursorPackImporter.ImportResult imported;
        try
        {
            imported = import();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"{failureMessage}\n{ex.Message}", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }

        var existingNames = _settings.Packs.Select(p => p.Name).ToHashSet();
        using var mappingForm = new RoleMappingForm(imported.PackName, imported.FolderPath, imported.Detection, existingNames, allowAbortBatch);
        if (mappingForm.ShowDialog(this) != DialogResult.OK)
        {
            // User skipped/aborted: remove the copied/extracted files so we don't leave orphaned folders.
            try { Directory.Delete(imported.FolderPath, recursive: true); } catch { /* best effort cleanup */ }
            return mappingForm.AbortBatch;
        }

        var pack = new CursorPack
        {
            Name = mappingForm.ResultPackName,
            FolderPath = imported.FolderPath,
            SchemeName = MakeUniqueSchemeName(mappingForm.ResultPackName),
            RoleFiles = mappingForm.ResultRoleFiles,
        };

        CursorRegistryService.RegisterScheme(pack);
        _settings.Packs.Add(pack);
        _saveSettings();
        RefreshPackList();
        return false;
    }

    private static string MakeUniqueSchemeName(string baseName)
    {
        // Scheme names live in HKCU\...\Cursors\Schemes alongside the user's other cursor
        // schemes, so avoid clobbering an unrelated scheme that happens to share the name.
        var candidate = baseName;
        var i = 2;
        while (Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Control Panel\Cursors\Schemes")?.GetValue(candidate) is not null)
        {
            candidate = $"{baseName} ({i++})";
        }
        return candidate;
    }

    private void OnEditPackClicked(object? sender, EventArgs e)
    {
        if (_packListBox.SelectedItem is not CursorPack pack)
        {
            return;
        }

        var allFiles = Directory.EnumerateFiles(pack.FolderPath, "*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".cur", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".ani", StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Re-run detection so any newer/better keyword matches show up as suggestions, but let
        // the pack's own saved assignments win where they exist - editing shouldn't silently
        // discard a mapping the user already fixed by hand.
        var detection = RoleDetector.DetectAll(allFiles);
        foreach (var (role, path) in pack.RoleFiles)
        {
            detection.Assigned[role] = path;
        }

        var existingNames = _settings.Packs.Where(p => p.Id != pack.Id).Select(p => p.Name).ToHashSet();
        using var mappingForm = new RoleMappingForm(pack.Name, pack.FolderPath, detection, existingNames);
        if (mappingForm.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        if (mappingForm.ResultPackName != pack.Name)
        {
            CursorRegistryService.RemoveScheme(pack.SchemeName);
            pack.Name = mappingForm.ResultPackName;
            pack.SchemeName = MakeUniqueSchemeName(mappingForm.ResultPackName);
        }
        pack.RoleFiles = mappingForm.ResultRoleFiles;
        CursorRegistryService.RegisterScheme(pack);

        if (_settings.ActivePackId == pack.Id)
        {
            CursorRegistryService.ApplyPack(pack); // live cursor should reflect the edit immediately
        }

        _saveSettings();
        RefreshPackList();
    }

    private void OnRenamePackClicked(object? sender, EventArgs e)
    {
        if (_packListBox.SelectedItem is not CursorPack pack)
        {
            return;
        }

        var newName = PromptText(this, "新しいデザイン名を入力してください:", pack.Name);
        if (string.IsNullOrWhiteSpace(newName) || newName == pack.Name)
        {
            return;
        }
        if (_settings.Packs.Any(p => p.Id != pack.Id && p.Name == newName))
        {
            MessageBox.Show(this, "同じ名前のデザインが既に存在します。", "確認", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        CursorRegistryService.RemoveScheme(pack.SchemeName);
        pack.Name = newName;
        pack.SchemeName = MakeUniqueSchemeName(newName);
        CursorRegistryService.RegisterScheme(pack);

        if (_settings.ActivePackId == pack.Id)
        {
            CursorRegistryService.ApplyPack(pack); // keep the "current cursor name" display in sync
        }

        _saveSettings();
        RefreshPackList();
    }

    private void OnRemovePackClicked(object? sender, EventArgs e)
    {
        if (_packListBox.SelectedItem is not CursorPack pack)
        {
            return;
        }

        if (MessageBox.Show(this, $"「{pack.Name}」を削除しますか?\n展開済みのカーソルファイルも削除されます。",
                "確認", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
        {
            return;
        }

        CursorRegistryService.RemoveScheme(pack.SchemeName);
        _settings.Packs.Remove(pack);
        foreach (var list in _settings.Lists)
        {
            list.PackIds.Remove(pack.Id);
        }
        _settings.Schedule.TimeSlots.RemoveAll(s => s.PackId == pack.Id);
        if (_settings.ActivePackId == pack.Id)
        {
            _settings.ActivePackId = null;
        }

        try { Directory.Delete(pack.FolderPath, recursive: true); } catch { /* best effort cleanup */ }

        _saveSettings();
        RefreshPackList();
        RefreshTimeSlotGridRows();
    }

    // ----- Lists tab -------------------------------------------------------

    private TabPage BuildListsTab()
    {
        var page = new TabPage("リスト管理");
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Padding = new Padding(10) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _listListBox.DisplayMember = "Name";
        _listListBox.SelectedIndexChanged += (_, _) => RefreshListMembersSource();
        layout.Controls.Add(_listListBox, 0, 0);

        var membersGroup = new GroupBox { Text = "リストに含めるデザイン", Dock = DockStyle.Fill };
        _listMembersBox.Dock = DockStyle.Fill;
        _listMembersBox.DisplayMember = "Name";
        _listMembersBox.ItemCheck += OnListMemberCheckChanged;
        membersGroup.Controls.Add(_listMembersBox);
        layout.Controls.Add(membersGroup, 1, 0);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        var addButton = new Button { Text = "新規作成", AutoSize = true };
        addButton.Click += OnAddListClicked;
        var renameButton = new Button { Text = "名前を変更", AutoSize = true };
        renameButton.Click += OnRenameListClicked;
        var removeButton = new Button { Text = "削除", AutoSize = true };
        removeButton.Click += OnRemoveListClicked;
        buttons.Controls.Add(addButton);
        buttons.Controls.Add(renameButton);
        buttons.Controls.Add(removeButton);
        layout.Controls.Add(buttons, 0, 1);
        layout.SetColumnSpan(buttons, 2);

        page.Controls.Add(layout);
        return page;
    }

    private void RefreshListList()
    {
        var selected = _listListBox.SelectedItem as CursorListModel;
        _listListBox.DataSource = null;
        _listListBox.DataSource = _settings.Lists;
        _listListBox.DisplayMember = "Name";
        if (selected is not null)
        {
            _listListBox.SelectedItem = _settings.Lists.FirstOrDefault(l => l.Id == selected.Id);
        }
        RefreshListMembersSource();
    }

    private void RefreshListMembersSource()
    {
        _suppressEvents = true;
        _listMembersBox.Items.Clear();
        var selectedList = _listListBox.SelectedItem as CursorListModel;
        foreach (var pack in _settings.Packs)
        {
            var isChecked = selectedList is not null && selectedList.PackIds.Contains(pack.Id);
            _listMembersBox.Items.Add(pack, isChecked);
        }
        _suppressEvents = false;
    }

    private void OnListMemberCheckChanged(object? sender, ItemCheckEventArgs e)
    {
        if (_suppressEvents)
        {
            return;
        }
        if (_listListBox.SelectedItem is not CursorListModel list)
        {
            return;
        }
        if (_listMembersBox.Items[e.Index] is not CursorPack pack)
        {
            return;
        }

        // Fires before the check state actually changes, so use e.NewValue.
        BeginInvoke(() =>
        {
            if (e.NewValue == CheckState.Checked)
            {
                if (!list.PackIds.Contains(pack.Id)) list.PackIds.Add(pack.Id);
            }
            else
            {
                list.PackIds.Remove(pack.Id);
            }
            _saveSettings();
        });
    }

    private void OnAddListClicked(object? sender, EventArgs e)
    {
        var name = PromptText(this, "新しいリスト名を入力してください:", "新しいリスト");
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }
        var list = new CursorListModel { Name = name };
        _settings.Lists.Add(list);
        _saveSettings();
        RefreshListList();
        RefreshActiveListCombo();
        _listListBox.SelectedItem = list;
    }

    private void OnRenameListClicked(object? sender, EventArgs e)
    {
        if (_listListBox.SelectedItem is not CursorListModel list)
        {
            return;
        }
        var newName = PromptText(this, "新しいリスト名を入力してください:", list.Name);
        if (string.IsNullOrWhiteSpace(newName))
        {
            return;
        }
        list.Name = newName;
        _saveSettings();
        RefreshListList();
        RefreshActiveListCombo();
    }

    private void OnRemoveListClicked(object? sender, EventArgs e)
    {
        if (_listListBox.SelectedItem is not CursorListModel list)
        {
            return;
        }
        if (MessageBox.Show(this, $"リスト「{list.Name}」を削除しますか?", "確認", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
        {
            return;
        }
        _settings.Lists.Remove(list);
        if (_settings.Schedule.ActiveListId == list.Id)
        {
            _settings.Schedule.ActiveListId = null;
        }
        _saveSettings();
        RefreshListList();
        RefreshActiveListCombo();
    }

    // ----- Schedule tab ------------------------------------------------

    private TabPage BuildScheduleTab()
    {
        var page = new TabPage("スケジュール");
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(10), AutoSize = true };

        // All three mode radio buttons must be direct children of the SAME container: WinForms
        // groups radio buttons for mutual exclusivity by immediate parent, not by form/tab, so
        // nesting _modeInterval in its own sub-panel (as before) silently let it be checked
        // alongside the other two instead of replacing them.
        var modeGroup = new GroupBox { Text = "切替モード", AutoSize = true, Dock = DockStyle.Top, Height = 110 };
        var modeTable = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 3, Padding = new Padding(8), AutoSize = true };
        modeTable.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        modeTable.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        modeTable.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        modeTable.Controls.Add(_modeManual, 0, 0);
        modeTable.SetColumnSpan(_modeManual, 3);

        modeTable.Controls.Add(_modeInterval, 0, 1);
        modeTable.Controls.Add(_intervalMinutes, 1, 1);
        modeTable.Controls.Add(new Label { Text = "分ごと", AutoSize = true, Anchor = AnchorStyles.Left, Padding = new Padding(4, 6, 0, 0) }, 2, 1);

        modeTable.Controls.Add(_modeTimeOfDay, 0, 2);
        modeTable.SetColumnSpan(_modeTimeOfDay, 3);

        modeGroup.Controls.Add(modeTable);
        layout.Controls.Add(modeGroup);

        _switchOnStartup.Margin = new Padding(0, 6, 0, 6);
        layout.Controls.Add(_switchOnStartup);

        var selectionGroup = new GroupBox { Text = "選択方法(一定間隔・起動時に使用)", AutoSize = true, Dock = DockStyle.Top, Height = 100 };
        var selectionFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, AutoSize = true, Padding = new Padding(8) };
        selectionFlow.Controls.Add(_selectSequential);
        selectionFlow.Controls.Add(_selectRandom);
        var listRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        listRow.Controls.Add(new Label { Text = "対象リスト:", AutoSize = true, Padding = new Padding(0, 6, 6, 0) });
        listRow.Controls.Add(_activeListCombo);
        selectionFlow.Controls.Add(listRow);
        selectionGroup.Controls.Add(selectionFlow);
        layout.Controls.Add(selectionGroup);

        var slotGroup = new GroupBox { Text = "時間帯テーブル(時間帯で自動切替を選択時)", Dock = DockStyle.Top, Height = 220 };
        var slotLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        slotLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        slotLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _timeSlotGrid.AutoGenerateColumns = false;
        _timeSlotGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Time", HeaderText = "開始時刻 (HH:mm)", Width = 140 });
        var packColumn = new DataGridViewComboBoxColumn { Name = "Pack", HeaderText = "デザイン", Width = 250, DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton };
        _timeSlotGrid.Columns.Add(packColumn);
        _timeSlotGrid.CellEndEdit += OnTimeSlotCellEndEdit;
        slotLayout.Controls.Add(_timeSlotGrid, 0, 0);

        var slotButtons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        var addSlotButton = new Button { Text = "行を追加", AutoSize = true };
        addSlotButton.Click += OnAddTimeSlotClicked;
        var removeSlotButton = new Button { Text = "選択行を削除", AutoSize = true };
        removeSlotButton.Click += OnRemoveTimeSlotClicked;
        slotButtons.Controls.Add(addSlotButton);
        slotButtons.Controls.Add(removeSlotButton);
        slotLayout.Controls.Add(slotButtons, 0, 1);

        slotGroup.Controls.Add(slotLayout);
        layout.Controls.Add(slotGroup);

        page.Controls.Add(layout);

        _modeManual.CheckedChanged += (_, _) => OnScheduleControlsChanged();
        _modeInterval.CheckedChanged += (_, _) => OnScheduleControlsChanged();
        _modeTimeOfDay.CheckedChanged += (_, _) => OnScheduleControlsChanged();
        _intervalMinutes.ValueChanged += (_, _) => OnScheduleControlsChanged();
        _switchOnStartup.CheckedChanged += (_, _) => OnScheduleControlsChanged();
        _selectSequential.CheckedChanged += (_, _) => OnScheduleControlsChanged();
        _selectRandom.CheckedChanged += (_, _) => OnScheduleControlsChanged();
        _activeListCombo.SelectedIndexChanged += (_, _) => OnScheduleControlsChanged();

        return page;
    }

    private void LoadScheduleUi()
    {
        _suppressEvents = true;
        var schedule = _settings.Schedule;
        _modeManual.Checked = schedule.Mode == ScheduleMode.Manual;
        _modeInterval.Checked = schedule.Mode == ScheduleMode.Interval;
        _modeTimeOfDay.Checked = schedule.Mode == ScheduleMode.TimeOfDay;
        _intervalMinutes.Value = Math.Clamp(schedule.IntervalMinutes, 1, 1440);
        _switchOnStartup.Checked = schedule.SwitchOnStartup;
        _selectSequential.Checked = schedule.SelectionMode == CursorSelectionMode.Sequential;
        _selectRandom.Checked = schedule.SelectionMode == CursorSelectionMode.Random;
        _suppressEvents = false;

        RefreshActiveListCombo();
        RefreshTimeSlotGridRows();
        UpdateScheduleControlsEnabled();
    }

    private void RefreshActiveListCombo()
    {
        var previousId = _settings.Schedule.ActiveListId;
        _suppressEvents = true;
        _activeListCombo.Items.Clear();
        _activeListCombo.Items.Add("(すべてのデザイン)");
        foreach (var list in _settings.Lists)
        {
            _activeListCombo.Items.Add(list);
        }
        var match = previousId is null ? 0 : _settings.Lists.FindIndex(l => l.Id == previousId) + 1;
        _activeListCombo.SelectedIndex = match >= 0 ? match : 0;
        _suppressEvents = false;
    }

    private void RefreshTimeSlotPackColumn()
    {
        if (_timeSlotGrid.Columns["Pack"] is DataGridViewComboBoxColumn packColumn)
        {
            packColumn.DataSource = null;
            packColumn.DataSource = _settings.Packs.ToList();
            packColumn.DisplayMember = "Name";
            packColumn.ValueMember = "Id";
        }
    }

    private void RefreshTimeSlotGridRows()
    {
        RefreshTimeSlotPackColumn();
        _suppressEvents = true;
        _timeSlotGrid.Rows.Clear();
        foreach (var slot in _settings.Schedule.TimeSlots.OrderBy(s => s.StartMinutes))
        {
            var timeText = TimeSpan.FromMinutes(slot.StartMinutes).ToString(@"hh\:mm");
            var rowIndex = _timeSlotGrid.Rows.Add(timeText, slot.PackId);
            _timeSlotGrid.Rows[rowIndex].Tag = slot;
        }
        _suppressEvents = false;
    }

    private void OnAddTimeSlotClicked(object? sender, EventArgs e)
    {
        if (_settings.Packs.Count == 0)
        {
            MessageBox.Show(this, "先に「パック管理」タブでデザインを登録してください。", "確認", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var slot = new TimeSlotEntry { StartMinutes = 0, PackId = _settings.Packs[0].Id };
        _settings.Schedule.TimeSlots.Add(slot);
        _saveSettings();
        RefreshTimeSlotGridRows();
    }

    private void OnRemoveTimeSlotClicked(object? sender, EventArgs e)
    {
        if (_timeSlotGrid.CurrentRow?.Tag is TimeSlotEntry slot)
        {
            _settings.Schedule.TimeSlots.Remove(slot);
            _saveSettings();
            RefreshTimeSlotGridRows();
        }
    }

    private void OnTimeSlotCellEndEdit(object? sender, DataGridViewCellEventArgs e)
    {
        if (_suppressEvents)
        {
            return;
        }
        var row = _timeSlotGrid.Rows[e.RowIndex];
        if (row.Tag is not TimeSlotEntry slot)
        {
            return;
        }

        if (e.ColumnIndex == _timeSlotGrid.Columns["Time"]!.Index)
        {
            var text = row.Cells["Time"].Value?.ToString() ?? "";
            if (TimeSpan.TryParseExact(text, @"hh\:mm", null, out var time) ||
                TimeSpan.TryParse(text, out time))
            {
                slot.StartMinutes = (int)time.TotalMinutes;
            }
            else
            {
                MessageBox.Show(this, "時刻は HH:mm 形式で入力してください (例: 09:00)。", "確認", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                row.Cells["Time"].Value = TimeSpan.FromMinutes(slot.StartMinutes).ToString(@"hh\:mm");
                return;
            }
        }
        else if (e.ColumnIndex == _timeSlotGrid.Columns["Pack"]!.Index)
        {
            if (row.Cells["Pack"].Value is string packId)
            {
                slot.PackId = packId;
            }
        }

        _saveSettings();
    }

    private void OnScheduleControlsChanged()
    {
        if (_suppressEvents)
        {
            return;
        }

        var schedule = _settings.Schedule;
        schedule.Mode = _modeInterval.Checked ? ScheduleMode.Interval
            : _modeTimeOfDay.Checked ? ScheduleMode.TimeOfDay
            : ScheduleMode.Manual;
        schedule.IntervalMinutes = (int)_intervalMinutes.Value;
        schedule.SwitchOnStartup = _switchOnStartup.Checked;
        schedule.SelectionMode = _selectRandom.Checked ? CursorSelectionMode.Random : CursorSelectionMode.Sequential;
        schedule.ActiveListId = _activeListCombo.SelectedItem is CursorListModel list ? list.Id : null;

        UpdateScheduleControlsEnabled();
        _saveSettings();
        _scheduler.OnSettingsChanged();
    }

    private void UpdateScheduleControlsEnabled()
    {
        _intervalMinutes.Enabled = _modeInterval.Checked;
        _timeSlotGrid.Enabled = _modeTimeOfDay.Checked;
        var selectionRelevant = _modeInterval.Checked || _switchOnStartup.Checked;
        _selectSequential.Enabled = selectionRelevant;
        _selectRandom.Enabled = selectionRelevant;
    }

    // ----- General tab ---------------------------------------------------

    private TabPage BuildGeneralTab()
    {
        var page = new TabPage("全般");
        var layout = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(14), AutoSize = true };

        _runAtStartupBox.Checked = StartupRegistration.IsRegistered();
        _runAtStartupBox.CheckedChanged += (_, _) => StartupRegistration.SetEnabled(_runAtStartupBox.Checked);
        layout.Controls.Add(_runAtStartupBox);

        _checkForUpdatesBox.Checked = _settings.CheckForUpdatesOnStartup;
        _checkForUpdatesBox.CheckedChanged += (_, _) =>
        {
            _settings.CheckForUpdatesOnStartup = _checkForUpdatesBox.Checked;
            _saveSettings();
        };
        layout.Controls.Add(_checkForUpdatesBox);

        var infoLabel = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(560, 0),
            Margin = new Padding(0, 16, 0, 0),
            Text = $"インポートしたカーソルファイルの保存場所:\n{SettingsStore.CursorPacksDir}\n\n" +
                   "デザインは Windows の「マウスのプロパティ > ポインター」の一覧にも表示され、そちらから選択することもできます。",
        };
        layout.Controls.Add(infoLabel);

        page.Controls.Add(layout);
        return page;
    }

    private static string? PromptText(IWin32Window owner, string prompt, string defaultValue)
    {
        using var dialog = new Form
        {
            Text = "入力",
            Width = 420,
            Height = 150,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            MaximizeBox = false,
        };
        var label = new Label { Text = prompt, AutoSize = true, Left = 12, Top = 12 };
        var textBox = new TextBox { Text = defaultValue, Left = 12, Top = 36, Width = 380 };
        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Left = 216, Top = 70, Width = 80 };
        var cancel = new Button { Text = "キャンセル", DialogResult = DialogResult.Cancel, Left = 312, Top = 70, Width = 80 };
        dialog.Controls.Add(label);
        dialog.Controls.Add(textBox);
        dialog.Controls.Add(ok);
        dialog.Controls.Add(cancel);
        dialog.AcceptButton = ok;
        dialog.CancelButton = cancel;

        return dialog.ShowDialog(owner) == DialogResult.OK ? textBox.Text.Trim() : null;
    }
}
