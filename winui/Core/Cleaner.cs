using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.ServiceProcess;

namespace CacheTrim
{
    /// <summary>
    /// Executes one clean pass. Nothing here throws: every failure becomes a log line,
    /// so a locked file or a denied folder never aborts the run.
    /// </summary>
    internal sealed class Cleaner
    {
        private readonly Action<string> _log;
        private long _bytes;
        private int _files;
        private int _skipped;

        public Cleaner(Action<string> log)
        {
            _log = log;
        }

        public long BytesFreed { get { return _bytes; } }
        public int FilesDeleted { get { return _files; } }
        public int FilesSkipped { get { return _skipped; } }

        public void Run(Config c)
        {
            DateTime? tempAge = c.TempAgeHours > 0
                ? (DateTime?)DateTime.UtcNow.AddHours(-c.TempAgeHours)
                : null;

            if (c.Temp)
                CleanTemp(tempAge);

            if (c.Wer)
                CleanWer();

            if (c.Browsers)
                CleanBrowsers();

            if (c.D3dCache)
                CleanShaderCache();

            if (c.Thumbs)
                CleanThumbnails();

            if (c.Dumps)
                CleanDumps();

            if (c.WinLogs)
                CleanWindowsLogs();

            if (c.UwpCache)
                CleanUwpCaches();

            if (c.Prefetch)
            {
                _log("-- prefetch (файлы старше 7 дней)");
                string windir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                Sweep(Path.Combine(windir, "Prefetch"), DateTime.UtcNow.AddDays(-7), "*");
            }

            if (c.UpdateCache)
                CleanWindowsUpdate();

            if (c.Dns || c.Arp || c.Netbios)
                CleanNetwork(c);

            if (c.Recycle)
            {
                _log(Native.EmptyRecycleBin() ? "-- корзина очищена" : "-- корзина: уже пуста или недоступна");
            }

            PurgeMemory(c);

            _log(string.Format("-- итого: {0}, файлов {1}, пропущено занятых {2}",
                FormatBytes(_bytes), _files, _skipped));
        }

        // ---------------- disk sections ----------------

        private void CleanTemp(DateTime? age)
        {
            _log("-- временные файлы");
            foreach (string path in TempTargets())
            {
                if (!Directory.Exists(path))
                    continue;

                long before = _bytes;
                int filesBefore = _files;
                Sweep(path, age, "*");
                _log(string.Format("  {0}  {1}  ({2} файлов)", path, FormatBytes(_bytes - before), _files - filesBefore));
            }

            // Other local profiles: only when elevated, and failures are silent.
            foreach (string path in OtherProfileTemps())
            {
                long before = _bytes;
                Sweep(path, age, "*");
                if (_bytes > before)
                    _log(string.Format("  {0}  {1}", path, FormatBytes(_bytes - before)));
            }
        }

        private void CleanWer()
        {
            _log("-- отчёты Windows Error Reporting");
            string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            long before = _bytes;
            Sweep(Path.Combine(programData, @"Microsoft\Windows\WER\ReportQueue"), null, "*");
            Sweep(Path.Combine(programData, @"Microsoft\Windows\WER\ReportArchive"), null, "*");
            Sweep(Path.Combine(programData, @"Microsoft\Windows\WER\Temp"), null, "*");
            Sweep(Path.Combine(programData, @"Microsoft\Windows\WER\ERQueue"), null, "*");
            _log("  " + FormatBytes(_bytes - before));
        }

        private void CleanBrowsers()
        {
            _log("-- кэши браузеров");
            long before = _bytes;
            foreach (string path in BrowserCacheDirs())
                Sweep(path, null, "*");
            _log("  " + FormatBytes(_bytes - before) + " (занятые файлы пропущены)");
        }

