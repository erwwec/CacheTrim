using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace CacheTrim;

// Third-party scripts are intentionally launched only from explicit UI actions.
// SHA-256 verifies the bundled bytes have not changed; it does not establish safety.
internal static class BundledTweakerScripts
{
    internal enum Script { Explorer, Windows }
    private sealed record Definition(string FileName, string Resource, string Hash, int Length);
    private static Definition Get(Script script) => script switch
    {
        Script.Explorer => new("explorer.bat", "CacheTrim.TweakerScripts.explorer.bat", "9C87A647B1AB6EB8C048816C6D1E9B0A4EF293F4F164596E878D4D053471A44D", 10146),
        Script.Windows => new("win-tweaker.bat", "CacheTrim.TweakerScripts.win-tweaker.bat", "21013C2E014E7558603D0C3E08E9995B4AE34D7C57A8D296EAA3E988F1DACED1", 23459),
        _ => throw new ArgumentOutOfRangeException(nameof(script))
    };

    internal static string Install(Script script)
    {
        var info = Get(script);
        string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CacheTrim", "TweakerScripts");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, info.FileName);
        using var resource = typeof(BundledTweakerScripts).Assembly.GetManifestResourceStream(info.Resource)
            ?? throw new InvalidDataException("Файл скрипта отсутствует в сборке CacheTrim.");
        InstallVerified(resource, path, info.Hash, info.Length);
        return path;
    }

    internal static void InstallVerified(Stream source, string path, string expectedHash, int expectedLength)
    {
        using var data = new MemoryStream();
        source.CopyTo(data);
        if (data.Length != expectedLength || !string.Equals(Convert.ToHexString(SHA256.HashData(data.ToArray())), expectedHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Встроенный скрипт не прошёл проверку целостности.");
        using (var existing = TryOpenVerified(path, expectedHash, expectedLength)) if (existing != null) return;
        string staged = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var output = new FileStream(staged, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { data.Position = 0; data.CopyTo(output); output.Flush(true); }
            using (var check = TryOpenVerified(staged, expectedHash, expectedLength))
                if (check == null) throw new InvalidDataException("Сохранённый скрипт повреждён.");
            File.Move(staged, path, true);
        }
        finally { try { File.Delete(staged); } catch (IOException) { } }
    }

    internal static FileStream TryOpenVerified(string path, string hash, int length)
    {
        FileStream stream = null;
        try
        {
            stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length != length || !string.Equals(Convert.ToHexString(SHA256.HashData(stream)), hash, StringComparison.OrdinalIgnoreCase)) { stream.Dispose(); return null; }
            stream.Position = 0;
            return stream;
        }
        catch (IOException) { stream?.Dispose(); return null; }
        catch (UnauthorizedAccessException) { stream?.Dispose(); return null; }
    }

    internal static bool SafeCommandPath(string path) => !string.IsNullOrWhiteSpace(path) && path.IndexOfAny(new[] { '%', '"', '\'', '\r', '\n' }) < 0;

    internal static void Launch(Script script)
    {
        var info = Get(script);
        string path = Install(script);
        // cmd expands percent variables, while the scripts interpolate their own path
        // into a single-quoted PowerShell string. Reject either unsafe character.
        if (!SafeCommandPath(path)) throw new InvalidOperationException("Путь к скрипту содержит недопустимые для командной строки символы.");
        FileStream verified = TryOpenVerified(path, info.Hash, info.Length)
            ?? throw new InvalidDataException("Скрипт изменился после установки. Запуск отменён.");
        try
        {
            var process = Process.Start(new ProcessStartInfo {
                FileName = Path.Combine(Environment.SystemDirectory, "cmd.exe"),
                Arguments = "/d /v:off /s /c \"\"" + path + "\"\"",
                WorkingDirectory = Path.GetDirectoryName(path)!, UseShellExecute = false, CreateNoWindow = false
            }) ?? throw new InvalidOperationException("Не удалось открыть окно скрипта.");
            // Prevent replacement or modification while cmd and PowerShell read the file.
            _ = Task.Run(async () => { try { await process.WaitForExitAsync(); } finally { verified.Dispose(); process.Dispose(); } });
        }
        catch { verified.Dispose(); throw; }
    }
}
