using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace CacheTrim
{
    internal sealed class MainForm : Form
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        private readonly Config _config;
        private readonly NavSidebar _nav;
        private readonly Panel _host;
        private readonly Label _statusAdmin;
        private readonly Label _statusTask;
        private readonly Label _statusRight;
        private readonly System.Windows.Forms.Timer _timer;
        private readonly List<Page> _pages = new List<Page>();

        private MemoryPage _memory;
        private CleanPage _clean;
        private OptimizePage _optimize;
        private StartupPage _startup;
        private LogPage _log;
        private SettingsPage _settings;

        private volatile bool _busy;
        private volatile bool _maintenanceBusy;

        public MainForm(Config config, int startPage)
        {
            _config = config;

            Text = "CacheTrim — оптимизатор кэша";
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = Theme.Body;
            ClientSize = new Size(1120, 760);
            MinimumSize = new Size(1010, 700);
            StartPosition = FormStartPosition.CenterScreen;
            Icon = SystemIcons.Shield;
            DoubleBuffered = true;
            Shown += delegate
            {
                try
                {
                    int dark = 1;
                    DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int));
                    DwmSetWindowAttribute(Handle, 19, ref dark, sizeof(int));
                }
                catch { }
            };

            _nav = new NavSidebar();
            _nav.Dock = DockStyle.Left;

            _host = new Panel();
            _host.Dock = DockStyle.Fill;
            _host.BackColor = Theme.Bg;

            Panel status = new Panel();
            status.Dock = DockStyle.Bottom;
            status.Height = 30;
            status.BackColor = Theme.Surface;

            _statusAdmin = Ui.Text(Native.IsElevated ? "АДМИН" : "БЕЗ ПРАВ", Theme.MonoBold,
                Native.IsElevated ? Theme.Accent : Theme.Red);
            _statusAdmin.SetBounds(14, 6, 90, 18);
            status.Controls.Add(_statusAdmin);

            _statusTask = Ui.Text("", Theme.Small, Theme.Muted);
            _statusTask.SetBounds(112, 7, 330, 18);
            status.Controls.Add(_statusTask);

            _statusRight = Ui.Text("", Theme.Small, Theme.Muted);
            _statusRight.TextAlign = ContentAlignment.MiddleRight;
            _statusRight.SetBounds(ClientSize.Width - 470, 7, 450, 18);
            _statusRight.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            status.Controls.Add(_statusRight);
            Resize += delegate
            {
                _statusRight.Left = Math.Max(450, ClientSize.Width - 470);
                _statusRight.Width = Math.Max(180, ClientSize.Width - _statusRight.Left - 20);
            };

            Controls.Add(_host);
            Controls.Add(status);
            Controls.Add(_nav);

            // Docking runs from the highest z-order down: rail, status, then fill.
            Controls.SetChildIndex(_host, 0);
            Controls.SetChildIndex(status, 1);
            Controls.SetChildIndex(_nav, 2);

            BuildPages();

            _nav.SelectionChanged += delegate { SwitchTo(_nav.Selected); };
            _nav.Selected = Math.Max(0, Math.Min(_pages.Count - 1, startPage));
            SwitchTo(_nav.Selected);

            _timer = new System.Windows.Forms.Timer();
            _timer.Interval = 1500;
            _timer.Tick += delegate
            {
                if (_pages.Count == 0)
                    return;
                Page current = _pages[_nav.Selected];
                if (current.Visible)
                    current.RefreshData();
            };
            _timer.Start();

            // Dark chrome: the client area is near-black, a light title bar reads as a bug.
            HandleCreated += delegate { Native.UseDarkTitleBar(Handle); };

            FormClosing += delegate
            {
                _timer.Stop();
                if (_clean != null)
                    _clean.ApplyTo(_config);
                _config.Save();
            };

            LogLine("CacheTrim " + AppInfo.Version + " — " + (Native.IsElevated ? "права администратора" : "без прав администратора"));
            RefreshTaskState();
            LogLine("готов. Выбери страницу слева, галочки — на «Очистке».");
        }

        public Config Settings
        {
            get { return _config; }
        }

        public void ShowDiagnostics()
        {
            using (DiagnosticsForm dialog = new DiagnosticsForm())
                dialog.ShowDialog(this);
        }

        public void OptimizeDrives()
        {
            if (!Native.IsElevated)
            {
                LogLine("оптимизация дисков требует прав администратора.");
                return;
            }
            DialogResult answer = MessageBox.Show(this,
                "Windows сама выберет подходящую операцию для каждого диска: дефрагментацию или retrim SSD. " +
                "Проход может занять несколько минут. Продолжить?",
                "Оптимизация дисков", MessageBoxButtons.YesNo, MessageBoxIcon.Information,
                MessageBoxDefaultButton.Button2);
            if (answer == DialogResult.Yes)
                RunMaintenance(Path.Combine(Environment.SystemDirectory, "defrag.exe"),
                    "/c /o /u /v", "оптимизация дисков");
        }

        public void CheckSystemFiles()
        {
            if (!Native.IsElevated)
            {
                LogLine("проверка системных файлов требует прав администратора.");
                return;
            }
            DialogResult answer = MessageBox.Show(this,
                "SFC проверит защищённые системные файлы и при необходимости восстановит их из хранилища Windows. " +
                "Проверка может занять до 30 минут. Продолжить?",
                "Проверка Windows", MessageBoxButtons.YesNo, MessageBoxIcon.Information,
                MessageBoxDefaultButton.Button2);
            if (answer == DialogResult.Yes)
                RunMaintenance(Path.Combine(Environment.SystemDirectory, "sfc.exe"),
                    "/scannow", "проверка системных файлов Windows");
        }

        public void OpenWindowsSettings(string uri)
        {
            try
            {
                Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                LogLine("не удалось открыть параметры Windows: " + ex.Message);
            }
        }

        private void RunMaintenance(string executable, string arguments, string label)
        {
            if (_maintenanceBusy)
            {
                LogLine("обслуживание уже выполняется.");
                return;
            }

            _maintenanceBusy = true;
            _statusRight.Text = label + "…";
            LogLine("--- " + label + " ---");
            Thread worker = new Thread(delegate()
            {
                int exitCode = -1;
                bool timedOut = false;
                StringBuilder output = new StringBuilder();
                try
                {
                    ProcessStartInfo info = new ProcessStartInfo(executable, arguments);
                    info.UseShellExecute = false;
                    info.CreateNoWindow = true;
                    info.RedirectStandardOutput = true;
                    info.RedirectStandardError = true;
                    info.WorkingDirectory = Application.StartupPath;
                    using (Process process = new Process())
                    {
                        process.StartInfo = info;
                        process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e)
                        {
                            if (!string.IsNullOrEmpty(e.Data))
                                lock (output) { if (output.Length < 32768) output.AppendLine(e.Data); }
                        };
                        process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
                        {
                            if (!string.IsNullOrEmpty(e.Data))
                                lock (output) { if (output.Length < 32768) output.AppendLine(e.Data); }
                        };
                        process.Start();
                        process.BeginOutputReadLine();
                        process.BeginErrorReadLine();
                        if (process.WaitForExit(1800000))
                        {
                            process.WaitForExit();
                            exitCode = process.ExitCode;
                        }
                        else
                        {
                            timedOut = true;
                            process.Kill();
                        }
                    }
                }
                catch (Exception ex)
                {
                    lock (output) { output.AppendLine(ex.Message); }
                }

                string[] lines;
                lock (output) { lines = output.ToString().Split(new string[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries); }
                BeginInvoke((MethodInvoker)delegate
                {
                    for (int i = 0; i < lines.Length; i++)
                        LogLine(lines[i]);
                    _maintenanceBusy = false;
                    _statusRight.Text = timedOut ? label + ": превышено время ожидания" :
                        label + (exitCode == 0 ? ": завершено" : ": код " + exitCode);
                    LogLine(timedOut ? label + ": превышено время ожидания." :
                        label + (exitCode == 0 ? ": завершено." : ": код завершения " + exitCode + "."));
                });
            });
            worker.IsBackground = true;
            worker.Start();
        }

        private void BuildPages()
        {
            _memory = new MemoryPage(this);
            _clean = new CleanPage(this);
            _optimize = new OptimizePage(this);
            _startup = new StartupPage(this);
            _log = new LogPage(this);
            _settings = new SettingsPage(this);

            _pages.Add(_memory);
            _pages.Add(_clean);
            _pages.Add(_optimize);
            _pages.Add(_startup);
            _pages.Add(_log);
            _pages.Add(_settings);

            foreach (Page page in _pages)
                _host.Controls.Add(page);
        }

        private void SwitchTo(int index)
        {
            if (index < 0 || index >= _pages.Count)
                return;

            foreach (Page page in _pages)
                page.HidePage();

            _pages[index].ShowPage();
        }

        public void LogLine(string line)
        {
            if (_log != null)
                _log.Append(line);
        }

        public void RefreshTaskState()
        {
            bool installed = TaskSetup.IsInstalled();
            _statusTask.Text = installed
                ? "автоочистка на входе: включена"
                : "автоочистка на входе: выключена";
            _statusTask.ForeColor = installed ? Theme.Accent : Theme.Muted;

            if (_settings != null)
                _settings.SetTaskState(installed);
        }

        public void InstallTask()
        {
            if (!Native.IsElevated)
            {
                LogLine("задачу можно поставить только от администратора.");
                return;
            }

            _clean.ApplyTo(_config);
            _config.Save();

            string error = TaskSetup.Install(Application.ExecutablePath, _config.DelaySeconds);
            if (error == null)
                LogLine(string.Format("автоочистка включена: задача \"{0}\", задержка {1} с.",
                    TaskSetup.TaskName, _config.DelaySeconds));
            else
                LogLine("не удалось создать задачу: " + error);

            RefreshTaskState();
        }

        public void RemoveTask()
        {
            string error = TaskSetup.Uninstall();
            LogLine(error == null ? "автоочистка снята." : "не удалось снять задачу: " + error);
            RefreshTaskState();
        }

        /// <summary>Purges memory only, honouring the per-option checkboxes.</summary>
        public void RunMemoryPurge()
        {
            Config c = _config.Clone();
            c.Temp = false;
            c.Browsers = false;
            c.Wer = false;
            c.D3dCache = false;
            c.Thumbs = false;
            c.Dumps = false;
            c.WinLogs = false;
            c.UwpCache = false;
            c.UpdateCache = false;
            c.Prefetch = false;
            c.Dns = false;
            c.Arp = false;
            c.Netbios = false;
            c.Recycle = false;

            RunClean(c, "память");
        }

        /// <summary>Every safe option on one pass; the recycle bin stays untouched.</summary>
        public void RunFullPass()
        {
            Config c = _config.Clone();
            c.WorkingSets = true;
            c.Standby = true;
            c.LowStandby = true;
            c.Modified = true;
            c.FileCache = true;
            c.Temp = true;
            c.Browsers = true;
            c.Wer = true;
            c.D3dCache = true;
            c.Thumbs = true;
            c.Dumps = true;
            c.WinLogs = true;
            c.UwpCache = true;
            c.UpdateCache = true;
            c.Prefetch = true;
            c.Dns = true;
            c.Arp = true;
            c.Netbios = true;
            c.Recycle = false;

            RunClean(c, "полный прогон");
        }

        public void RunClean(Config cfg, string label)
        {
            if (_busy)
                return;

            _clean.ApplyTo(_config);
            _config.Save();

            _busy = true;
            _statusRight.Text = "чищу: " + label + "…";
            LogLine("--- " + label + " ---");

            Thread worker = new Thread(delegate()
            {
                Native.MemStats before = Native.GetMemoryStats();
                Cleaner cleaner = new Cleaner(LogLine);
                try
                {
                    cleaner.Run(cfg);
                }
                catch (Exception ex)
                {
                    LogLine("ошибка: " + ex.Message);
                }

                Native.MemStats after = Native.GetMemoryStats();
                long delta = (long)after.AvailPhys - (long)before.AvailPhys;
                string summary = string.Format("доступно {0} → {1} ({2}{3}), мусора {4}",
                    Cleaner.FormatBytes((long)before.AvailPhys),
                    Cleaner.FormatBytes((long)after.AvailPhys),
                    delta >= 0 ? "+" : "",
                    Cleaner.FormatBytes(delta),
                    Cleaner.FormatBytes(cleaner.BytesFreed));
                LogLine(summary);

                BeginInvoke((MethodInvoker)delegate
                {
                    _busy = false;
                    _statusRight.Text = "последняя чистка " +
                        DateTime.Now.ToString("HH:mm") + ": " + Cleaner.FormatBytes(cleaner.BytesFreed);
                    if (_memory != null)
                        _memory.RefreshData();
                });
            });

            worker.IsBackground = true;
            worker.Start();
        }
    }
}
