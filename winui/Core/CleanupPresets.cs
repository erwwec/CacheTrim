using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace CacheTrim;
// Presets deliberately contain only cleanup-category flags, never exclusions or settings outside cleanup.
internal static class CleanupPresets
{
    private static readonly string[] Fields = { "WorkingSets", "Standby", "LowStandby", "Modified", "FileCache", "Temp", "Browsers", "Wer", "UpdateCache", "D3dCache", "Thumbs", "Dumps", "WinLogs", "UwpCache", "Recycle", "Prefetch", "Dns", "Arp", "Netbios" };
    internal static string PathName => Path.Combine(Config.ConfigDir, "cleanup-presets.json");
    internal static Dictionary<string, bool> Capture(Config config) => Fields.ToDictionary(x => x, x => (bool)typeof(Config).GetField(x)!.GetValue(config)!);
    internal static void Apply(Config config, Dictionary<string, bool> preset)
    {
        if (preset == null || Fields.Any(x => !preset.ContainsKey(x)) || preset.Keys.Any(x => !Fields.Contains(x))) throw new InvalidDataException("Набор неполный или содержит неизвестные категории.");
        foreach (string name in Fields) typeof(Config).GetField(name)!.SetValue(config, preset[name]);
    }
    internal static Dictionary<string, Dictionary<string, bool>> Load(string path = null)
    {
        path ??= PathName;
        if (!File.Exists(path)) return new Dictionary<string, Dictionary<string, bool>>(StringComparer.OrdinalIgnoreCase);
        var data = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, bool>>>(File.ReadAllText(path)) ?? throw new InvalidDataException("Не удалось прочитать наборы.");
        var result = new Dictionary<string, Dictionary<string, bool>>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in data) { ValidateName(item.Key); Apply(new Config(), item.Value); result.Add(item.Key, item.Value); }
        return result;
    }
    internal static void Save(Dictionary<string, Dictionary<string, bool>> presets, string path = null)
    {
        path ??= PathName;
        foreach (var item in presets) { ValidateName(item.Key); Apply(new Config(), item.Value); }
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string tmp = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try { File.WriteAllText(tmp, JsonSerializer.Serialize(presets, new JsonSerializerOptions { WriteIndented = true })); if (File.Exists(path)) File.Replace(tmp, path, null); else File.Move(tmp, path); }
        finally { if (File.Exists(tmp)) File.Delete(tmp); }
    }
    internal static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 80 || name.Any(char.IsControl)) throw new ArgumentException("Укажи название от 1 до 80 символов без переносов строк.");
    }
}
