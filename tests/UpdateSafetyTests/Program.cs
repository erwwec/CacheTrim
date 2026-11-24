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
        } finally { Directory.Delete(dir,true); }
    }
}
