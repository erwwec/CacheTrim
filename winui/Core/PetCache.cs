using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace CacheTrim;

internal static class PetCache
{
    // Hashes of previously published DesktopPet.exe releases. Older pets require explicit consent.
    internal static readonly (string Version, string Hash)[] PreviousVersions =
    [
        ("2.1.16", "878bb93fde2f5452e5c77f17a8daf35f9dc205992676df5472fb56d95b70fe45"),
        ("2.1.15", "76de5744780dd950e428a514d3b6c36fc30d407dcd57c815ef1df082738c2dab")
    ];
    internal static string PathToPet => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CacheTrim", "Companion", "DesktopPet.exe");
    internal static bool IsHash(string value) => value != null && value.Length == 64 && value.All(Uri.IsHexDigit);

    // Keep this handle open until after Process.Start so the checked file cannot be swapped on Windows.
    internal static FileStream TryOpenVerified(string path, string expected)
    {
        if (!IsHash(expected)) return null;
        FileStream stream = null;
        try
        {
            stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length < 4096 || stream.Length > 300L * 1024 * 1024 || stream.ReadByte() != 'M' || stream.ReadByte() != 'Z')
            { stream.Dispose(); return null; }
            stream.Position = 0;
            string actual = Convert.ToHexString(SHA256.HashData(stream));
            if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            { stream.Dispose(); return null; }
            stream.Position = 0;
            return stream;
        }
        catch (IOException) { stream?.Dispose(); return null; }
        catch (UnauthorizedAccessException) { stream?.Dispose(); return null; }
    }
}
