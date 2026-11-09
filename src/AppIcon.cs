using System.Drawing.Drawing2D;

namespace CustomFancyZone;

/// <summary>Icône de la zone de notification, dessinée à la volée (une grande zone + deux petites).</summary>
internal static class AppIcon
{
    public static Icon Create()
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var accent = new SolidBrush(Color.FromArgb(0x4C, 0xC2, 0xFF));
            using var gray = new SolidBrush(Color.FromArgb(0xB4, 0xB4, 0xB4));
            Fill(g, accent, new RectangleF(1, 3, 17, 26));
            Fill(g, gray, new RectangleF(20, 3, 11, 12));
            Fill(g, gray, new RectangleF(20, 17, 11, 12));
        }
        return Icon.FromHandle(bmp.GetHicon());
    }

    private static void Fill(Graphics g, Brush brush, RectangleF r)
    {
        using var path = FlyoutForm.RoundedPath(r, 3);
        g.FillPath(brush, path);
    }
}
