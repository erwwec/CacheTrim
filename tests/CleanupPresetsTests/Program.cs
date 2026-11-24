using CacheTrim;
namespace CacheTrim { internal static class Theme { public const string Indigo="Indigo", Ocean="Ocean", Graphite="Graphite", LiquidGlass="LiquidGlass", Violet="Violet", Rose="Rose"; } }
internal static class Program
{
    static void Check(bool ok, string label) { if (!ok) throw new Exception(label); Console.WriteLine("OK: " + label); }
    static void Main()
    {
        string dir = Path.Combine(Path.GetTempPath(), "CacheTrimPresets-" + Guid.NewGuid()); string path = Path.Combine(dir, "presets.json");
        try {
            var original = new Config { Temp = false, Recycle = false, Dns = false, ThemeName = "ocean", DelaySeconds = 71, TempAgeHours = 91 };
            var entries = new Dictionary<string, Dictionary<string, bool>> { ["Игры"] = CleanupPresets.Capture(original) };
            CleanupPresets.Save(entries, path); var loaded = CleanupPresets.Load(path);
            var target = new Config { Temp = true, Recycle = true, Dns = true, ThemeName = "violet", DelaySeconds = 10, TempAgeHours = 22 };
            CleanupPresets.Apply(target, loaded["Игры"]);
            Check(!target.Temp && !target.Recycle && !target.Dns, "selected categories roundtrip");
            Check(target.ThemeName == "violet" && target.DelaySeconds == 10 && target.TempAgeHours == 22, "unrelated settings unchanged");
            var broken = CleanupPresets.Capture(original); broken.Remove("Temp"); bool rejected = false;
            try { CleanupPresets.Apply(target, broken); } catch (InvalidDataException) { rejected = true; }
            Check(rejected, "incomplete preset rejected");
        } finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }
}
