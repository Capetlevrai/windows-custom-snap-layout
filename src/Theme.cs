using Microsoft.Win32;

namespace CustomFancyZone;

/// <summary>Couleurs du menu, calquées sur le thème Windows (clair/sombre) et sa couleur d'accentuation.</summary>
internal sealed record Theme(
    bool Dark,
    Color Background,
    Color Tile,
    Color TileHot,
    Color TileBorder,
    Color Ghost,
    Color Accent,
    Color Text,
    Color SubText)
{
    public static Theme Current()
    {
        bool dark = false;
        try
        {
            dark = Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "AppsUseLightTheme", 1) is int v && v == 0;
        }
        catch
        {
            // Thème clair par défaut.
        }

        // Windows utilise la teinte « claire 2 » de l'accent en mode sombre, « sombre 1 » en mode clair.
        var accent = ReadAccent(dark ? 1 : 4)
            ?? (dark ? Color.FromArgb(0x4C, 0xC2, 0xFF) : Color.FromArgb(0x00, 0x67, 0xC0));

        return dark
            ? new Theme(true, Hex(0x2C2C2C), Hex(0x3D3D3D), Hex(0x4B4B4B), Hex(0x707070), Hex(0x353535),
                accent, Hex(0xF2F2F2), Hex(0x9E9E9E))
            : new Theme(false, Hex(0xF3F3F3), Hex(0xFFFFFF), Hex(0xE6E6E6), Hex(0x8C8C8C), Hex(0xE8E8E8),
                accent, Hex(0x1A1A1A), Hex(0x6B6B6B));
    }

    private static Color? ReadAccent(int index)
    {
        try
        {
            if (Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Accent",
                    "AccentPalette", null) is byte[] p && p.Length >= (index + 1) * 4)
                return Color.FromArgb(p[index * 4], p[index * 4 + 1], p[index * 4 + 2]);
        }
        catch
        {
            // Couleur par défaut.
        }
        return null;
    }

    private static Color Hex(int rgb) => Color.FromArgb((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
}
