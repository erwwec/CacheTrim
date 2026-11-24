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
        private readonly CleanupOptions _options;
        private bool _preview;
        private string _category = "temp";
        private DateTime? _categoryCutoff;
        public readonly List<string> PreviewPaths = new List<string>();
        private const int PreviewPathLimit = 3000;

        public Cleaner(Action<string> log, CleanupOptions options = null)
        {
            _log = log;
            _options = options ?? CleanupOptions.Load();
            _options.Validate();
        }

        public void Preview(Config c) { _preview = true; try { Run(c); } finally { _preview = false; } }
        private void Category(string category)
        {
            _category = category;
            int hours = _options.AgeFor(category);
            _categoryCutoff = hours > 0 ? DateTime.UtcNow.AddHours(-hours) : (DateTime?)null;
        }
        private DateTime? EffectiveCutoff(DateTime? cutoff)
        {
            if (!_categoryCutoff.HasValue) return cutoff;
            return !cutoff.HasValue || _categoryCutoff.Value < cutoff.Value ? _categoryCutoff : cutoff;
        }
        private void HandleFile(FileInfo info)
        {
            if (_options.IsExcluded(info.FullName)) { _skipped++; return; }
            if (_categoryCutoff.HasValue && info.LastWriteTimeUtc >= _categoryCutoff.Value) return;
            long length = info.Length;
            if (_preview)
            {
                if (PreviewPaths.Count < PreviewPathLimit) PreviewPaths.Add(_category + " | " + info.FullName);
            }
            else DeleteFilePreservingAttributes(info);
            _bytes += length;
            _files++;
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
            { Category("temp"); CleanTemp(tempAge); }

            if (c.Wer)
            { Category("reports"); CleanWer(); }

            if (c.Browsers)
            { Category("browsers"); CleanBrowsers(); }

            if (c.D3dCache)
            { Category("shaders"); CleanShaderCache(); }

            if (c.Thumbs)
            { Category("thumbnails"); CleanThumbnails(); }

            if (c.Dumps)
            { Category("dumps"); CleanDumps(); }

            if (c.WinLogs)
            { Category("logs"); CleanWindowsLogs(); }

            if (c.UwpCache)
            { Category("uwp"); CleanUwpCaches(); }

            if (c.Prefetch)
            {
                Category("prefetch");
                _log("-- prefetch (файлы старше 7 дней)");
                string windir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                Sweep(Path.Combine(windir, "Prefetch"), DateTime.UtcNow.AddDays(-7), "*");
            }

            if (c.UpdateCache)
            { Category("updates"); CleanWindowsUpdate(); }

            if (!_preview && (c.Dns || c.Arp || c.Netbios))
                CleanNetwork(c);

            if (c.Recycle && !_preview)
            {
                // Windows empties the whole recycle bin, bypassing path exclusions.
                if (_options.ExcludedPaths.Count > 0) _log("-- корзина пропущена: включены исключения");
                else _log(Native.EmptyRecycleBin() ? "-- корзина очищена" : "-- корзина: уже пуста или недоступна");
            }

            if (!_preview) PurgeMemory(c);

            _log(string.Format(_preview ? "-- предпросмотр: {0}, файлов {1}, пропущено {2}" : "-- итого: {0}, файлов {1}, пропущено {2}",
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
            Sweep(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CrashDumps"), null, "*");
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
            // Only downloaded update files are touched, never the BITS job database or component store.
            bool restartWua;
            bool restartBits;
            restartWua = false; restartBits = false;
            bool wuaStopped = _preview || StopService("wuauserv", out restartWua);
            bool bitsStopped = _preview || StopService("bits", out restartBits);
            try
            {
                if (!wuaStopped || !bitsStopped)
                    _log("  службы обновления заняты, удалятся только незанятые файлы");

                Sweep(Path.Combine(windir, @"SoftwareDistribution\Download"), null, "*");
                Sweep(Path.Combine(windir, @"SoftwareDistribution\DeliveryOptimization"), null, "*");
            }
            finally
            {
                if (restartWua)
                    LogServiceRestartFailure("wuauserv", StartService("wuauserv"), _log);
                if (restartBits)
                    LogServiceRestartFailure("bits", StartService("bits"), _log);
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
            if (!Directory.Exists(root) || _options.IsExcluded(root))
                return;
            olderThanUtc = EffectiveCutoff(olderThanUtc);

            try
            {
                // Check every ancestor, not only the leaf: a junction higher in the
                // path can otherwise redirect cleanup outside the intended location.
                for (DirectoryInfo directory = new DirectoryInfo(Path.GetFullPath(root)); directory != null; directory = directory.Parent)
                {
                    if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        _log("  пропущен путь через ссылку на каталог: " + directory.FullName);
                        _skipped++;
                        return;
                    }
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

                        HandleFile(info);
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
                        if (_options.IsExcluded(sub) || (File.GetAttributes(sub) & FileAttributes.ReparsePoint) != 0)
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

            // Preview is read-only, including empty directories.
            if (_preview) return;
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
                if (!info.Exists) return;
                // A direct file target must not bypass the same junction protection as Sweep.
                for (DirectoryInfo directory = info.Directory; directory != null; directory = directory.Parent)
                {
                    if ((directory.Attributes & FileAttributes.ReparsePoint) != 0) { _skipped++; return; }
                }
                HandleFile(info);
            }
            catch (Exception)
            {
                _skipped++;
            }
        }

        private static void DeleteFilePreservingAttributes(FileInfo info)
        {
            FileAttributes originalAttributes = info.Attributes;
            try
            {
                if ((originalAttributes & FileAttributes.ReadOnly) != 0)
                    info.Attributes = originalAttributes & ~FileAttributes.ReadOnly;
                info.Delete();
            }
            catch
            {
                try
                {
                    if (File.Exists(info.FullName))
                        File.SetAttributes(info.FullName, originalAttributes);
                }
                catch (Exception) { }
                throw;
            }
        }

        private static IEnumerable<string> TempTargets()
        {
            List<string> paths = new List<string>();
            string windir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            paths.Add(Path.Combine(windir, "Temp"));

            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            paths.Add(Path.Combine(local, "Temp"));

            return paths;
        }

        private IEnumerable<string> BrowserCacheDirs()
        {
            List<string> result = new List<string>();
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            var roots = new Dictionary<string, string>
            {
                ["Chrome"] = Path.Combine(local, @"Google\Chrome\User Data"),
                ["Edge"] = Path.Combine(local, @"Microsoft\Edge\User Data"),
                ["Brave"] = Path.Combine(local, @"BraveSoftware\Brave-Browser\User Data"),
                ["Vivaldi"] = Path.Combine(local, @"Vivaldi\User Data"),
                ["Chromium"] = Path.Combine(local, @"Chromium\User Data"),
                ["Yandex"] = Path.Combine(local, @"Yandex\YandexBrowser\User Data"),
                ["Opera"] = Path.Combine(roaming, @"Opera Software\Opera Stable")
            };

            string[] profiles = { "Default", "Profile 1", "Profile 2", "Profile 3", "Profile 4", "Profile 5", "Guest Profile" };
            string[] subdirs = { "Cache", @"Code Cache", "GPUCache", @"Service Worker\CacheStorage", "DawnCache", "GrShaderCache" };

            foreach (var browser in roots)
            {
                if (!_options.IncludesBrowser(browser.Key)) continue;
                string root = browser.Value;
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

        private static string StartService(string name)
        {
            try
            {
                using (ServiceController sc = new ServiceController(name))
                {
                    if (sc.Status == ServiceControllerStatus.Running)
                        return null;
                    if (sc.Status != ServiceControllerStatus.Stopped)
                        return "служба занята другой операцией";

                    sc.Start();
                    sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(12));
                    return sc.Status == ServiceControllerStatus.Running ? null : "служба не запустилась";
                }
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        internal static void LogServiceRestartFailure(string name, string error, Action<string> log)
        {
            if (error != null)
                log("  не удалось перезапустить службу " + name + ": " + error);
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
