using CacheTrim;
using System.Diagnostics;

namespace CacheTrim
{
    internal static class Config { public static string ConfigDir = Path.Combine(Path.GetTempPath(), "CacheTrimWhatsNewTest-" + Guid.NewGuid()); }
    internal static class App { internal static void Log(string message) => Console.WriteLine(message); }
}

internal static class Program
{
    private static void Check(bool test, string description)
    {
        if (!test) throw new Exception("FAILED: " + description);
        Console.WriteLine("OK: " + description);
    }

    private static void Main(string[] args)
    {
        Check(WhatsNewState.IsOlderVersion("2.1.12.0", "2.1.13"), "older version");
        Check(!WhatsNewState.IsOlderVersion("2.1.13.0", "2.1.13.0"), "same version");
        Check(!WhatsNewState.IsOlderVersion("invalid", "2.1.13"), "invalid version");
        string dir = Path.Combine(Path.GetTempPath(), "CacheTrimWhatsNewTest-" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        try
        {
            string target = Path.Combine(dir, "CacheTrim.exe");
            Func<string, string> version = _ => "2.1.12.0";
            Check(WhatsNewState.FindLegacyPreviousVersion(target, "2.1.13", version) == null, "no backup");
            File.WriteAllText(target + ".previous", "test");
            Check(WhatsNewState.FindLegacyPreviousVersion(target, "2.1.13", version) == "2.1.12.0", "legacy update");
            WhatsNewState.MarkShown("2.1.13");
            Check(WhatsNewState.FindLegacyPreviousVersion(target, "2.1.13", version) == null, "show only once");
            if (args.Length > 0 && OperatingSystem.IsWindows())
            {
                File.Delete(Path.Combine(Config.ConfigDir, "whats-new-shown.txt"));
                File.Copy(args[0], target + ".previous", true);
                string actual = FileVersionInfo.GetVersionInfo(target + ".previous").FileVersion;
                Check(!string.IsNullOrEmpty(actual), "read version from renamed Windows EXE");
                Check(WhatsNewState.FindLegacyPreviousVersion(target, "999.0.0") == actual, "detect real Windows EXE backup");
            }
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            if (Directory.Exists(Config.ConfigDir)) Directory.Delete(Config.ConfigDir, true);
        }
    }
}
