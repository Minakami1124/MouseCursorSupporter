namespace MouseCursorSupporter;

static class Program
{
    [STAThread]
    static void Main()
    {
        using var singleInstanceMutex = new Mutex(initiallyOwned: true, "MouseCursorSupporter-SingleInstance", out var createdNew);
        if (!createdNew)
        {
            MessageBox.Show("マウスカーソル自動切替は既に起動しています(タスクトレイを確認してください)。",
                "マウスカーソル自動切替", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayAppContext());
    }
}
