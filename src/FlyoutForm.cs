using System.Drawing.Drawing2D;

namespace CustomFancyZone;

/// <summary>
/// Le menu qui s'affiche sous le bouton Agrandir. Il ne prend jamais le focus :
/// la fenêtre à placer reste active pendant qu'on choisit une zone.
/// </summary>
internal sealed class FlyoutForm : Form
{
    private sealed record TileGeometry(Rectangle Tile, Rectangle[] Zones);

    private IReadOnlyList<SnapLayout> _layouts = [];
    private TileGeometry[] _tiles = [];
    private Theme _theme = Theme.Current();
    private double _scale = 1;
    private bool _showNames = true;
    private Rectangle _footer;
    private (int Layout, int Zone) _hover = (-1, -1);
    private Font? _font;
    private Font? _symbolFont;
    private Font? _smallFont;
    private Rectangle[] _zoomButtons = [];
    private Rectangle _zoomLabel;
    private int _hoverButton = -1;

    private static readonly string[] ZoomLabels = ["−", "↺", "+"]; // ↺ = remettre à 100 % (le zoom réel est illisible)
    private static readonly string[] ZoomHints =
        ["Dézoomer la fenêtre (Ctrl −)", "Remettre la fenêtre à 100 % (Ctrl 0)", "Zoomer la fenêtre (Ctrl +)"];

    /// <summary>Bouton de zoom cliqué : -1 dézoomer, 0 remettre à 100 %, +1 zoomer.</summary>
    public event Action<int>? ZoomRequested;

    /// <summary>Zone survolée (-1, -1 quand plus rien n'est survolé).</summary>
    public event Action<int, int>? ZoneHovered;

    /// <summary>Zone cliquée.</summary>
    public event Action<int, int>? ZoneChosen;

