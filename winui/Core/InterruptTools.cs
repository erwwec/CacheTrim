using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace CacheTrim;

internal static class InterruptTools
{
    private const string EnumPath = @"SYSTEM\CurrentControlSet\Enum\PCI";
    private const string PolicySuffix = @"\Device Parameters\Interrupt Management\Affinity Policy";
    private static readonly string BackupFile = Path.Combine(SystemToolkit.DataDirectory, "interrupt-backups.json");
    internal sealed record Device(string Instance, string Name, string State) { public override string ToString() => $"{Name} · {Instance} · {State}"; }
    internal sealed record Backup(string Instance, bool KeyExisted, bool ValueExisted, int Original, int Applied);

    internal static IReadOnlyList<Device> ListPci()
    {
        var result = new List<Device>();
        using var root = Registry.LocalMachine.OpenSubKey(EnumPath);
        if (root == null) return result;
        foreach (var deviceName in root.GetSubKeyNames().Take(2000)) {
            using var device = root.OpenSubKey(deviceName); if (device == null) continue;
            foreach (var instance in device.GetSubKeyNames().Take(2000)) {
                try {
                    using var key = device.OpenSubKey(instance); if (key == null) continue;
                    string name = (key.GetValue("FriendlyName") ?? key.GetValue("DeviceDesc") ?? deviceName).ToString()!;
                    if (name.Contains(';')) name = name[(name.LastIndexOf(';') + 1)..];
                    using var affinity = key.OpenSubKey(@"Device Parameters\Interrupt Management\Affinity Policy");
                    object val = affinity?.GetValue("DevicePolicy");
                    result.Add(new(deviceName + "\\" + instance, name, val == null ? "политика не задана" : "DevicePolicy=" + val));
                } catch (System.Security.SecurityException) {} catch (UnauthorizedAccessException) {} catch (IOException) {}
            }
        }
        return result.OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }
    internal static bool ValidInstance(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var parts = value.Split('\\');
        return parts.Length == 2 && parts.All(p => p.Length > 0 && p != "." && p != ".." && !p.Contains('/') && !p.Contains('\0'));
    }
    private static List<Backup> Read(string file)
    {
        if (!File.Exists(file)) return new();
        if (new FileInfo(file).Length > 1_000_000) throw new InvalidDataException("Файл резервных копий прерываний слишком велик.");
        try { return JsonSerializer.Deserialize<List<Backup>>(File.ReadAllText(file)) ?? throw new InvalidDataException("Резерв прерываний повреждён."); }
        catch (JsonException ex) { throw new InvalidDataException("Резерв прерываний повреждён; изменения остановлены.", ex); }
    }
    private static void Write(string file, List<Backup> backups)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file)!); string temp = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temp, JsonSerializer.Serialize(backups)); File.Move(temp, file, true); }
        finally { try { File.Delete(temp); } catch (IOException) {} }
    }
    internal static void Apply(Device device, int policy) => ApplyAt(Registry.LocalMachine, EnumPath, BackupFile, device.Instance, policy);
    internal static void Restore(Device device) => RestoreAt(Registry.LocalMachine, EnumPath, BackupFile, device.Instance);
    internal static void ApplyAt(RegistryKey root, string devicesRoot, string backupPath, string instance, int policy)
    {
        if (!ValidInstance(instance) || policy is not (0 or 1 or 2 or 3 or 5)) throw new ArgumentException("Неверное устройство или политика.");
        // Only an enumerated existing device may be changed; never create device paths.
        using var device = root.OpenSubKey(devicesRoot + "\\" + instance, false) ?? throw new InvalidOperationException("Устройство больше не найдено.");
        string path = devicesRoot + "\\" + instance + PolicySuffix;
        using var existing = root.OpenSubKey(path);
        bool keyExisted = existing != null, valueExisted = existing?.GetValueNames().Contains("DevicePolicy", StringComparer.OrdinalIgnoreCase) == true;
        int original = 0;
        if (valueExisted) {
            if (existing!.GetValueKind("DevicePolicy") != RegistryValueKind.DWord) throw new InvalidDataException("Неожиданный тип DevicePolicy; изменение отменено.");
            original = (int)existing.GetValue("DevicePolicy")!;
        }
        var backups = Read(backupPath); int index = backups.FindIndex(b => string.Equals(b.Instance, instance, StringComparison.OrdinalIgnoreCase));
        if (index >= 0) {
            var old = backups[index];
            bool sameAsApplied = valueExisted && original == old.Applied;
            bool sameAsOriginal = valueExisted == old.ValueExisted && (!valueExisted || original == old.Original);
            if (!sameAsApplied && !sameAsOriginal) throw new InvalidOperationException("Политика устройства изменилась вне CacheTrim; изменение отменено.");
            backups[index] = old with { Applied = policy };
        }
        else backups.Add(new Backup(instance, keyExisted, valueExisted, original, policy));
        // Save a complete, validated backup before touching the registry.
        Write(backupPath, backups);
        using var target = root.CreateSubKey(path, true) ?? throw new UnauthorizedAccessException("Windows запретила изменение политики устройства.");
        target.SetValue("DevicePolicy", policy, RegistryValueKind.DWord); target.Flush();
    }
    internal static void RestoreAt(RegistryKey root, string devicesRoot, string backupPath, string instance)
    {
        if (!ValidInstance(instance)) throw new ArgumentException("Неверное устройство.");
        var backups = Read(backupPath); var backup = backups.LastOrDefault(x => string.Equals(x.Instance, instance, StringComparison.OrdinalIgnoreCase)) ?? throw new InvalidOperationException("Резерв для этого устройства не найден.");
        string path = devicesRoot + "\\" + instance + PolicySuffix;
        using (var key = root.OpenSubKey(path, true)) {
            if (key == null) throw new InvalidOperationException("Ключ устройства отсутствует; восстанови его вручную из резервной копии.");
            if (!key.GetValueNames().Contains("DevicePolicy", StringComparer.OrdinalIgnoreCase) || key.GetValueKind("DevicePolicy") != RegistryValueKind.DWord || (int)key.GetValue("DevicePolicy")! != backup.Applied)
                throw new InvalidOperationException("Политика изменилась вне CacheTrim. Автоматический откат отменён, чтобы не перезаписать чужие изменения.");
            if (backup.ValueExisted) key.SetValue("DevicePolicy", backup.Original, RegistryValueKind.DWord); else key.DeleteValue("DevicePolicy", false);
            key.Flush();
        }
        // Do not delete key or unrelated values; another app may have populated it.
        backups.Remove(backup); Write(backupPath, backups);
    }
}
