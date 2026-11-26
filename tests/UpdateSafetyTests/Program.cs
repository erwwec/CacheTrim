using CacheTrim;
namespace CacheTrim { internal static class AppInfo { public const string Version="2.1.14", GitHubReleaseApi="https://api.github.com/repos/example/example/releases/latest"; } internal static class ReleaseVersionGuard { public static bool Matches(string a, string b) => a == b; } }
internal static class Program
{
    static void Check(bool ok, string text) { if (!ok) throw new Exception(text); Console.WriteLine("OK: " + text); }
    sealed class Capture : IProgress<UpdateDownloadProgress> { public readonly List<UpdateDownloadProgress> Items = new(); public void Report(UpdateDownloadProgress value) => Items.Add(value); }
    static void Main()
    {
        string dir=Path.Combine(Path.GetTempPath(),"CacheTrimUpdateTest-"+Guid.NewGuid()); Directory.CreateDirectory(dir);
        try {
            Check(HotkeyPolicy.Valid(1, 2, 3) && HotkeyPolicy.Valid(0, 0, 0), "three distinct shortcuts and disabled choices accepted");
            Check(!HotkeyPolicy.Valid(1, 1, 0) && !HotkeyPolicy.Valid(0, 2, 2) && !HotkeyPolicy.Valid(3, 0, 3), "duplicate shortcuts rejected");
            Check(!HotkeyPolicy.Valid(7, 0, 0) && !HotkeyPolicy.Valid(0, -1, 0), "shortcut range enforced");
            Check(BundledTweakerScripts.SafeCommandPath(@"C:\Users\User Name\script.bat"), "ordinary script path is safe");
            Check(!BundledTweakerScripts.SafeCommandPath(@"C:\Users\%TEMP%\script.bat") && !BundledTweakerScripts.SafeCommandPath("C:\"bad.bat") && !BundledTweakerScripts.SafeCommandPath(@"C:\Users\O'Brien\script.bat"), "cmd expansion and quote injection rejected");
            foreach (var item in new[] { ("explorer.bat", "9C87A647B1AB6EB8C048816C6D1E9B0A4EF293F4F164596E878D4D053471A44D", 10146), ("win-tweaker.bat", "21013C2E014E7558603D0C3E08E9995B4AE34D7C57A8D296EAA3E988F1DACED1", 23459) }) {
                string sourcePath = Path.Combine("companion", "TweakerScripts", item.Item1);
                string target = Path.Combine(dir, item.Item1);
                using (var source = File.OpenRead(sourcePath)) BundledTweakerScripts.InstallVerified(source, target, item.Item2, item.Item3);
                using (var verified = BundledTweakerScripts.TryOpenVerified(target, item.Item2, item.Item3)) {
                    Check(verified != null, item.Item1 + " preserved and verified");
                    if (OperatingSystem.IsWindows()) { bool blocked = false; try { using var write = new FileStream(target, FileMode.Open, FileAccess.Write, FileShare.ReadWrite); } catch (IOException) { blocked = true; } Check(blocked, "script remains locked against modification"); }
                }
                File.WriteAllText(target, "tampered");
                Check(BundledTweakerScripts.TryOpenVerified(target, item.Item2, item.Item3) == null, "tampered script rejected");
                using (var source = File.OpenRead(sourcePath)) BundledTweakerScripts.InstallVerified(source, target, item.Item2, item.Item3);
                Check(File.ReadAllBytes(target).SequenceEqual(File.ReadAllBytes(sourcePath)), "verified script restored from embedded bytes");
                try { using var bad = new MemoryStream(new byte[] {1, 2, 3}); BundledTweakerScripts.InstallVerified(bad, target, item.Item2, item.Item3); throw new Exception("bad script accepted"); }
                catch (InvalidDataException) { Check(File.ReadAllBytes(target).SequenceEqual(File.ReadAllBytes(sourcePath)), "bad embedded bytes cannot replace valid script"); }
            }
            byte[] download = new byte[4096]; new Random(42).NextBytes(download);
            var events = new Capture();
            using (var source = new MemoryStream(download)) using (var target = new MemoryStream()) {
                string actual = UpdateService.CopyUpdateStream(source, target, download.Length, 5000, events);
                Check(target.ToArray().SequenceEqual(download), "downloaded bytes retained");
                Check(actual == Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(download)).ToLowerInvariant(), "stream hash remains correct");
                Check(events.Items.First().DownloadedBytes == 0 && events.Items.Last().DownloadedBytes == download.Length && events.Items.Last().TotalBytes == download.Length, "known size reports correct byte progress");
            }
            events.Items.Clear();
            using (var source = new MemoryStream(download)) using (var target = new MemoryStream()) UpdateService.CopyUpdateStream(source, target, null, 5000, events);
            Check(events.Items.All(e => e.TotalBytes == null) && events.Items.Last().DownloadedBytes == download.Length, "unknown total remains indeterminate");
            events.Items.Clear();
            using (var source = new MemoryStream(download)) using (var target = new MemoryStream()) UpdateService.CopyUpdateStream(source, target, 10, 5000, events);
            Check(events.Items.Last().TotalBytes == null, "incorrect announced length does not report false percent");
            try { using var source = new MemoryStream(download); using var target = new MemoryStream(); UpdateService.CopyUpdateStream(source, target, null, 100, events); throw new Exception("oversized update accepted"); }
            catch (InvalidDataException) { Check(true, "oversized stream rejected"); }
            string path=Path.Combine(dir,"file"); File.WriteAllBytes(path,new byte[]{0,1,2});
            Check(!UpdateService.HasWindowsExecutableHeader(path),"invalid header rejected");
            File.Delete(path); Check(!File.Exists(path),"rejected file can be deleted after checking");
            File.WriteAllBytes(path,new byte[]{(byte)'M',(byte)'Z',0});
            Check(UpdateService.HasWindowsExecutableHeader(path),"MZ signature accepted");
            File.WriteAllBytes(path,Array.Empty<byte>()); Check(!UpdateService.HasWindowsExecutableHeader(path),"empty file rejected");
            byte[] valid = new byte[] { (byte)'M', (byte)'Z', 1, 2, 3, 4 };
            string hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(valid));
            using (var first = new MemoryStream(valid[..3])) using (var second = new MemoryStream(valid[3..]))
                AnimeAvatarPackage.Extract(new Stream[] { first, second }, path, hash, valid.Length);
            Check(File.ReadAllBytes(path).SequenceEqual(valid), "split avatar reconstructed and verified");
            try { using var bad = new MemoryStream(new byte[]{(byte)'M',(byte)'Z',9,9,9,9}); AnimeAvatarPackage.Extract(new Stream[]{bad}, path, hash, valid.Length); throw new Exception("tampered avatar accepted"); }
            catch (InvalidDataException) { Check(File.ReadAllBytes(path).SequenceEqual(valid), "tampered avatar rejected without replacing valid file"); }
            Check(Directory.GetFiles(dir, "*.download").Length == 0, "staged avatar is cleaned up");
            byte[] executable = new byte[4096]; executable[0] = (byte)'M'; executable[1] = (byte)'Z';
            File.WriteAllBytes(path, executable);
            string petHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(executable));
            using (var verifiedAvatar = AnimeAvatarPackage.TryOpenVerified(path, petHash, executable.Length))
            {
                Check(verifiedAvatar != null, "matching avatar verified under a held handle");
                if (OperatingSystem.IsWindows())
                {
                    bool blocked = false;
                    try { using var write = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite); }
                    catch (IOException) { blocked = true; }
                    Check(blocked, "verified avatar cannot be modified before launch");
                }
            }
            Check(AnimeAvatarPackage.TryOpenVerified(path, new string('0', 64), executable.Length) == null, "tampered avatar is not opened for launch");
            using (var locked = PetCache.TryOpenVerified(path, petHash)) Check(locked != null, "matching cached pet verified");
            Check(PetCache.TryOpenVerified(path, new string('0', 64)) == null, "tampered cached pet rejected");
            string installed = Path.Combine(dir, "installed", "DesktopPet.exe");
            Check(PetCache.TryInstallBundled(path, installed, petHash) && File.ReadAllBytes(installed).SequenceEqual(executable), "verified bundled pet installed");
            byte[] tampered = (byte[])executable.Clone(); tampered[4] = 1; File.WriteAllBytes(path, tampered);
            Check(!PetCache.TryInstallBundled(path, installed, petHash) && File.ReadAllBytes(installed).SequenceEqual(executable), "tampered bundled pet cannot replace verified copy");
            Check(!Directory.EnumerateFiles(Path.GetDirectoryName(installed)!, "*.download").Any(), "no bundled staging files left");
            Check(PetCache.IsPreviousReleaseHash(PetCache.PreviousVersions[0].Hash.ToUpperInvariant()) && !PetCache.IsPreviousReleaseHash(petHash), "old release pins identified separately from V2");
            Check(PetCache.TryOpenVerified(path, "bad") == null, "invalid trust hash rejected");
            File.WriteAllBytes(path, new byte[] { (byte)'M', (byte)'Z' });
            Check(PetCache.TryOpenVerified(path, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)))) == null, "undersized executable rejected");
            string resourceDir = Path.Combine("companion", "AnimeAvatar");
            using (var part0 = File.OpenRead(Path.Combine(resourceDir, "AnimeAvatar.exe.part00")))
            using (var part1 = File.OpenRead(Path.Combine(resourceDir, "AnimeAvatar.exe.part01")))
                AnimeAvatarPackage.Extract(new Stream[] { part0, part1 }, path, "A3256EA1B9476354A32BFA8C592741A98E59682821EE7F0AD20CB5BD87C69D74", 27351552);
            Check(new FileInfo(path).Length == 27351552 && AnimeAvatarPackage.FileMatches(path, "A3256EA1B9476354A32BFA8C592741A98E59682821EE7F0AD20CB5BD87C69D74", 27351552), "supplied avatar parts reconstruct with pinned SHA-256");
        } finally { Directory.Delete(dir,true); }
    }
}
