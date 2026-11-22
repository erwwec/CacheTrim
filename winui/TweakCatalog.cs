using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
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
        public bool PotentiallyRisky;
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
            list.Add(Reg("Игровая запись", "Отключить оверлей Xbox Game Bar",
                "Для тех, кто не пользуется Win+G: оверлей не будет открываться поверх игры",
                false, true,
                new RegItem { Machine = false, Path = @"SOFTWARE\Microsoft\GameBar", Value = "UseNexusForGameBarEnabled", On = 0, Off = 1 }));

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

            Tweak powerThrottling = Reg("Производительность процессора", "Не ограничивать скорость процессора в фоне",
                "Windows перестанет снижать скорость процессора у фоновых задач. На ноутбуке батарея может разряжаться быстрее, а нагрев увеличиться. Для возврата перезагрузите компьютер.",
                true, false,
                new RegItem { Machine = true, Path = @"SYSTEM\CurrentControlSet\Control\Power\PowerThrottling", Value = "PowerThrottlingOff", On = 1, Off = 0 });
            powerThrottling.Module = "Расширенные";
            powerThrottling.PotentiallyRisky = true;
            list.Add(powerThrottling);

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

}

