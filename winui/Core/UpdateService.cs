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

        File.WriteAllText(checksumFile, actual, Encoding.ASCII);
        string scriptPath = Path.Combine(updateRoot, "ApplyUpdate.ps1");
        StringBuilder script = new();
        script.AppendLine("$processId = " + processId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        script.AppendLine("$source = " + PowerShellLiteral(stagedExe));
        script.AppendLine("$target = " + PowerShellLiteral(targetExe));
        script.AppendLine("Wait-Process -Id $processId -ErrorAction SilentlyContinue");
        script.AppendLine("Start-Sleep -Milliseconds 800");
        script.AppendLine("$backup = $target + '.previous'");
        script.AppendLine("if (Test-Path -LiteralPath $target) { Copy-Item -LiteralPath $target -Destination $backup -Force }");
        script.AppendLine("$installed = $false");
        script.AppendLine("for ($i = 0; $i -lt 12; $i++) { try { [System.IO.File]::Copy($source, $target, $true); $installed = $true; break } catch { Start-Sleep -Seconds 1 } }");
        script.AppendLine("if ($installed) { Start-Process -FilePath $target } elseif (Test-Path -LiteralPath $backup) { Copy-Item -LiteralPath $backup -Destination $target -Force; Start-Process -FilePath $target }");
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
