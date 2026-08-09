namespace MouseCursorSupporter.Core;

/// <summary>Draws a small self-contained tray icon so the app doesn't need an external .ico asset.</summary>
public static class TrayIconFactory
{
    public static Icon CreateArrowIcon()
    {
        using var bitmap = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            Point[] arrow =
            [
                new Point(6, 4),
                new Point(6, 27),
                new Point(12, 21),
                new Point(16, 29),
                new Point(20, 27),
                new Point(16, 19),
                new Point(24, 19),
            ];

            using var fill = new SolidBrush(Color.FromArgb(255, 41, 121, 255));
            using var outline = new Pen(Color.White, 1.5f);
            g.FillPolygon(fill, arrow);
            g.DrawPolygon(outline, arrow);
        }

        var handle = bitmap.GetHicon();
        return Icon.FromHandle(handle);
    }
}
