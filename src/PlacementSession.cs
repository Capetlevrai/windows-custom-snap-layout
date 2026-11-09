namespace CustomFancyZone;

/// <summary>
/// Placement des fenêtres d'une même disposition, avec ajustement automatique : une fenêtre qui refuse de
/// rétrécir (taille minimale imposée par l'application, comme Claude ou Discord) est recalée contre le bord de
/// l'écran au lieu de déborder, et les fenêtres et zones voisines sont réduites d'autant.
/// Résultat : rien n'est coupé, et la voisine garde tout l'espace restant.
/// </summary>
internal sealed class PlacementSession(Rectangle work, IEnumerable<Rectangle> pending)
{
    private const int Tolerance = 2;
    private readonly List<(IntPtr Window, Rectangle Rect)> _placed = [];

    /// <summary>Zones encore vides, dans l'ordre ; elles rétrécissent si une fenêtre voisine déborde.</summary>
    public List<Rectangle> Pending { get; } = [.. pending];

    public bool Place(IntPtr window, Rectangle target, bool activate)
    {
        if (!WindowPlacer.Place(window, target, activate)) return false;

        var actual = WindowPlacer.FrameOf(window);
        var final = target;
        if (actual.Width > target.Width + Tolerance)
        {
            int w = Math.Min(actual.Width, work.Width);
            // Une zone collée au bord droit (ou bas) grandit vers l'intérieur de l'écran.
            int left = target.Right >= work.Right - Tolerance ? target.Right - w : target.Left;
            final.X = Math.Clamp(left, work.Left, work.Right - w);
            final.Width = w;
        }
        if (actual.Height > target.Height + Tolerance)
        {
            int h = Math.Min(actual.Height, work.Height);
            int top = target.Bottom >= work.Bottom - Tolerance ? target.Bottom - h : target.Top;
            final.Y = Math.Clamp(top, work.Top, work.Bottom - h);
            final.Height = h;
        }

        if (final != target)
        {
            WindowPlacer.Place(window, final, activate);
            Log.Write($"Taille minimale : {Native.Describe(window)} prend {final} au lieu de {target}");

            for (int i = 0; i < _placed.Count; i++)
            {
                var (other, rect) = _placed[i];
                var shrunk = Cut(rect, final);
                if (shrunk == rect) continue;
                WindowPlacer.Place(other, shrunk, activate: false);
                _placed[i] = (other, shrunk);
            }
            for (int i = 0; i < Pending.Count; i++) Pending[i] = Cut(Pending[i], final);
        }

        _placed.Add((window, final));
        return true;
    }

    /// <summary>Retire de <paramref name="r"/> la partie recouverte par <paramref name="blocker"/>.</summary>
    private static Rectangle Cut(Rectangle r, Rectangle blocker)
    {
        var overlap = Rectangle.Intersect(r, blocker);
        if (overlap.IsEmpty) return r;

        Rectangle result;
        if (overlap.Height >= r.Height - Tolerance) // voisine à gauche ou à droite
            result = r.Left < blocker.Left
                ? Rectangle.FromLTRB(r.Left, r.Top, blocker.Left, r.Bottom)
                : Rectangle.FromLTRB(blocker.Right, r.Top, r.Right, r.Bottom);
        else if (overlap.Width >= r.Width - Tolerance) // voisine au-dessus ou en dessous
            result = r.Top < blocker.Top
                ? Rectangle.FromLTRB(r.Left, r.Top, r.Right, blocker.Top)
                : Rectangle.FromLTRB(r.Left, blocker.Bottom, r.Right, r.Bottom);
        else
            return r; // recouvrement en coin : on ne touche à rien

        return result.Width >= 100 && result.Height >= 100 ? result : r;
    }
}
