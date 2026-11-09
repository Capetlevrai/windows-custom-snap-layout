using System.Globalization;
using System.Runtime.InteropServices;

namespace CustomFancyZone;

/// <summary>Commandes de diagnostic (voir README, section « Ligne de commande »).</summary>
internal static class Cli
{
    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(int processId);

    public static int Run(string[] args, string exeDir)
    {
        AttachConsole(-1);
        var store = new ConfigStore(Path.Combine(exeDir, "layouts.json"));

        switch (args[0].ToLowerInvariant())
        {
            case "--quit":
                if (EventWaitHandle.TryOpenExisting(TrayApp.QuitEventName, out var ev))
                {
                    ev.Set();
                    Console.WriteLine("Signal d'arrêt envoyé.");
                }
                else
                {
                    Console.WriteLine("Aucune instance en cours.");
                }
                return 0;

            case "--check":
            {
                store.EnsureFile();
                var problems = store.Load();
                Console.WriteLine($"{store.FilePath}");
                for (int i = 0; i < store.Layouts.Count; i++)
                {
                    var l = store.Layouts[i];
                    var zones = l.Zones.Select((z, j) => Pct(z) + (l.Zoom[j] is int zm ? $" zoom {zm} %" : ""));
                    Console.WriteLine($"  [{i}] {l.Name} : {string.Join("  ", zones)}");
                }
                foreach (var p in problems) Console.WriteLine("  ! " + p);
                return problems.Count == 0 ? 0 : 1;
            }

            case "--hittest" when args.Length >= 3:
            {
                var pt = new Native.POINT(int.Parse(args[1], CultureInfo.InvariantCulture),
                    int.Parse(args[2], CultureInfo.InvariantCulture));
                var child = Native.WindowFromPoint(pt);
                var root = Native.GetAncestor(child, Native.GA_ROOT);
                Console.WriteLine($"Point {pt}");
                Console.WriteLine($"  sous le curseur : {Native.Describe(child)} -> hit {Native.HitTest(child, pt)}");
                if (root != child)
                    Console.WriteLine($"  fenêtre racine  : {Native.Describe(root)} -> hit {Native.HitTest(root, pt)}");
                Console.WriteLine($"  (HTMAXBUTTON = {Native.HTMAXBUTTON})");
                store.EnsureFile();
                store.Load();
                if (TrayApp.CustomButtonRect(root, store.Config) is Rectangle custom)
                    Console.WriteLine($"  bouton Agrandir dessiné par l'appli (customButtons) : {custom} -> " +
                                      (custom.Contains(pt.X, pt.Y) ? "SOUS LE POINT" : "ailleurs"));
                return 0;
            }

            case "--place" when args.Length >= 4:
            {
                store.EnsureFile();
                store.Load();
                var hwnd = new IntPtr(ParseHandle(args[1]));
                var layout = store.Layouts[int.Parse(args[2], CultureInfo.InvariantCulture)];
                var zone = layout.Zones[int.Parse(args[3], CultureInfo.InvariantCulture)];
                Native.GetWindowRect(hwnd, out var wr);
                var center = new Native.POINT((wr.Left + wr.Right) / 2, (wr.Top + wr.Bottom) / 2);
                var hmon = Native.MonitorFromPoint(center, Native.MONITOR_DEFAULTTONEAREST);
                var gap = (int)Math.Round(store.Config.ZoneGap * Native.ScaleOf(hmon));
                var rect = WindowPlacer.ZoneRect(zone, Native.WorkArea(hmon), gap);
                bool ok = WindowPlacer.Place(hwnd, rect);
                Console.WriteLine($"{(ok ? "OK" : "ÉCHEC")} : {Native.Describe(hwnd)} -> « {layout.Name} » {rect}");
                return ok ? 0 : 1;
            }

            case "--zoom" when args.Length >= 3:
            {
                var hwnd = new IntPtr(ParseHandle(args[1]));
                bool ok = args[2] switch
                {
                    "+" => Zoom.Step(hwnd, 1),
                    "-" => Zoom.Step(hwnd, -1),
                    "0" => Zoom.Step(hwnd, 0),
                    var pct => Zoom.Set(hwnd, int.Parse(pct, CultureInfo.InvariantCulture)),
                };
                Console.WriteLine(ok ? "OK" : "Focus impossible : rien n'a été envoyé.");
                return ok ? 0 : 1;
            }

            case "--assist" when args.Length >= 4:
                return Assist(store, args);

            case "--demo":
                return Demo(store, args);

            default:
                Console.WriteLine("""
                    CustomFancyZone : sans argument, lance l'outil dans la zone de notification.
                      --check                      valide layouts.json et liste les dispositions
                      --hittest X Y                ce que Windows trouve sous un point (pixels physiques)
                      --place HWND DISPO ZONE      place une fenêtre (HWND en 0x… ou décimal)
                      --assist HWND DISPO ZONE [HWND…]  place une fenêtre puis lance l'assistance d'ancrage
                                                   (HWND… = seules fenêtres proposées, pour les tests)
                      --zoom HWND +|-|0|POURCENT   zoome une fenêtre (Ctrl + / Ctrl − / Ctrl 0)
                      --demo [SECONDES] [DISPO ZONE]  affiche le menu au centre de l'écran
                      --quit                       ferme l'instance en cours
                    """);
                return args[0] is "--help" or "-h" or "/?" ? 0 : 2;
        }
    }

