using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace CacheTrim
{
    // ------------------------------------------------------------------ tweak model

    /// <summary>
    /// One reversible system setting. Checkbox in the UI means "оптимизация применена",
    /// not "Windows-служба запущена" — Read() answers exactly that question.
    /// </summary>
    internal sealed class Tweak
    {
        public string Group;
        public string Module;
        public string Name;
        public string Hint;
        public bool Admin;
        public bool NeedsSignOut;
        public Func<bool> Read;
        public Func<bool, string> Write;

        public bool IsOn()
        {
            try
            {
                return Read();
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Returns null on success, otherwise a human-readable reason.</summary>
        public string Set(bool on)
        {
            try
            {
                return Write(on);
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
    }

    internal sealed class RegItem
    {
        public bool Machine;
        public string Path;
        public string Value;
        public object On;
        public object Off;
    }

    // ------------------------------------------------------------------ tweak catalogue

    internal static class Tweaks
    {
        private static readonly string ScExe = System.IO.Path.Combine(Environment.SystemDirectory, "sc.exe");

        public const string HighPerfScheme = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
        public const string BalancedScheme = "381b4222-f694-41f0-9685-ff5bb260df2e";

        private const string PowerSchemes = @"SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes";
        private const string ServicesKey = @"SYSTEM\CurrentControlSet\Services\";

        public static List<Tweak> All()
        {
            List<Tweak> list = new List<Tweak>();

            // ---------------- питание ----------------

            list.Add(PowerPlan());
            list.Add(Hibernation());
            list.Add(Reg("Питание", "Отключить быстрый запуск",
                "HiberbootEnabled=0: полное выключение вместо гибернации ядра — чище старт драйверов",
                false, true,
                new RegItem { Machine = true, Path = @"SYSTEM\CurrentControlSet\Control\Session Manager\Power", Value = "HiberbootEnabled", On = 0, Off = 1 }));

            // ---------------- службы и телеметрия ----------------

            list.Add(Service("Службы и телеметрия", "Отключить телеметрию (DiagTrack)",
                "Служба «Подключённые пользовательские возможности и телеметрия» — постоянно шлёт данные",
                "DiagTrack", 2));
            list.Add(Service("Службы и телеметрия", "Отключить WAP-push (dmwappushservice)",
                "Служба обмена сообщениями устройства; для обычной работы не нужна",
                "dmwappushservice", 3));
            list.Add(Service("Службы и телеметрия", "Отключить SysMain (SuperFetch)",
                "Перестаёт сам наполнять standby-список — потребление памяти становится предсказуемым",
                "SysMain", 2));
            list.Add(Service("Службы и телеметрия", "Отключить индексацию поиска",
                "WSearch не строит индекс: меньше диска и фоновой нагрузки, поиск в Проводнике медленнее",
                "WSearch", 2));

            list.Add(Reg("Службы и телеметрия", "Телеметрия в политике = 0",
                "HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\DataCollection\\AllowTelemetry",
                true, true,
                new RegItem { Machine = true, Path = @"SOFTWARE\Policies\Microsoft\Windows\DataCollection", Value = "AllowTelemetry", On = 0, Off = 1 }));

            list.Add(Reg("Службы и телеметрия", "Без P2P в Windows Update",
                "Delivery Optimization = только HTTP, раздача обновлений другим машинам выключена",
                true, false,
                new RegItem { Machine = true, Path = @"SOFTWARE\Microsoft\Windows\CurrentVersion\DeliveryOptimization\Config", Value = "DODownloadMode", On = 0, Off = 1 }));

            // ---------------- интерфейс ----------------

            list.Add(VisualEffects());
            list.Add(Reg("Интерфейс", "Без задержки меню",
                "MenuShowDelay=0: меню раскрывается без паузы 400 мс",
                false, true,
                new RegItem { Machine = false, Path = @"Control Panel\Desktop", Value = "MenuShowDelay", On = "0", Off = "400" }));
            list.Add(Reg("Интерфейс", "Без анимации окон",
                "MinAnimate=0: окна не разворачиваются анимацией",
                false, true,
                new RegItem { Machine = false, Path = @"Control Panel\Desktop\WindowMetrics", Value = "MinAnimate", On = "0", Off = "1" }));
            list.Add(Reg("Интерфейс", "Отключить прозрачность",
                "EnableTransparency=0: панель задач и меню Пуск без размытия — дешевле для GPU",
                false, false,
                new RegItem { Machine = false, Path = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize", Value = "EnableTransparency", On = 0, Off = 1 }));
            list.Add(Reg("Интерфейс", "Без веб-поиска в Пуске",
                "BingSearchEnabled=0, ConnectedSearchUseWeb=0: Пуск ищет только локально",
                false, true,
                new RegItem { Machine = false, Path = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Search", Value = "BingSearchEnabled", On = 0, Off = 1 },
                new RegItem { Machine = false, Path = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Search", Value = "ConnectedSearchUseWeb", On = 0, Off = 1 }));
            list.Add(Reg("Интерфейс", "Отключить виджеты",
                "AllowNewsAndInterests=0: панель новостей не грузится в фоне",
                true, true,
                new RegItem { Machine = true, Path = @"SOFTWARE\Policies\Microsoft\Dsh", Value = "AllowNewsAndInterests", On = 0, Off = 1 }));
            list.Add(Reg("Игровая запись", "Отключить системный Game DVR",
                "Фоновая запись игр выключена: минус постоянный оверхед на GPU и диск",
                true, true,
                new RegItem { Machine = false, Path = @"System\GameConfigStore", Value = "GameDVR_Enabled", On = 0, Off = 1 },
                new RegItem { Machine = true, Path = @"SOFTWARE\Policies\Microsoft\Windows\GameDVR", Value = "AllowGameDVR", On = 0, Off = 1 }));

            list.Add(Reg("Игровой режим", "Включить игровой режим Windows",
                "Windows отдаёт игре приоритет ресурсов; применяется к профилю текущего пользователя",
                false, false,
                new RegItem { Machine = false, Path = @"SOFTWARE\Microsoft\GameBar", Value = "AutoGameModeEnabled", On = 1, Off = 0 },
                new RegItem { Machine = false, Path = @"SOFTWARE\Microsoft\GameBar", Value = "AllowAutoGameMode", On = 1, Off = 0 }));
            list.Add(Reg("Игровая запись", "Отключить запись игровых клипов",
                "Запись клипов в фоне выключена; Game Bar и ручной запуск оверлея остаются доступны",
                false, false,
                new RegItem { Machine = false, Path = @"SOFTWARE\Microsoft\Windows\CurrentVersion\GameDVR", Value = "AppCaptureEnabled", On = 0, Off = 1 }));

            // ---------------- диск ----------------

            list.Add(Reg("Диск и файловая система", "Без обновления last access",
                "NtfsDisableLastAccessUpdate=1: NTFS не пишет время последнего чтения",
                true, false,
                new RegItem { Machine = true, Path = @"SYSTEM\CurrentControlSet\Control\FileSystem", Value = "NtfsDisableLastAccessUpdate", On = 1, Off = 0 }));
            list.Add(Reg("Диск и файловая система", "Длинные пути (>260)",
                "LongPathsEnabled=1: программы и архиваторы больше не упираются в MAX_PATH",
                true, false,
                new RegItem { Machine = true, Path = @"SYSTEM\CurrentControlSet\Control\FileSystem", Value = "LongPathsEnabled", On = 1, Off = 0 }));
            list.Add(Reg("Диск и файловая система", "Отключить автовоспроизведение",
                "DisableAutoplay=1: вставленная флешка не запускает обработчик",
                false, false,
                new RegItem { Machine = false, Path = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\AutoplayHandlers", Value = "DisableAutoplay", On = 1, Off = 0 }));

            return list;
        }

        public static List<string> Groups(List<Tweak> items)
        {
            List<string> groups = new List<string>();
            foreach (Tweak t in items)
            {
                if (!groups.Contains(t.Group))
                    groups.Add(t.Group);
            }
            return groups;
        }

        // ---------------- builders ----------------

        private static Tweak PowerPlan()
        {
            Tweak t = new Tweak();
            t.Module = "Windows";
            t.Group = "Питание";
            t.Name = "План «Высокая производительность»";
            t.Hint = "powercfg /setactive: процессор не сбрасывает частоту, отклик ровнее; ноутбук ест больше";
            t.Admin = true;
            t.NeedsSignOut = false;
            t.Read = delegate
            {
                using (RegistryKey k = Registry.LocalMachine.OpenSubKey(PowerSchemes))
                {
                    if (k == null)
                        return false;
                    string active = k.GetValue("ActivePowerScheme") as string;
                    return active != null &&
                        active.Equals(HighPerfScheme, StringComparison.OrdinalIgnoreCase);
                }
            };
            t.Write = delegate(bool on)
            {
                string output;
                int code = Run("powercfg.exe", "/setactive " + (on ? HighPerfScheme : BalancedScheme), out output);
                return code == 0 ? null : "powercfg: " + output;
            };
            return t;
        }

        private static Tweak Hibernation()
        {
            Tweak t = new Tweak();
            t.Module = "Windows";
            t.Group = "Питание";
            t.Name = "Отключить гибернацию";
            t.Hint = "powercfg /h off: hiberfil.sys уходит с диска (несколько ГБ); заодно пропадает быстрый запуск";
            t.Admin = true;
            t.NeedsSignOut = false;
            t.Read = delegate
            {
                using (RegistryKey k = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Power"))
                {
                    object v = k == null ? null : k.GetValue("HibernateEnabled");
                    return v != null && Convert.ToInt32(v, CultureInfo.InvariantCulture) == 0;
                }
            };
            t.Write = delegate(bool on)
            {
                string output;
                int code = Run("powercfg.exe", on ? "/hibernate off" : "/hibernate on", out output);
                // powercfg refuses to enable hibernation on machines with fast startup off in some builds;
                // the registry read above is the source of truth either way.
                return code == 0 ? null : "powercfg: " + output;
            };
            return t;
        }

        private static Tweak VisualEffects()
        {
            Tweak t = new Tweak();
            t.Module = "Windows";
            t.Group = "Интерфейс";
            t.Name = "Визуальные эффекты: производительность";
            t.Hint = "VisualFXSetting=2 + SPI_SETUIEFFECTS: без теней, анимаций и плавного сглаживания";
            t.Admin = false;
            t.NeedsSignOut = true;
            t.Read = delegate
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects"))
                {
                    object v = k == null ? null : k.GetValue("VisualFXSetting");
                    return v != null && Convert.ToInt32(v, CultureInfo.InvariantCulture) == 2;
                }
            };
            t.Write = delegate(bool on)
            {
                WriteValue(false, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects",
                    "VisualFXSetting", on ? 2 : 0);

                bool effects = !on;
                SystemParametersInfo(SpiSetUiEffects, (uint)(effects ? 1 : 0), IntPtr.Zero,
                    SpifUpdateIniFile | SpifSendChange);
                return null;
            };
            return t;
        }

        private static Tweak Reg(string group, string name, string hint, bool admin, bool signOut,
            params RegItem[] items)
        {
            Tweak t = new Tweak();
            t.Module = group == "Игровая запись" || group == "Игровой режим" ? "Игры" : "Windows";
            t.Group = group;
            t.Name = name;
            t.Hint = hint;
            t.Admin = admin;
            t.NeedsSignOut = signOut;
            t.Read = delegate
            {
                foreach (RegItem item in items)
                {
                    if (!Matches(item))
                        return false;
                }
                return true;
            };
            t.Write = delegate(bool on)
            {
                foreach (RegItem item in items)
                    WriteValue(item.Machine, item.Path, item.Value, on ? item.On : item.Off);
                return null;
            };
            return t;
        }

        private static Tweak Service(string group, string name, string hint, string serviceName, int stockStart)
        {
            Tweak t = new Tweak();
            t.Module = "Windows";
            t.Group = group;
            t.Name = name;
            t.Hint = hint;
            t.Admin = true;
            t.NeedsSignOut = false;
            t.Read = delegate
            {
                object start = ServiceStart(serviceName);
                return start != null && Convert.ToInt32(start, CultureInfo.InvariantCulture) == 4;
            };
            t.Write = delegate(bool on)
            {
                string output;
                int startMode = on ? 4 : stockStart;

                if (on)
                {
                    Run(ScExe, "stop \"" + serviceName + "\"", out output);
                }

                int code = Run(ScExe,
                    "config \"" + serviceName + "\" start= " + StartName(startMode), out output);
                if (code != 0)
                    return "sc config " + serviceName + ": " + output;

                if (!on)
                {
                    int startCode = Run(ScExe, "start \"" + serviceName + "\"", out output);
                    if (startCode != 0 && output.IndexOf("already", StringComparison.OrdinalIgnoreCase) < 0)
                        return "служба настроена, но не запустилась: " + output;
                }
                return null;
            };
            return t;
        }

        private static string StartName(int start)
        {
            if (start == 2)
                return "auto";
            if (start == 3)
                return "demand";
            return "disabled";
        }

        private static object ServiceStart(string serviceName)
        {
            using (RegistryKey k = Registry.LocalMachine.OpenSubKey(ServicesKey + serviceName))
                return k == null ? null : k.GetValue("Start");
        }

        private static bool Matches(RegItem item)
        {
            object actual = ReadValue(item.Machine, item.Path, item.Value);
            if (actual == null)
                return false;

            if (item.On is int)
                return Convert.ToInt32(actual, CultureInfo.InvariantCulture) == (int)item.On;

            return string.Equals(Convert.ToString(actual, CultureInfo.InvariantCulture),
                Convert.ToString(item.On, CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase);
        }

        private static object ReadValue(bool machine, string path, string value)
        {
            using (RegistryKey root = machine ? Registry.LocalMachine : Registry.CurrentUser)
            using (RegistryKey key = root.OpenSubKey(path))
                return key == null ? null : key.GetValue(value);
        }

        private static void WriteValue(bool machine, string path, string value, object data)
        {
            using (RegistryKey root = machine ? Registry.LocalMachine : Registry.CurrentUser)
            using (RegistryKey key = root.CreateSubKey(path))
            {
                if (key == null)
                    throw new InvalidOperationException("не открыть ключ " + path);
                key.SetValue(value, data);
            }
        }

        /// <summary>Runs a console tool and returns its exit code; output is collected for error text.</summary>
        public static int Run(string exe, string arguments, out string output)
        {
            ProcessStartInfo psi = new ProcessStartInfo(exe, arguments);
            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.CreateNoWindow = true;

            using (Process p = Process.Start(psi))
            {
                string stdout = p.StandardOutput.ReadToEnd();
                string stderr = p.StandardError.ReadToEnd();
                p.WaitForExit(30000);
                output = (stdout + " " + stderr).Trim();
                return p.HasExited ? p.ExitCode : -1;
            }
        }

        private const uint SpiSetUiEffects = 0x103F;
        private const uint SpifUpdateIniFile = 0x01;
        private const uint SpifSendChange = 0x02;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SystemParametersInfo(uint action, uint param, IntPtr vparam, uint flags);
    }

    // ------------------------------------------------------------------ page

    internal sealed class OptimizePage : Page
    {
        private sealed class Row
        {
            public Tweak Tweak;
            public CheckBox Box;
        }

        private readonly ScrollHost _scroll;
        private readonly List<Row> _rows = new List<Row>();
        private readonly List<CardPanel> _cards = new List<CardPanel>();
        private readonly List<Tweak> _catalog;
        private readonly Label _status;
        private readonly Label _moduleHint;
        private readonly UiButton _systemTab;
        private readonly UiButton _gamesTab;
        private readonly Panel _tabs;
        private readonly Panel _footer;
        private string _module = "Windows";
        private bool _loading;

        public OptimizePage(MainForm host) : base(host)
        {
            _footer = new Panel();
            _footer.Height = 54;
            _footer.BackColor = Theme.Bg;
            Content.Controls.Add(_footer);

            Button all = Ui.Primary("ПРИМЕНИТЬ МОДУЛЬ");
            all.Width = 194;
            all.Location = new Point(0, 6);
            all.Click += delegate { ApplyAll(true); };
            _footer.Controls.Add(all);

            Button stock = Ui.Ghost("ВЕРНУТЬ ПО УМОЛЧАНИЮ");
            stock.Width = 210;
            stock.Location = new Point(204, 6);
            stock.Click += delegate { ApplyAll(false); };
            _footer.Controls.Add(stock);

            Button reload = Ui.Ghost("ОБНОВИТЬ");
            reload.Width = 140;
            reload.Location = new Point(426, 6);
            reload.Click += delegate { Load(); };
            _footer.Controls.Add(reload);

            _status = Ui.Text("", Theme.Small, Theme.Muted);
            _status.TextAlign = ContentAlignment.MiddleRight;
            _status.SetBounds(566, 16, 260, 20);
            _footer.Controls.Add(_status);
            _footer.Resize += delegate
            {
                _status.Left = Math.Max(580, _footer.ClientSize.Width - 274);
                _status.Width = Math.Max(120, _footer.ClientSize.Width - _status.Left - 14);
            };

            _tabs = new Panel();
            _tabs.Height = 46;
            _tabs.BackColor = Theme.Bg;
            _systemTab = (UiButton)Ui.Ghost("WINDOWS");
            _systemTab.SetBounds(0, 4, 132, 36);
            _systemTab.Click += delegate { ShowModule("Windows"); };
            _tabs.Controls.Add(_systemTab);
            _gamesTab = (UiButton)Ui.Ghost("ИГРЫ");
            _gamesTab.SetBounds(142, 4, 112, 36);
            _gamesTab.Click += delegate { ShowModule("Игры"); };
            _tabs.Controls.Add(_gamesTab);
            _moduleHint = Ui.Text("Системные параметры Windows и обслуживание", Theme.Small, Theme.Muted);
            _moduleHint.SetBounds(272, 12, 520, 20);
            _tabs.Controls.Add(_moduleHint);
            Content.Controls.Add(_tabs);

            _scroll = new ScrollHost();
            _scroll.Dock = DockStyle.None;
            _scroll.Columns = 2;
            _scroll.Gap = 12;
            _scroll.BottomPad = 16;
            _scroll.Include = delegate(Control control)
            {
                return string.Equals(Convert.ToString(control.Tag), _module, StringComparison.Ordinal);
            };
            Content.Controls.Add(_scroll);
            Content.Resize += delegate { LayoutRegions(); };
            LayoutRegions();

            _catalog = Tweaks.All();
            int windowsCount = 0;
            int gamesCount = 0;
            foreach (Tweak tweak in _catalog)
            {
                if (tweak.Module == "Игры") gamesCount++;
                else windowsCount++;
            }
            _systemTab.Text = "WINDOWS  ·  " + windowsCount.ToString(CultureInfo.InvariantCulture);
            _gamesTab.Text = "ИГРЫ  ·  " + gamesCount.ToString(CultureInfo.InvariantCulture);
            Build();
        }

        public override string Title
        {
            get { return "Оптимизация"; }
        }

        protected override string Subtitle
        {
            get { return "раздельные профили Windows и игр; каждый параметр можно вернуть в исходное состояние"; }
        }

        protected override void OnActivated()
        {
            _scroll.Relayout();
            Load();
        }

        private void Build()
        {
            List<string> groups = Tweaks.Groups(_catalog);

            foreach (string group in groups)
            {
                CardPanel card = new CardPanel();
                // Real width comes from ScrollHost, but anchors are computed on creation,
                // so the card starts at the layout width instead of Panel's default 200.
                card.Width = 800;
                card.Height = 40;
                card.Tag = group == "Игровой режим" || group == "Игровая запись" ? "Игры" : "Windows";
                _cards.Add(card);

                Label caption = new Label();
                caption.Text = group.ToUpperInvariant();
                caption.Font = new Font("Segoe UI Semibold", 8.25f);
                caption.ForeColor = Theme.Accent;
                caption.BackColor = Theme.Surface;
                caption.SetBounds(20, 16, Math.Max(200, card.Width - 40), 18);
                caption.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                card.Controls.Add(caption);

                Panel rule = new Panel();
                rule.BackColor = Theme.RowLine;
                rule.SetBounds(20, 42, card.Width - 40, 1);
                rule.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                card.Controls.Add(rule);

                int top = 46;
                foreach (Tweak t in _catalog)
                {
                    if (t.Group != group)
                        continue;
                    AddRow(card, t, top);
                    top += 48;
                }

                card.Height = top + 10;
                _scroll.Add(card);
            }

            _scroll.Add(BuildActionsCard("Windows"));
            _scroll.Add(BuildActionsCard("Игры"));
            ShowModule(_module);
            Load();
        }

        private CardPanel BuildActionsCard(string module)
        {
            bool games = module == "Игры";
            CardPanel card = new CardPanel();
            card.Width = 800;
            card.Height = games ? 174 : 220;
            card.Tag = module;
            _cards.Add(card);

            Label caption = new Label();
            caption.Text = games ? "ИНСТРУМЕНТЫ ДЛЯ ИГР" : "ОБСЛУЖИВАНИЕ WINDOWS";
            caption.Font = new Font("Segoe UI Semibold", 8.25f);
            caption.ForeColor = Theme.Accent;
            caption.BackColor = Theme.Surface;
            caption.SetBounds(20, 16, card.Width - 40, 18);
            caption.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            card.Controls.Add(caption);

            Label hint = new Label();
            hint.Text = games
                ? "Открой системные параметры, чтобы выбрать GPU для конкретной игры."
                : "Запуск штатных средств Windows. Операции могут занять несколько минут.";
            hint.Font = Theme.Small;
            hint.ForeColor = Theme.Muted;
            hint.BackColor = Theme.Surface;
            hint.SetBounds(20, 48, card.Width - 40, 30);
            hint.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            card.Controls.Add(hint);

            if (games)
            {
                Button graphics = Ui.Ghost("НАСТРОЙКИ ГРАФИКИ");
                graphics.SetBounds(20, 88, card.Width - 40, 38);
                graphics.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                graphics.Click += delegate { Host.OpenWindowsSettings("ms-settings:display-graphics"); };
                card.Controls.Add(graphics);

                Button mode = Ui.Ghost("ПАРАМЕТРЫ ИГРОВОГО РЕЖИМА");
                mode.SetBounds(20, 134, card.Width - 40, 38);
                mode.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                mode.Click += delegate { Host.OpenWindowsSettings("ms-settings:gaming-gamemode"); };
                card.Controls.Add(mode);
            }
            else
            {
                Button drives = Ui.Ghost("ОПТИМИЗИРОВАТЬ ДИСКИ");
                drives.SetBounds(20, 88, card.Width - 40, 38);
                drives.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                drives.Click += delegate { Host.OptimizeDrives(); };
                card.Controls.Add(drives);

                Button files = Ui.Ghost("ПРОВЕРИТЬ ФАЙЛЫ WINDOWS");
                files.SetBounds(20, 134, card.Width - 40, 38);
                files.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                files.Click += delegate { Host.CheckSystemFiles(); };
                card.Controls.Add(files);

                Button storage = Ui.Ghost("ПАРАМЕТРЫ ХРАНИЛИЩА");
                storage.SetBounds(20, 180, card.Width - 40, 38);
                storage.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                storage.Click += delegate { Host.OpenWindowsSettings("ms-settings:storagesense"); };
                card.Controls.Add(storage);
            }
            return card;
        }

        private void LayoutRegions()
        {
            int width = Content.ClientSize.Width;
            int height = Content.ClientSize.Height;
            _footer.SetBounds(0, Math.Max(0, height - _footer.Height), width, _footer.Height);
            _tabs.SetBounds(0, 0, width, _tabs.Height);
            _scroll.SetBounds(0, _tabs.Height + 4, width,
                Math.Max(0, height - _tabs.Height - 4 - _footer.Height));
        }

        private void ShowModule(string module)
        {
            _module = module;
            foreach (CardPanel card in _cards)
                card.Visible = string.Equals(Convert.ToString(card.Tag), module, StringComparison.Ordinal);
            _systemTab.Accent = module == "Windows";
            _gamesTab.Accent = module == "Игры";
            _systemTab.Invalidate();
            _gamesTab.Invalidate();
            _moduleHint.Text = module == "Игры"
                ? "Игровой режим Windows, оверлей и фоновая запись"
                : "Системные параметры Windows и обслуживание";
            _scroll.Relayout();
            UpdateStatus();
        }

        private void AddRow(CardPanel card, Tweak t, int top)
        {
            RowPanel row = new RowPanel();
            row.SetBounds(20, top, Math.Max(200, card.Width - 40), 48);
            row.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            card.Controls.Add(row);

            Label name = new Label();
            name.Text = t.Name;
            name.ForeColor = Theme.Text;
            name.BackColor = Theme.Surface;
            name.Font = new Font("Segoe UI Semibold", 9.5f);
            name.SetBounds(0, 1, Math.Max(120, row.Width - 76), 19);
            name.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            row.Controls.Add(name);

            Label hint = new Label();
            hint.Text = t.Hint + (t.NeedsSignOut ? "  ·  нужен перезаход" : string.Empty);
            hint.ForeColor = t.NeedsSignOut ? Theme.Amber : Theme.Faint;
            hint.BackColor = Theme.Surface;
            hint.Font = Theme.Small;
            hint.AutoEllipsis = true;
            hint.SetBounds(0, 23, Math.Max(120, row.Width - 76), 15);
            hint.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            row.Controls.Add(hint);

            ToggleSwitch toggle = new ToggleSwitch();
            toggle.SetBounds(row.Width - 44, 13, 42, 22);
            toggle.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            row.Controls.Add(toggle);

            Row entry = new Row();
            entry.Tweak = t;
            entry.Box = toggle;
            _rows.Add(entry);

            toggle.CheckedChanged += delegate { OnToggled(entry); };

            // Clicking the row is the same as flipping the switch — the target is big.
            EventHandler flip = delegate
            {
                toggle.Checked = !toggle.Checked;
            };
            row.Click += flip;
            name.Click += flip;
            hint.Click += flip;
        }

        private void OnToggled(Row row)
        {
            if (_loading)
                return;

            bool wanted = row.Box.Checked;
            if (row.Tweak.Admin && !Native.IsElevated)
            {
                Host.LogLine("оптимизация: \"" + row.Tweak.Name + "\" требует прав администратора.");
                Restore(row);
                return;
            }

            RunAsync(delegate
            {
                string error = row.Tweak.Set(wanted);
                bool actual = row.Tweak.IsOn();

                BeginInvoke((MethodInvoker)delegate
                {
                    _loading = true;
                    row.Box.Checked = actual;
                    _loading = false;

                    if (error == null)
                        Host.LogLine(string.Format("оптимизация: \"{0}\" -> {1}", row.Tweak.Name,
                            actual ? "включено" : "выключено"));
                    else
                        Host.LogLine("оптимизация: \"" + row.Tweak.Name + "\" — ошибка: " + error);
                });
            });
        }

        private void RunAsync(Action action)
        {
            System.Threading.Thread worker = new System.Threading.Thread(delegate() { action(); });
            worker.IsBackground = true;
            worker.Start();
        }

        private void Restore(Row row)
        {
            _loading = true;
            row.Box.Checked = row.Tweak.IsOn();
            _loading = false;
        }

        public void Load()
        {
            _loading = true;
            foreach (Row row in _rows)
            {
                row.Box.Checked = row.Tweak.IsOn();
            }
            _loading = false;
            UpdateStatus();
        }

        private void UpdateStatus()
        {
            int on = 0;
            int total = 0;
            foreach (Row row in _rows)
            {
                if (row.Tweak.Module != _module) continue;
                total++;
                if (row.Box.Checked)
                    on++;
            }
            _status.Text = string.Format(CultureInfo.InvariantCulture,
                "{0}: включено {1} из {2}", _module, on, total);
        }

        private void ApplyAll(bool on)
        {
            if (on && !Native.IsElevated)
            {
                Host.LogLine("оптимизация: часть пунктов требует прав администратора.");
            }

            Host.LogLine("оптимизация: " + (on ? "применяю всё" : "возвращаю стоковые значения") + "…");

            RunAsync(delegate
            {
                int changed = 0;
                int failed = 0;

                foreach (Row row in _rows)
                {
                    if (row.Tweak.Module != _module)
                        continue;
                    if (on && row.Tweak.Admin && !Native.IsElevated)
                        continue;

                    string error = row.Tweak.Set(on);
                    if (error != null)
                    {
                        failed++;
                        string name = row.Tweak.Name;
                        BeginInvoke((MethodInvoker)delegate { Host.LogLine("оптимизация: \"" + name + "\" — " + error); });
                    }
                    else
                    {
                        changed++;
                    }
                }

                BeginInvoke((MethodInvoker)delegate
                {
                    Load();
                    Host.LogLine(string.Format("оптимизация: готово, изменено {0}, ошибок {1}", changed, failed));
                });
            });
        }
    }
}
