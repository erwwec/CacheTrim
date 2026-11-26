using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CacheTrim;

// All operations here are opt-in. Read-only diagnostics must never mutate Windows.
internal static class SystemToolkit
{
    internal static readonly string DataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CacheTrim", "SystemToolkit");
    internal record BenchmarkResult(DateTimeOffset Time, long Iterations, double Seconds, double Score, int LogicalProcessors);

    internal static async Task<BenchmarkResult> BenchmarkAsync(int seconds, CancellationToken cancellationToken)
    {
        if (seconds < 1 || seconds > 30) throw new ArgumentOutOfRangeException(nameof(seconds));
        // Fixed single-threaded SHA-256 workload, not a CPU/GPU gaming FPS prediction.
        return await Task.Run(() => {
            var block = new byte[4096]; RandomNumberGenerator.Fill(block);
            byte[] output = new byte[32]; long count = 0;
            var watch = Stopwatch.StartNew();
            while (watch.Elapsed.TotalSeconds < seconds)
            {
                cancellationToken.ThrowIfCancellationRequested();
                SHA256.HashData(block, output);
                output.CopyTo(block, 0); count++;
            }
            watch.Stop();
            return new BenchmarkResult(DateTimeOffset.Now, count, watch.Elapsed.TotalSeconds, count / watch.Elapsed.TotalSeconds, Environment.ProcessorCount);
        }, cancellationToken);
    }

