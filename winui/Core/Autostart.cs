using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Win32;

namespace CacheTrim
{
    /// <summary>
    /// Enumerate and toggle logon autostart entries (Run keys + Startup folders).
    /// Disabling never deletes anything: the entry moves into a CacheTrim backup
    /// key/folder and comes back byte-for-byte on enable.
    /// </summary>
    internal static class Autostart
    {
        private const string BackupKey = @"Software\CacheTrim\Disabled";

        public enum Source
        {
            CurrentUser = 0,
            LocalMachine = 1,
            LocalMachine32 = 2,
            UserStartupFolder = 3,
            CommonStartupFolder = 4
        }

        public sealed class Entry
        {
            public Source Origin;
            public string Name;
            public string Command;
            public bool Enabled;
            public bool RequiresAdmin;
            public string Location;
        }

        public static string SourceLabel(Source s)
        {
            switch (s)
            {
                case Source.CurrentUser: return "HKCU\\Run";
                case Source.LocalMachine: return "HKLM\\Run";
                case Source.LocalMachine32: return "HKLM\\Run (32)";
                case Source.UserStartupFolder: return "Папка автозагрузки";
                default: return "Общая автозагрузка";
            }
        }

        public static List<Entry> List()
        {
            List<Entry> entries = new List<Entry>();

            AddRegistry(entries, Source.CurrentUser, Registry.CurrentUser);
            AddRegistry(entries, Source.LocalMachine, Registry.LocalMachine);
            AddRegistry(entries, Source.LocalMachine32, Registry.LocalMachine);

            AddFolder(entries, Source.UserStartupFolder,
                Environment.GetFolderPath(Environment.SpecialFolder.Startup));
            AddFolder(entries, Source.CommonStartupFolder,
                Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup));

            AddDisabled(entries);

            entries.Sort(delegate(Entry a, Entry b)
            {
                if (a.Enabled != b.Enabled) return a.Enabled ? -1 : 1;
                return string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase);
            });

