using System;
using System.IO;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CacheTrim;

internal sealed class UpdateInfo
{
    public string Version { get; init; } = string.Empty;
    public string ReleaseUrl { get; init; } = string.Empty;
    public string DownloadUrl { get; init; } = string.Empty;
    public string ChecksumUrl { get; init; } = string.Empty;
    public bool Available { get; init; }
}

internal static class UpdateService
{
    private static readonly HttpClient Client = new();

    static UpdateService()
    {
        Client.DefaultRequestHeaders.UserAgent.ParseAdd("CacheTrim/" + AppInfo.Version);
    }

    public static UpdateInfo CheckLatest()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, AppInfo.GitHubReleaseApi);
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        request.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true, NoStore = true };
        using HttpResponseMessage response = Client.Send(request);
        response.EnsureSuccessStatusCode();
        using JsonDocument document = JsonDocument.Parse(response.Content.ReadAsStream());
        JsonElement release = document.RootElement;

        string latest = release.GetProperty("tag_name").GetString()?.TrimStart('v', 'V') ?? string.Empty;
        if (!System.Version.TryParse(latest, out System.Version? latestVersion) ||
            !System.Version.TryParse(AppInfo.Version, out System.Version? currentVersion))
            throw new InvalidDataException("Не удалось прочитать номер версии GitHub Release.");

        string exeUrl = string.Empty;
        string hashUrl = string.Empty;
        foreach (JsonElement asset in release.GetProperty("assets").EnumerateArray())
        {
            string name = asset.GetProperty("name").GetString() ?? string.Empty;
            string url = asset.GetProperty("browser_download_url").GetString() ?? string.Empty;
            if (name == "CacheTrim.exe") exeUrl = url;
            if (name == "CacheTrim.exe.sha256") hashUrl = url;
        }

        bool available = latestVersion > currentVersion;
        if (available && (exeUrl.Length == 0 || hashUrl.Length == 0))
            throw new InvalidDataException("В релизе нет EXE или контрольной суммы.");

        return new UpdateInfo
        {
            Version = latest,
            ReleaseUrl = release.GetProperty("html_url").GetString() ?? string.Empty,
            DownloadUrl = exeUrl,
            ChecksumUrl = hashUrl,
            Available = available
        };
    }

    public static void DownloadAndLaunch(UpdateInfo update, string targetExe, int processId)
    {
        if (update is null || !update.Available)
            throw new InvalidOperationException("Нет доступного обновления.");
        if (string.IsNullOrWhiteSpace(targetExe) || !File.Exists(targetExe))
            throw new FileNotFoundException("Не удалось определить файл CacheTrim, который нужно обновить.", targetExe);
        RequireGitHubUrl(update.DownloadUrl);
        RequireGitHubUrl(update.ChecksumUrl);

        string updateRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CacheTrim", "UpdateCache");
        Directory.CreateDirectory(updateRoot);
        string stagedExe = Path.Combine(updateRoot, "CacheTrim.exe");
        string checksumFile = Path.Combine(updateRoot, "CacheTrim.exe.sha256");

        string actual;
        try
        {
            using HttpRequestMessage request = new(HttpMethod.Get, update.DownloadUrl);
            using HttpResponseMessage response = Client.Send(request, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            using Stream source = response.Content.ReadAsStream();
            using FileStream output = new(stagedExe, FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 256);
            using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            byte[] buffer = new byte[1024 * 256];
            int read;
            while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
            {
                output.Write(buffer, 0, read);
                hash.AppendData(buffer, 0, read);
            }
            output.Flush(true);
            if (output.Length < 4096)
                throw new InvalidDataException("Загруженный файл слишком мал для исполняемого файла Windows.");
            actual = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
        }
        catch
        {
            if (File.Exists(stagedExe)) File.Delete(stagedExe);
            throw;
        }

        using (FileStream check = File.OpenRead(stagedExe))
        {
            if (check.ReadByte() != 'M' || check.ReadByte() != 'Z')
            {
                File.Delete(stagedExe);
                throw new InvalidDataException("Загруженный файл не похож на исполняемый файл Windows.");
            }
        }
        string checksumText;
        try
        {
            checksumText = Client.GetStringAsync(update.ChecksumUrl).GetAwaiter().GetResult()
                .Trim().Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
        }
        catch
        {
            File.Delete(stagedExe);
            throw;
        }
        if (!string.Equals(checksumText, actual, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(stagedExe);
            throw new InvalidDataException("Контрольная сумма обновления не совпала.");
        }

        string binaryVersion = FileVersionInfo.GetVersionInfo(stagedExe).FileVersion;
        if (!ReleaseVersionGuard.Matches(update.Version, binaryVersion))
        {
            File.Delete(stagedExe);
            throw new InvalidDataException("Версия скачанного CacheTrim не совпадает с версией релиза " + update.Version + ".");
        }

        File.WriteAllText(checksumFile, actual, Encoding.ASCII);
        string scriptPath = Path.Combine(updateRoot, "ApplyUpdate.ps1");
        string logPath = Path.Combine(updateRoot, "ApplyUpdate.log");
        StringBuilder script = new();
        script.AppendLine("$ErrorActionPreference = 'Stop'");
        // Do not name this PowerShell variable $processId: variable names are case-insensitive,
        // so that would collide with PowerShell's read-only automatic variable $PID.
        script.AppendLine("$parentPid = " + processId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        script.AppendLine("$source = " + PowerShellLiteral(stagedExe));
        script.AppendLine("$target = " + PowerShellLiteral(targetExe));
        script.AppendLine("$log = " + PowerShellLiteral(logPath));
        script.AppendLine("$expectedHash = " + PowerShellLiteral(actual));
        script.AppendLine("$backupCreated = $false");
        script.AppendLine("try {");
        script.AppendLine("  Set-Content -LiteralPath $log -Value ('Starting update ' + (Get-Date -Format o)) -Encoding UTF8");
        script.AppendLine("  Wait-Process -Id $parentPid -ErrorAction SilentlyContinue");
        script.AppendLine("$backup = $target + '.previous'");
        script.AppendLine("  if (Test-Path -LiteralPath $target) { Copy-Item -LiteralPath $target -Destination $backup -Force; $backupCreated = $true }");
        script.AppendLine("$installed = $false");
        script.AppendLine("  for ($i = 0; $i -lt 20; $i++) { try { [System.IO.File]::Copy($source, $target, $true); $installed = $true; break } catch { Start-Sleep -Seconds 1 } }");
        script.AppendLine("  if (-not $installed) { throw 'Windows не разрешила заменить CacheTrim.exe.' }");
        script.AppendLine("  $actualHash = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant()");
        script.AppendLine("  if ($actualHash -ne $expectedHash) { throw 'Проверка установленного файла не прошла.' }");
        script.AppendLine("  Add-Content -LiteralPath $log -Value 'Update installed and verified.' -Encoding UTF8");
        script.AppendLine("  Start-Process -FilePath $target");
        script.AppendLine("} catch {");
        script.AppendLine("  Add-Content -LiteralPath $log -Value ('UPDATE FAILED: ' + $_.Exception.Message) -Encoding UTF8");
        script.AppendLine("  if ($backupCreated -and (Test-Path -LiteralPath $backup)) {");
        script.AppendLine("    try { Copy-Item -LiteralPath $backup -Destination $target -Force -ErrorAction Stop; Start-Process -FilePath $target; Add-Content -LiteralPath $log -Value 'Previous version restored and started.' -Encoding UTF8 }");
        script.AppendLine("    catch { Add-Content -LiteralPath $log -Value ('RESTORE FAILED: ' + $_.Exception.Message) -Encoding UTF8; try { (New-Object -ComObject WScript.Shell).Popup('Не удалось восстановить старую версию. Резервная копия находится здесь: ' + $backup + '. Подробности: ' + $log, 0, 'CacheTrim — ошибка восстановления', 16) | Out-Null } catch { } }");
        script.AppendLine("  } else {");
        script.AppendLine("    try { if (Test-Path -LiteralPath $target) { Start-Process -FilePath $target } } catch { }");
        script.AppendLine("    try { (New-Object -ComObject WScript.Shell).Popup('Не удалось установить обновление CacheTrim. Текущий файл не был заменён. Подробности: ' + $log, 0, 'CacheTrim — обновление', 48) | Out-Null } catch { }");
        script.AppendLine("  }");
        script.AppendLine("}");
        File.WriteAllText(scriptPath, script.ToString(), Encoding.Unicode);

        string powershell = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell\\v1.0\\powershell.exe");
        Process helper = Process.Start(new ProcessStartInfo(powershell,
            "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"" + scriptPath + "\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        }) ?? throw new InvalidOperationException("Не удалось запустить установщик обновления.");
    }

    private static void RequireGitHubUrl(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) || uri.Scheme != Uri.UriSchemeHttps ||
            (uri.Host != "github.com" && uri.Host != "objects.githubusercontent.com" &&
             uri.Host != "release-assets.githubusercontent.com"))
            throw new InvalidDataException("GitHub вернул неожиданный адрес файла.");
    }

    private static string PowerShellLiteral(string value) => "'" + value.Replace("'", "''") + "'";
}
