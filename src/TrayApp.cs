using System.Diagnostics;
using Microsoft.Win32;

namespace CustomFancyZone;

/// <summary>
/// Cœur de l'outil : surveille la souris, ouvre le menu quand elle reste sur un bouton Agrandir,
/// place la fenêtre dans la zone cliquée. Vit dans la zone de notification.
/// </summary>
internal sealed class TrayApp : ApplicationContext
{
    private const string AppName = "CustomFancyZone";
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public const string QuitEventName = @"Local\CustomFancyZone.Quit";

    private readonly ConfigStore _store;
    private readonly NotifyIcon _tray;
    private readonly FlyoutForm _flyout = new();
    private readonly PreviewForm _preview = new();
    private readonly SnapAssistForm _assist = new();
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 40 };
    private readonly System.Windows.Forms.Timer _reloadDebounce = new() { Interval = 300 };
    private readonly FileSystemWatcher _watcher;
    private readonly EventWaitHandle _quitEvent;
    private readonly uint _ownPid = (uint)Environment.ProcessId;
    private readonly ToolStripMenuItem _nativeItem;
    private readonly ToolStripMenuItem _startupItem;
    private readonly ToolStripMenuItem _pauseItem;
    private bool _paused;
    private bool _balloonIsNativeWarning;

    // Détection du survol.
    private Native.POINT _lastPt = new(int.MinValue, int.MinValue);
    private IntPtr _candidate, _candidateHitWnd, _suppressed;
    private long _candidateSince, _lastProbe;

    // Menu ouvert.
    private IntPtr _target;
    private IReadOnlyList<SnapLayout> _shownLayouts = [];
    private Rectangle _button, _work;
    private double _scale = 1;
    private Theme _theme = Theme.Current();
    private long _outsideSince = -1;

    public TrayApp(string exeDir)
    {
        _store = new ConfigStore(Path.Combine(exeDir, "layouts.json"));
        _store.EnsureFile();
        var problems = _store.Load();

        _nativeItem = new ToolStripMenuItem("Menu natif de Windows…", null, (_, _) => ToggleNative());
        _startupItem = new ToolStripMenuItem("Lancer avec Windows", null, (_, _) => ToggleStartup());
        _pauseItem = new ToolStripMenuItem("Pause", null, (_, _) => TogglePause());
        _tray = new NotifyIcon
        {
            Icon = AppIcon.Create(),
            Text = AppName,
            Visible = true,
            ContextMenuStrip = BuildMenu(),
        };
        _tray.DoubleClick += (_, _) => OpenConfig();
        _tray.BalloonTipClicked += (_, _) =>
        {
            if (_balloonIsNativeWarning && NativeSnap.IsEnabled()) ToggleNative();
        };
        _tray.ContextMenuStrip.Opening += (_, _) => RefreshMenu();

        _ = _flyout.Handle;
        _ = _preview.Handle;
        _ = _assist.Handle;
        _flyout.ZoneHovered += OnZoneHovered;
        _flyout.ZoneChosen += OnZoneChosen;
        _flyout.ZoomRequested += direction => Zoom.Step(_target, direction);

        RefreshMenu();

        _watcher = new FileSystemWatcher(exeDir, "layouts.json")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
            SynchronizingObject = _flyout,
        };
        _watcher.Changed += (_, _) => RestartReloadDebounce();
        _watcher.Created += (_, _) => RestartReloadDebounce();
        _watcher.Renamed += (_, _) => RestartReloadDebounce();
        _watcher.EnableRaisingEvents = true;
        _reloadDebounce.Tick += (_, _) =>
        {
            _reloadDebounce.Stop();
            Reload();
        };

        // Permet à « CustomFancyZone.exe --quit » (et au script de build) de fermer proprement l'instance.
        _quitEvent = new EventWaitHandle(false, EventResetMode.AutoReset, QuitEventName);
        ThreadPool.RegisterWaitForSingleObject(_quitEvent, (_, _) => _flyout.BeginInvoke(ExitThread),
            null, Timeout.Infinite, true);

        _timer.Tick += (_, _) => OnTick();
        _timer.Start();

        Log.Write($"Démarrage : {_store.Layouts.Count} dispositions, config {_store.FilePath}");
        if (problems.Count > 0) Warn(problems);
        else if (NativeSnap.IsEnabled()) WarnNative();
    }

    // ---------------------------------------------------------------- Boucle de surveillance

    private void OnTick()
    {
        if (_paused || _assist.Visible) return;
        Native.GetCursorPos(out var pt);
        long now = Environment.TickCount64;

        if (_flyout.Visible)
        {
            TickWhileOpen(pt, now);
            return;
        }

        if (Native.AnyMouseButtonDown())
        {
            // Clic ou glisser en cours : pas de menu.
            _candidate = IntPtr.Zero;
            _lastPt = new(int.MinValue, int.MinValue);
            return;
        }

        bool moved = pt.X != _lastPt.X || pt.Y != _lastPt.Y;
        _lastPt = pt;
        // Souris immobile : on revérifie quand même de temps en temps (une fenêtre a pu passer devant).
        if (moved || (_candidate == IntPtr.Zero && now - _lastProbe >= 300))
        {
            _lastProbe = now;
            if (!ProbeMaxButton(pt, out var root, out var hitWnd))
            {
                _candidate = IntPtr.Zero;
                _suppressed = IntPtr.Zero;
                return;
            }
            if (root != _candidate)
            {
                _candidate = root;
                _candidateHitWnd = hitWnd;
                _candidateSince = now;
            }
            _candidateCustomButton = _probedCustomButton;
        }

        if (_candidate != IntPtr.Zero && _candidate != _suppressed &&
            now - _candidateSince >= Math.Max(0, _store.Config.HoverDelayMs))
            ShowFlyout(pt);
    }

    private void TickWhileOpen(Native.POINT pt, long now)
    {
        var p = new Point(pt.X, pt.Y);
        bool overFlyout = _flyout.Bounds.Contains(p);

        if (Native.AnyMouseButtonDown() && !overFlyout)
        {
            // Clic ailleurs (y compris sur le bouton Agrandir lui-même) : on referme.
            HideFlyout();
            _suppressed = _target;
            return;
        }
        if (!Native.IsWindow(_target))
        {
            HideFlyout();
            return;
        }

        int slack = (int)Math.Round(6 * _scale);
        var buttonZone = Rectangle.Inflate(_button, slack, slack);
        var bridge = Rectangle.Union(buttonZone,
            new Rectangle(_button.Left - slack, Math.Min(_button.Bottom, _flyout.Top), _button.Width + 2 * slack,
                Math.Abs(_flyout.Top - _button.Bottom) + 1));
        bool inside = overFlyout || buttonZone.Contains(p) || bridge.Contains(p) ||
                      Rectangle.Inflate(_flyout.Bounds, slack, slack).Contains(p);

        if (inside)
        {
            _outsideSince = -1;
            return;
        }
        if (_outsideSince < 0) _outsideSince = now;
        else if (now - _outsideSince >= Math.Max(0, _store.Config.HideDelayMs)) HideFlyout();
    }

    /// <summary>La souris est-elle sur le bouton Agrandir d'une fenêtre (autre que les nôtres) ?</summary>
    private bool ProbeMaxButton(Native.POINT pt, out IntPtr root, out IntPtr hitWnd)
    {
        root = hitWnd = IntPtr.Zero;
        var child = Native.WindowFromPoint(pt);
        if (child == IntPtr.Zero) return false;
        root = Native.GetAncestor(child, Native.GA_ROOT);
        if (root == IntPtr.Zero) return false;

        // Une infobulle (fenêtre flottante sans titre, comme le « Maximize » d'Unreal) peut recouvrir le bouton :
        // on regarde la fenêtre qui est dessous.
        for (int guard = 0; guard < 8 && IsTooltipLike(root); guard++)
        {
            var below = Native.GetWindow(root, Native.GW_HWNDNEXT);
            while (below != IntPtr.Zero &&
                   !(Native.IsWindowVisible(below) && Native.GetWindowRect(below, out var br) &&
                     br.ToRectangle().Contains(pt.X, pt.Y)))
                below = Native.GetWindow(below, Native.GW_HWNDNEXT);
            if (below == IntPtr.Zero) return false;
            root = child = below;
        }

        Native.GetWindowThreadProcessId(root, out var pid);
        if (pid == _ownPid) return false;

        // Filtre bon marché : le bouton est forcément dans le haut de la fenêtre.
        if (!Native.GetWindowRect(root, out var wr)) return false;
        double scale = Native.ScaleOf(Native.MonitorFromPoint(pt, Native.MONITOR_DEFAULTTONEAREST));
        if (pt.Y - wr.Top > (int)Math.Round(120 * scale)) return false;

        _probedCustomButton = null;
        foreach (var h in child == root ? [root] : new[] { child, root })
        {
            if (Native.HitTest(h, pt) == Native.HTMAXBUTTON)
            {
                hitWnd = h;
                return true;
            }
        }

        // Application qui dessine son bouton sans le déclarer à Windows (Unreal…) : position connue.
        if (CustomButtonRect(root, _store.Config) is Rectangle custom && custom.Contains(pt.X, pt.Y))
        {
            hitWnd = root;
            _probedCustomButton = custom;
            return true;
        }
        return false;
    }

    private Rectangle? _probedCustomButton, _candidateCustomButton;

    private static bool IsTooltipLike(IntPtr hwnd)
    {
        long ex = Native.GetWindowLongPtr(hwnd, Native.GWL_EXSTYLE).ToInt64();
        return (ex & Native.WS_EX_TOOLWINDOW) != 0 && Native.GetWindowTextLength(hwnd) == 0;
    }

    /// <summary>Bouton Agrandir d'après la liste customButtons (classe de fenêtre), en pixels physiques.</summary>
    internal static Rectangle? CustomButtonRect(IntPtr root, AppConfig config)
    {
        var buttons = config.CustomButtons;
        if (buttons is not { Count: > 0 }) return null;
        string cls = Native.ClassOf(root);
        var rule = buttons.FirstOrDefault(b => string.Equals(b.WindowClass, cls, StringComparison.OrdinalIgnoreCase));
        if (rule is null) return null;

        var frame = WindowPlacer.FrameOf(root);
        uint dpi = Native.GetDpiForWindow(root);
        double s = dpi > 0 ? dpi / 96.0 : 1.0;
        int S(int v) => (int)Math.Round(v * s);
        return new Rectangle(frame.Right - S(rule.Right) - S(rule.Width), frame.Top + S(rule.Top), S(rule.Width), S(rule.Height));
    }

    /// <summary>Retrouve les bords du bouton en interrogeant la fenêtre autour du curseur.</summary>
    private static Rectangle ProbeButton(IntPtr hwnd, Native.POINT pt, double scale)
    {
        int step = Math.Max(1, (int)Math.Round(2 * scale));
        int max = (int)Math.Round(90 * scale);

        bool Hit(int x, int y) => Native.HitTest(hwnd, new Native.POINT(x, y)) == Native.HTMAXBUTTON;

        int Edge(int dx, int dy)
        {
            int d = 0;
            while (d < max && Hit(pt.X + (d + step) * dx, pt.Y + (d + step) * dy)) d += step;
            return d;
        }

        int left = Edge(-1, 0), right = Edge(1, 0), up = Edge(0, -1), down = Edge(0, 1);
        return Rectangle.FromLTRB(pt.X - left, pt.Y - up, pt.X + right + 1, pt.Y + down + 1);
    }

    // ---------------------------------------------------------------- Menu

    private void ShowFlyout(Native.POINT pt)
    {
        var target = _candidate;
        var hitWnd = _candidateHitWnd;
        _candidate = IntPtr.Zero;
        _suppressed = target; // pas de réouverture tant que la souris n'a pas quitté le bouton

        var layouts = _store.Layouts;
        if (layouts.Count == 0 || !Native.IsWindow(target)) return;

        var hmon = Native.MonitorFromPoint(pt, Native.MONITOR_DEFAULTTONEAREST);
        var work = Native.WorkArea(hmon);
        double scale = Native.ScaleOf(hmon);
        var button = _candidateCustomButton ?? ProbeButton(hitWnd, pt, scale);
        double aspect = Math.Clamp(work.Width / (double)Math.Max(1, work.Height), 1.2, 2.4);

        var theme = Theme.Current();
        var size = _flyout.Prepare(layouts, _store.Config, theme, scale, aspect);

        int margin = (int)Math.Round(8 * scale);
        int offset = (int)Math.Round(2 * scale);
        int x = button.Left + button.Width / 2 - size.Width / 2;
        int y = button.Bottom + offset;
        if (y + size.Height > work.Bottom - margin && button.Top - offset - size.Height >= work.Top + margin)
            y = button.Top - offset - size.Height; // pas la place en dessous : au-dessus
        x = Math.Clamp(x, work.Left + margin, Math.Max(work.Left + margin, work.Right - margin - size.Width));
        y = Math.Clamp(y, work.Top, Math.Max(work.Top, work.Bottom - margin - size.Height));

        _target = target;
        _shownLayouts = layouts;
        _button = button;
        _work = work;
        _scale = scale;
        _theme = theme;
        _outsideSince = -1;
        _flyout.ShowAt(new Rectangle(x, y, size.Width, size.Height));

        if (_store.Config.DebugLog)
            Log.Write($"Menu ouvert pour {Native.Describe(target)}, bouton {button}, écran {work} @ {scale:0.##}x");
    }

    private void HideFlyout()
    {
        _preview.Hide();
        _flyout.Hide();
        _outsideSince = -1;
    }

    private Rectangle ZoneOnScreen(int layout, int zone) =>
        WindowPlacer.ZoneRect(_shownLayouts[layout].Zones[zone], _work,
            (int)Math.Round(Math.Max(0, _store.Config.ZoneGap) * _scale));

    private void OnZoneHovered(int layout, int zone)
    {
        if (!_store.Config.ShowPreview || layout < 0 || !_flyout.Visible)
        {
            _preview.Hide();
            return;
        }
        _preview.ShowAt(ZoneOnScreen(layout, zone), _theme, _flyout.Handle);
    }

    private void OnZoneChosen(int layout, int zone)
    {
        var target = _target;
        var chosen = _shownLayouts[layout];
        var rect = ZoneOnScreen(layout, zone);
        var others = Enumerable.Range(0, chosen.Zones.Count).Where(j => j != zone).ToList();
        var emptyZones = others.Select(j => ZoneOnScreen(layout, j)).ToList();
        var emptyZooms = others.Select(j => chosen.Zoom[j]).ToList();
        HideFlyout();
        _suppressed = target;

        // Comme Windows : la fenêtre va dans la zone cliquée, puis l'assistance propose
        // les autres fenêtres ouvertes pour remplir les zones restées vides.
        bool assist = _store.Config.SnapAssist && emptyZones.Count > 0;
        var zoom = chosen.Zoom[zone];
        var session = new PlacementSession(_work, emptyZones);
        bool ok = session.Place(target, rect, activate: !assist && zoom is null);
        if (!ok) Log.Write($"Échec du placement de {Native.Describe(target)} (fenêtre administrateur ?)");
        else if (_store.Config.DebugLog)
            Log.Write($"Placé {Native.Describe(target)} dans « {chosen.Name} » zone {zone + 1} -> {rect}");
        if (zoom is int pct) Zoom.Set(target, pct);
        if (assist) _assist.Start(session, emptyZooms, target, _theme, _scale);
    }


    // ---------------------------------------------------------------- Zone de notification

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add(new ToolStripMenuItem(AppName) { Enabled = false });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Modifier les dispositions…", null, (_, _) => OpenConfig());
        menu.Items.Add("Ouvrir le dossier", null, (_, _) => Shell(Path.GetDirectoryName(_store.FilePath)!));
        menu.Items.Add("Recharger", null, (_, _) => Reload());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_nativeItem);
        menu.Items.Add(_startupItem);
        menu.Items.Add(_pauseItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Quitter", null, (_, _) => ExitThread());
        return menu;
    }

    private void OpenConfig() => Shell(_store.FilePath);

    private static void Shell(string path)
    {
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
        catch (Exception ex) { Log.Write($"Ouverture de {path} impossible : {ex.Message}"); }
    }

    private void RestartReloadDebounce()
    {
        _reloadDebounce.Stop();
        _reloadDebounce.Start();
    }

    private void Reload()
    {
        var problems = _store.Load();
        Log.Write($"Configuration relue : {_store.Layouts.Count} dispositions");
        if (problems.Count > 0) Warn(problems);
    }

    private void Warn(List<string> problems)
    {
        foreach (var p in problems) Log.Write("layouts.json : " + p);
        var text = string.Join("\n", problems.Take(4));
        _balloonIsNativeWarning = false;
        _tray.ShowBalloonTip(6000, "CustomFancyZone : problème dans layouts.json",
            text.Length > 250 ? text[..250] + "…" : text, ToolTipIcon.Warning);
    }

    private void WarnNative()
    {
        _balloonIsNativeWarning = true;
        _tray.ShowBalloonTip(10000, "CustomFancyZone : le menu de Windows est encore actif",
            "Les deux menus vont se superposer. Clique ici pour désactiver celui de Windows.", ToolTipIcon.Info);
    }

    /// <summary>Désactive ou rétablit le menu de Windows, après confirmation (l'Explorateur redémarre).</summary>
    private void ToggleNative()
    {
        bool enabled = NativeSnap.IsEnabled();
        string question = enabled
            ? "Désactiver le menu d'ancrage de Windows (survol du bouton Agrandir) ?\n\n" +
              "Sinon les deux menus se superposent. Pour appliquer le réglage, l'Explorateur Windows redémarre : " +
              "la barre des tâches disparaît une seconde et les fenêtres de l'Explorateur de fichiers se ferment."
            : "Réactiver le menu d'ancrage de Windows ?\n\n" +
              "Il s'affichera en plus de celui de CustomFancyZone. L'Explorateur Windows redémarre pour l'appliquer.";
        if (MessageBox.Show(question, AppName, MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
            return;
        try
        {
            if (enabled) NativeSnap.Disable();
            else NativeSnap.Restore();
        }
        catch (Exception ex)
        {
            Log.Write("Réglage du menu natif impossible : " + ex.Message);
            NativeSnap.OpenSettings();
        }
        RefreshMenu();
    }

    private void RefreshMenu()
    {
        _nativeItem.Text = NativeSnap.IsEnabled()
            ? "Menu natif de Windows : actif (le désactiver…)"
            : "Menu natif de Windows : désactivé (le réactiver…)";
        _startupItem.Checked = IsStartupEnabled();
        _pauseItem.Checked = _paused;
    }

    private void TogglePause()
    {
        _paused = !_paused;
        _pauseItem.Checked = _paused;
        if (_paused) HideFlyout();
        _candidate = IntPtr.Zero;
    }

    private static bool IsStartupEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(AppName) is string;
    }

    private void ToggleStartup()
    {
        using (var key = Registry.CurrentUser.CreateSubKey(RunKey))
        {
            if (IsStartupEnabled()) key.DeleteValue(AppName, false);
            else key.SetValue(AppName, $"\"{Environment.ProcessPath}\"");
        }
        _startupItem.Checked = IsStartupEnabled();
    }

    protected override void ExitThreadCore()
    {
        _timer.Stop();
        _watcher.EnableRaisingEvents = false;
        _watcher.Dispose();
        _tray.Visible = false;
        _tray.Dispose();
        _assist.Close();
        _preview.Close();
        _flyout.Close();
        Log.Write("Arrêt");
        base.ExitThreadCore();
    }
}
