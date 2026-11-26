using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CacheTrim;
internal static class ResidualSearch
{
    internal sealed record Results(List<string> Paths, int UnavailableRoots, bool Truncated);
    internal static Results Find(string term, IEnumerable<string> roots, int limit = 200)
    {
        term = (term ?? "").Trim();
        if (term.Length < 4 || term.Length > 80 || term.IndexOfAny(new[] { '\r', '\n', '\0', '/', '\\' }) >= 0) throw new ArgumentException("Введи часть названия программы: 4–80 символов без пути.");
        if (limit < 1 || limit > 1000) throw new ArgumentOutOfRangeException(nameof(limit));
        var found = new List<string>(); int unavailable = 0; bool truncated = false;
        foreach (var root in roots.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                if (!Directory.Exists(root)) { unavailable++; continue; }
                foreach (var path in Directory.EnumerateDirectories(root))
                {
                    if (!Path.GetFileName(path).Contains(term, StringComparison.OrdinalIgnoreCase)) continue;
                    if (found.Count == limit) { truncated = true; break; }
                    found.Add(path);
                }
                if (truncated) break;
            }
            catch (IOException) { unavailable++; }
            catch (UnauthorizedAccessException) { unavailable++; }
        }
        return new Results(found, unavailable, truncated);
    }
    internal static string[] DefaultRoots() => new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) };
}
