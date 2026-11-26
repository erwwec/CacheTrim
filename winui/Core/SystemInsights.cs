using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;

namespace CacheTrim;

// Read-only checks. Scripts are constants; no user-provided text is executed.
internal static class SystemInsights
{
    internal const string DiskScript = @"
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
try {
  $items = @(
    Get-PhysicalDisk -ErrorAction Stop | ForEach-Object {
      $disk = $_; $counter = $null; $err = $null
      try { $counter = $disk | Get-StorageReliabilityCounter -ErrorAction Stop } catch { $err = 'Счётчики недоступны' }
      [pscustomobject]@{ Name = [string]$disk.FriendlyName; Media = [string]$disk.MediaType; Health = [string]$disk.HealthStatus;
        Temperature = $counter.Temperature; Wear = $counter.Wear; Hours = $counter.PowerOnHours;
        ReadErrors = $counter.ReadErrorsUncorrected; WriteErrors = $counter.WriteErrorsUncorrected; Error = $err }
    }
  )
  ConvertTo-Json -InputObject $items -Depth 3 -Compress
} catch { [Console]::Error.WriteLine('Не удалось получить сведения о физических дисках: ' + $_.Exception.Message); exit 1 }
";
    internal const string SecurityScript = @"
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$boot = $null; $present = $null; $ready = $null
try { $boot = Confirm-SecureBootUEFI -ErrorAction Stop } catch { }
try { $tpm = Get-Tpm -ErrorAction Stop; $present = $tpm.TpmPresent; $ready = $tpm.TpmReady } catch { }
[pscustomobject]@{ SecureBoot = $boot; TpmPresent = $present; TpmReady = $ready } | ConvertTo-Json -Compress
";
    internal static string RunDisk() => FormatDisk(Run(DiskScript));
    internal static string RunSecurity() => FormatSecurity(Run(SecurityScript));

    private static string Run(string script)
    {
        string exe = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
        string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
        var info = new ProcessStartInfo(exe) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 };
        info.ArgumentList.Add("-NoLogo"); info.ArgumentList.Add("-NoProfile"); info.ArgumentList.Add("-NonInteractive"); info.ArgumentList.Add("-EncodedCommand"); info.ArgumentList.Add(encoded);
        using var p = Process.Start(info) ?? throw new InvalidOperationException("Не удалось запустить Windows PowerShell.");
        var stdout = p.StandardOutput.ReadToEndAsync(); var stderr = p.StandardError.ReadToEndAsync();
        if (!p.WaitForExit(30000)) { try { p.Kill(true); } catch { } throw new TimeoutException("Проверка не ответила за 30 секунд."); }
        string output = stdout.GetAwaiter().GetResult(); string error = stderr.GetAwaiter().GetResult();
        if (p.ExitCode != 0) throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? "Windows не предоставила данные." : error.Trim());
        return output;
    }

    internal static string FormatDisk(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array) throw new FormatException("Windows вернула неожиданный формат данных дисков.");
        var result = new StringBuilder("Данные предоставляет Windows и драйвер накопителя; часть значений может отсутствовать или быть неточной. Нулевые значения не доказывают исправность. Проверка ничего не изменяет.\n\n");
        int count = 0;
        foreach (var disk in doc.RootElement.EnumerateArray())
        {
            if (disk.ValueKind != JsonValueKind.Object) throw new FormatException("Windows вернула некорректную запись диска.");
            count++;
            result.AppendLine(Value(disk, "Name"));
            result.AppendLine("Тип: " + Value(disk, "Media") + " · состояние Windows: " + Value(disk, "Health"));
            result.AppendLine("Температура: " + Value(disk, "Temperature", " °C") + " · износ: " + Value(disk, "Wear", " %"));
            result.AppendLine("Время работы: " + Value(disk, "Hours", " ч"));
            result.AppendLine("Неисправленные ошибки чтения: " + Value(disk, "ReadErrors") + " · записи: " + Value(disk, "WriteErrors"));
            if (disk.TryGetProperty("Error", out var err) && err.ValueKind == JsonValueKind.String) result.AppendLine(err.GetString());
            result.AppendLine();
        }
        if (count == 0) result.AppendLine("Windows не вернула физических дисков.");
        return result.ToString();
    }

    internal static string FormatSecurity(string json)
    {
        using var doc = JsonDocument.Parse(json); var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object) throw new FormatException("Windows вернула неожиданный формат состояния защиты.");
        return "Проверка только читает состояние Windows и не меняет настройки BIOS или системы. Совместимость с конкретной игрой не гарантируется.\n\n" +
            "Secure Boot: " + State(root, "SecureBoot", "включён", "выключен") + "\n" +
            "TPM: " + State(root, "TpmPresent", "обнаружен", "не обнаружен") + "\n" +
            "Готовность TPM: " + State(root, "TpmReady", "готов", "не готов") + "\n\n" +
            "Если значение неизвестно, Windows или устройство не предоставили ответ. Требования и настройки зависят от игры и материнской платы; перед изменениями ознакомься с их официальными инструкциями.";
    }
    private static string State(JsonElement obj, string name, string yes, string no) => obj.TryGetProperty(name, out var v) ? v.ValueKind switch { JsonValueKind.True => yes, JsonValueKind.False => no, _ => "неизвестно" } : "неизвестно";
    private static string Value(JsonElement obj, string name, string suffix = "")
    {
        if (!obj.TryGetProperty(name, out var v)) return "нет данных";
        string s = v.ValueKind switch { JsonValueKind.String => v.GetString(), JsonValueKind.Number => v.GetRawText(), _ => null };
        return string.IsNullOrWhiteSpace(s) ? "нет данных" : s + suffix;
    }
}
