using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Application = System.Windows.Application;

namespace CacheTrim;

public partial class App : Application
{
    internal static Config Settings { get; private set; } = new();
    internal static event Action<string>? LogAdded;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Settings = Config.Load();
        Theme.Apply(Settings.ThemeName, Settings.AccentColorHex);
        if (e.Args.Any(a => string.Equals(a, "--silent", StringComparison.OrdinalIgnoreCase)))
        {
            RunSilent(Settings);
            Shutdown();
            return;
        }
        int page = 0;
        foreach (string argument in e.Args)
        {
            if (argument.StartsWith("--page=", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(argument.Substring(7), out int parsed)) page = Math.Max(0, Math.Min(6, parsed));
        }
        MainWindow window = new(page);
        MainWindow = window;
        window.Show();
    }

    internal static void Log(string message)
    {
        string line = "[" + DateTime.Now.ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) + "] " + message;
        try
        {
            Directory.CreateDirectory(Config.LogDir);
            File.AppendAllText(Config.HistoryPath, line + Environment.NewLine, new System.Text.UTF8Encoding(false));
        }
        catch { }
        LogAdded?.Invoke(line);
    }

    private static void RunSilent(Config config)
    {
        if (!Native.IsElevated) { Log("запуск без прав администратора — очистка пропущена"); return; }
        Log("автоочистка при входе запущена");
        try
        {
            Cleaner cleaner = new(Log);
            cleaner.Run(config);
            Native.MemStats stats = Native.GetMemoryStats();
            Log("автоочистка завершена · удалено " + Cleaner.FormatBytes(cleaner.BytesFreed) + " · доступно " + Cleaner.FormatBytes((long)stats.AvailPhys));
        }
        catch (Exception ex) { Log("ошибка автоочистки: " + ex.Message); }
    }
}
