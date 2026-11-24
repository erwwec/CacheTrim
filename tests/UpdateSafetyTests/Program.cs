using CacheTrim;
namespace CacheTrim { internal static class AppInfo { public const string Version="2.1.14", GitHubReleaseApi="https://api.github.com/repos/example/example/releases/latest"; } internal static class ReleaseVersionGuard { public static bool Matches(string a, string b) => a == b; } }
internal static class Program
{
    static void Check(bool ok, string text) { if (!ok) throw new Exception(text); Console.WriteLine("OK: " + text); }
    static void Main()
    {
        string dir=Path.Combine(Path.GetTempPath(),"CacheTrimUpdateTest-"+Guid.NewGuid()); Directory.CreateDirectory(dir);
        try {
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
            using (var locked = PetCache.TryOpenVerified(path, petHash)) Check(locked != null, "matching cached pet verified");
            Check(PetCache.TryOpenVerified(path, new string('0', 64)) == null, "tampered cached pet rejected");
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
