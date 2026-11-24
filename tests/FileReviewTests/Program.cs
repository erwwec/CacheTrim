using CacheTrim;
namespace CacheTrim { internal static class Cleaner { public static string FormatBytes(long n) => n.ToString(); } }
internal static class Program
{
    static void Check(bool b, string m) { if (!b) throw new Exception(m); Console.WriteLine("OK: " + m); }
    static void Main()
    {
        string root = Path.Combine(Path.GetTempPath(), "CacheTrimReview-" + Guid.NewGuid());
        string outside = Path.Combine(Path.GetTempPath(), "CacheTrimOutside-" + Guid.NewGuid());
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "nested")); Directory.CreateDirectory(outside);
            File.WriteAllText(Path.Combine(root,"a.txt"), "dupe"); File.WriteAllText(Path.Combine(root,"nested","b.txt"), "dupe");
            File.WriteAllText(Path.Combine(root,"other.txt"), "diff"); File.WriteAllText(Path.Combine(root,"picture.png"), "image");
            string installer = Path.Combine(root,"setup.exe"); File.WriteAllText(installer, "installer"); File.SetLastWriteTimeUtc(installer, DateTime.UtcNow.AddDays(-45));
            File.WriteAllText(Path.Combine(outside,"external.txt"), "dupe");
            string link = Path.Combine(root,"outside-link");
            if (OperatingSystem.IsWindows()) { using var proc = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe", "/c mklink /J \"" + link + "\" \"" + outside + "\"") { UseShellExecute = false, CreateNoWindow = true }); proc!.WaitForExit(); if (proc.ExitCode != 0) throw new Exception("junction create failed"); }
            else Directory.CreateSymbolicLink(link, outside);
            var dup = FileReview.Scan(root, ReviewMode.Duplicates, CancellationToken.None);
            Check(dup.Items.Count == 2 && dup.Items.All(i => !i.Path.Contains("external")), "duplicates are hash-verified; junction skipped");
            Check(FileReview.Scan(root, ReviewMode.Installers, CancellationToken.None).Items.Any(i => i.Path == installer), "old installer found");
            Check(FileReview.Scan(root, ReviewMode.Captures, CancellationToken.None).Items.Any(i => i.Path.EndsWith("picture.png")), "images found");
            var largest = FileReview.Scan(root, ReviewMode.Largest, CancellationToken.None);
            Check(largest.Items.Any(i => i.IsFolder && i.Path.EndsWith("nested") && i.Size == 4), "folder sizes include nested files");
            Check(File.Exists(Path.Combine(root,"a.txt")) && File.Exists(Path.Combine(outside,"external.txt")), "scans never delete files");
        }
        catch(Exception ex) { Console.Error.WriteLine("::error::File review test failed: " + ex.ToString().Replace("\r", " ").Replace("\n", " ")); throw; }
        finally { string link = Path.Combine(root, "outside-link"); if (Directory.Exists(link)) Directory.Delete(link, false); if (Directory.Exists(root)) Directory.Delete(root, true); if (Directory.Exists(outside)) Directory.Delete(outside, true); }
    }
}
