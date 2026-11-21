using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace CacheTrim
{
    internal sealed class UpdateInfo
    {
        public string Version;
        public string ReleaseUrl;
        public string DownloadUrl;
        public string ChecksumUrl;
        public bool Available;
    }

    internal static class UpdateService
    {
        public static UpdateInfo CheckLatest()
        {
            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(AppInfo.GitHubReleaseApi);
            request.Method = "GET";
            request.Accept = "application/vnd.github+json";
            request.UserAgent = "CacheTrim/" + AppInfo.Version;
            request.Timeout = 15000;

            string json;
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            using (StreamReader reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                json = reader.ReadToEnd();

            JavaScriptSerializer serializer = new JavaScriptSerializer();
            IDictionary release = serializer.DeserializeObject(json) as IDictionary;
            if (release == null)
                throw new InvalidDataException("GitHub вернул некорректные данные релиза.");

            string tag = Convert.ToString(release["tag_name"]);
            string latest = tag.TrimStart('v', 'V');
            Version latestVersion;
            Version currentVersion;
            if (!Version.TryParse(latest, out latestVersion) || !Version.TryParse(AppInfo.Version, out currentVersion))
                throw new InvalidDataException("Не удалось прочитать номер версии GitHub Release.");

            string exeUrl = null;
            string hashUrl = null;
            IList assets = release["assets"] as IList;
            if (assets != null)
            {
                foreach (object value in assets)
                {
                    IDictionary asset = value as IDictionary;
                    if (asset == null) continue;
                    string name = Convert.ToString(asset["name"]);
                    string url = Convert.ToString(asset["browser_download_url"]);
                    if (name == "CacheTrim.exe") exeUrl = url;
                    if (name == "CacheTrim.exe.sha256") hashUrl = url;
                }
            }

            if (latestVersion.CompareTo(currentVersion) > 0 && (exeUrl == null || hashUrl == null))
                throw new InvalidDataException("В релизе нет EXE или контрольной суммы.");

            return new UpdateInfo
            {
                Version = latest,
                ReleaseUrl = Convert.ToString(release["html_url"]),
                DownloadUrl = exeUrl,
                ChecksumUrl = hashUrl,
                Available = latestVersion.CompareTo(currentVersion) > 0
            };
        }

        public static void DownloadAndLaunch(UpdateInfo update, string targetExe, int processId)
        {
            if (update == null || !update.Available)
                throw new InvalidOperationException("Нет доступного обновления.");
            RequireGitHubUrl(update.DownloadUrl);
            RequireGitHubUrl(update.ChecksumUrl);

            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
            string updateRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CacheTrim", "UpdateCache");
            Directory.CreateDirectory(updateRoot);
            string stagedExe = Path.Combine(updateRoot, "CacheTrim.exe");
            string checksumFile = Path.Combine(updateRoot, "CacheTrim.exe.sha256");

            using (WebClient client = new WebClient())
            {
                client.Headers[HttpRequestHeader.UserAgent] = "CacheTrim/" + AppInfo.Version;
                byte[] executable = client.DownloadData(update.DownloadUrl);
                if (executable.Length < 4096 || executable[0] != (byte)'M' || executable[1] != (byte)'Z')
                    throw new InvalidDataException("Загруженный файл не похож на исполняемый файл Windows.");
                File.WriteAllBytes(stagedExe, executable);
                string checksum = client.DownloadString(update.ChecksumUrl);
                string expected = checksum.Trim().Split(new char[] { ' ', '\t', '\r', '\n' },
                    StringSplitOptions.RemoveEmptyEntries)[0];
                string actual;
                using (SHA256 sha = SHA256.Create())
                    actual = ToHex(sha.ComputeHash(executable));
                if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(stagedExe);
                    throw new InvalidDataException("Контрольная сумма обновления не совпала.");
                }
                File.WriteAllText(checksumFile, actual, Encoding.ASCII);
            }

            string scriptPath = Path.Combine(updateRoot, "ApplyUpdate.ps1");
            StringBuilder script = new StringBuilder();
            script.AppendLine("$processId = " + processId.ToString(System.Globalization.CultureInfo.InvariantCulture));
            script.AppendLine("$source = " + PowerShellLiteral(stagedExe));
            script.AppendLine("$target = " + PowerShellLiteral(targetExe));
            script.AppendLine("Wait-Process -Id $processId -ErrorAction SilentlyContinue");
            script.AppendLine("Start-Sleep -Milliseconds 800");
            script.AppendLine("$backup = $target + '.previous'");
            script.AppendLine("if (Test-Path -LiteralPath $target) { Copy-Item -LiteralPath $target -Destination $backup -Force }");
            script.AppendLine("$installed = $false");
            script.AppendLine("for ($i = 0; $i -lt 12; $i++) {");
            script.AppendLine("  try { [System.IO.File]::Copy($source, $target, $true); $installed = $true; break } catch { Start-Sleep -Seconds 1 }");
            script.AppendLine("}");
            script.AppendLine("if ($installed) { Start-Process -FilePath $target } elseif (Test-Path -LiteralPath $backup) { Copy-Item -LiteralPath $backup -Destination $target -Force; Start-Process -FilePath $target }");
            File.WriteAllText(scriptPath, script.ToString(), Encoding.Unicode);

            string powershell = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell\\v1.0\\powershell.exe");
            ProcessStartInfo info = new ProcessStartInfo(powershell,
                "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"" + scriptPath + "\"");
            info.UseShellExecute = false;
            info.CreateNoWindow = true;
            info.WindowStyle = ProcessWindowStyle.Hidden;
            using (Process helper = Process.Start(info))
            {
                if (helper == null)
                    throw new InvalidOperationException("Не удалось запустить установщик обновления.");
            }
        }

        private static void RequireGitHubUrl(string value)
        {
            Uri uri;
            if (!Uri.TryCreate(value, UriKind.Absolute, out uri) || uri.Scheme != Uri.UriSchemeHttps ||
                (uri.Host != "github.com" && uri.Host != "objects.githubusercontent.com" &&
                 uri.Host != "release-assets.githubusercontent.com"))
                throw new InvalidDataException("GitHub вернул неожиданный адрес файла.");
        }

        private static string PowerShellLiteral(string value)
        {
            return "'" + value.Replace("'", "''") + "'";
        }

        private static string ToHex(byte[] bytes)
        {
            StringBuilder result = new StringBuilder(bytes.Length * 2);
            for (int i = 0; i < bytes.Length; i++)
                result.Append(bytes[i].ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
            return result.ToString();
        }
    }
}
