using MouseCursorSupporter.Core;

namespace MouseCursorSupporter.Forms;

public enum UpdateChoice
{
    Later,
    Skip,
    UpdateNow,
}

public sealed class UpdateAvailableForm : Form
{
    public UpdateChoice Choice { get; private set; } = UpdateChoice.Later;

    public UpdateAvailableForm(Version currentVersion, UpdateInfo update)
    {
        Text = "アップデートのお知らせ";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        Width = 420;
        Height = 200;

        var label = new Label
        {
            Text = $"新しいバージョンが利用可能です。\n\n現在: {currentVersion}\n最新: {update.Version}\n\n今すぐ更新しますか?",
            AutoSize = false,
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            TextAlign = ContentAlignment.MiddleLeft,
        };
        Controls.Add(label);

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 46,
            Padding = new Padding(8),
        };

        var updateButton = new Button { Text = "今すぐ更新", AutoSize = true };
        updateButton.Click += (_, _) => { Choice = UpdateChoice.UpdateNow; DialogResult = DialogResult.OK; Close(); };

        var laterButton = new Button { Text = "後で", AutoSize = true };
        laterButton.Click += (_, _) => { Choice = UpdateChoice.Later; DialogResult = DialogResult.Cancel; Close(); };

        var skipButton = new Button { Text = "このバージョンをスキップ", AutoSize = true };
        skipButton.Click += (_, _) => { Choice = UpdateChoice.Skip; DialogResult = DialogResult.Cancel; Close(); };

        buttonPanel.Controls.Add(updateButton);
        buttonPanel.Controls.Add(laterButton);
        buttonPanel.Controls.Add(skipButton);
        Controls.Add(buttonPanel);

        AcceptButton = updateButton;
        CancelButton = laterButton;
    }
}
