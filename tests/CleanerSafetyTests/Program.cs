using System.Reflection;
using CacheTrim;
namespace CacheTrim {
    internal sealed class Config {
        public bool Temp, Wer, Browsers, D3dCache, Thumbs, Dumps, WinLogs, UwpCache, Prefetch, UpdateCache, Dns, Arp, Netbios, Recycle, WorkingSets, Modified, Standby, LowStandby, FileCache;
        public int TempAgeHours;
        internal static void EnsureCleanupSettingsReadable(bool requireExisting = false) { }
        internal void EnsureCleanupSnapshotReadable() { }
    }
    internal static class Native {
        public enum PurgeTarget { WorkingSets, ModifiedList, StandbyList, LowPriorityStandby }
        public static string Purge(PurgeTarget t) => null;
        public static string FlushSystemFileCache() => null;
        public static bool EmptyRecycleBin() => true;
        public static bool FlushDnsCache() => true;
    }
}
internal static class Program {
    private static void Main() {
        try { Run(); }
        catch (Exception ex) {
            Console.Error.WriteLine("::error::Cleaner test failed: " + ex.ToString().Replace("\r", " ").Replace("\n", " "));
            throw;
        }
    }
    private static void Run() {
        string baseDir = Path.Combine(Path.GetTempPath(), "CacheTrimSafety-" + Guid.NewGuid());
        try {
            string regular = Path.Combine(baseDir, "regular");
            string outside = Path.Combine(baseDir, "outside");
            Directory.CreateDirectory(regular);
            Directory.CreateDirectory(Path.Combine(outside, "Temp"));
            string ordinary = Path.Combine(regular, "delete-me.tmp");
            string protectedFile = Path.Combine(outside, "Temp", "protected.tmp");
            File.WriteAllText(ordinary, "ordinary"); File.WriteAllText(protectedFile, "protected");
            var options = new CleanupOptions();
            var cleaner = new Cleaner(Console.WriteLine, options);
            var sweep = typeof(Cleaner).GetMethod("Sweep", BindingFlags.NonPublic | BindingFlags.Instance)!;
            sweep.Invoke(cleaner, new object[] { regular, null, "*" });
            if (File.Exists(ordinary)) throw new Exception("ordinary temp file not cleaned");
            Console.WriteLine("OK: ordinary temp file cleaned");
            string runningProcess = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
            var guard = new GameGuard(new[] { Path.Combine(baseDir, runningProcess + ".exe") });
            try { guard.Check(true); throw new Exception("running protected process was ignored"); }
            catch (InvalidOperationException ex) when (ex.Message.Contains(runningProcess, StringComparison.OrdinalIgnoreCase)) { Console.WriteLine("OK: running protected process blocks maintenance"); }
            try { guard.Check(); throw new Exception("repeat check bypassed active game"); }
            catch (InvalidOperationException ex) when (ex.Message.Contains(runningProcess, StringComparison.OrdinalIgnoreCase)) { Console.WriteLine("OK: rapid retry does not bypass game guard"); }

            string excludedDir = Path.Combine(regular, "excluded");
            Directory.CreateDirectory(excludedDir);
            string excluded = Path.Combine(excludedDir, "keep.tmp");
            string candidate = Path.Combine(regular, "candidate.tmp");
            File.WriteAllText(excluded, "do not delete"); File.WriteAllText(candidate, "candidate");
            options.ExcludedPaths.Add(excludedDir); options.Validate();
            var preview = new Cleaner(Console.WriteLine, options);
            typeof(Cleaner).GetField("_preview", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(preview, true);
            sweep.Invoke(preview, new object[] { regular, null, "*" });
            if (!File.Exists(candidate) || !File.Exists(excluded)) throw new Exception("preview deleted data");
            if (!preview.PreviewPaths.Any(p => p.EndsWith("candidate.tmp")) || preview.PreviewPaths.Any(p => p.EndsWith("keep.tmp"))) throw new Exception("preview ignored exclusions");
            sweep.Invoke(cleaner, new object[] { regular, null, "*" });
            if (!File.Exists(excluded) || File.Exists(candidate)) throw new Exception("cleanup ignored exclusions");
            Console.WriteLine("OK: preview is read-only and respects exclusions; cleanup respects exclusions");
            string old = Path.Combine(regular, "old.tmp"); string recent = Path.Combine(regular, "recent.tmp");
            File.WriteAllText(old, "old"); File.WriteAllText(recent, "new");
            File.SetLastWriteTimeUtc(old, DateTime.UtcNow.AddDays(-2));
            options.MinimumAgeHours["temp"] = 24;
            typeof(Cleaner).GetMethod("Category", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(cleaner, new object[] { "temp" });
            sweep.Invoke(cleaner, new object[] { regular, null, "*" });
            if (File.Exists(old) || !File.Exists(recent)) throw new Exception("per-category age not respected");
            Console.WriteLine("OK: age policy leaves recent files intact");
            try { new CleanupOptions { ExcludedPaths = new List<string> { "relative-path" } }.Validate(); throw new Exception("invalid exclusion accepted"); }
            catch (ArgumentException) { Console.WriteLine("OK: invalid exclusion is rejected"); }
            string link = Path.Combine(baseDir, "link");
            if (OperatingSystem.IsWindows()) {
                using var proc = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe", "/c mklink /J \"" + link + "\" \"" + outside + "\"") { UseShellExecute = false, CreateNoWindow = true });
                proc!.WaitForExit();
                if (proc.ExitCode != 0) throw new Exception("could not create test junction");
            } else Directory.CreateSymbolicLink(link, outside);
            sweep.Invoke(cleaner, new object[] { Path.Combine(link, "Temp"), null, "*" });
            if (!File.Exists(protectedFile)) throw new Exception("file behind ancestor link was deleted");
            Console.WriteLine("OK: ancestor directory link is skipped");
            string browserRoot = Path.Combine(baseDir, "browser");
            Directory.CreateDirectory(Path.Combine(browserRoot, "Default"));
            Directory.CreateDirectory(Path.Combine(browserRoot, "Profile 6"));
            Directory.CreateDirectory(Path.Combine(browserRoot, "Profile 123"));
            Directory.CreateDirectory(Path.Combine(browserRoot, "Profile bad"));
            string profileLink = Path.Combine(browserRoot, "Profile 7");
            if (OperatingSystem.IsWindows()) {
                using var proc = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe", "/c mklink /J \"" + profileLink + "\" \"" + outside + "\"") { UseShellExecute = false, CreateNoWindow = true });
                proc!.WaitForExit();
                if (proc.ExitCode != 0) throw new Exception("could not create profile junction");
            } else Directory.CreateSymbolicLink(profileLink, outside);
            var profiles = Cleaner.BrowserProfiles(browserRoot).Select(Path.GetFileName).ToArray();
            if (!profiles.Contains("Default") || !profiles.Contains("Profile 6") ||
                !profiles.Contains("Profile 123") || profiles.Contains("Profile bad") || profiles.Contains("Profile 7"))
                throw new Exception("browser profiles incorrectly enumerated");
            Console.WriteLine("OK: all numbered browser profiles discovered without unrelated folders");
            Directory.Delete(profileLink, false);
        } finally {
            string link = Path.Combine(baseDir, "link");
            // Delete the junction itself, never recurse through it.
            if (Directory.Exists(link)) Directory.Delete(link, false);
            if (Directory.Exists(baseDir)) Directory.Delete(baseDir, true);
        }
    }
}