        private void CleanShaderCache()
        {
            _log("-- кэш шейдеров (D3D/NVIDIA/AMD)");
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            long before = _bytes;
            Sweep(Path.Combine(local, "D3DSCache"), null, "*");
            Sweep(Path.Combine(local, @"NVIDIA\DXCache"), null, "*");
            Sweep(Path.Combine(local, @"NVIDIA\GLCache"), null, "*");
            Sweep(Path.Combine(local, @"AMD\DxcCache"), null, "*");
            Sweep(Path.Combine(local, @"AMD\DxCache"), null, "*");
            Sweep(Path.Combine(local, @"Intel\ShaderCache"), null, "*");
            _log("  " + FormatBytes(_bytes - before));
        }

        private void CleanThumbnails()
        {
            _log("-- эскизы проводника");
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string explorer = Path.Combine(local, @"Microsoft\Windows\Explorer");
            long before = _bytes;
            int skippedBefore = _skipped;
            Sweep(explorer, null, "thumbcache_*.db");
            Sweep(explorer, null, "iconcache_*.db");
            _log("  " + FormatBytes(_bytes - before) +
                 (_skipped > skippedBefore ? " (часть занята проводником)" : string.Empty));
        }

        private void CleanDumps()
        {
            _log("-- дампы памяти");
            string windir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            long before = _bytes;
            DeleteFile(Path.Combine(windir, "MEMORY.DMP"));
            Sweep(Path.Combine(windir, "Minidump"), null, "*");
            Sweep(Path.Combine(windir, "LiveKernelReports"), null, "*");
            _log("  " + FormatBytes(_bytes - before));
        }

        private void CleanWindowsLogs()
        {
            _log("-- старые логи Windows (старше 7 дней)");
            string windir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string logs = Path.Combine(windir, "Logs");
            DateTime cutoff = DateTime.UtcNow.AddDays(-7);
            long before = _bytes;
            Sweep(Path.Combine(logs, "CBS"), cutoff, "*");
            Sweep(Path.Combine(logs, "DISM"), cutoff, "*");
            Sweep(Path.Combine(logs, "MoSetup"), cutoff, "*");
            Sweep(Path.Combine(logs, "WindowsUpdate"), cutoff, "*");
            Sweep(Path.Combine(logs, "SIH"), cutoff, "*");
            _log("  " + FormatBytes(_bytes - before));
        }

        private void CleanUwpCaches()
        {
            _log("-- кэши UWP-приложений");
            string packages = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages");
            if (!Directory.Exists(packages))
                return;

            long before = _bytes;
            string[] dirs;
            try
            {
                dirs = Directory.GetDirectories(packages);
            }
            catch (Exception)
            {
                return;
            }

            // Bounded: a machine with hundreds of packages must not stall the pass.
            int budget = Math.Min(dirs.Length, 300);
            for (int i = 0; i < budget; i++)
            {
                Sweep(Path.Combine(dirs[i], "LocalCache"), null, "*");
                Sweep(Path.Combine(dirs[i], "TempState"), null, "*");
            }

            _log("  " + FormatBytes(_bytes - before) + " (" + budget + " пакетов)");
        }

        private void CleanWindowsUpdate()
        {
            _log("-- кэш Windows Update / Delivery Optimization");
            long before = _bytes;
            string windir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

            // Only the download queues are touched, never the component store.
            bool restartWua;
            bool restartBits;
            bool wuaStopped = StopService("wuauserv", out restartWua);
            bool bitsStopped = StopService("bits", out restartBits);
            try
            {
                if (!wuaStopped || !bitsStopped)
                    _log("  службы обновления заняты, удалятся только незанятые файлы");

                Sweep(Path.Combine(windir, @"SoftwareDistribution\Download"), null, "*");
                Sweep(Path.Combine(programData, @"Microsoft\Network\Downloader"), null, "*");
                Sweep(Path.Combine(windir, @"SoftwareDistribution\DeliveryOptimization"), null, "*");
            }
            finally
            {
                if (restartWua) StartService("wuauserv");
                if (restartBits) StartService("bits");
            }

            _log("  " + FormatBytes(_bytes - before));
        }

