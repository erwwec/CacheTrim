using System;
using System.IO;
using System.Security.Cryptography;

namespace CacheTrim;

// A user-supplied closed-source executable. The pinned hash proves byte identity,
// not trustworthiness or permission to run it without user consent.
internal static class AnimeAvatarPackage
{
    private const string Expected = "A3256EA1B9476354A32BFA8C592741A98E59682821EE7F0AD20CB5BD87C69D74";
    private const long ExpectedLength = 27351552;

    public static string EnsureExtracted()
    {
        string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CacheTrim", "Companion");
        Directory.CreateDirectory(dir);
        string target = Path.Combine(dir, "AnimeAvatar.exe");
        if (File.Exists(target) && FileMatches(target, Expected, ExpectedLength)) return target;
        var assembly = typeof(AnimeAvatarPackage).Assembly;
        using Stream first = assembly.GetManifestResourceStream("CacheTrim.AnimeAvatar.part00") ?? throw new InvalidDataException("В обновлении нет первой части аниме-аватара.");
        using Stream second = assembly.GetManifestResourceStream("CacheTrim.AnimeAvatar.part01") ?? throw new InvalidDataException("В обновлении нет второй части аниме-аватара.");
        Extract(new[] { first, second }, target, Expected, ExpectedLength);
        return target;
    }

    internal static bool FileMatches(string path, string expectedHash, long expectedLength)
    {
        using FileStream file = File.OpenRead(path);
        if (file.Length != expectedLength || file.ReadByte() != 'M' || file.ReadByte() != 'Z') return false;
        file.Position = 0;
        return string.Equals(Convert.ToHexString(SHA256.HashData(file)), expectedHash, StringComparison.OrdinalIgnoreCase);
    }

    internal static void Extract(Stream[] parts, string target, string expectedHash, long expectedLength)
    {
        string staged = target + "." + Guid.NewGuid().ToString("N") + ".download";
        try
        {
            using (FileStream output = new(staged, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                byte[] buffer = new byte[1024 * 256];
                long total = 0;
                foreach (Stream part in parts)
                {
                    int read;
                    while ((read = part.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        total += read;
                        if (total > expectedLength) throw new InvalidDataException("Размер аниме-аватара не совпадает.");
                        hash.AppendData(buffer, 0, read);
                        output.Write(buffer, 0, read);
                    }
                }
                output.Flush(true);
                if (total != expectedLength || !string.Equals(Convert.ToHexString(hash.GetHashAndReset()), expectedHash, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Проверка целостности аниме-аватара не прошла.");
            }
            if (!FileMatches(staged, expectedHash, expectedLength)) throw new InvalidDataException("Файл аниме-аватара повреждён.");
            File.Move(staged, target, true);
        }
        finally { try { if (File.Exists(staged)) File.Delete(staged); } catch { } }
    }
}
