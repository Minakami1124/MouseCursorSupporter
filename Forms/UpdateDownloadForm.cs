using MouseCursorSupporter.Core;

namespace MouseCursorSupporter.Forms;

public sealed class UpdateDownloadForm : Form
{
    private readonly ProgressBar _progressBar = new() { Dock = DockStyle.Top, Height = 24, Minimum = 0, Maximum = 100 };
    private readonly Label _statusLabel = new() { Dock = DockStyle.Top, Height = 24, TextAlign = ContentAlignment.MiddleLeft };
    private readonly CancellationTokenSource _cts = new();

    public string? DownloadedFilePath { get; private set; }
    public Exception? Error { get; private set; }

    public UpdateDownloadForm(UpdateInfo update)
    {
        Text = "アップデートをダウンロード中...";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        ControlBox = false;
        Width = 420;
        Height = 150;

        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16) };
        _statusLabel.Text = $"{update.AssetFileName} を取得しています...";
        panel.Controls.Add(_progressBar);
        panel.Controls.Add(_statusLabel);

        var cancelButton = new Button { Text = "キャンセル", Dock = DockStyle.Bottom, Height = 32 };
        cancelButton.Click += (_, _) => { _cts.Cancel(); };

        Controls.Add(panel);
        Controls.Add(cancelButton);

        Shown += async (_, _) => await RunDownloadAsync(update);
    }

    private async Task RunDownloadAsync(UpdateInfo update)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), update.AssetFileName);
        var progress = new Progress<int>(percent =>
        {
            _progressBar.Value = Math.Clamp(percent, 0, 100);
            _statusLabel.Text = $"ダウンロード中... {percent}%";
        });

        try
        {
            await UpdateChecker.DownloadAsync(update.DownloadUrl, tempPath, progress, _cts.Token);
            DownloadedFilePath = tempPath;
            DialogResult = DialogResult.OK;
        }
        catch (OperationCanceledException)
        {
            DialogResult = DialogResult.Cancel;
        }
        catch (Exception ex)
        {
            Error = ex;
            DialogResult = DialogResult.Abort;
        }
        finally
        {
            Close();
        }
    }
}
