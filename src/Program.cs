using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace CacheTrim
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            bool silent = false;
            int startPage = 0;
            foreach (string a in args)
            {
                if (a.Equals("--silent", StringComparison.OrdinalIgnoreCase))
                    silent = true;

                // Hidden helper for screenshots/automation: --page=N
                if (a.StartsWith("--page=", StringComparison.OrdinalIgnoreCase))
                {
                    int parsed;
                    if (int.TryParse(a.Substring(7), out parsed))
                        startPage = parsed;
                }
            }

            Config config = Config.Load();

            if (silent)
            {
                Environment.Exit(RunSilent(config));
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm(config, startPage));
        }

        /// <summary>Headless pass used by the logon task. Exit 0 when the clean ran, 1 without rights.</summary>
        private static int RunSilent(Config config)
        {
            List<string> lines = new List<string>();
            Action<string> log = delegate(string line)
            {
                lines.Add("[" + DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + "] " + line);
            };

            int code = 0;
            if (!Native.IsElevated)
            {
                log("запуск без прав администратора - чистка памяти и системных папок пропущена");
                code = 1;
            }
            else
            {
                Cleaner cleaner = new Cleaner(log);
                cleaner.Run(config);
                log("освобождено файлов: " + Cleaner.FormatBytes(cleaner.BytesFreed));

                Native.MemStats stats = Native.GetMemoryStats();
                log(string.Format("после: занято {0} / {1}, доступно {2}",
                    Cleaner.FormatBytes((long)(stats.TotalPhys - stats.AvailPhys)),
                    Cleaner.FormatBytes((long)stats.TotalPhys),
                    Cleaner.FormatBytes((long)stats.AvailPhys)));
            }

            WriteHistory(lines);
            return code;
        }

        private static void WriteHistory(List<string> lines)
        {
            try
            {
                Directory.CreateDirectory(Config.LogDir);
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("==== " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + " ====");
                foreach (string line in lines)
                    sb.AppendLine(line);
                sb.AppendLine();
                File.AppendAllText(Config.HistoryPath, sb.ToString(), new UTF8Encoding(false));

                // Bounded log: a long-lived install must never grow without limit.
                FileInfo info = new FileInfo(Config.HistoryPath);
                if (info.Exists && info.Length > 512 * 1024)
                {
                    string[] kept = File.ReadAllLines(Config.HistoryPath);
                    int start = Math.Max(0, kept.Length - 2000);
                    List<string> tail = new List<string>();
                    for (int i = start; i < kept.Length; i++)
                        tail.Add(kept[i]);
                    File.WriteAllLines(Config.HistoryPath, tail.ToArray(), new UTF8Encoding(false));
                }
            }
            catch (Exception)
            {
            }
        }
    }
}
