using CacheTrim;
using System.Text.Json;
namespace CacheTrim { internal static class Theme { public const string Indigo="Indigo", Ocean="Ocean", Graphite="Graphite", LiquidGlass="LiquidGlass", Violet="Violet", Rose="Rose"; } }
internal static class Program
{
    static void Check(bool ok, string label) { if (!ok) throw new Exception(label); Console.WriteLine("OK: " + label); }
    static void Reject(Action action, string label) { try { action(); } catch (InvalidDataException) { Check(true, label); return; } throw new Exception("not rejected: " + label); }
    static void Main()
    {
        string dir = Path.Combine(Path.GetTempPath(), "CacheTrimConfigSafety-" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        try
        {
            string optionsPath = Path.Combine(dir, "options.json");
            Check(CleanupOptions.LoadFromPath(optionsPath).Browsers.Count == 7, "new installation gets default cleanup options");
            string valid = JsonSerializer.Serialize(new CleanupOptions());
            File.WriteAllText(optionsPath, valid);
            Check(CleanupOptions.LoadFromPath(optionsPath).ExcludedPaths.Count == 0, "complete options accepted");
            foreach (string json in new[] { "{}", "{\"ExcludedPaths\":[],\"Browsers\":[]}", "{\"ExcludedPaths\":null,\"MinimumAgeHours\":{},\"Browsers\":[]}", "{", "[]", "{\"ExcludedPaths\":[],\"ExcludedPaths\":[],\"MinimumAgeHours\":{},\"Browsers\":[]}" })
            {
                File.WriteAllText(optionsPath, json);
                Reject(() => CleanupOptions.LoadFromPath(optionsPath), "incomplete, invalid or duplicate options rejected");
            }
            string configPath = Path.Combine(dir, "config.ini");
            Config.ValidateCleanupSettingsFile(configPath, false);
            Reject(() => Config.ValidateCleanupSettingsFile(configPath, true), "missing scheduled config rejected");
            string[] keys = { "working_sets", "standby", "low_standby", "modified", "file_cache", "temp", "browsers", "wer", "update_cache", "d3d_cache", "thumbnails", "dumps", "windows_logs", "uwp_cache", "recycle", "prefetch", "dns", "arp", "netbios" };
            string all = string.Join("\n", keys.Select(k => k + "=0")) + "\ntemp_age_hours=24\ntheme=Indigo\n";
            File.WriteAllText(configPath, all);
            var config = Config.LoadFromPath(configPath, true);
            config.EnsureCleanupSnapshotReadable();
            Check(!config.Temp && !config.Browsers && config.TempAgeHours == 24, "complete legacy-compatible config parsed safely");
            foreach (string broken in new[] { "", "temp=0\n", all.Replace("browsers=0\n", ""), all.Replace("temp=0\n", "temp=oops\n"), all + "temp=1\n", all.Replace("temp_age_hours=24", "temp_age_hours=2147483647"), all + "bad line\n" })
            {
                File.WriteAllText(configPath, broken);
                Reject(() => Config.LoadFromPath(configPath, true), "damaged scheduled config rejected");
                Config loaded = Config.LoadFromPath(configPath);
                Reject(loaded.EnsureCleanupSnapshotReadable, "damaged manual config cannot be used for cleanup");
            }
        }
        finally { Directory.Delete(dir, true); }
    }
}
