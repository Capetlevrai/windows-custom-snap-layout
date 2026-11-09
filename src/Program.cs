namespace CustomFancyZone;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        // Coordonnées en pixels physiques sur tous les écrans, quelle que soit leur mise à l'échelle.
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        var exeDir = AppContext.BaseDirectory;
        Log.FilePath = Path.Combine(exeDir, "CustomFancyZone.log");

        if (args.Length > 0) return Cli.Run(args, exeDir);

        using var mutex = new Mutex(true, @"Local\CustomFancyZone.Instance", out bool firstInstance);
        if (!firstInstance) return 0;

        Application.ThreadException += (_, e) => Log.Write("Erreur : " + e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log.Write("Erreur fatale : " + e.ExceptionObject);

        Application.Run(new TrayApp(exeDir));
        return 0;
    }
}
