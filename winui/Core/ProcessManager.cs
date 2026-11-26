using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace CacheTrim;

internal static class ProcessManager
{
    internal sealed record Entry(int Id, string Name, string Path, long StartUtcTicks, int SessionId, long MemoryBytes, string Priority)
    {
        public override string ToString() => $"{Name}  ·  PID {Id}  ·  {MemoryBytes / 1024 / 1024} МиБ  ·  {Priority}  ·  {Path}";
    }
    internal static List<Entry> List()
    {
        var result = new List<Entry>();
        foreach (var p in Process.GetProcesses()) using (p)
        {
            try { result.Add(Snapshot(p)); } catch { /* protected/exited processes are intentionally skipped */ }
        }
        return result.OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase).ThenBy(x => x.Id).ToList();
    }
    private static Entry Snapshot(Process p) => new(p.Id, p.ProcessName, p.MainModule?.FileName ?? throw new InvalidOperationException("Путь процесса недоступен."), p.StartTime.ToUniversalTime().Ticks, p.SessionId, p.WorkingSet64, p.PriorityClass.ToString());
    internal static void Validate(Entry selected, Entry current, int selfId, int ownSession, string windowsDirectory)
    {
        if (selected.Id != current.Id || selected.StartUtcTicks != current.StartUtcTicks || !string.Equals(selected.Path, current.Path, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Процесс изменился после обновления списка. Обнови список и выбери его заново.");
        if (current.Id == selfId || current.Id <= 4 || current.SessionId != ownSession) throw new InvalidOperationException("Этот процесс защищён или находится в другом сеансе.");
        string windows = System.IO.Path.GetFullPath(windowsDirectory).TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar) + System.IO.Path.DirectorySeparatorChar;
        string path = System.IO.Path.GetFullPath(current.Path);
        if (path.StartsWith(windows, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Процессы из папки Windows здесь не изменяются.");
    }
    internal static T WithCheckedProcess<T>(Entry entry, Func<Process, T> action)
    {
        using var current = Process.GetProcessById(entry.Id);
        using var self = Process.GetCurrentProcess();
        Validate(entry, Snapshot(current), self.Id, self.SessionId, Environment.GetFolderPath(Environment.SpecialFolder.Windows));
        return action(current);
    }
    internal static void Stop(Entry entry) => WithCheckedProcess(entry, p => { p.Kill(); return 0; });
    internal static void SetPriority(Entry entry, ProcessPriorityClass priority)
    {
        if (priority != ProcessPriorityClass.BelowNormal && priority != ProcessPriorityClass.Normal && priority != ProcessPriorityClass.AboveNormal)
            throw new ArgumentOutOfRangeException(nameof(priority));
        WithCheckedProcess(entry, p => { p.PriorityClass = priority; return 0; });
    }
}