        private void CleanNetwork(Config c)
        {
            _log("-- сетевые кэши");

            if (c.Dns)
                _log(Native.FlushDnsCache() ? "  dns: сброшен" : "  dns: сброс не удался");

            if (c.Arp)
            {
                string output;
                int code = RunTool(Path.Combine(Environment.SystemDirectory, "netsh.exe"), "interface ip delete arpcache", out output);
                _log(code == 0 ? "  arp: сброшен" : "  arp: код " + code);
            }

            if (c.Netbios)
            {
                string output;
                int code = RunTool(Path.Combine(Environment.SystemDirectory, "nbtstat.exe"), "-R", out output);
                _log(code == 0 ? "  netbios: сброшен" : "  netbios: код " + code);
            }
        }

        // ---------------- memory ----------------

        private void PurgeMemory(Config c)
        {
            _log("-- оперативная память");

            if (c.WorkingSets)
                Report("trim working set", Native.Purge(Native.PurgeTarget.WorkingSets));

            if (c.Modified)
                Report("flush modified page list", Native.Purge(Native.PurgeTarget.ModifiedList));

            if (c.Standby)
                Report("empty standby list", Native.Purge(Native.PurgeTarget.StandbyList));

            if (c.LowStandby)
                Report("empty low-priority standby", Native.Purge(Native.PurgeTarget.LowPriorityStandby));

            if (c.FileCache)
                Report("trim system file cache", Native.FlushSystemFileCache());
        }

        private void Report(string name, string error)
        {
            if (error == null)
                _log("  ok   " + name);
            else
                _log("  FAIL " + name + " -> " + error);
        }

        // ---------------- file sweeping ----------------

        private void Sweep(string root, DateTime? olderThanUtc, string pattern)
        {
            if (!Directory.Exists(root))
                return;

            try
            {
                if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0)
                {
                    _log("  пропущена ссылка на каталог: " + root);
                    return;
                }
            }
            catch (Exception)
            {
                return;
            }

            Stack<string> stack = new Stack<string>();
            List<string> dirs = new List<string>();
            stack.Push(root);

            while (stack.Count > 0)
            {
                string dir = stack.Pop();
                dirs.Add(dir);

                string[] files;
                try
                {
                    files = Directory.GetFiles(dir, pattern);
                }
                catch (Exception)
                {
                    continue;
                }

                foreach (string file in files)
                {
                    try
                    {
                        FileInfo info = new FileInfo(file);
                        if (!info.Exists)
                            continue;

                        if (olderThanUtc.HasValue && info.LastWriteTimeUtc >= olderThanUtc.Value)
                            continue;

                        long length = info.Length;
                        if ((info.Attributes & FileAttributes.ReadOnly) != 0)
                            info.Attributes = FileAttributes.Normal;
                        info.Delete();
                        _bytes += length;
                        _files++;
                    }
                    catch (Exception)
                    {
                        _skipped++;
                    }
                }

                string[] subdirs;
                try
                {
                    subdirs = Directory.GetDirectories(dir);
                }
                catch (Exception)
                {
                    continue;
                }

                foreach (string sub in subdirs)
                {
                    try
                    {
                        if ((File.GetAttributes(sub) & FileAttributes.ReparsePoint) != 0)
                        {
                            _skipped++;
                            continue;
                        }
                        stack.Push(sub);
                    }
                    catch (Exception)
                    {
                        _skipped++;
                    }
                }
            }

