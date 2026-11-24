using System.Reflection;
using CacheTrim;
namespace CacheTrim {
    internal sealed class Config {
        public bool Temp, Wer, Browsers, D3dCache, Thumbs, Dumps, WinLogs, UwpCache, Prefetch, UpdateCache, Dns, Arp, Netbios, Recycle, WorkingSets, Modified, Standby, LowStandby, FileCache;
        public int TempAgeHours;
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
            var cleaner = new Cleaner(Console.WriteLine);
            var sweep = typeof(Cleaner).GetMethod("Sweep", BindingFlags.NonPublic | BindingFlags.Instance)!;
            sweep.Invoke(cleaner, new object[] { regular, null, "*" });
            if (File.Exists(ordinary)) throw new Exception("ordinary temp file not cleaned");
            Console.WriteLine("OK: ordinary temp file cleaned");
            string link = Path.Combine(baseDir, "link");
            if (OperatingSystem.IsWindows()) {
                using var proc = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe", "/c mklink /J \"" + link + "\" \"" + outside + "\"") { UseShellExecute = false, CreateNoWindow = true });
                proc!.WaitForExit();
                if (proc.ExitCode != 0) throw new Exception("could not create test junction");
            } else Directory.CreateSymbolicLink(link, outside);
            sweep.Invoke(cleaner, new object[] { Path.Combine(link, "Temp"), null, "*" });
            if (!File.Exists(protectedFile)) throw new Exception("file behind ancestor link was deleted");
            Console.WriteLine("OK: ancestor directory link is skipped");
        } finally { if (Directory.Exists(baseDir)) Directory.Delete(baseDir, true); }
    }
}
