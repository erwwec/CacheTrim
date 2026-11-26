using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace CacheTrim;
internal static class GameCapture
{
    // The official standalone executable of PresentMon v2.6.0 (GitHub release SHA-256).
    internal const string TrustedSha256 = "b2a706bc6ad475749e3b7e3409263aa1e6906d45bdcf993f6dbc0f660188f1af";
    internal static string[] Arguments(int pid, int seconds, int delay, string outputPath, string session)
    {
        if (pid <= 4 || seconds < 5 || seconds > 120 || delay < 0 || delay > 30 || !Path.IsPathFullyQualified(outputPath) || !Guid.TryParseExact(session, "N", out _)) throw new ArgumentException("Некорректные параметры записи.");
        return new[] { "--process_id", pid.ToString(CultureInfo.InvariantCulture), "--timed", seconds.ToString(CultureInfo.InvariantCulture), "--delay", delay.ToString(CultureInfo.InvariantCulture), "--terminate_after_timed", "--no_console_stats", "--output_file", outputPath, "--session_name", "CacheTrim-" + session };
    }
    internal static async Task<string> RunAsync(string toolPath, ProcessManager.Entry target, int seconds, int delay)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Запись доступна только в Windows.");
        if (!Path.IsPathFullyQualified(toolPath)) throw new ArgumentException("Укажи полный путь к PresentMon.");
        using var tool = new FileStream(toolPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        string hash = Convert.ToHexString(SHA256.HashData(tool)).ToLowerInvariant();
        if (hash != TrustedSha256) throw new InvalidDataException("SHA-256 не совпадает с официальным PresentMon 2.6.0 x64. Этот файл не будет запущен.");
        using var process = Process.GetProcessById(target.Id);
        string currentPath = process.MainModule?.FileName ?? throw new InvalidOperationException("Не удалось проверить путь процесса.");
        if (process.StartTime.ToUniversalTime().Ticks != target.StartUtcTicks || !string.Equals(currentPath, target.Path, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Процесс изменился. Обнови список и выбери его заново.");
        string session = Guid.NewGuid().ToString("N");
        string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CacheTrim", "GameCaptures", session);
        if (Directory.Exists(dir)) throw new IOException("Папка записи уже существует. Повтори попытку.");
        Directory.CreateDirectory(dir);
        string output = Path.Combine(dir, "capture.csv");
        var args = Arguments(target.Id, seconds, delay, output, session);
        var info = new ProcessStartInfo(toolPath) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in args) info.ArgumentList.Add(arg);
        using var capture = Process.Start(info) ?? throw new InvalidOperationException("Не удалось запустить PresentMon.");
        var stdout = capture.StandardOutput.ReadToEndAsync(); var stderr = capture.StandardError.ReadToEndAsync();
        await capture.WaitForExitAsync();
        string detail = (await stdout) + "\n" + (await stderr);
        if (capture.ExitCode != 0 || !File.Exists(output)) throw new InvalidOperationException("PresentMon не завершил запись (код " + capture.ExitCode + "). " + detail.Trim());
        try { return Summarize(output) + "\n\nCSV: " + output; }
        catch (Exception ex) { return "Запись сохранена, но рассчитать показатели не удалось: " + ex.Message + "\nCSV: " + output; }
    }
    internal static string Summarize(string file)
    {
        using var reader = new StreamReader(file);
        string head = reader.ReadLine() ?? throw new InvalidDataException("CSV пуст.");
        var cols = Csv(head); int frame = cols.FindIndex(s => s == "MsBetweenPresents"); int chain = cols.FindIndex(s => s == "SwapChainAddress");
        if (frame < 0 || chain < 0) throw new InvalidDataException("Нет необходимых столбцов PresentMon.");
        var groups = new Dictionary<string, List<double>>(StringComparer.Ordinal); string line; int rows = 0;
        while ((line = reader.ReadLine()) != null)
        {
            if (++rows > 2000000) throw new InvalidDataException("Слишком много строк для анализа; открыт исходный CSV.");
            var c = Csv(line); if (Math.Max(frame, chain) >= c.Count || !double.TryParse(c[frame], NumberStyles.Float, CultureInfo.InvariantCulture, out double ms) || !double.IsFinite(ms) || ms <= 0 || ms > 10000) continue;
            if (!groups.TryGetValue(c[chain], out var values)) groups[c[chain]] = values = new List<double>();
            values.Add(ms);
        }
        if (groups.Count == 0) throw new InvalidDataException("В CSV нет пригодных интервалов кадров.");
        var data = groups.OrderByDescending(g => g.Value.Count).First(); var valuesSorted = data.Value.OrderBy(x => x).ToList();
        double average = 1000.0 * valuesSorted.Count / valuesSorted.Sum();
        double p99 = valuesSorted[Math.Max(0, (int)Math.Ceiling(valuesSorted.Count * 0.99) - 1)];
        return "Показаны интервалы Present для самой активной цепочки кадров, не гарантированно фактически отображённые кадры.\n" +
            "Среднее: " + average.ToString("0.0", CultureInfo.InvariantCulture) + " кадров/с · 99-й процентиль интервала: " + p99.ToString("0.0", CultureInfo.InvariantCulture) + " мс\n" +
            "Измерений: " + valuesSorted.Count + "; цепочек найдено: " + groups.Count + ". Данные могут быть неполными из-за античита, API игры или драйвера.";
    }
    internal static List<string> Csv(string line)
    {
        var result = new List<string>(); var value = new StringBuilder(); bool quoted = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"') { if (quoted && i + 1 < line.Length && line[i + 1] == '"') { value.Append('"'); i++; } else quoted = !quoted; }
            else if (c == ',' && !quoted) { result.Add(value.ToString()); value.Clear(); }
            else value.Append(c);
        }
        if (quoted) throw new InvalidDataException("Неполная строка CSV.");
        result.Add(value.ToString()); return result;
    }
}