    internal static void SaveBenchmark(BenchmarkResult result, string path)
    {
        var records = ReadBenchmarks(path).Take(99).ToList(); records.Insert(0, result);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string tmp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(tmp, JsonSerializer.Serialize(records)); File.Move(tmp, path, true); }
        finally { try { File.Delete(tmp); } catch (IOException) {} }
    }

    internal static IReadOnlyList<BenchmarkResult> ReadBenchmarks(string path)
    {
        if (!File.Exists(path)) return Array.Empty<BenchmarkResult>();
        // Never overwrite an unreadable/corrupt history.
        var bytes = new FileInfo(path).Length;
        if (bytes > 1_000_000) throw new InvalidDataException("История бенчмарка слишком велика.");
        try { return JsonSerializer.Deserialize<List<BenchmarkResult>>(File.ReadAllText(path)) ?? throw new InvalidDataException("История пуста или повреждена."); }
        catch (JsonException ex) { throw new InvalidDataException("История бенчмарка повреждена; сохранение остановлено.", ex); }
    }

    internal static string RenderBenchmarkHistory(IEnumerable<BenchmarkResult> results)
    {
        var rows = results.Take(100).ToList();
        if (rows.Count == 0) return "Замеров пока нет.";
        var ranking = rows.Select(r => r.Score).OrderByDescending(s => s).ToArray();
        return string.Join(Environment.NewLine, rows.Select(r => $"{r.Time.LocalDateTime:g}  |  {r.Score:N0} хешей/с  |  {r.Seconds:N1} с  |  место {Array.IndexOf(ranking, r.Score) + 1} из {rows.Count}"));
    }

    internal const string DiskScript = """
$ErrorActionPreference='Stop'
$disks = @(Get-PhysicalDisk)
if ($disks.Count -eq 0) { 'Накопители не найдены.'; exit 0 }
foreach ($d in $disks) {
  $r = $null
  try { $r = Get-StorageReliabilityCounter -PhysicalDisk $d -ErrorAction Stop } catch {}
  function val($v,$suffix) { if ($null -eq $v) { 'нет данных' } else { "$v$suffix" } }
  "Диск: $($d.FriendlyName)  [$($d.MediaType), $($d.BusType)]"
  "Состояние Windows: $($d.HealthStatus)"
  "Температура: $(val $r.Temperature ' °C'); износ: $(val $r.Wear '%'); часы работы: $(val $r.PowerOnHours ' ч')"
  "Ошибки чтения (все / неисправленные): $(val $r.ReadErrorsTotal '') / $(val $r.ReadErrorsUncorrected '')"
  "Ошибки записи (все / неисправленные): $(val $r.WriteErrorsTotal '') / $(val $r.WriteErrorsUncorrected '')"
  ''
}
'Поля зависят от накопителя и драйвера. Износ 100% означает достижение расчётного лимита; отсутствие данных не означает исправность.'
""";

    internal const string SecurityScript = """
$ErrorActionPreference='Continue'
try { $x = Confirm-SecureBootUEFI -ErrorAction Stop; "Secure Boot: $(if ($x) {'включён'} else {'выключен'})" }
catch { 'Secure Boot: неизвестно (не поддерживается, выключен UEFI или доступ запрещён).' }
try { $t = Get-Tpm -ErrorAction Stop; "TPM: присутствует=$($t.TpmPresent); готов=$($t.TpmReady); включён=$($t.TpmEnabled); активен=$($t.TpmActivated)" }
catch { 'TPM: нет данных или доступ запрещён.' }
try { $v=Get-CimInstance -ClassName Win32_DeviceGuard -Namespace root\Microsoft\Windows\DeviceGuard -ErrorAction Stop; "VBS: состояние $($v.VirtualizationBasedSecurityStatus) (0 — отключено, 1 — включено, 2 — работает)." }
catch { 'VBS: нет данных.' }
'Проверка только читает состояние. Результат не гарантирует совместимость с античитами и не изменяет BIOS или защиту.'
""";

    internal static async Task<string> ReadPowerShellAsync(string script, CancellationToken token)
    {
        string exe = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
        var start = new ProcessStartInfo(exe) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 };
        start.ArgumentList.Add("-NoLogo"); start.ArgumentList.Add("-NoProfile"); start.ArgumentList.Add("-NonInteractive"); start.ArgumentList.Add("-EncodedCommand");
        start.ArgumentList.Add(Convert.ToBase64String(Encoding.Unicode.GetBytes("[Console]::OutputEncoding = [Text.UTF8Encoding]::new();\n" + script)));
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Не удалось запустить системный PowerShell.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(45));
        using var registration = timeout.Token.Register(() => { try { if (!process.HasExited) process.Kill(true); } catch { } });
        var stdout = process.StandardOutput.ReadToEndAsync(timeout.Token); var stderr = process.StandardError.ReadToEndAsync(timeout.Token);
        await process.WaitForExitAsync(timeout.Token); string output = await stdout; string error = await stderr;
        if (process.ExitCode != 0 && string.IsNullOrWhiteSpace(output)) throw new InvalidOperationException("Системная диагностика не удалась: " + error.Trim());
        return output.Trim() + (process.ExitCode != 0 ? "\n\nДиагностика завершилась с ошибкой: " + error.Trim() : "");
    }

    internal static bool ValidPackageId(string id) => id.Length is > 0 and <= 150 && Regex.IsMatch(id, @"\A[A-Za-z0-9][A-Za-z0-9._-]*\z", RegexOptions.CultureInvariant);
    internal static bool ValidSearch(string s) => s.Length is > 0 and <= 100 && s.All(c => !char.IsControl(c));
    internal static string WingetPath() => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WindowsApps", "winget.exe");
    internal static ProcessStartInfo Winget(string path, IEnumerable<string> args, bool capture)
    {
        var start = new ProcessStartInfo(path) { UseShellExecute = false, CreateNoWindow = capture, RedirectStandardOutput = capture, RedirectStandardError = capture };
        foreach (var arg in args) start.ArgumentList.Add(arg);
        return start;
    }
    internal static async Task<string> RunWingetReadAsync(string[] args, CancellationToken token)
    {
        string path = WingetPath(); if (!File.Exists(path)) throw new FileNotFoundException("WinGet не найден. Установи или обнови «Установщик приложений» Microsoft Store.");
        using var process = Process.Start(Winget(path, args, true)) ?? throw new InvalidOperationException("Не удалось открыть WinGet.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(60));
        using var reg = timeout.Token.Register(() => { try { if (!process.HasExited) process.Kill(true); } catch { } });
        var output = process.StandardOutput.ReadToEndAsync(timeout.Token); var error = process.StandardError.ReadToEndAsync(timeout.Token);
        await process.WaitForExitAsync(timeout.Token); var text = await output; var err = await error;
        return text.Trim() + (string.IsNullOrWhiteSpace(err) ? "" : "\n" + err.Trim()) + (process.ExitCode == 0 ? "" : $"\nWinGet завершился с кодом {process.ExitCode}.");
    }
}
