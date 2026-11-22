using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Application = System.Windows.Application;

namespace CacheTrim;

public partial class App : Application
{
    private const long MaxLogBytes = 512 * 1024;
    private static readonly object LogSync = new();
    internal static Config Settings { get; private set; } = new();
    internal static event Action<string>? LogAdded;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Settings = Config.Load();
        Theme.Apply(Settings.ThemeName, Settings.AccentColorHex);
        if (e.Args.Any(a => string.Equals(a, "--silent", StringComparison.OrdinalIgnoreCase)))
        {
            Shutdown(RunSilent(Settings));
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
            lock (LogSync)
            {
                Directory.CreateDirectory(Config.LogDir);
                string entry = line + Environment.NewLine;
                long nextLength = (File.Exists(Config.HistoryPath) ? new FileInfo(Config.HistoryPath).Length : 0) + Encoding.UTF8.GetByteCount(entry);
                if (nextLength > MaxLogBytes)
                    TrimHistory();
                File.AppendAllText(Config.HistoryPath, entry, new UTF8Encoding(false));
            }
        }
        catch { }
        LogAdded?.Invoke(line);
    }

    private static void TrimHistory()
    {
        string path = Config.HistoryPath;
        string temporaryPath = path + ".tmp";
        try
        {
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                long start = Math.Max(0, stream.Length - MaxLogBytes / 2);
                stream.Position = start;
                using (StreamReader reader = new StreamReader(stream, Encoding.UTF8, true, 4096, true))
                {
                    if (start > 0) reader.ReadLine();
                    File.WriteAllText(temporaryPath, reader.ReadToEnd(), new UTF8Encoding(false));
                }
            }
            File.Replace(temporaryPath, path, null);
        }
        catch
        {
            try { File.Delete(temporaryPath); }
            catch { }
        }
    }

    private static int RunSilent(Config config)
    {
        if (!Native.IsElevated) { Log("запуск без прав администратора — очистка пропущена"); return 1; }
        Log("автоочистка при входе запущена");
        try
        {
            Cleaner cleaner = new(Log);
            cleaner.Run(config);
            Native.MemStats stats = Native.GetMemoryStats();
            Log("автоочистка завершена · удалено " + Cleaner.FormatBytes(cleaner.BytesFreed) + " · доступно " + Cleaner.FormatBytes((long)stats.AvailPhys));
            return 0;
        }
        catch (Exception ex) { Log("ошибка автоочистки: " + ex.Message); return 1; }
    }
}
