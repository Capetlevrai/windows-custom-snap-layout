using System.Runtime.InteropServices;

namespace CustomFancyZone;

internal static class WindowPlacer
{
    /// <summary>
    /// Rectangle (pixels physiques) d'une zone sur la zone de travail d'un écran.
    /// <paramref name="gap"/> laisse un espace entre les fenêtres et autour d'elles.
    /// </summary>
    public static Rectangle ZoneRect(RectangleF zone, Rectangle work, int gap)
    {
        int l = work.Left + (int)Math.Round(zone.Left * work.Width);
        int t = work.Top + (int)Math.Round(zone.Top * work.Height);
        int r = work.Left + (int)Math.Round(zone.Right * work.Width);
        int b = work.Top + (int)Math.Round(zone.Bottom * work.Height);
        if (gap > 0)
        {
            int half = gap / 2;
            l += zone.Left <= 0.001f ? gap : gap - half;
            t += zone.Top <= 0.001f ? gap : gap - half;
            r -= zone.Right >= 0.999f ? gap : half;
            b -= zone.Bottom >= 0.999f ? gap : half;
        }
        return Rectangle.FromLTRB(l, t, Math.Max(l + 50, r), Math.Max(t + 50, b));
    }

    /// <summary>
    /// Place la fenêtre pour que sa partie visible occupe exactement <paramref name="target"/>.
    /// Windows 11 entoure les fenêtres d'une bordure invisible (zone de redimensionnement) :
    /// on la compense avec DWMWA_EXTENDED_FRAME_BOUNDS.
    /// </summary>
    /// <summary>Partie visible de la fenêtre (sans les bordures invisibles), en pixels physiques.</summary>
    public static Rectangle FrameOf(IntPtr hwnd)
    {
        if (Native.DwmGetWindowAttribute(hwnd, Native.DWMWA_EXTENDED_FRAME_BOUNDS, out var f,
                Marshal.SizeOf<Native.RECT>()) == 0)
            return f.ToRectangle();
        Native.GetWindowRect(hwnd, out var w);
        return w.ToRectangle();
    }

    public static bool Place(IntPtr hwnd, Rectangle target, bool activate = true)
    {
        if (!Native.IsWindow(hwnd)) return false;
        if (Native.IsIconic(hwnd) || Native.IsZoomed(hwnd))
            Native.ShowWindow(hwnd, activate ? Native.SW_RESTORE : Native.SW_SHOWNOACTIVATE);

        bool ok = false;
        // Deux passes : si la fenêtre change d'écran (et donc de mise à l'échelle), ses bordures changent aussi.
        for (int pass = 0; pass < 2; pass++)
        {
            if (!Native.GetWindowRect(hwnd, out var window)) return false;
            var frame = window;
            if (Native.DwmGetWindowAttribute(hwnd, Native.DWMWA_EXTENDED_FRAME_BOUNDS, out var f,
                    Marshal.SizeOf<Native.RECT>()) == 0)
                frame = f;

            int left = frame.Left - window.Left, top = frame.Top - window.Top;
            int right = window.Right - frame.Right, bottom = window.Bottom - frame.Bottom;
            ok = Native.SetWindowPos(hwnd, IntPtr.Zero,
                target.Left - left, target.Top - top,
                target.Width + left + right, target.Height + top + bottom,
                Native.SWP_NOZORDER | Native.SWP_NOACTIVATE | Native.SWP_NOOWNERZORDER);
            if (!ok) break;
        }

        // Passe devant les autres fenêtres ; seule la fenêtre survolée récupère le focus.
        Native.SetWindowPos(hwnd, Native.HWND_TOP, 0, 0, 0, 0,
            Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
        if (activate) Native.SetForegroundWindow(hwnd);
        return ok;
    }
}
