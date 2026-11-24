using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace CacheTrim;

// Conservative best-effort guard: Windows fullscreen signals + explicitly chosen executable names.
internal sealed class GameGuard
{
    private readonly List<string> _names;
    private DateTime _nextCheck;
    public static string SettingsPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CacheTrim", "protected-games.json");
    public GameGuard() : this(LoadPaths()) { }
    internal GameGuard(IEnumerable<string> paths) { _names = Validate(paths).Select(p => Path.GetFileNameWithoutExtension(p)).Distinct(StringComparer.OrdinalIgnoreCase).ToList(); }
    public static List<string> LoadPaths()
    {
        if (!File.Exists(SettingsPath)) return new List<string>();
        try
        {
            var paths = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(SettingsPath)) ?? throw new InvalidDataException("Список игр пуст или повреждён.");
            return Validate(paths);
        }
        catch (Exception ex) { throw new InvalidDataException("Не удалось прочитать список защищённых игр; обслуживание остановлено: " + ex.Message, ex); }
    }
    public static List<string> Validate(IEnumerable<string> paths)
    {
        var result = new List<string>();
        foreach (string path in paths)
        {
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) || !string.Equals(Path.GetExtension(path), ".exe", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Укажи полный путь к EXE-файлу игры: " + path);
            result.Add(Path.GetFullPath(path));
        }
        return result.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }
    public static void SavePaths(IEnumerable<string> paths)
    {
        var list = Validate(paths); Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
        string temp = SettingsPath + ".tmp-" + Guid.NewGuid().ToString("N");
        try { File.WriteAllText(temp, JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true })); if (File.Exists(SettingsPath)) File.Replace(temp, SettingsPath, null); else File.Move(temp, SettingsPath); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    public void Check(bool force = false)
    {
        if (!force && DateTime.UtcNow < _nextCheck) return;
        foreach (var name in _names)
        {
            var processes = Process.GetProcessesByName(name);
            try { if (processes.Length > 0) throw new InvalidOperationException("Обслуживание остановлено: запущена защищённая игра «" + name + "»."); }
            finally { foreach (var process in processes) process.Dispose(); }
        }
        if (OperatingSystem.IsWindows() && SHQueryUserNotificationState(out int state) == 0 && (state == 3 || state == 4 || state == 7))
            throw new InvalidOperationException("Обслуживание остановлено: Windows сообщает о полноэкранном приложении или презентации.");
        _nextCheck = DateTime.UtcNow.AddSeconds(2);
    }
    [DllImport("shell32.dll")]
    private static extern int SHQueryUserNotificationState(out int state);
}
