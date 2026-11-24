using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;

namespace CacheTrim;

internal enum ReviewMode { Largest, Duplicates, Installers, Captures }
internal sealed record ReviewItem(string Path, long Size, string Description, bool IsFolder = false)
{
    public override string ToString() => Cleaner.FormatBytes(Size) + "  |  " + Description + "  |  " + Path;
}
internal sealed record ReviewResult(List<ReviewItem> Items, int Examined, int Skipped, bool Truncated);
internal static class FileReview
{
    private sealed record ScannedFile(FileInfo Info, long Length, DateTime LastWriteTimeUtc);
    private const int MaxFiles = 100000;
    private const int MaxResults = 2000;
    private static readonly HashSet<string> Media = new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".webp", ".bmp", ".gif", ".mp4", ".mkv", ".mov", ".webm", ".wmv", ".avi" };

    internal static ReviewResult Scan(string root, ReviewMode mode, CancellationToken token)
    {
        root = Path.GetFullPath(root);
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException("Выбранная папка не найдена.");
        for (var parent = new DirectoryInfo(root); parent != null; parent = parent.Parent)
            if ((parent.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("Путь проходит через ссылку или точку подключения. Выбери обычную папку.");
        var all = new List<ScannedFile>(); var stack = new Stack<string>(); stack.Push(root);
        int skipped = 0; bool truncated = false;
        while (stack.Count > 0 && !truncated)
        {
            token.ThrowIfCancellationRequested();
            string folder = stack.Pop();
            try
            {
                foreach (string file in Directory.EnumerateFiles(folder))
                {
                    token.ThrowIfCancellationRequested();
                    try { var info = new FileInfo(file); if ((info.Attributes & FileAttributes.ReparsePoint) != 0) { skipped++; continue; } all.Add(new ScannedFile(info, info.Length, info.LastWriteTimeUtc)); }
                    catch (IOException) { skipped++; } catch (UnauthorizedAccessException) { skipped++; }
                    if (all.Count >= MaxFiles) { truncated = true; break; }
                }
                if (!truncated) foreach (string sub in Directory.EnumerateDirectories(folder))
                {
                    token.ThrowIfCancellationRequested();
                    try { if ((File.GetAttributes(sub) & FileAttributes.ReparsePoint) == 0) stack.Push(sub); else skipped++; }
                    catch (IOException) { skipped++; } catch (UnauthorizedAccessException) { skipped++; }
                }
            }
            catch (IOException) { skipped++; } catch (UnauthorizedAccessException) { skipped++; }
        }
        var items = new List<ReviewItem>();
        if (mode == ReviewMode.Duplicates)
        {
            var groups = all.Where(x => x.Length > 0).GroupBy(x => x.Length).Where(g => g.Count() > 1);
            foreach (var sizeGroup in groups)
            {
                var hashes = new Dictionary<string, List<ScannedFile>>(StringComparer.Ordinal);
                foreach (var file in sizeGroup)
                {
                    token.ThrowIfCancellationRequested();
                    try
                    {
                        long beforeSize = file.Length; DateTime beforeTime = file.LastWriteTimeUtc;
                        using var stream = new FileStream(file.Info.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);
                        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                        byte[] buffer = new byte[1024 * 256]; int read;
                        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0) { token.ThrowIfCancellationRequested(); hash.AppendData(buffer, 0, read); }
                        file.Info.Refresh();
                        if (!file.Info.Exists || file.Info.Length != beforeSize || file.Info.LastWriteTimeUtc != beforeTime) { skipped++; continue; }
                        string digest = Convert.ToHexString(hash.GetHashAndReset());
                        if (!hashes.TryGetValue(digest, out var list)) hashes[digest] = list = new List<ScannedFile>();
                        list.Add(file);
                    }
                    catch (IOException) { skipped++; } catch (UnauthorizedAccessException) { skipped++; }
                }
                foreach (var pair in hashes.Where(x => x.Value.Count > 1))
                    foreach (var file in pair.Value) items.Add(new ReviewItem(file.Info.FullName, file.Length, "SHA-256: " + pair.Key[..12]));
            }
        }
        else
        {
            foreach (var file in all)
            {
                token.ThrowIfCancellationRequested();
                bool include = mode switch {
                    ReviewMode.Largest => true,
                    ReviewMode.Captures => Media.Contains(file.Info.Extension),
                    ReviewMode.Installers => IsInstaller(file.Info) && file.LastWriteTimeUtc < DateTime.UtcNow.AddDays(-30),
                    _ => false
                };
                if (include) items.Add(new ReviewItem(file.Info.FullName, file.Length, mode == ReviewMode.Installers ? "возможный установщик, старше 30 дней" : "файл"));
            }
        }
        if (mode == ReviewMode.Largest)
        {
            // Totals for the immediate subfolders, to avoid double-counting nested folders.
            var folderSizes = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in all)
            {
                string relative = Path.GetRelativePath(root, file.Info.FullName);
                int separator = relative.IndexOf(Path.DirectorySeparatorChar);
                if (separator <= 0) continue;
                string folder = Path.Combine(root, relative.Substring(0, separator));
                folderSizes[folder] = folderSizes.GetValueOrDefault(folder) + file.Length;
            }
            foreach (var folder in folderSizes) items.Add(new ReviewItem(folder.Key, folder.Value, "папка (с учётом вложенных файлов)", true));
        }
        items = items.OrderByDescending(x => x.Size).ThenBy(x => x.Path, StringComparer.OrdinalIgnoreCase).ToList();
        if (items.Count > MaxResults) { items = items.Take(MaxResults).ToList(); truncated = true; }
        return new ReviewResult(items, all.Count, skipped, truncated);
    }
    private static bool IsInstaller(FileInfo file)
    {
        string extension = file.Extension.ToLowerInvariant();
        if (extension == ".msi" || extension == ".msix" || extension == ".msixbundle") return true;
        if (extension != ".exe") return false;
        string name = file.Name;
        return name.Contains("setup", StringComparison.OrdinalIgnoreCase) || name.Contains("install", StringComparison.OrdinalIgnoreCase);
    }
}
