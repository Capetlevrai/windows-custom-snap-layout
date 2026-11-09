using System.Text;

namespace CustomFancyZone;

/// <summary>Fenêtres proposées par l'assistance d'ancrage, et leurs icônes.</summary>
internal static class AppWindows
{
    private static readonly Dictionary<(string, int), Icon?> Icons = [];
    private static readonly uint OwnPid = (uint)Environment.ProcessId;

    public static string? ExePathOf(IntPtr hwnd)
    {
        Native.GetWindowThreadProcessId(hwnd, out var pid);
        var process = Native.OpenProcess(Native.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (process == IntPtr.Zero) return null;
        try
        {
            var sb = new StringBuilder(1024);
            int size = sb.Capacity;
            return Native.QueryFullProcessImageName(process, 0, sb, ref size) ? sb.ToString(0, size) : null;
        }
        finally
        {
            Native.CloseHandle(process);
        }
    }

    /// <summary>
    /// Fenêtres qu'on peut ancrer, de la plus récemment utilisée à la plus ancienne (comme Windows),
    /// sans celles de <paramref name="exclude"/>.
    /// </summary>
    public static List<IntPtr> Candidates(ISet<IntPtr> exclude, int max)
    {
        var list = new List<IntPtr>();
        Native.EnumWindows((hwnd, _) =>
        {
            if (!exclude.Contains(hwnd) && IsSnappable(hwnd)) list.Add(hwnd);
            return list.Count < max;
        }, IntPtr.Zero);
        return list;
    }

    /// <summary>Icône de l'exécutable de la fenêtre (mise en cache).</summary>
    public static Icon? IconOf(IntPtr hwnd, int size)
    {
        var path = ExePathOf(hwnd);
        if (path is null) return null;
        if (Icons.TryGetValue((path, size), out var cached)) return cached;
        Icon? icon = null;
        try { icon = Icon.ExtractIcon(path, 0, size) ?? Icon.ExtractAssociatedIcon(path); }
        catch (Exception ex) { Log.Write($"Icône de {path} illisible : {ex.Message}"); }
        Icons[(path, size)] = icon;
        return icon;
    }

    /// <summary>
    /// Une fenêtre d'application redimensionnable : visible, sans propriétaire, ni palette ni calque transparent,
    /// pas masquée par DWM (autre bureau virtuel), avec un titre. Les nôtres sont exclues.
    /// </summary>
    private static bool IsSnappable(IntPtr hwnd)
    {
        if (!Native.IsWindowVisible(hwnd) || Native.GetWindow(hwnd, Native.GW_OWNER) != IntPtr.Zero) return false;
        long style = Native.GetWindowLongPtr(hwnd, Native.GWL_STYLE).ToInt64();
        long exStyle = Native.GetWindowLongPtr(hwnd, Native.GWL_EXSTYLE).ToInt64();
        if ((style & Native.WS_THICKFRAME) == 0) return false;
        if ((exStyle & (Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE | Native.WS_EX_TRANSPARENT)) != 0) return false;
        if (Native.DwmGetWindowAttributeInt(hwnd, Native.DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0 && cloaked != 0)
            return false;
        if (Native.GetWindowTextLength(hwnd) == 0) return false;
        Native.GetWindowThreadProcessId(hwnd, out var pid);
        return pid != OwnPid;
    }
}