            return entries;
        }

        private static void AddRegistry(List<Entry> entries, Source source, RegistryKey root)
        {
            string sub = source == Source.LocalMachine32
                ? @"Software\Wow6432Node\Microsoft\Windows\CurrentVersion\Run"
                : @"Software\Microsoft\Windows\CurrentVersion\Run";

            try
            {
                using (RegistryKey key = root.OpenSubKey(sub))
                {
                    if (key == null)
                        return;

                    foreach (string name in key.GetValueNames())
                    {
                        Entry e = new Entry();
                        e.Origin = source;
                        e.Name = name;
                        e.Command = Convert.ToString(key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames));
                        e.Enabled = true;
                        e.RequiresAdmin = source != Source.CurrentUser;
                        e.Location = key.Name;
                        entries.Add(e);
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        private static void AddFolder(List<Entry> entries, Source source, string folder)
        {
            if (string.IsNullOrEmpty(folder))
                return;

            if (Directory.Exists(folder))
            {
                string[] files;
                try
                {
                    files = Directory.GetFiles(folder);
                }
                catch (Exception)
                {
                    files = new string[0];
                }

                foreach (string file in files)
                {
                    if (file.EndsWith(".ini", StringComparison.OrdinalIgnoreCase))
                        continue;

                    Entry e = new Entry();
                    e.Origin = source;
                    e.Name = Path.GetFileNameWithoutExtension(file);
                    e.Command = Path.GetFileName(file);
                    e.Enabled = true;
                    e.RequiresAdmin = source == Source.CommonStartupFolder;
                    e.Location = folder;
                    entries.Add(e);
                }
            }

            // Entries parked by CacheTrim show up as disabled rows.
            string backup = BackupFolder(source);
            if (!Directory.Exists(backup))
                return;

            try
            {
                foreach (string file in Directory.GetFiles(backup))
                {
                    Entry e = new Entry();
                    e.Origin = source;
                    e.Name = Path.GetFileNameWithoutExtension(file);
                    e.Command = Path.GetFileName(file);
                    e.Enabled = false;
                    e.RequiresAdmin = source == Source.CommonStartupFolder;
                    e.Location = folder;
                    entries.Add(e);
                }
            }
            catch (Exception)
            {
            }
        }

        private static void AddDisabled(List<Entry> entries)
        {
            if (Directory.Exists(BackupFolder(Source.UserStartupFolder)) ||
                Directory.Exists(BackupFolder(Source.CommonStartupFolder)))
            {
                // handled inside AddFolder
            }

            foreach (Source source in new[] { Source.CurrentUser, Source.LocalMachine, Source.LocalMachine32 })
            {
                try
                {
                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey(BackupKey + "\\" + SourceId(source)))
                    {
                        if (key == null)
                            continue;

                        foreach (string name in key.GetValueNames())
                        {
                            Entry e = new Entry();
                            e.Origin = source;
                            e.Name = name;
                            e.Command = Convert.ToString(key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames));
                            e.Enabled = false;
                            e.RequiresAdmin = source != Source.CurrentUser;
                            e.Location = source == Source.LocalMachine32
                                ? @"HKLM\Software\Wow6432Node\Microsoft\Windows\CurrentVersion\Run"
                                : (source == Source.LocalMachine
                                    ? @"HKLM\Software\Microsoft\Windows\CurrentVersion\Run"
                                    : @"HKCU\Software\Microsoft\Windows\CurrentVersion\Run");
                            entries.Add(e);
                        }
                    }
                }
                catch (Exception)
                {
                }
            }
        }

        public static string SetEnabled(Entry e, bool enabled)
        {
            try
            {
                if (e.Enabled == enabled)
                    return null;

                if (e.Origin == Source.UserStartupFolder || e.Origin == Source.CommonStartupFolder)
                    return ToggleFolder(e, enabled);

                return ToggleRegistry(e, enabled);
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        private static string ToggleRegistry(Entry e, bool enabled)
        {
            RegistryKey root = e.Origin == Source.CurrentUser ? Registry.CurrentUser : Registry.LocalMachine;
            string sub = e.Origin == Source.LocalMachine32
                ? @"Software\Wow6432Node\Microsoft\Windows\CurrentVersion\Run"
                : @"Software\Microsoft\Windows\CurrentVersion\Run";
            string backupPath = BackupKey + "\\" + SourceId(e.Origin);

            if (!enabled)
            {
                using (RegistryKey key = root.OpenSubKey(sub, true))
                {
                    if (key == null)
                        return "ключ автозагрузки недоступен";
                    using (RegistryKey backup = Registry.CurrentUser.CreateSubKey(backupPath))
                    {
                        if (backup == null)
                            return "резерв автозагрузки недоступен";
                        return MoveRegistryValueToBackup(key, backup, e.Name);
                    }
                }
            }

            using (RegistryKey backup = Registry.CurrentUser.OpenSubKey(backupPath))
            {
                if (backup == null)
                    return "нет резервной копии записи";

                object value = backup.GetValue(e.Name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                if (value == null)
                    return "нет резервной копии записи";

                using (RegistryKey existing = root.OpenSubKey(sub, true))
                {
                    if (existing != null && existing.GetValue(e.Name, null, RegistryValueOptions.DoNotExpandEnvironmentNames) != null)
                        return "запись с таким именем уже есть в автозагрузке; резерв сохранён";
                }

                RegistryValueKind kind = backup.GetValueKind(e.Name);
                using (RegistryKey key = root.CreateSubKey(sub))
                    key.SetValue(e.Name, value, kind);
            }

            using (RegistryKey backup = Registry.CurrentUser.OpenSubKey(backupPath, true))
            {
                if (backup != null)
                    backup.DeleteValue(e.Name, false);
            }
            return null;
        }

        internal static string MoveRegistryValueToBackup(RegistryKey source, RegistryKey backup, string name)
        {
            foreach (string existingName in backup.GetValueNames())
            {
                if (string.Equals(existingName, name, StringComparison.OrdinalIgnoreCase))
                    return "резервная запись с таким именем уже существует; новая запись не отключена";
            }

            object value = source.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
            if (value == null)
                return "запись уже удалена";

            RegistryValueKind kind = source.GetValueKind(name);
            backup.SetValue(name, value, kind);
            try
            {
                source.DeleteValue(name, false);
            }
            catch
            {
                try { backup.DeleteValue(name, false); }
                catch (Exception) { }
                throw;
            }
            return null;
        }

        private static string ToggleFolder(Entry e, bool enabled)
        {
            string folder = e.Origin == Source.UserStartupFolder
                ? Environment.GetFolderPath(Environment.SpecialFolder.Startup)
                : Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup);
            string backup = BackupFolder(e.Origin);

            if (!enabled)
            {
                string file = Path.Combine(folder, e.Command);
                if (!File.Exists(file))
                    return "файл не найден";

                Directory.CreateDirectory(backup);
                File.Move(file, Path.Combine(backup, Path.GetFileName(file)));
                return null;
            }

            string parked = Path.Combine(backup, e.Command);
            if (!File.Exists(parked))
                return "нет резервной копии файла";

            Directory.CreateDirectory(folder);
            File.Move(parked, Path.Combine(folder, Path.GetFileName(parked)));
            return null;
        }

        private static string SourceId(Source s)
        {
            switch (s)
            {
                case Source.CurrentUser: return "HKCU";
                case Source.LocalMachine: return "HKLM";
                case Source.LocalMachine32: return "HKLM32";
                case Source.UserStartupFolder: return "UserStartup";
                default: return "CommonStartup";
            }
        }

        private static string BackupFolder(Source s)
        {
            return Path.Combine(Path.Combine(Config.ConfigDir, "StartupBackup"), SourceId(s));
        }
    }
}
