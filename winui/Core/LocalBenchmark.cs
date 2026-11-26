using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CacheTrim;

// Single-threaded CPU/hash throughput only. Never claim this is an overall PC or gaming score.
internal static class LocalBenchmark
{
    internal sealed record Result(DateTimeOffset TimeUtc, double MebibytesPerSecond);
    private static string HistoryPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CacheTrim", "cpu-benchmark-history.json");
    internal static string Run()
    {
        var data = new byte[1024 * 1024];
        for (int i = 0; i < data.Length; i++) data[i] = (byte)(i % 251);
        _ = SHA256.HashData(data); // warm-up is excluded
        var watch = Stopwatch.StartNew(); long rounds = 0;
        do { _ = SHA256.HashData(data); rounds++; } while (watch.Elapsed < TimeSpan.FromSeconds(3));
        watch.Stop();
        double rate = rounds / watch.Elapsed.TotalSeconds;
        var result = new Result(DateTimeOffset.UtcNow, rate);
        var history = AddAndSave(HistoryPath, result);
        return Format(result, history);
    }
    internal static List<Result> AddAndSave(string path, Result result)
    {
        if (!double.IsFinite(result.MebibytesPerSecond) || result.MebibytesPerSecond <= 0) throw new ArgumentException("Некорректный результат теста.");
        var history = File.Exists(path) ? JsonSerializer.Deserialize<List<Result>>(File.ReadAllText(path)) ?? throw new InvalidDataException("История пуста или повреждена.") : new List<Result>();
        if (history.Any(x => x == null || !double.IsFinite(x.MebibytesPerSecond) || x.MebibytesPerSecond <= 0)) throw new InvalidDataException("История повреждена; она не была изменена.");
        history.Add(result);
        if (history.Count > 100) history.RemoveRange(0, history.Count - 100);
        string directory = Path.GetDirectoryName(path) ?? throw new InvalidOperationException("Не задана папка истории.");
        Directory.CreateDirectory(directory);
        string temp = Path.Combine(directory, ".cpu-benchmark-" + Guid.NewGuid().ToString("N") + ".tmp");
        try { File.WriteAllText(temp, JsonSerializer.Serialize(history)); File.Move(temp, path, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
        return history;
    }
    internal static string Format(Result result, IReadOnlyList<Result> history)
    {
        var text = new StringBuilder("Только скорость SHA-256 в одном потоке, не общая оценка ПК и не прогноз FPS. Скорость зависит от загрузки, температуры, питания и реализации .NET. Для сравнения используй одинаковые условия.\n\n");
        text.AppendLine("Результат: " + result.MebibytesPerSecond.ToString("0.0", CultureInfo.InvariantCulture) + " МиБ/с");
        text.AppendLine("Локальная история (новые сверху; не онлайн-рейтинг):");
        foreach (var item in history.Reverse().Take(10)) text.AppendLine(item.TimeUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") + " — " + item.MebibytesPerSecond.ToString("0.0", CultureInfo.InvariantCulture) + " МиБ/с");
        return text.ToString();
    }
}
