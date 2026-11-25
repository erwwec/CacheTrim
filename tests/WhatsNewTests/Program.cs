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
        var releases = new[] { ("v2.8", "восемь"), ("v2.7", "семь"), ("v2.6", "шесть"), ("v2.5", "старая"), ("v2.9", "будущая") };
        string history = ReleaseHistory.Format(releases, "2.5.0.0", "2.8.0");
        Check(history.Contains("восемь") && history.Contains("семь") && history.Contains("шесть"), "skipped versions included");
        Check(!history.Contains("старая") && !history.Contains("будущая"), "old and future versions excluded");
        Check(history.IndexOf("восемь") < history.IndexOf("семь") && history.IndexOf("семь") < history.IndexOf("шесть"), "newest first");
        Check(ReleaseHistory.Format(releases, "2.5", "2.10") == null, "missing current release does not claim complete history");
        Check(ReleaseHistory.Format(releases, "invalid", "2.8") == null, "invalid version fallback");
        using var sample = System.Text.Json.JsonDocument.Parse("""
        [
          {"tag_name":"v2.9", "body":"nine", "draft":false, "prerelease":false, "published_at":"2026-11-24T12:00:00Z"},
          {"tag_name":"v2.10", "body":null, "draft":false, "prerelease":false},
          {"tag_name":"v3.0", "body":"draft", "draft":true, "prerelease":false},
          {"tag_name":"v2.11", "body":"beta", "draft":false, "prerelease":true},
          {"tag_name":"unversioned", "body":"invalid", "draft":false, "prerelease":false}
        ]
        """);
        var page = ReleaseHistory.SortReleases(ReleaseHistory.ReadPage(sample.RootElement));
        Check(page.Count == 2, "history excludes drafts, prereleases and nonnumeric tags");
        Check(page[0].Version == "v2.10" && page[1].Version == "v2.9", "all history sorted numerically, newest first");
        Check(page[0].Notes == "" && page[1].Notes == "nine" && page[1].PublishedAt.HasValue, "empty notes and release date handled");
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
