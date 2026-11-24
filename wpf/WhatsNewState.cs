using System;
using System.Diagnostics;
using System.IO;

namespace CacheTrim;

// Older updaters restart without --updated-from, but keep a .previous backup.
internal static class WhatsNewState
{
    private static string MarkerPath => Path.Combine(Config.ConfigDir, "whats-new-shown.txt");

    internal static string FindLegacyPreviousVersion(string executablePath, string currentVersion) =>
        FindLegacyPreviousVersion(executablePath, currentVersion, path => FileVersionInfo.GetVersionInfo(path).FileVersion);

    internal static string FindLegacyPreviousVersion(string executablePath, string currentVersion, Func<string, string> readVersion)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath + ".previous")) return null;
            if (File.Exists(MarkerPath) && File.ReadAllText(MarkerPath).Trim() == currentVersion) return null;
            string previous = readVersion(executablePath + ".previous");
            return IsOlderVersion(previous, currentVersion) ? previous : null;
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }

    internal static bool IsOlderVersion(string previous, string current) =>
        Version.TryParse(previous, out Version oldVersion) &&
        Version.TryParse(current, out Version newVersion) && oldVersion < newVersion;

    internal static void MarkShown(string currentVersion)
    {
        try
        {
            Directory.CreateDirectory(Config.ConfigDir);
            File.WriteAllText(MarkerPath, currentVersion);
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            App.Log("не удалось сохранить отметку окна изменений: " + ex.Message);
        }
    }
}
