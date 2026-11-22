using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Security.Cryptography;
using System.Text;
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
        private const string PowerPlanBackup = @"Software\CacheTrim\TweakBackup\PowerPlan";
        private const string VisualEffectsBackup = @"Software\CacheTrim\TweakBackup\VisualEffects";

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
                string active = GetActivePowerScheme();
                return active != null && active.Equals(HighPerfScheme, StringComparison.OrdinalIgnoreCase);
            };
            t.Write = delegate(bool on)
            {
                string target = on ? HighPerfScheme : BalancedScheme;
                bool createdBackup = false;
                if (on)
                {
                    using (RegistryKey existing = Registry.CurrentUser.OpenSubKey(PowerPlanBackup))
                    {
                        if (existing == null)
                        {
                            string active = GetActivePowerScheme();
                            if (string.IsNullOrEmpty(active))
                                return "не удалось прочитать текущий план питания";
                            using (RegistryKey backup = Registry.CurrentUser.CreateSubKey(PowerPlanBackup))
                            {
                                if (backup == null) return "не удалось сохранить текущий план питания";
                                backup.SetValue("Scheme", active, RegistryValueKind.String);
                                createdBackup = true;
                            }
                        }
                    }
                }
                else
                {
                    using (RegistryKey backup = Registry.CurrentUser.OpenSubKey(PowerPlanBackup))
                    {
                        string original = backup == null ? null : backup.GetValue("Scheme") as string;
                        if (!string.IsNullOrEmpty(original)) target = original;
                    }
                }

                string output;
                int code = Run("powercfg.exe", "/setactive " + target, out output);
                if (code != 0 && createdBackup)
                {
                    try { Registry.CurrentUser.DeleteSubKeyTree(PowerPlanBackup, false); }
                    catch (Exception) { }
                }
                if (code == 0 && !on)
                {
                    try { Registry.CurrentUser.DeleteSubKeyTree(PowerPlanBackup, false); }
                    catch (Exception) { }
                }
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
            RegItem visualEffectsItem = new RegItem
            {
                Machine = false,
                Path = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects",
                Value = "VisualFXSetting",
                On = 2,
                Off = 0
            };
            t.Write = delegate(bool on)
            {
                RegItem[] items = { visualEffectsItem };
                if (on)
                {
                    CaptureRegistryValue(visualEffectsItem);
                    CaptureUiEffects();
                    bool registryApplied = false;
                    try
                    {
                        ApplyRegistryItems(items, true);
                        registryApplied = true;
                        if (!SetUiEffects(false))
                        {
                            RestoreVisualEffects(visualEffectsItem);
                            throw new InvalidOperationException("не удалось отключить системные эффекты интерфейса");
                        }
                    }
                    catch
                    {
                        if (!registryApplied) DeleteUiEffectsBackup();
                        throw;
                    }
                }
                else
                {
                    RestoreVisualEffects(visualEffectsItem);
                }
                return null;
            };
            return t;
        }

        private static string GetActivePowerScheme()
        {
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(PowerSchemes))
                return key == null ? null : key.GetValue("ActivePowerScheme") as string;
        }

        private static void CaptureUiEffects()
        {
            using (RegistryKey backup = Registry.CurrentUser.CreateSubKey(VisualEffectsBackup))
            {
                if (backup == null) throw new InvalidOperationException("не удалось сохранить параметры интерфейса");
                if (Convert.ToInt32(backup.GetValue("Captured", 0), CultureInfo.InvariantCulture) == 1) return;
                int current = 0;
                IntPtr value = Marshal.AllocHGlobal(sizeof(int));
                try
                {
                    if (!SystemParametersInfo(SpiGetUiEffects, 0, value, 0))
                        throw new InvalidOperationException("не удалось прочитать параметры системных эффектов");
                    current = Marshal.ReadInt32(value);
                }
                finally { Marshal.FreeHGlobal(value); }
                backup.SetValue("Enabled", current == 0 ? 0 : 1, RegistryValueKind.DWord);
                backup.SetValue("Captured", 1, RegistryValueKind.DWord);
            }
        }

        private static bool SetUiEffects(bool enabled)
        {
            return SystemParametersInfo(SpiSetUiEffects, (uint)(enabled ? 1 : 0), IntPtr.Zero,
                SpifUpdateIniFile | SpifSendChange);
        }

        private static void RestoreUiEffects()
        {
            int enabled = 1;
            using (RegistryKey backup = Registry.CurrentUser.OpenSubKey(VisualEffectsBackup))
            {
                if (backup != null && Convert.ToInt32(backup.GetValue("Captured", 0), CultureInfo.InvariantCulture) == 1)
                    enabled = Convert.ToInt32(backup.GetValue("Enabled", 1), CultureInfo.InvariantCulture) != 0 ? 1 : 0;
            }
            if (!SystemParametersInfo(SpiSetUiEffects, (uint)enabled, IntPtr.Zero, SpifUpdateIniFile | SpifSendChange))
                throw new InvalidOperationException("параметр реестра восстановлен, но Windows не приняла исходное состояние эффектов");
        }

        private static void RestoreVisualEffects(RegItem item)
        {
            if (!RestoreRegistryValue(item))
                WriteValue(item.Machine, item.Path, item.Value, item.Off);
            RestoreUiEffects();
            DeleteRegistryBackup(item);
            DeleteUiEffectsBackup();
        }

        private static void DeleteUiEffectsBackup()
        {
            try { Registry.CurrentUser.DeleteSubKeyTree(VisualEffectsBackup, false); }
            catch (Exception) { }
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
                return ApplyRegistryItems(items, on);
            };
            return t;
        }

        internal static string ApplyRegistryItems(RegItem[] items, bool on)
        {
            if (on)
            {
                foreach (RegItem item in items)
                    CaptureRegistryValue(item);

                try
                {
                    foreach (RegItem item in items)
                        WriteValue(item.Machine, item.Path, item.Value, item.On);
                }
                catch (Exception applyError)
                {
                    Exception rollbackError = null;
                    foreach (RegItem item in items)
                    {
                        try { RestoreRegistryValue(item); }
                        catch (Exception ex) { if (rollbackError == null) rollbackError = ex; }
                    }
                    if (rollbackError == null)
                    {
                        foreach (RegItem item in items)
                            DeleteRegistryBackup(item);
                        throw;
                    }
                    throw new InvalidOperationException(applyError.Message + "; не удалось полностью вернуть исходные параметры: " + rollbackError.Message, applyError);
                }
                return null;
            }

            foreach (RegItem item in items)
            {
                if (!RestoreRegistryValue(item))
                    WriteValue(item.Machine, item.Path, item.Value, item.Off);
            }
            foreach (RegItem item in items)
                DeleteRegistryBackup(item);
            return null;
        }

        private static void CaptureRegistryValue(RegItem item)
        {
            string backupPath = RegistryBackupPath(item);
            using (RegistryKey backup = Registry.CurrentUser.CreateSubKey(backupPath))
            {
                if (backup == null)
                    throw new InvalidOperationException("не удалось сохранить исходное значение реестра");
                if (Convert.ToInt32(backup.GetValue("Captured", 0), CultureInfo.InvariantCulture) == 1)
                    return;

                foreach (string name in backup.GetValueNames())
                    backup.DeleteValue(name, false);

                bool exists = false;
                object original = null;
                RegistryValueKind kind = RegistryValueKind.String;
                using (RegistryKey root = item.Machine ? Registry.LocalMachine : Registry.CurrentUser)
                using (RegistryKey key = root.OpenSubKey(item.Path))
                {
                    if (key != null)
                    {
                        foreach (string name in key.GetValueNames())
                        {
                            if (string.Equals(name, item.Value, StringComparison.OrdinalIgnoreCase))
                            {
                                exists = true;
                                original = key.GetValue(item.Value, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                                kind = key.GetValueKind(item.Value);
                                break;
                            }
                        }
                    }
                }

                backup.SetValue("Exists", exists ? 1 : 0, RegistryValueKind.DWord);
                if (exists)
                {
                    if (original == null)
                        throw new InvalidOperationException("не удалось прочитать исходное значение реестра");
                    backup.SetValue("Kind", (int)kind, RegistryValueKind.DWord);
                    backup.SetValue("Data", original, kind);
                }
                backup.SetValue("Captured", 1, RegistryValueKind.DWord);
            }
        }

        private static bool RestoreRegistryValue(RegItem item)
        {
            string backupPath = RegistryBackupPath(item);
            using (RegistryKey backup = Registry.CurrentUser.OpenSubKey(backupPath))
            {
                if (backup == null || Convert.ToInt32(backup.GetValue("Captured", 0), CultureInfo.InvariantCulture) != 1)
                    return false;

                bool existed = Convert.ToInt32(backup.GetValue("Exists", 0), CultureInfo.InvariantCulture) == 1;
                using (RegistryKey root = item.Machine ? Registry.LocalMachine : Registry.CurrentUser)
                {
                    if (existed)
                    {
                        object data = backup.GetValue("Data", null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                        if (data == null)
                            throw new InvalidOperationException("резервная копия значения реестра повреждена");
                        RegistryValueKind kind = (RegistryValueKind)Convert.ToInt32(backup.GetValue("Kind"), CultureInfo.InvariantCulture);
                        using (RegistryKey key = root.CreateSubKey(item.Path))
                        {
                            if (key == null)
                                throw new InvalidOperationException("не удалось восстановить ключ реестра");
                            key.SetValue(item.Value, data, kind);
                        }
                    }
                    else
                    {
                        using (RegistryKey key = root.OpenSubKey(item.Path, true))
                        {
                            if (key != null)
                                key.DeleteValue(item.Value, false);
                        }
                    }
                }
                return true;
            }
        }

        private static void DeleteRegistryBackup(RegItem item)
        {
            Registry.CurrentUser.DeleteSubKeyTree(RegistryBackupPath(item), false);
        }

        private static string RegistryBackupPath(RegItem item)
        {
            string identity = (item.Machine ? "HKLM" : "HKCU") + "\n" + item.Path + "\n" + item.Value;
            byte[] hash;
            using (SHA256 sha = SHA256.Create())
                hash = sha.ComputeHash(Encoding.UTF8.GetBytes(identity));
            return @"Software\CacheTrim\TweakBackup\Registry\" + BitConverter.ToString(hash).Replace("-", string.Empty);
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
                string backupPath = @"Software\CacheTrim\TweakBackup\Services\" + serviceName;
                if (on)
                {
                    object currentValue = ServiceStart(serviceName);
                    if (currentValue == null)
                        return "не удалось прочитать параметры службы " + serviceName;

                    bool wasRunning;
                    string statusError = GetServiceRunning(serviceName, out wasRunning);
                    if (statusError != null)
                        return statusError;

                    using (RegistryKey existing = Registry.CurrentUser.OpenSubKey(backupPath))
                    {
                        if (existing == null)
                        {
                            using (RegistryKey backup = Registry.CurrentUser.CreateSubKey(backupPath))
                            {
                                if (backup == null)
                                    return "не удалось сохранить исходные параметры службы";
                                backup.SetValue("Start", Convert.ToInt32(currentValue, CultureInfo.InvariantCulture), RegistryValueKind.DWord);
                                backup.SetValue("WasRunning", wasRunning ? 1 : 0, RegistryValueKind.DWord);
                            }
                        }
                    }

                    statusError = StopServiceIfRunning(serviceName, wasRunning);
                    if (statusError != null)
                        return statusError;

                    int disableCode = Run(ScExe, "config \"" + serviceName + "\" start= disabled", out output);
                    if (disableCode != 0)
                    {
                        if (wasRunning) StartServiceIfNeeded(serviceName);
                        return "sc config " + serviceName + ": " + output;
                    }
                    return null;
                }

                int startMode = stockStart;
                bool startAfterRestore = false;
                using (RegistryKey backup = Registry.CurrentUser.OpenSubKey(backupPath))
                {
                    if (backup != null)
                    {
                        object savedStart = backup.GetValue("Start");
                        if (savedStart != null)
                            startMode = Convert.ToInt32(savedStart, CultureInfo.InvariantCulture);
                        object wasRunning = backup.GetValue("WasRunning");
                        startAfterRestore = wasRunning != null && Convert.ToInt32(wasRunning, CultureInfo.InvariantCulture) != 0;
                    }
                }

                int code = Run(ScExe, "config \"" + serviceName + "\" start= " + StartName(startMode), out output);
                if (code != 0)
                    return "sc config " + serviceName + ": " + output;

                if (startAfterRestore)
                {
                    string startError = StartServiceIfNeeded(serviceName);
                    if (startError != null)
                        return startError;
                }

                try { Registry.CurrentUser.DeleteSubKeyTree(backupPath, false); }
                catch (Exception) { }
                return null;
            };
            return t;
        }

        private static string GetServiceRunning(string serviceName, out bool running)
        {
            running = false;
            try
            {
                using (ServiceController service = new ServiceController(serviceName))
                {
                    ServiceControllerStatus status = service.Status;
                    if (status == ServiceControllerStatus.Running) running = true;
                    else if (status != ServiceControllerStatus.Stopped)
                        return "служба " + serviceName + " занята другой операцией";
                }
                return null;
            }
            catch (Exception ex) { return "не удалось проверить службу " + serviceName + ": " + ex.Message; }
        }

        private static string StopServiceIfRunning(string serviceName, bool wasRunning)
        {
            if (!wasRunning) return null;
            try
            {
                using (ServiceController service = new ServiceController(serviceName))
                {
                    if (service.Status == ServiceControllerStatus.Stopped) return null;
                    if (service.Status != ServiceControllerStatus.Running)
                        return "служба " + serviceName + " занята другой операцией";
                    service.Stop();
                    service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(20));
                    return service.Status == ServiceControllerStatus.Stopped ? null : "не удалось остановить службу " + serviceName;
                }
            }
            catch (Exception ex) { return "не удалось остановить службу " + serviceName + ": " + ex.Message; }
        }

        private static string StartServiceIfNeeded(string serviceName)
        {
            try
            {
                using (ServiceController service = new ServiceController(serviceName))
                {
                    if (service.Status == ServiceControllerStatus.Running) return null;
                    if (service.Status != ServiceControllerStatus.Stopped)
                        return "служба " + serviceName + " занята другой операцией";
                    service.Start();
                    service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(20));
                    return service.Status == ServiceControllerStatus.Running ? null : "служба " + serviceName + " не запустилась";
                }
            }
            catch (Exception ex) { return "не удалось запустить службу " + serviceName + ": " + ex.Message; }
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
        public static int Run(string exe, string arguments, out string output, int timeoutMilliseconds = 30000)
        {
            ProcessResult result = ProcessRunner.Run(exe, arguments, timeoutMilliseconds);
            output = result.Output;
            return result.ExitCode;
        }

        private const uint SpiGetUiEffects = 0x103E;
        private const uint SpiSetUiEffects = 0x103F;
        private const uint SpifUpdateIniFile = 0x01;
        private const uint SpifSendChange = 0x02;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SystemParametersInfo(uint action, uint param, IntPtr vparam, uint flags);
    }

}