    /// <summary>Place une fenêtre comme un clic dans le menu, puis lance l'assistance d'ancrage.</summary>
    private static int Assist(ConfigStore store, string[] args)
    {
        store.EnsureFile();
        store.Load();
        var hwnd = new IntPtr(ParseHandle(args[1]));
        var layout = store.Layouts[int.Parse(args[2], CultureInfo.InvariantCulture)];
        int zone = int.Parse(args[3], CultureInfo.InvariantCulture);

        Native.GetWindowRect(hwnd, out var wr);
        var hmon = Native.MonitorFromPoint(new Native.POINT((wr.Left + wr.Right) / 2, (wr.Top + wr.Bottom) / 2),
            Native.MONITOR_DEFAULTTONEAREST);
        var work = Native.WorkArea(hmon);
        double scale = Native.ScaleOf(hmon);
        int gap = (int)Math.Round(store.Config.ZoneGap * scale);
        Rectangle Rect(int j) => WindowPlacer.ZoneRect(layout.Zones[j], work, gap);

        var others = Enumerable.Range(0, layout.Zones.Count).Where(j => j != zone).ToList();
        var session = new PlacementSession(work, others.Select(Rect));
        session.Place(hwnd, Rect(zone), activate: false);
        var zooms = others.Select(j => layout.Zoom[j]).ToList();

        // Arguments suivants (facultatifs) : seules fenêtres à proposer, pour tester sans toucher aux autres.
        var only = args.Skip(4).Select(a => new IntPtr(ParseHandle(a))).ToHashSet();
        using var assist = new SnapAssistForm();
        assist.Start(session, zooms, hwnd, Theme.Current(), scale, only.Count > 0 ? only.Contains : null);
        if (!assist.Visible)
        {
            Console.WriteLine("Aucune fenêtre à proposer.");
            return 0;
        }
        void Report()
        {
            Console.WriteLine($"Assistance 0x{assist.Handle.ToInt64():X} à {assist.Bounds}");
            foreach (var (window, bounds) in assist.Cells) Console.WriteLine($"  {bounds} : {Native.Describe(window)}");
        }
        Report();
        assist.VisibleChanged += (_, _) =>
        {
            if (assist.Visible) Report();
            else Application.ExitThread();
        };
        assist.LocationChanged += (_, _) => { if (assist.Visible) Report(); };
        Application.Run();
        Console.WriteLine("Assistance terminée.");
        return 0;
    }

    private static int Demo(ConfigStore store, string[] args)
    {
        store.EnsureFile();
        store.Load();
        int seconds = args.Length > 1 ? int.Parse(args[1], CultureInfo.InvariantCulture) : 8;

        Native.GetCursorPos(out var cursor);
        var hmon = Native.MonitorFromPoint(cursor, Native.MONITOR_DEFAULTTONEAREST);
        var work = Native.WorkArea(hmon);
        double scale = Native.ScaleOf(hmon);
        double aspect = Math.Clamp(work.Width / (double)Math.Max(1, work.Height), 1.2, 2.4);

        using var flyout = new FlyoutForm();
        var size = flyout.Prepare(store.Layouts, store.Config, Theme.Current(), scale, aspect);
        flyout.ShowAt(new Rectangle(work.Left + (work.Width - size.Width) / 2,
            work.Top + (work.Height - size.Height) / 2, size.Width, size.Height));
        if (args.Length > 3)
            flyout.SetHover(int.Parse(args[2], CultureInfo.InvariantCulture), int.Parse(args[3], CultureInfo.InvariantCulture));
        flyout.ZoneChosen += (l, z) => Console.WriteLine($"Choisi : « {store.Layouts[l].Name} » zone {z + 1}");

        using var timer = new System.Windows.Forms.Timer { Interval = Math.Max(1, seconds) * 1000 };
        timer.Tick += (_, _) => Application.ExitThread();
        timer.Start();
        Application.Run();
        return 0;
    }

    private static long ParseHandle(string s) =>
        s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? long.Parse(s[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture)
            : long.Parse(s, CultureInfo.InvariantCulture);

    private static string Pct(RectangleF z) =>
        string.Create(CultureInfo.InvariantCulture,
            $"[{z.X * 100:0.#}, {z.Y * 100:0.#}, {z.Width * 100:0.#}, {z.Height * 100:0.#}]");
}
