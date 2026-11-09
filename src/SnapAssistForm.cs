namespace CustomFancyZone;

/// <summary>
/// Assistance d'ancrage, comme Windows : après le placement d'une fenêtre, chaque zone restée vide affiche
/// les miniatures (en direct, via DWM) des autres fenêtres ouvertes ; un clic place la fenêtre choisie,
/// puis on passe à la zone vide suivante. Échap ou un clic ailleurs arrête.
/// </summary>
internal sealed class SnapAssistForm : Form
{
    private const int MaxWindows = 12;

    private sealed record Cell(IntPtr Window, string Title, Icon? Icon, Rectangle Bounds, Rectangle Header,
        Rectangle Thumb, IntPtr ThumbnailId);

    private PlacementSession _session = new(Rectangle.Empty, []);
    private List<int?> _zooms = [];
    private int _step;
    private HashSet<IntPtr> _used = [];
    private IntPtr _lastPlaced;
    private List<Cell> _cells = [];
    private int _hover = -1;
    private Theme _theme = Theme.Current();
    private double _scale = 1;
    private Font? _font;
    private bool _busy;
    private Func<IntPtr, bool>? _only;

    public SnapAssistForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        KeyPreview = true;
        Text = "CustomFancyZone assistance";
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.Style = Native.WS_POPUP;
            cp.ExStyle |= Native.WS_EX_TOPMOST | Native.WS_EX_TOOLWINDOW;
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        int round = Native.DWMWCP_ROUND;
        Native.DwmSetWindowAttribute(Handle, Native.DWMWA_WINDOW_CORNER_PREFERENCE, ref round, sizeof(int));
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == Native.WM_DPICHANGED)
        {
            m.Result = IntPtr.Zero;
            return;
        }
        base.WndProc(ref m);
    }

    /// <summary>
    /// Lance l'assistance pour les zones encore vides de <paramref name="session"/>, après le placement
    /// de <paramref name="placed"/>.
    /// </summary>
    public void Start(PlacementSession session, IReadOnlyList<int?> zooms, IntPtr placed, Theme theme,
        double scale, Func<IntPtr, bool>? only = null)
    {
        _only = only;
        _session = session;
        _zooms = [.. zooms];
        _step = 0;
        _used = [placed];
        _lastPlaced = placed;
        _theme = theme;
        _scale = scale;
        _font?.Dispose();
        _font = FlyoutForm.CreateFont((int)Math.Round(12 * scale));
        if (!ShowStep()) Finish();
    }

    /// <summary>Vignettes affichées : fenêtre proposée et rectangle (coordonnées clientes). Pour le diagnostic.</summary>
    public IEnumerable<(IntPtr Window, Rectangle Bounds)> Cells => _cells.Select(c => (c.Window, c.Bounds));

    private int S(double v) => (int)Math.Round(v * _scale);

    /// <summary>Affiche les miniatures dans la zone vide courante. Faux s'il ne reste ni zone ni fenêtre.</summary>
    private bool ShowStep()
    {
        ClearThumbnails();
        if (_step >= _session.Pending.Count) return false;
        var windows = AppWindows.Candidates(_used, MaxWindows);
        if (_only is not null) windows = windows.Where(_only).ToList();
        if (windows.Count == 0) return false;

        _busy = true;
        _ = Handle;
        Bounds = _session.Pending[_step];
        LayoutCells(windows);
        _hover = -1;
        if (!Visible) Show();
        Native.SetWindowPos(Handle, Native.HWND_TOPMOST, 0, 0, 0, 0,
            Native.SWP_NOMOVE | Native.SWP_NOSIZE);
        Native.SetForegroundWindow(Handle);
        Activate();
        Invalidate();
        _busy = false;
        return true;
    }

    /// <summary>Grille de vignettes : on choisit le nombre de colonnes qui donne les plus grandes miniatures.</summary>
    private void LayoutCells(List<IntPtr> windows)
    {
        int pad = S(16), header = S(32), inner = S(6);
        var area = new Rectangle(pad, pad, ClientSize.Width - 2 * pad, ClientSize.Height - 2 * pad);
        int n = windows.Count;

        int bestCols = 1, bestW = 0, bestH = 0;
        double bestScore = -1;
        for (int cols = 1; cols <= n; cols++)
        {
            int rows = (n + cols - 1) / cols;
            int w = (area.Width - (cols - 1) * pad) / cols;
            int h = (area.Height - (rows - 1) * pad) / rows;
            if (w <= 0 || h <= header) continue;
            double score = Math.Min(w, (h - header) * 1.6); // miniature au format ~16/10
            if (score > bestScore) { bestScore = score; bestCols = cols; bestW = w; bestH = h; }
        }
        bestW = Math.Min(bestW, S(420));
        bestH = Math.Min(bestH, header + (int)(bestW / 1.6) + inner);

        int rowsUsed = (n + bestCols - 1) / bestCols;
        int gridW = bestCols * bestW + (bestCols - 1) * pad;
        int gridH = rowsUsed * bestH + (rowsUsed - 1) * pad;
        int x0 = area.Left + (area.Width - gridW) / 2, y0 = area.Top + (area.Height - gridH) / 2;

        int iconSize = S(16);
        _cells = [];
        for (int i = 0; i < n; i++)
        {
            var hwnd = windows[i];
            var cell = new Rectangle(x0 + i % bestCols * (bestW + pad), y0 + i / bestCols * (bestH + pad), bestW, bestH);
            var head = new Rectangle(cell.Left + inner, cell.Top, cell.Width - 2 * inner, header);
            var slot = Rectangle.FromLTRB(cell.Left + inner, cell.Top + header, cell.Right - inner, cell.Bottom - inner);

            var thumbRect = slot;
            IntPtr id = IntPtr.Zero;
            if (Native.DwmRegisterThumbnail(Handle, hwnd, out id) == 0)
            {
                if (Native.DwmQueryThumbnailSourceSize(id, out var src) == 0 && src.cx > 0 && src.cy > 0)
                {
                    double k = Math.Min(slot.Width / (double)src.cx, slot.Height / (double)src.cy);
                    int tw = (int)(src.cx * k), th = (int)(src.cy * k);
                    thumbRect = new Rectangle(slot.Left + (slot.Width - tw) / 2, slot.Top + (slot.Height - th) / 2, tw, th);
                }
                var props = new Native.DWM_THUMBNAIL_PROPERTIES
                {
                    dwFlags = Native.DWM_TNP_RECTDESTINATION | Native.DWM_TNP_VISIBLE | Native.DWM_TNP_OPACITY |
                              Native.DWM_TNP_SOURCECLIENTAREAONLY,
                    rcDestination = new Native.RECT
                        { Left = thumbRect.Left, Top = thumbRect.Top, Right = thumbRect.Right, Bottom = thumbRect.Bottom },
                    opacity = 255,
                    fVisible = 1,
                    fSourceClientAreaOnly = 0,
                };
                Native.DwmUpdateThumbnailProperties(id, ref props);
            }
            else
            {
                id = IntPtr.Zero;
            }
            _cells.Add(new Cell(hwnd, Native.TextOf(hwnd), AppWindows.IconOf(hwnd, iconSize), cell, head, thumbRect, id));
        }
    }

    private void ClearThumbnails()
    {
        foreach (var c in _cells)
            if (c.ThumbnailId != IntPtr.Zero) Native.DwmUnregisterThumbnail(c.ThumbnailId);
        _cells = [];
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(_theme.Background);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        float radius = S(6), pen = Math.Max(1f, (float)Math.Round(_scale));

        for (int i = 0; i < _cells.Count; i++)
        {
            var c = _cells[i];
            bool hot = i == _hover;
            FlyoutForm.DrawRounded(g, c.Bounds, radius, hot ? _theme.TileHot : _theme.Tile,
                hot ? _theme.Accent : _theme.TileBorder, hot ? pen * 2 : pen);

            int iconSize = S(16);
            int x = c.Header.Left + S(4);
            if (c.Icon is Icon icon)
            {
                g.DrawIcon(icon, new Rectangle(x, c.Header.Top + (c.Header.Height - iconSize) / 2, iconSize, iconSize));
                x += iconSize + S(8);
            }
            if (_font is not null)
                TextRenderer.DrawText(g, c.Title, _font, Rectangle.FromLTRB(x, c.Header.Top, c.Header.Right, c.Header.Bottom),
                    _theme.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis |
                                 TextFormatFlags.NoPrefix);
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int hover = _cells.FindIndex(c => c.Bounds.Contains(e.Location));
        if (hover == _hover) return;
        _hover = hover;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hover < 0) return;
        _hover = -1;
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left) return;
        int i = _cells.FindIndex(c => c.Bounds.Contains(e.Location));
        Log.Write($"Assistance : clic en {e.Location} -> vignette {i}");
        if (i >= 0) Pick(_cells[i].Window);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Escape) Finish();
    }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        // Clic sur autre chose : on arrête, comme Windows. Vérifié un instant plus tard, car l'outil
        // donne lui-même brièvement le focus à une fenêtre pour régler son zoom.
        BeginInvoke(() =>
        {
            if (!_busy && Visible && Native.GetForegroundWindow() != Handle) Finish();
        });
    }

    private void Pick(IntPtr window)
    {
        _busy = true;
        var zone = _session.Pending[_step];
        ClearThumbnails();
        bool ok = _session.Place(window, zone, activate: false);
        Log.Write($"Assistance : {Native.Describe(window)} -> {zone} {(ok ? "OK" : "ÉCHEC")}");
        if (ok && _step < _zooms.Count && _zooms[_step] is int zoom) Zoom.Set(window, zoom);
        _used.Add(window);
        _lastPlaced = window;
        _step++;
        bool more = ShowStep();
        _busy = false;
        if (!more) Finish();
    }

    private void Finish()
    {
        _busy = true;
        ClearThumbnails();
        Hide();
        if (Native.IsWindow(_lastPlaced)) Native.SetForegroundWindow(_lastPlaced);
        _busy = false;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ClearThumbnails();
            _font?.Dispose();
        }
        base.Dispose(disposing);
    }
}
