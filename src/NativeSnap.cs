using System.Diagnostics;
using Microsoft.Win32;

namespace CustomFancyZone;

/// <summary>
/// Le menu natif de Windows (« Afficher les dispositions d'ancrage lorsque je survole le bouton… »).
/// L'Explorateur ne relit pas ce réglage quand on écrit dans le registre (testé : WM_SETTINGCHANGE et
/// SystemParametersInfo ne suffisent pas, seul Paramètres l'applique à chaud). On se contente donc de lire
/// l'état et d'ouvrir la bonne page de Paramètres.
/// </summary>
internal static class NativeSnap
{
    private const string AdvancedKey = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";

    public static bool IsEnabled()
    {
        try { return Registry.GetValue(AdvancedKey, "EnableSnapAssistFlyout", 1) is not int v || v != 0; }
        catch { return true; }
    }

    public static void OpenSettings()
    {
        try { Process.Start(new ProcessStartInfo("ms-settings:multitasking") { UseShellExecute = true }); }
        catch (Exception ex) { Log.Write("Ouverture de Paramètres impossible : " + ex.Message); }
    }
}
