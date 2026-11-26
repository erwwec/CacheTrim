using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace CacheTrim;
// Profiles are applied only to a manually selected, revalidated process. No autostart.
internal static class ProcessProfiles
{
    internal static string DefaultPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CacheTrim", "process-priority-profiles.json");
    private static Dictionary<string, string> Load(string file)
    {
        if (!File.Exists(file)) return new(StringComparer.OrdinalIgnoreCase);
        var data = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(file)) ?? throw new InvalidDataException("Файл профилей пуст или повреждён.");
        var profiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in data)
        {
            if (string.IsNullOrWhiteSpace(item.Key) || !IsAllowed(item.Value) || !profiles.TryAdd(item.Key, item.Value)) throw new InvalidDataException("Файл профилей повреждён. Данные не изменены.");
        }
        return profiles;
    }
    internal static bool IsAllowed(string value) => value == "BelowNormal" || value == "Normal" || value == "AboveNormal";
    internal static void Save(string file, string executablePath, string priority)
    {
        if (string.IsNullOrWhiteSpace(executablePath) || !Path.IsPathFullyQualified(executablePath) || !IsAllowed(priority)) throw new ArgumentException("Некорректный путь или приоритет профиля.");
        var profiles = Load(file); profiles[executablePath] = priority;
        string dir = Path.GetDirectoryName(file) ?? throw new InvalidOperationException("Не задана папка профилей."); Directory.CreateDirectory(dir);
        string temp = Path.Combine(dir, ".process-profiles-" + Guid.NewGuid().ToString("N") + ".tmp");
        try { File.WriteAllText(temp, JsonSerializer.Serialize(profiles)); File.Move(temp, file, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    internal static ProcessPriorityClass Find(string file, string executablePath)
    {
        var profiles = Load(file);
        if (!profiles.TryGetValue(executablePath, out var priority)) throw new InvalidOperationException("Для этого файла нет сохранённого профиля.");
        return Enum.Parse<ProcessPriorityClass>(priority);
    }
}