            // Deepest first so parents are empty by the time we try them.
            for (int i = dirs.Count - 1; i >= 0; i--)
            {
                try
                {
                    if (string.Equals(dirs[i], root, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (Directory.GetFileSystemEntries(dirs[i]).Length == 0)
                        Directory.Delete(dirs[i], false);
                }
                catch (Exception)
                {
                }
            }
        }

        private void DeleteFile(string path)
        {
            try
            {
                FileInfo info = new FileInfo(path);
                if (!info.Exists)
                    return;

                long length = info.Length;
                info.Attributes = FileAttributes.Normal;
                info.Delete();
                _bytes += length;
                _files++;
            }
            catch (Exception)
            {
                _skipped++;
            }
        }

        private static IEnumerable<string> TempTargets()
        {
            List<string> paths = new List<string>();
            string windir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            paths.Add(Path.Combine(windir, "Temp"));

            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            paths.Add(Path.Combine(local, "Temp"));
            paths.Add(Path.Combine(local, "CrashDumps"));
            paths.Add(Path.Combine(local, @"Microsoft\Windows\INetCache"));

            return paths;
        }

        private static IEnumerable<string> OtherProfileTemps()
        {
            List<string> paths = new List<string>();
            string root = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
            if (string.IsNullOrEmpty(root))
                return paths;

            string current = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string[] dirs;
            try
            {
                dirs = Directory.GetDirectories(Path.Combine(root, "Users"));
            }
            catch (Exception)
            {
                return paths;
            }

            foreach (string dir in dirs)
            {
                if (string.Equals(dir, current, StringComparison.OrdinalIgnoreCase))
                    continue;

                string temp = Path.Combine(dir, @"AppData\Local\Temp");
                if (Directory.Exists(temp))
                    paths.Add(temp);
            }

            return paths;
        }

        private static IEnumerable<string> BrowserCacheDirs()
        {
            List<string> result = new List<string>();
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            string[] roots =
            {
                Path.Combine(local, @"Google\Chrome\User Data"),
                Path.Combine(local, @"Microsoft\Edge\User Data"),
                Path.Combine(local, @"BraveSoftware\Brave-Browser\User Data"),
                Path.Combine(local, @"Vivaldi\User Data"),
                Path.Combine(local, @"Chromium\User Data"),
                Path.Combine(local, @"Yandex\YandexBrowser\User Data"),
                Path.Combine(roaming, @"Opera Software\Opera Stable")
            };

            string[] profiles = { "Default", "Profile 1", "Profile 2", "Profile 3", "Profile 4", "Profile 5", "Guest Profile" };
            string[] subdirs = { "Cache", @"Code Cache", "GPUCache", @"Service Worker\CacheStorage", "DawnCache", "GrShaderCache" };

            foreach (string root in roots)
            {
                if (!Directory.Exists(root))
                    continue;

                foreach (string profile in profiles)
                {
                    foreach (string sub in subdirs)
                        result.Add(Path.Combine(Path.Combine(root, profile), sub));
                }

                foreach (string sub in subdirs)
                    result.Add(Path.Combine(root, sub));
            }

            return result;
        }

        // ---------------- external tools ----------------

        private static int RunTool(string exe, string args, out string output)
        {
            try
            {
                ProcessResult result = ProcessRunner.Run(exe, args, 15000);
                output = result.Output;
                return result.ExitCode;
            }
            catch (Exception ex)
            {
                output = ex.Message;
                return -1;
            }
        }

        private static bool StopService(string name, out bool restartAfterward)
        {
            restartAfterward = false;
            try
            {
                using (ServiceController sc = new ServiceController(name))
                {
                    ServiceControllerStatus status = sc.Status;
                    if (status == ServiceControllerStatus.Stopped)
                        return true;
                    if (status != ServiceControllerStatus.Running)
                        return false;

                    restartAfterward = true;
                    sc.Stop();
                    sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(12));
                    return sc.Status == ServiceControllerStatus.Stopped;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void StartService(string name)
        {
            try
            {
                using (ServiceController sc = new ServiceController(name))
                {
                    if (sc.Status == ServiceControllerStatus.Running)
                        return;

                    sc.Start();
                    sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(12));
                }
            }
            catch (Exception)
            {
            }
        }

        // ---------------- formatting ----------------

        public static string FormatBytes(long bytes)
        {
            if (bytes < 1024)
                return bytes.ToString(CultureInfo.InvariantCulture) + " B";

            double value = bytes;
            string[] units = { "KB", "MB", "GB", "TB" };
            int unit = -1;
            while (value >= 1024 && unit < units.Length - 1)
            {
                value /= 1024;
                unit++;
            }

            return value.ToString("0.0", CultureInfo.InvariantCulture) + " " + units[unit];
        }
    }
}
