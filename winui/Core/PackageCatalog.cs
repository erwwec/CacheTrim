using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CacheTrim;
internal static class PackageCatalog
{
    internal static bool ValidId(string s) => s != null && Regex.IsMatch(s, @"\A[A-Za-z0-9][A-Za-z0-9._+\-]{0,127}\z", RegexOptions.CultureInvariant);
    internal static string CheckQuery(string value)
    {
        string s = (value ?? "").Trim();
        if (s.Length < 2 || s.Length > 80 || s.IndexOfAny(new[] { '\r', '\n', '\0' }) >= 0) throw new ArgumentException("Введите от 2 до 80 символов без переносов строк.");
        return s;
    }
    internal static string[] SearchArgs(string query, string source)
    {
        if (source != "winget" && source != "msstore") throw new ArgumentException("Неизвестный источник.");
        return new[] { "search", "--name", CheckQuery(query), "--source", source, "--accept-source-agreements", "--disable-interactivity" };
    }
    internal static string[] ListArgs() => new[] { "list", "--accept-source-agreements", "--disable-interactivity" };
    internal static string[] InstallArgs(string id, string source)
    {
        if (!ValidId(id) || (source != "winget" && source != "msstore")) throw new ArgumentException("Некорректный ID или источник.");
        return new[] { "install", "--id", id, "--exact", "--source", source, "--accept-source-agreements", "--disable-interactivity" };
    }
    internal static string[] UninstallArgs(string id)
    {
        if (!ValidId(id)) throw new ArgumentException("Некорректный ID программы.");
        return new[] { "uninstall", "--id", id, "--exact", "--disable-interactivity" };
    }
    internal static async Task<string> RunAsync(string[] args)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("WinGet доступен только в Windows.");
        string alias = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WindowsApps", "winget.exe");
        if (!File.Exists(alias)) throw new FileNotFoundException("WinGet не найден. Установи или обнови App Installer от Microsoft и включи псевдоним winget.exe.");
        var info = new ProcessStartInfo(alias) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 };
        foreach (string arg in args) info.ArgumentList.Add(arg);
        using var p = Process.Start(info) ?? throw new InvalidOperationException("Не удалось запустить WinGet.");
        // Do not kill an installer on timeout: it may be in the middle of modifying a system.
        var output = p.StandardOutput.ReadToEndAsync(); var error = p.StandardError.ReadToEndAsync();
        await p.WaitForExitAsync();
        return (await output) + "\n" + (await error) + "\nКод WinGet: " + p.ExitCode;
    }
}