    public FlyoutForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        Text = "CustomFancyZone";
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.Style = Native.WS_POPUP;
            cp.ExStyle |= Native.WS_EX_TOPMOST | Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE;
            cp.ClassStyle |= Native.CS_DROPSHADOW;
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
        switch (m.Msg)
        {
            case Native.WM_MOUSEACTIVATE:
                m.Result = Native.MA_NOACTIVATE;
                return;
            case Native.WM_DPICHANGED:
                // On calcule nous-mêmes la taille pour l'écran visé : on ignore le redimensionnement automatique.
                m.Result = IntPtr.Zero;
                return;
        }
        base.WndProc(ref m);
    }

    /// <summary>Calcule la géométrie du menu et renvoie sa taille en pixels physiques.</summary>
    public Size Prepare(IReadOnlyList<SnapLayout> layouts, AppConfig cfg, Theme theme, double scale, double aspect)
    {
        _layouts = layouts;
        _theme = theme;
        _scale = scale;
        _showNames = cfg.ShowNames;
        _hover = (-1, -1);

        int S(double v) => (int)Math.Round(v * scale);

        int n = layouts.Count;
        int slots = n + (cfg.ShowZoomBar ? 1 : 0); // le zoom occupe la case qui suit la dernière disposition
        int cols = Math.Clamp(cfg.Columns, 1, Math.Max(1, slots));
        int rows = (slots + cols - 1) / cols;
        int pad = S(12), gap = S(12), zoneGap = Math.Max(2, S(4));
        int tileW = S(Math.Clamp(cfg.TileWidth, 48, 400));
        int tileH = Math.Max(S(24), (int)Math.Round(tileW / aspect));

        _tiles = new TileGeometry[n];
        for (int i = 0; i < n; i++)
        {
            var tile = new Rectangle(pad + i % cols * (tileW + gap), pad + i / cols * (tileH + gap), tileW, tileH);
            var zones = layouts[i].Zones;
            var rects = new Rectangle[zones.Count];
            for (int j = 0; j < zones.Count; j++) rects[j] = ZoneInTile(tile, zones[j], zoneGap);
            _tiles[i] = new TileGeometry(tile, rects);
        }

        int width = pad * 2 + cols * tileW + (cols - 1) * gap;
        int y = pad + rows * tileH + (rows - 1) * gap;

        // Zoom : une vignette comme les autres, dans la case libre après la dernière disposition :
        // « Zoom » en petit, puis trois boutons dessinés comme des zones : −  100%  +
        _hoverButton = -1;
        _zoomButtons = [];
        if (cfg.ShowZoomBar)
        {
            var tile = new Rectangle(pad + n % cols * (tileW + gap), pad + n / cols * (tileH + gap), tileW, tileH);
            int labelH = Math.Min(S(22), tileH / 3);
            _zoomLabel = new Rectangle(tile.Left, tile.Top, tile.Width, labelH);
            int top = tile.Top + labelH, bottom = tile.Bottom;
            int inner = tile.Width - 2 * zoneGap;
            int side = (int)Math.Round(inner / 3.4), mid = inner - 2 * side; // bouton central un peu plus large
            int x = tile.Left;
            _zoomButtons = [Rectangle.FromLTRB(x, top, x + side, bottom),
                Rectangle.FromLTRB(x + side + zoneGap, top, x + side + zoneGap + mid, bottom),
                Rectangle.FromLTRB(x + side + mid + 2 * zoneGap, top, tile.Right, bottom)];
        }

        int footerH = S(32);
        _footer = new Rectangle(pad, y, width - pad * 2, footerH);
        int height = y + (_showNames ? footerH : pad);

        _font?.Dispose();
        _font = CreateFont(S(12));
        _symbolFont?.Dispose();
        _symbolFont = CreateFont(S(15));
        _smallFont?.Dispose();
        _smallFont = CreateFont(S(11));
        return new Size(width, height);
    }

    public void ShowAt(Rectangle bounds)
    {
        _ = Handle;
        Bounds = bounds;
        if (!Visible) Show();
        Native.SetWindowPos(Handle, Native.HWND_TOPMOST, 0, 0, 0, 0,
            Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
        Invalidate();
    }

    /// <summary>Force une zone survolée (mode démo, pour les captures).</summary>
    public void SetHover(int layout, int zone) => SetHoverCore((layout, zone));

    internal static Font CreateFont(int px)
    {
        foreach (var family in new[] { "Segoe UI Variable Text", "Segoe UI" })
        {
            try
            {
                using var ff = new FontFamily(family);
                return new Font(ff, px, FontStyle.Regular, GraphicsUnit.Pixel);
            }
            catch (ArgumentException)
            {
                // Police absente : on essaie la suivante.
            }
        }
        return new Font(FontFamily.GenericSansSerif, px, GraphicsUnit.Pixel);
    }

    private static Rectangle ZoneInTile(Rectangle tile, RectangleF z, int gap)
    {
        int half = gap / 2;
        int l = tile.Left + (int)Math.Round(z.Left * tile.Width);
        int t = tile.Top + (int)Math.Round(z.Top * tile.Height);
        int r = tile.Left + (int)Math.Round(z.Right * tile.Width);
        int b = tile.Top + (int)Math.Round(z.Bottom * tile.Height);
        // Écart seulement entre zones voisines : les bords extérieurs restent alignés sur la vignette.
        if (z.Left > 0.001f) l += gap - half;
        if (z.Top > 0.001f) t += gap - half;
        if (z.Right < 0.999f) r -= half;
        if (z.Bottom < 0.999f) b -= half;
        return Rectangle.FromLTRB(l, t, Math.Max(l + 2, r), Math.Max(t + 2, b));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(_theme.Background);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        float radius = (float)(4 * _scale);
        float pen = Math.Max(1f, (float)Math.Round(_scale));

        for (int i = 0; i < _tiles.Length; i++)
        {
            var tile = _tiles[i];
            bool tileHot = _hover.Layout == i;
            if (!_layouts[i].CoversTile)
                DrawRounded(g, tile.Tile, radius, _theme.Ghost, null, pen);

            for (int j = 0; j < tile.Zones.Length; j++)
            {
                bool hot = tileHot && _hover.Zone == j;
                var fill = hot ? _theme.Accent : tileHot ? _theme.TileHot : _theme.Tile;
                var border = hot ? _theme.Accent : _theme.TileBorder;
                DrawRounded(g, tile.Zones[j], radius, fill, border, pen);
            }
        }

        if (_zoomButtons.Length == 3 && _smallFont is not null && _symbolFont is not null)
        {
            const TextFormatFlags center = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                                           TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix;
            TextRenderer.DrawText(g, "Zoom", _smallFont, _zoomLabel, _theme.SubText, _theme.Background, center);
            for (int b = 0; b < 3; b++)
            {
                bool hot = b == _hoverButton;
                DrawRounded(g, _zoomButtons[b], radius, hot ? _theme.Accent : _theme.Tile,
                    hot ? _theme.Accent : _theme.TileBorder, pen);
                var color = hot ? (_theme.Dark ? Color.Black : Color.White) : _theme.Text;
                TextRenderer.DrawText(g, ZoomLabels[b], _symbolFont, _zoomButtons[b], color, center);
            }
        }

        if (_showNames && _font is not null)
        {
            string text;
            Color color;
            if (_hoverButton >= 0)
            {
                text = ZoomHints[_hoverButton];
                color = _theme.Text;
            }
            else if (_hover.Layout >= 0)
            {
                var layout = _layouts[_hover.Layout];
                text = layout.Zones.Count > 1
                    ? $"{layout.Name}  ·  zone {_hover.Zone + 1}/{layout.Zones.Count}"
                    : layout.Name;
                color = _theme.Text;
            }
            else
            {
                text = "Clique une zone pour y placer la fenêtre";
                color = _theme.SubText;
            }
            TextRenderer.DrawText(g, text, _font, _footer, color, _theme.Background,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
                TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }
    }

    internal static void DrawRounded(Graphics g, Rectangle r, float radius, Color fill, Color? border, float penWidth)
    {
        var rf = new RectangleF(r.X + penWidth / 2f, r.Y + penWidth / 2f, r.Width - penWidth, r.Height - penWidth);
        using var path = RoundedPath(rf, radius);
        using (var brush = new SolidBrush(fill)) g.FillPath(brush, path);
        if (border is Color c)
        {
            using var p = new Pen(c, penWidth);
            g.DrawPath(p, path);
        }
    }

    internal static GraphicsPath RoundedPath(RectangleF r, float radius)
    {
        var path = new GraphicsPath();
        float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        if (d <= 0.5f)
        {
            path.AddRectangle(r);
            return path;
        }
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        SetHoverButton(Array.FindIndex(_zoomButtons, r => r.Contains(e.Location)));
        SetHoverCore(HitZone(e.Location));
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        SetHoverButton(-1);
        SetHoverCore((-1, -1));
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left) return;
        int button = Array.FindIndex(_zoomButtons, r => r.Contains(e.Location));
        if (button >= 0)
        {
            ZoomRequested?.Invoke(button - 1); // -1, 0, +1 ; le menu reste ouvert pour enchaîner les crans
            return;
        }
        var hit = HitZone(e.Location);
        if (hit.Layout >= 0) ZoneChosen?.Invoke(hit.Layout, hit.Zone);
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (Visible) return;
        _hover = (-1, -1);
        _hoverButton = -1;
    }

    private void SetHoverButton(int button)
    {
        if (button == _hoverButton) return;
        _hoverButton = button;
        Invalidate();
    }

    private (int Layout, int Zone) HitZone(Point p)
    {
        int slack = Math.Max(2, (int)Math.Round(4 * _scale));
        for (int i = 0; i < _tiles.Length; i++)
        {
            var tile = _tiles[i];
            if (!tile.Tile.Contains(p)) continue;

            int best = -1;
            double bestDist = double.MaxValue;
            for (int j = 0; j < tile.Zones.Length; j++)
            {
                var z = tile.Zones[j];
                if (z.Contains(p)) return (i, j);
                double dx = Math.Max(0, Math.Max(z.Left - p.X, p.X - z.Right));
                double dy = Math.Max(0, Math.Max(z.Top - p.Y, p.Y - z.Bottom));
                double dist = Math.Sqrt(dx * dx + dy * dy);
                if (dist < bestDist) { bestDist = dist; best = j; }
            }
            // Dans l'interstice entre deux zones : on prend la plus proche.
            return best >= 0 && bestDist <= slack ? (i, best) : (-1, -1);
        }
        return (-1, -1);
    }

    private void SetHoverCore((int Layout, int Zone) hover)
    {
        if (hover == _hover) return;
        _hover = hover;
        Invalidate();
        ZoneHovered?.Invoke(hover.Layout, hover.Zone);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _font?.Dispose();
            _symbolFont?.Dispose();
            _smallFont?.Dispose();
        }
        base.Dispose(disposing);
    }
}
