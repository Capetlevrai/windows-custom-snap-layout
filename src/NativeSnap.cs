using System.Diagnostics;
using Microsoft.Win32;

namespace CustomFancyZone;

/// <summary>
/// Le menu natif de Windows (« Afficher les dispositions d'ancrage lorsque je survole le bouton… »).
/// Le réglage est la valeur EnableSnapAssistFlyout du registre, mais l'Explorateur ne la relit qu'à son démarrage
/// (testé : WM_SETTINGCHANGE et SystemParametersInfo ne suffisent pas ; seule l'appli Paramètres l'applique à chaud).
/// Pour l'appliquer, on écrit donc la valeur puis on redémarre l'Explorateur. L'état d'avant est mémorisé pour
/// pouvoir le rétablir (désinstallation).
/// </summary>
internal static class NativeSnap
{
    private const string AdvancedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
    private const string ValueName = "EnableSnapAssistFlyout";
    private const string OwnKey = @"Software\CustomFancyZone";
    private const string SavedName = "PreviousSnapAssistFlyout"; // -1 = valeur absente (Windows : option cochée)

    public static bool IsEnabled() => (ReadRaw() ?? 1) != 0;

    /// <summary>Décoche l'option de Windows et l'applique. Faux si elle l'était déjà.</summary>
    public static bool Disable()
    {
        if (!IsEnabled()) return false;
        using (var own = Registry.CurrentUser.CreateSubKey(OwnKey))
        {
            if (own.GetValue(SavedName) is null) own.SetValue(SavedName, ReadRaw() ?? -1, RegistryValueKind.DWord);
        }
        Write(0);
        Log.Write("Menu natif de Windows désactivé (Explorateur redémarré)");
        RestartExplorer();
        return true;
    }

    /// <summary>
    /// Rétablit l'option telle qu'avant <see cref="Disable"/> ; sans état mémorisé, la recoche.
    /// Faux s'il n'y avait rien à changer.
    /// </summary>
    public static bool Restore()
    {
        int? saved;
        using (var own = Registry.CurrentUser.OpenSubKey(OwnKey, writable: true))
        {
            saved = own?.GetValue(SavedName) as int?;
            own?.DeleteValue(SavedName, false);
        }
        int target = saved is int s && s >= 0 ? s : 1;
        if ((ReadRaw() ?? 1) == target) return false;
        Write(target);
        Log.Write($"Menu natif de Windows rétabli ({(target != 0 ? "coché" : "décoché")}, Explorateur redémarré)");
        RestartExplorer();
        return true;
    }

    public static void OpenSettings()
    {
        try { Process.Start(new ProcessStartInfo("ms-settings:multitasking") { UseShellExecute = true }); }
        catch (Exception ex) { Log.Write("Ouverture de Paramètres impossible : " + ex.Message); }
    }

    private static int? ReadRaw()
    {
        using var key = Registry.CurrentUser.OpenSubKey(AdvancedKey);
        return key?.GetValue(ValueName) as int?;
    }

    private static void Write(int value)
    {
        using var key = Registry.CurrentUser.CreateSubKey(AdvancedKey);
        key.SetValue(ValueName, value, RegistryValueKind.DWord);
    }

    /// <summary>
    /// Redémarre l'Explorateur de la session (barre des tâches incluse) pour qu'il relise le réglage.
    /// Les fenêtres de l'Explorateur de fichiers ouvertes sont fermées.
    /// </summary>
    private static void RestartExplorer()
    {
        int session = Process.GetCurrentProcess().SessionId;
        Process[] Shells() => Process.GetProcessesByName("explorer").Where(p => p.SessionId == session).ToArray();

        foreach (var p in Shells())
        {
            try
            {
                p.Kill();
                p.WaitForExit(5000);
            }
            catch (Exception ex)
            {
                Log.Write("Arrêt de l'Explorateur impossible : " + ex.Message);
            }
        }

        // Windows relance souvent l'Explorateur tout seul : on lui laisse quelques secondes, sinon on le relance.
        for (int i = 0; i < 40; i++)
        {
            Thread.Sleep(100);
            if (Shells().Length > 0) return;
        }
        Process.Start(new ProcessStartInfo(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"))
        {
            UseShellExecute = true,
        });
    }
}
