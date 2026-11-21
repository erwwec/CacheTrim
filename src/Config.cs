using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace CacheTrim
{
    /// <summary>Plain INI-style settings, no external dependencies.</summary>
    internal sealed class Config
    {
        // memory
        public bool WorkingSets = true;
        public bool Standby = true;
        public bool LowStandby = false;
        public bool Modified = true;
        public bool FileCache = true;

        // disk
        public bool Temp = true;
        public bool Browsers = true;
        public bool Wer = true;
        public bool UpdateCache = false;
        public bool D3dCache = true;
        public bool Thumbs = true;
        public bool Dumps = true;
        public bool WinLogs = false;
        public bool UwpCache = false;
        public bool Recycle = false;
        public bool Prefetch = false;

        // network / system
        public bool Dns = true;
        public bool Arp = false;
        public bool Netbios = false;

        /// <summary>Files younger than this are left alone. 0 = clean everything not locked.</summary>
        public int TempAgeHours = 0;

        /// <summary>Delay before the logon task runs, seconds.</summary>
        public int DelaySeconds = 20;

        public Config Clone()
        {
            return (Config)MemberwiseClone();
        }

        public static string ConfigDir
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CacheTrim"); }
        }

        public static string LogDir
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CacheTrim"); }
        }

        public static string ConfigPath
        {
            get { return Path.Combine(ConfigDir, "config.ini"); }
        }

        public static string HistoryPath
        {
            get { return Path.Combine(LogDir, "history.log"); }
        }

        public static Config Load()
        {
            Config c = new Config();
            try
            {
                if (!File.Exists(ConfigPath))
                    return c;

                Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (string raw in File.ReadAllLines(ConfigPath))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";"))
                        continue;

                    int eq = line.IndexOf('=');
                    if (eq <= 0)
                        continue;

                    map[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
                }

                c.WorkingSets = Bool(map, "working_sets", c.WorkingSets);
                c.Standby = Bool(map, "standby", c.Standby);
                c.LowStandby = Bool(map, "low_standby", c.LowStandby);
                c.Modified = Bool(map, "modified", c.Modified);
                c.FileCache = Bool(map, "file_cache", c.FileCache);

                c.Temp = Bool(map, "temp", c.Temp);
                c.Browsers = Bool(map, "browsers", c.Browsers);
                c.Wer = Bool(map, "wer", c.Wer);
                c.UpdateCache = Bool(map, "update_cache", c.UpdateCache);
                c.D3dCache = Bool(map, "d3d_cache", c.D3dCache);
                c.Thumbs = Bool(map, "thumbnails", c.Thumbs);
                c.Dumps = Bool(map, "dumps", c.Dumps);
                c.WinLogs = Bool(map, "windows_logs", c.WinLogs);
                c.UwpCache = Bool(map, "uwp_cache", c.UwpCache);
                c.Recycle = Bool(map, "recycle", c.Recycle);
                c.Prefetch = Bool(map, "prefetch", c.Prefetch);

                c.Dns = Bool(map, "dns", c.Dns);
                c.Arp = Bool(map, "arp", c.Arp);
                c.Netbios = Bool(map, "netbios", c.Netbios);

                c.TempAgeHours = Number(map, "temp_age_hours", c.TempAgeHours);
                c.DelaySeconds = Number(map, "delay_seconds", c.DelaySeconds);
            }
            catch (Exception)
            {
                // A broken config never stops a clean; defaults are the safe path.
            }
            return c;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(ConfigDir);
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("# CacheTrim settings");
                sb.AppendLine("working_sets=" + B(WorkingSets));
                sb.AppendLine("standby=" + B(Standby));
                sb.AppendLine("low_standby=" + B(LowStandby));
                sb.AppendLine("modified=" + B(Modified));
                sb.AppendLine("file_cache=" + B(FileCache));
                sb.AppendLine("temp=" + B(Temp));
                sb.AppendLine("browsers=" + B(Browsers));
                sb.AppendLine("wer=" + B(Wer));
                sb.AppendLine("update_cache=" + B(UpdateCache));
                sb.AppendLine("d3d_cache=" + B(D3dCache));
                sb.AppendLine("thumbnails=" + B(Thumbs));
                sb.AppendLine("dumps=" + B(Dumps));
                sb.AppendLine("windows_logs=" + B(WinLogs));
                sb.AppendLine("uwp_cache=" + B(UwpCache));
                sb.AppendLine("recycle=" + B(Recycle));
                sb.AppendLine("prefetch=" + B(Prefetch));
                sb.AppendLine("dns=" + B(Dns));
                sb.AppendLine("arp=" + B(Arp));
                sb.AppendLine("netbios=" + B(Netbios));
                sb.AppendLine("temp_age_hours=" + TempAgeHours.ToString(CultureInfo.InvariantCulture));
                sb.AppendLine("delay_seconds=" + DelaySeconds.ToString(CultureInfo.InvariantCulture));
                File.WriteAllText(ConfigPath, sb.ToString(), new UTF8Encoding(false));
            }
            catch (Exception)
            {
                // Read-only profile: settings stay in memory for this run.
            }
        }

        private static string B(bool value)
        {
            return value ? "1" : "0";
        }

        private static bool Bool(Dictionary<string, string> map, string key, bool fallback)
        {
            string v;
            if (!map.TryGetValue(key, out v))
                return fallback;
            return v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                   v.Equals("yes", StringComparison.OrdinalIgnoreCase);
        }

        private static int Number(Dictionary<string, string> map, string key, int fallback)
        {
            string v;
            int parsed;
            if (map.TryGetValue(key, out v) && int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
                return parsed < 0 ? 0 : parsed;
            return fallback;
        }
    }
}
