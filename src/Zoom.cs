using System.Runtime.InteropServices;

namespace CustomFancyZone;

/// <summary>
/// Zoom d'une application par ses raccourcis clavier : Ctrl − / Ctrl + / Ctrl 0 (pavé numérique pour − et +,
/// indépendants de la disposition du clavier). L'outil ne peut pas lire le zoom actuel d'une application :
/// pour un niveau précis, il remet à 100 % puis avance d'autant de paliers qu'il faut (paliers de Chrome/Electron).
/// </summary>
internal static class Zoom
{
    /// <summary>Paliers de zoom de Chrome, Edge et des applications Electron (Claude, VS Code, Discord…).</summary>
    public static readonly int[] Levels = [25, 33, 50, 67, 75, 80, 90, 100, 110, 125, 150, 175, 200, 250, 300, 400, 500];

    private const ushort VK_CONTROL = 0x11;
    private const ushort VK_0 = 0x30;
    private const ushort VK_ADD = 0x6B;
    private const ushort VK_SUBTRACT = 0x6D;
    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public IntPtr extra; }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT { public ushort wVk, wScan; public uint dwFlags, time; public IntPtr extra; }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion { [FieldOffset(0)] public MOUSEINPUT mi; [FieldOffset(0)] public KEYBDINPUT ki; }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT { public uint type; public InputUnion u; }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, INPUT[] inputs, int size);

    /// <summary>Un cran : -1 dézoome, +1 zoome, 0 remet à 100 %.</summary>
    public static bool Step(IntPtr hwnd, int direction)
    {
        if (!Focus(hwnd)) return false;
        Press(direction < 0 ? VK_SUBTRACT : direction > 0 ? VK_ADD : VK_0);
        return true;
    }

    /// <summary>Met l'application au palier le plus proche de <paramref name="percent"/>.</summary>
    public static bool Set(IntPtr hwnd, int percent)
    {
        if (!Focus(hwnd)) return false;
        int target = Array.IndexOf(Levels, Nearest(percent));
        int hundred = Array.IndexOf(Levels, 100);
        Press(VK_0);
        for (int i = 0; i < Math.Abs(target - hundred); i++)
        {
            Thread.Sleep(40);
            Press(target < hundred ? VK_SUBTRACT : VK_ADD);
        }
        return true;
    }

    public static int Nearest(int percent) => Levels.MinBy(l => Math.Abs(l - percent));

    /// <summary>
    /// Donne le focus à la fenêtre et attend qu'elle l'ait vraiment. Sans cela, on n'envoie rien :
    /// les touches iraient à une autre application.
    /// </summary>
    private static bool Focus(IntPtr hwnd)
    {
        if (!Native.IsWindow(hwnd)) return false;
        Native.SetForegroundWindow(hwnd);
        for (int i = 0; i < 20 && Native.GetForegroundWindow() != hwnd; i++) Thread.Sleep(15);
        bool ok = Native.GetForegroundWindow() == hwnd;
        if (!ok) Log.Write($"Zoom annulé : impossible de donner le focus à {Native.Describe(hwnd)}");
        return ok;
    }

    private static void Press(ushort vk)
    {
        INPUT Key(ushort k, bool up) => new()
        {
            type = INPUT_KEYBOARD,
            u = new InputUnion { ki = new KEYBDINPUT { wVk = k, dwFlags = up ? KEYEVENTF_KEYUP : 0 } },
        };
        var inputs = new[] { Key(VK_CONTROL, false), Key(vk, false), Key(vk, true), Key(VK_CONTROL, true) };
        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
    }
}
