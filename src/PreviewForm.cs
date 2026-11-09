namespace CustomFancyZone;

/// <summary>Rectangle translucide qui montre, sur l'écran, où la fenêtre va atterrir.</summary>
internal sealed class PreviewForm : Form
{
    private Color _border = Color.White;

    public PreviewForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        Opacity = 0.35;
        Text = "CustomFancyZone preview";
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
            cp.ExStyle |= Native.WS_EX_TOPMOST | Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE |
                          Native.WS_EX_TRANSPARENT | Native.WS_EX_LAYERED;
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
                m.Result = IntPtr.Zero;
                return;
        }
        base.WndProc(ref m);
    }

    /// <summary>Affiche l'aperçu juste sous le menu (<paramref name="flyout"/>) dans l'ordre d'empilement.</summary>
    public void ShowAt(Rectangle bounds, Theme theme, IntPtr flyout)
    {
        _ = Handle;
        BackColor = theme.Accent;
        _border = theme.Dark ? Color.White : theme.Text;
        Bounds = bounds;
        if (!Visible) Show();
        Native.SetWindowPos(Handle, flyout, 0, 0, 0, 0,
            Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        float w = Math.Max(2f, DeviceDpi / 48f);
        using var pen = new Pen(_border, w);
        e.Graphics.DrawRectangle(pen, w / 2, w / 2, Width - w, Height - w);
    }
}
