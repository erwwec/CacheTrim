using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace CacheTrim;

internal static class GamingToolkit
{
    internal sealed record SteamGame(string Name, uint AppId, string Library);
    private static readonly Regex Pair = new("\\\"(?<key>[^\\\"]+)\\\"\\s+\\\"(?<val>(?:\\\\.|[^\\\"\\\\])*)\\\"", RegexOptions.CultureInvariant);
    internal static IEnumerable<(string Key, string Value)> ParseVdfPairs(string vdf) => Pair.Matches(vdf).Select(m => (m.Groups["key"].Value, m.Groups["val"].Value.Replace("\\\\", "\\").Replace("\\\"", "\"")));
    internal static IReadOnlyList<SteamGame> DiscoverSteam(string steamRoot)
    {
        var libraries = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { steamRoot };
        string config = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
        if (File.Exists(config) && new FileInfo(config).Length < 1_000_000)
            foreach (var pair in ParseVdfPairs(File.ReadAllText(config))) if (pair.Key.Equals("path", StringComparison.OrdinalIgnoreCase) && Path.IsPathFullyQualified(pair.Value)) libraries.Add(pair.Value);
        var games = new List<SteamGame>();
        foreach (var library in libraries.Take(64))
        {
            string folder = Path.Combine(library, "steamapps");
            if (!Directory.Exists(folder)) continue;
            try {
                foreach (string manifest in Directory.EnumerateFiles(folder, "appmanifest_*.acf").Take(5000)) {
                    if (new FileInfo(manifest).Length > 1_000_000) continue;
                    var fields = ParseVdfPairs(File.ReadAllText(manifest)).ToList();
                    string name = fields.FirstOrDefault(x => x.Key == "name").Value;
                    string id = fields.FirstOrDefault(x => x.Key == "appid").Value;
                    if (!string.IsNullOrWhiteSpace(name) && uint.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out uint appId) && appId > 0)
                        games.Add(new SteamGame(name, appId, library));
                }
            } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
        return games.GroupBy(x => x.AppId).Select(x => x.First()).OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }
    internal static string GetSteamRoot()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
        return key?.GetValue("SteamPath") as string ?? "";
    }

    internal sealed record EpicGame(string Name, string InstallLocation);
    internal static IReadOnlyList<EpicGame> DiscoverEpic(string manifestsFolder)
    {
        var games = new List<EpicGame>();
        if (!Directory.Exists(manifestsFolder)) return games;
        try {
            foreach (string manifest in Directory.EnumerateFiles(manifestsFolder, "*.item").Take(5000)) {
                try {
                    if (new FileInfo(manifest).Length > 1_000_000) continue;
                    using var data = JsonDocument.Parse(File.ReadAllText(manifest));
                    if (!data.RootElement.TryGetProperty("DisplayName", out var name) || name.ValueKind != JsonValueKind.String || !data.RootElement.TryGetProperty("InstallLocation", out var location) || location.ValueKind != JsonValueKind.String) continue;
                    var title = name.GetString(); var path = location.GetString();
                    if (!string.IsNullOrWhiteSpace(title) && !string.IsNullOrWhiteSpace(path) && Path.IsPathFullyQualified(path)) games.Add(new(title, path));
                } catch (IOException) { } catch (UnauthorizedAccessException) { } catch (JsonException) { }
            }
        } catch (IOException) { } catch (UnauthorizedAccessException) { }
        return games.OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }
    internal static string GetEpicManifestFolder() => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Epic", "EpicGamesLauncher", "Data", "Manifests");

    private const string PresentMonUrl = "https://github.com/GameTechDev/PresentMon/releases/download/v2.3.1/PresentMon-2.3.1-x64.exe";
    private const string PresentMonHash = "364E5D98D4D134BD54DD25C22ED2CA2F4883F8BC3ED6502BEE0C151E3436D30C";
    private const int PresentMonSize = 421312;
    internal static readonly string PresentMonLicense = "https://github.com/GameTechDev/PresentMon/blob/v2.3.1/LICENSE.txt";
    private static bool ValidExe(string path)
    {
        try { using var f = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read); return f.Length == PresentMonSize && Convert.ToHexString(SHA256.HashData(f)) == PresentMonHash; }
        catch (IOException) { return false; } catch (UnauthorizedAccessException) { return false; }
    }
    internal static async Task<string> EnsurePresentMonAsync(CancellationToken token)
    {
        string dir = Path.Combine(SystemToolkit.DataDirectory, "tools"); Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "PresentMon-2.3.1-x64.exe");
        if (ValidExe(path)) return path;
        string staged = path + "." + Guid.NewGuid().ToString("N") + ".download";
        try {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(90) };
            using var response = await client.GetAsync(PresentMonUrl, HttpCompletionOption.ResponseHeadersRead, token); response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength is long n && n != PresentMonSize) throw new InvalidDataException("Размер PresentMon не совпадает.");
            await using (var source = await response.Content.ReadAsStreamAsync(token))
            await using (var dest = new FileStream(staged, FileMode.CreateNew, FileAccess.Write, FileShare.None, 32768, true)) {
                byte[] buffer = new byte[32768]; int read, total = 0;
                while ((read = await source.ReadAsync(buffer, token)) != 0) { total += read; if (total > PresentMonSize) throw new InvalidDataException("Неверный размер PresentMon."); await dest.WriteAsync(buffer.AsMemory(0, read), token); }
            }
            if (!ValidExe(staged)) throw new InvalidDataException("Хеш PresentMon не совпадает. Загрузка отклонена.");
            File.Move(staged, path, true); return path;
        } finally { try { File.Delete(staged); } catch (IOException) {} }
    }

    internal sealed record CaptureSummary(int Frames, double AveragePresentedFps, double P99FrameTimeMs, string SwapChain);
    internal static string[] ParseCsvLine(string line)
    {
        var list = new List<string>(); var field = new StringBuilder(); bool quote = false;
        for (int i = 0; i < line.Length; i++) {
            char c = line[i];
            if (c == '"') { if (quote && i + 1 < line.Length && line[i + 1] == '"') { field.Append('"'); i++; } else quote = !quote; }
            else if (c == ',' && !quote) { list.Add(field.ToString()); field.Clear(); } else field.Append(c);
        }
        if (quote) throw new InvalidDataException("Некорректная строка CSV.");
        list.Add(field.ToString()); return list.ToArray();
    }
    internal static CaptureSummary SummarizeCsv(TextReader reader)
    {
        var header = ParseCsvLine(reader.ReadLine() ?? throw new InvalidDataException("Пустой CSV."));
        int elapsedIndex = Array.IndexOf(header, "MsBetweenPresents"), chainIndex = Array.IndexOf(header, "SwapChainAddress");
        if (elapsedIndex < 0 || chainIndex < 0) throw new InvalidDataException("Нужные столбцы PresentMon не найдены.");
        var groups = new Dictionary<string, List<double>>(); int rows = 0; string line;
        while ((line = reader.ReadLine()) != null && ++rows <= 2_000_000) {
            var cells = ParseCsvLine(line); if (cells.Length <= Math.Max(elapsedIndex, chainIndex)) continue;
            if (!double.TryParse(cells[elapsedIndex], NumberStyles.Float, CultureInfo.InvariantCulture, out double ms) || !double.IsFinite(ms) || ms <= 0 || ms > 10_000) continue;
            if (!groups.TryGetValue(cells[chainIndex], out var list)) groups[cells[chainIndex]] = list = new(); list.Add(ms);
        }
        var best = groups.OrderByDescending(x => x.Value.Count).FirstOrDefault();
        if (best.Value == null || best.Value.Count < 2) throw new InvalidDataException("Недостаточно данных кадров. Проверь, что игра запущена и поддерживает запись.");
        best.Value.Sort(); double p99 = best.Value[(int)Math.Ceiling(best.Value.Count * 0.99) - 1];
        return new(best.Value.Count, 1000 / best.Value.Average(), p99, best.Key);
    }
    internal static async Task<(CaptureSummary Summary, string Csv)> CaptureAsync(ManagedProcessTools.ProcessIdentity target, int seconds, CancellationToken token)
    {
        if (seconds < 5 || seconds > 120) throw new ArgumentOutOfRangeException(nameof(seconds));
        using var targetProcess = ManagedProcessTools.OpenChecked(target);
        string executable = await EnsurePresentMonAsync(token);
        using var verified = new FileStream(executable, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (verified.Length != PresentMonSize || Convert.ToHexString(SHA256.HashData(verified)) != PresentMonHash) throw new InvalidDataException("Файл PresentMon изменён.");
        if (targetProcess.HasExited || !ManagedProcessTools.Same(ManagedProcessTools.Identify(targetProcess), target)) throw new InvalidOperationException("Процесс игры завершился или изменился.");
        Directory.CreateDirectory(SystemToolkit.DataDirectory);
        string csv = Path.Combine(SystemToolkit.DataDirectory, "game-capture-" + Guid.NewGuid().ToString("N") + ".csv");
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { "--process_id", target.Pid.ToString(CultureInfo.InvariantCulture), "--timed", seconds.ToString(CultureInfo.InvariantCulture), "--terminate_after_timed", "--terminate_on_proc_exit", "--output_file", csv, "--session_name", "CacheTrim-" + Guid.NewGuid().ToString("N"), "--no_console_stats" }) start.ArgumentList.Add(arg);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Не удалось запустить PresentMon.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(seconds + 40));
        using var reg = timeout.Token.Register(() => { try { if (!process.HasExited) process.Kill(true); } catch { } });
        var stdout = process.StandardOutput.ReadToEndAsync(timeout.Token); var stderr = process.StandardError.ReadToEndAsync(timeout.Token);
        await process.WaitForExitAsync(timeout.Token); string output = await stdout, error = await stderr;
        if (process.ExitCode != 0 || !File.Exists(csv)) throw new InvalidOperationException("PresentMon не смог записать кадры. " + (error + " " + output).Trim());
        if (new FileInfo(csv).Length > 200_000_000) throw new InvalidDataException("CSV превышает 200 МБ.");
        using var reader = File.OpenText(csv); return (SummarizeCsv(reader), csv);
    }
}
