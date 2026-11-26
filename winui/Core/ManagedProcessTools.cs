using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace CacheTrim;

internal static class ManagedProcessTools
{
    internal sealed record ProcessIdentity(int Pid, long StartUtcTicks, string Name, string Path);
    internal sealed record ProcessBackup(ProcessIdentity Identity, ProcessPriorityClass Priority, long Affinity, ProcessPriorityClass AppliedPriority, long AppliedAffinity);
    internal sealed record ProcessProfile(string Path, ProcessPriorityClass Priority, long Affinity);
    private static readonly string BackupPath = Path.Combine(SystemToolkit.DataDirectory, "process-backups.json");
    private static readonly string ProfilePath = Path.Combine(SystemToolkit.DataDirectory, "process-profiles.json");

    internal static ProcessIdentity Identify(Process p)
    {
        string path = p.MainModule?.FileName ?? throw new InvalidOperationException("Не удалось определить путь к процессу.");
        return new(p.Id, p.StartTime.ToUniversalTime().Ticks, p.ProcessName, path);
    }
    internal static bool Same(ProcessIdentity a, ProcessIdentity b) => a.Pid == b.Pid && a.StartUtcTicks == b.StartUtcTicks && string.Equals(a.Path, b.Path, StringComparison.OrdinalIgnoreCase);
    internal static bool IsProtected(ProcessIdentity p, string windowsDirectory, int currentPid)
    {
        if (p.Pid <= 4 || p.Pid == currentPid || string.IsNullOrWhiteSpace(p.Path)) return true;
        string dir = Path.GetFullPath(windowsDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return Path.GetFullPath(p.Path).StartsWith(dir, StringComparison.OrdinalIgnoreCase);
    }
    internal static Process OpenChecked(ProcessIdentity identity)
    {
        if (IsProtected(identity, Environment.GetFolderPath(Environment.SpecialFolder.Windows), Environment.ProcessId))
            throw new InvalidOperationException("Системный процесс или сам CacheTrim менять нельзя.");
        var p = Process.GetProcessById(identity.Pid);
        try { if (!Same(Identify(p), identity)) throw new InvalidOperationException("Процесс изменился; обнови список."); return p; }
        catch { p.Dispose(); throw; }
    }

    private static List<T> Read<T>(string path)
    {
        if (!File.Exists(path)) return new();
        if (new FileInfo(path).Length > 1_000_000) throw new InvalidDataException("Файл настроек процессов слишком велик.");
        try { return JsonSerializer.Deserialize<List<T>>(File.ReadAllText(path)) ?? throw new InvalidDataException("Файл настроек процессов повреждён."); }
        catch (JsonException ex) { throw new InvalidDataException("Настройки процессов повреждены; изменения остановлены.", ex); }
    }
    private static void Write<T>(string path, List<T> data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temp, JsonSerializer.Serialize(data)); File.Move(temp, path, true); }
        finally { try { File.Delete(temp); } catch (IOException) {} }
    }
    internal static void Change(ProcessIdentity identity, ProcessPriorityClass priority, long affinity)
    {
        if (priority is not (ProcessPriorityClass.Idle or ProcessPriorityClass.BelowNormal or ProcessPriorityClass.Normal or ProcessPriorityClass.AboveNormal or ProcessPriorityClass.High))
            throw new ArgumentException("Недопустимый приоритет.");
        if (affinity == 0) throw new ArgumentException("Нужно выбрать хотя бы одно ядро.");
        if (Environment.ProcessorCount > 64) throw new NotSupportedException("На компьютерах с несколькими группами процессоров изменение ядер пока не поддерживается.");
        using var p = OpenChecked(identity);
        var backups = Read<ProcessBackup>(BackupPath);
        var beforePriority = p.PriorityClass; var beforeAffinity = p.ProcessorAffinity.ToInt64();
        int index = backups.FindIndex(b => Same(b.Identity, identity));
        ProcessBackup backup;
        if (index >= 0) {
            backup = backups[index];
            if (!MatchesKnown(backup, beforePriority, beforeAffinity)) throw new InvalidOperationException("Настройки процесса изменились вне CacheTrim; операция отменена.");
            backup = backup with { AppliedPriority = priority, AppliedAffinity = affinity }; backups[index] = backup;
        } else { backup = new(identity, beforePriority, beforeAffinity, priority, affinity); backups.Add(backup); }
        // Persist original and intended values before touching the process.
        Write(BackupPath, backups);
        try { p.ProcessorAffinity = new IntPtr(affinity); p.PriorityClass = priority; }
        catch { try { p.ProcessorAffinity = new IntPtr(beforeAffinity); p.PriorityClass = beforePriority; } catch { } throw; }
    }
    internal static bool MatchesKnown(ProcessBackup backup, ProcessPriorityClass priority, long affinity) =>
        (backup.Priority == priority && backup.Affinity == affinity) || (backup.AppliedPriority == priority && backup.AppliedAffinity == affinity);
    internal static void Restore(ProcessIdentity identity)
    {
        using var p = OpenChecked(identity);
        var backups = Read<ProcessBackup>(BackupPath); var backup = backups.LastOrDefault(b => Same(b.Identity, identity))
            ?? throw new InvalidOperationException("Сохранённое исходное состояние этого процесса не найдено.");
        var beforePriority = p.PriorityClass; var beforeAffinity = p.ProcessorAffinity.ToInt64();
        if (!MatchesKnown(backup, beforePriority, beforeAffinity)) throw new InvalidOperationException("Настройки процесса изменились вне CacheTrim; откат отменён, чтобы не перезаписать их.");
        try { p.ProcessorAffinity = new IntPtr(backup.Affinity); p.PriorityClass = backup.Priority; }
        catch { try { p.ProcessorAffinity = new IntPtr(beforeAffinity); p.PriorityClass = beforePriority; } catch { } throw; }
        backups.RemoveAll(b => Same(b.Identity, identity)); Write(BackupPath, backups);
    }
    internal static void SaveProfile(ProcessIdentity identity)
    {
        using var p = OpenChecked(identity); var profiles = Read<ProcessProfile>(ProfilePath);
        profiles.RemoveAll(x => string.Equals(x.Path, identity.Path, StringComparison.OrdinalIgnoreCase));
        profiles.Add(new(identity.Path, p.PriorityClass, p.ProcessorAffinity.ToInt64())); Write(ProfilePath, profiles);
    }
    internal static void ApplyProfile(ProcessIdentity identity)
    {
        var profile = Read<ProcessProfile>(ProfilePath).LastOrDefault(x => string.Equals(x.Path, identity.Path, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("Профиль для этого EXE не найден.");
        Change(identity, profile.Priority, profile.Affinity);
    }
}
