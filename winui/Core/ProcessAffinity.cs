using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
namespace CacheTrim;
internal static class ProcessAffinity
{
    internal sealed record State(ulong ProcessMask, ulong SystemMask);
    internal sealed record Backup(int Id, long StartUtcTicks, string Path, ulong OriginalMask);
    private static string BackupPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CacheTrim", "affinity-backups.json");
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GetProcessAffinityMask(IntPtr process, out UIntPtr processMask, out UIntPtr systemMask);
    [DllImport("kernel32.dll")] private static extern ushort GetActiveProcessorGroupCount();
    private static State Read(Process p)
    {
        if (!OperatingSystem.IsWindows() || GetActiveProcessorGroupCount() != 1) throw new NotSupportedException("Настройка доступна только на Windows с одной группой процессоров; для нескольких групп нужны другие средства.");
        if (!GetProcessAffinityMask(p.Handle, out var mask, out var system)) throw new InvalidOperationException("Windows не предоставила маску процессоров (код " + Marshal.GetLastWin32Error() + ").");
        return new State(mask.ToUInt64(), system.ToUInt64());
    }
    internal static State Read(ProcessManager.Entry entry) => ProcessManager.WithCheckedProcess(entry, Read);
    internal static void ValidateMask(ulong mask, ulong available)
    {
        if (mask == 0 || (mask & ~available) != 0) throw new ArgumentException("Выбери хотя бы один доступный логический процессор.");
    }
    internal static void Apply(ProcessManager.Entry entry, ulong mask) => Apply(entry, mask, BackupPath);
    internal static void Apply(ProcessManager.Entry entry, ulong mask, string backupPath)
    {
        ProcessManager.WithCheckedProcess(entry, p =>
        {
            State state = Read(p); ValidateMask(mask, state.SystemMask);
            SaveOriginal(backupPath, entry, state.ProcessMask);
            p.ProcessorAffinity = unchecked((IntPtr)(long)mask);
            return 0;
        });
    }
    internal static void Restore(ProcessManager.Entry entry) => Restore(entry, BackupPath);
    internal static void Restore(ProcessManager.Entry entry, string backupPath)
    {
        ProcessManager.WithCheckedProcess(entry, p =>
        {
            var backup = Load(backupPath).FirstOrDefault(x => Matches(x, entry)) ?? throw new InvalidOperationException("Для этого запуска процесса сохранённой маски нет.");
            var state = Read(p); ValidateMask(backup.OriginalMask, state.SystemMask);
            p.ProcessorAffinity = unchecked((IntPtr)(long)backup.OriginalMask);
            return 0;
        });
    }
    internal static bool Matches(Backup b, ProcessManager.Entry e) => b.Id == e.Id && b.StartUtcTicks == e.StartUtcTicks && string.Equals(b.Path, e.Path, StringComparison.OrdinalIgnoreCase);
    internal static List<Backup> Load(string path)
    {
        if (!File.Exists(path)) return new();
        var b = JsonSerializer.Deserialize<List<Backup>>(File.ReadAllText(path)) ?? throw new InvalidDataException("Резерв масок повреждён.");
        if (b.Any(x => x == null || x.Id <= 4 || x.StartUtcTicks <= 0 || string.IsNullOrWhiteSpace(x.Path) || x.OriginalMask == 0)) throw new InvalidDataException("Резерв масок повреждён; данные не изменены.");
        return b;
    }
    internal static void SaveOriginal(string path, ProcessManager.Entry entry, ulong mask)
    {
        if (mask == 0) throw new ArgumentException("Нельзя сохранить пустую маску.");
        var backups = Load(path);
        if (backups.Any(x => Matches(x, entry))) return;
        // Keep backups for earlier processes as well; do not silently discard them.
        backups.Add(new Backup(entry.Id, entry.StartUtcTicks, entry.Path, mask));
        if (backups.Count > 2000) throw new InvalidOperationException("Достигнут лимит резервных масок; сохранение остановлено.");
        string dir = Path.GetDirectoryName(path) ?? throw new IOException("Папка резервных масок неизвестна."); Directory.CreateDirectory(dir);
        string tmp = Path.Combine(dir, ".affinity-" + Guid.NewGuid().ToString("N") + ".tmp");
        try { File.WriteAllText(tmp, JsonSerializer.Serialize(backups)); File.Move(tmp, path, true); }
        finally { if (File.Exists(tmp)) File.Delete(tmp); }
    }
}
