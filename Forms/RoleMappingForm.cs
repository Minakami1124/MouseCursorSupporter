using MouseCursorSupporter.Core;

namespace MouseCursorSupporter.Forms;

/// <summary>
/// Lets the user review (and correct) the auto-detected role assignment for a freshly
/// extracted cursor pack before it's registered as a Windows pointer scheme.
/// </summary>
public sealed class RoleMappingForm : Form
{
    private sealed record FileChoice(string Display, string? Path)
    {
        public override string ToString() => Display;
    }

    private readonly TextBox _nameBox;
    private readonly Dictionary<CursorRole, ComboBox> _combos = new();
    private readonly HashSet<string> _existingNames;

    public string ResultPackName { get; private set; } = "";
    public Dictionary<CursorRole, string> ResultRoleFiles { get; } = new();

    public RoleMappingForm(string suggestedName, string folderPath, RoleDetector.DetectionResult detection, HashSet<string> existingNames)
    {
        _existingNames = existingNames;

        Text = "カーソルの役割を確認";
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        Width = 560;
        Height = 640;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            Padding = new Padding(12),
            RowCount = 3,
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var namePanel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        namePanel.Controls.Add(new Label { Text = "デザイン名:", AutoSize = true, Padding = new Padding(0, 6, 6, 0) });
        _nameBox = new TextBox { Text = suggestedName, Width = 350 };
        namePanel.Controls.Add(_nameBox);
        root.Controls.Add(namePanel, 0, 0);

        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        var grid = new TableLayoutPanel
        {
            ColumnCount = 2,
            AutoSize = true,
            Dock = DockStyle.Top,
            Padding = new Padding(0, 4, 0, 4),
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        // Common choice list shared as base; each role gets its own combo instance.
        var allFileChoices = detection.Assigned.Values
            .Concat(detection.Candidates.Values.SelectMany(v => v))
            .Concat(detection.Unmatched)
            .Distinct()
            .OrderBy(p => Path.GetFileName(p))
            .Select(p => new FileChoice(Path.GetRelativePath(folderPath, p), p))
            .ToList();

        int row = 0;
        foreach (var role in CursorRoleInfo.SchemeOrder)
        {
            grid.RowCount = row + 1;
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var label = new Label
            {
                Text = CursorRoleInfo.DisplayNameJa[role],
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Padding = new Padding(0, 6, 12, 0),
            };
            grid.Controls.Add(label, 0, row);

            var combo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 360,
                Margin = new Padding(0, 3, 0, 3),
            };
            combo.Items.Add(new FileChoice("(なし)", null));
            foreach (var choice in allFileChoices)
            {
                combo.Items.Add(choice);
            }

            var preselected = detection.Assigned.TryGetValue(role, out var assignedPath) ? assignedPath : null;
            var selectIndex = 0;
            if (preselected is not null)
            {
                for (var i = 0; i < combo.Items.Count; i++)
                {
                    if (((FileChoice)combo.Items[i]!).Path == preselected)
                    {
                        selectIndex = i;
                        break;
                    }
                }
            }
            combo.SelectedIndex = selectIndex;

            _combos[role] = combo;
            grid.Controls.Add(combo, 1, row);
            row++;
        }

        scroll.Controls.Add(grid);
        root.Controls.Add(scroll, 0, 1);

        var buttonPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, AutoSize = true };
        var okButton = new Button { Text = "この内容で登録", AutoSize = true };
        okButton.Click += OnOkClicked;
        var cancelButton = new Button { Text = "キャンセル", AutoSize = true, DialogResult = DialogResult.Cancel };
        buttonPanel.Controls.Add(okButton);
        buttonPanel.Controls.Add(cancelButton);
        root.Controls.Add(buttonPanel, 0, 2);

        Controls.Add(root);
        AcceptButton = okButton;
        CancelButton = cancelButton;
    }

    private void OnOkClicked(object? sender, EventArgs e)
    {
        var name = _nameBox.Text.Trim();
        if (name.Length == 0)
        {
            MessageBox.Show(this, "デザイン名を入力してください。", "確認", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (_existingNames.Contains(name))
        {
            MessageBox.Show(this, "同じ名前のデザインが既に登録されています。別の名前を指定してください。", "確認", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var roleFiles = new Dictionary<CursorRole, string>();
        foreach (var (role, combo) in _combos)
        {
            if (combo.SelectedItem is FileChoice { Path: not null } choice)
            {
                roleFiles[role] = choice.Path;
            }
        }

        if (!roleFiles.ContainsKey(CursorRole.Arrow)
            && MessageBox.Show(this, "「通常の選択」(矢印)が未割り当てです。このまま登録しますか?", "確認",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
        {
            return;
        }

        ResultPackName = name;
        ResultRoleFiles.Clear();
        foreach (var kv in roleFiles)
        {
            ResultRoleFiles[kv.Key] = kv.Value;
        }

        DialogResult = DialogResult.OK;
        Close();
    }
}
