namespace CustomFancyZone;

/// <summary>Journal minimal (CustomFancyZone.log à côté de l'exe), plafonné à 512 Ko.</summary>
internal static class Log
{
    private static readonly object Gate = new();

    public static string? FilePath { get; set; }

    public static void Write(string message)
    {
        if (FilePath is null) return;
        try
        {
            lock (Gate)
            {
                var info = new FileInfo(FilePath);
                if (info.Exists && info.Length > 512 * 1024) info.Delete();
                File.AppendAllText(FilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}  {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // Le journal ne doit jamais faire planter l'outil.
        }
    }
}
