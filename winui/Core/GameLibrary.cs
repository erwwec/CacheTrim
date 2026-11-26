using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
namespace CacheTrim;
internal static class GameLibrary
{
    internal sealed record Game(string Name, string Executable)
    {
        public override string ToString() => Name + "  ·  " + Executable + (File.Exists(Executable) ? "" : "  ·  файл не найден");
    }
    internal static string DefaultPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CacheTrim", "games.json");
    internal static List<Game> Validate(IEnumerable<Game> games)
    {
        var list = new List<Game>(); var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var g in games)
        {
            if (g == null || string.IsNullOrWhiteSpace(g.Name) || g.Name.Trim().Length > 100 || g.Name.IndexOfAny(new[] { '\r', '\n', '\0' }) >= 0 || string.IsNullOrWhiteSpace(g.Executable) || !Path.IsPathFullyQualified(g.Executable) || !string.Equals(Path.GetExtension(g.Executable), ".exe", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Укажи название и полный путь к EXE игры.");
            string path = Path.GetFullPath(g.Executable);
            if (!paths.Add(path)) throw new InvalidDataException("Игра с таким EXE уже добавлена.");
            list.Add(new Game(g.Name.Trim(), path));
            if (list.Count > 200) throw new InvalidDataException("В списке не может быть больше 200 игр.");
        }
        return list.OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }
    internal static List<Game> Load(string path)
    {
        if (!File.Exists(path)) return new List<Game>();
        try { return Validate(JsonSerializer.Deserialize<List<Game>>(File.ReadAllText(path)) ?? throw new InvalidDataException("Список пуст или повреждён.")); }
        catch (Exception ex) when (ex is JsonException || ex is InvalidDataException || ex is ArgumentException) { throw new InvalidDataException("Список игр не удалось прочитать, он не будет перезаписан: " + ex.Message, ex); }
    }
    internal static void Save(string path, IEnumerable<Game> games)
    {
        if (File.Exists(path)) _ = Load(path); // Do not silently overwrite corrupt data.
        var list = Validate(games); string dir = Path.GetDirectoryName(path) ?? throw new InvalidOperationException("Не задана папка списка игр."); Directory.CreateDirectory(dir);
        string temp = Path.Combine(dir, ".games-" + Guid.NewGuid().ToString("N") + ".tmp");
        try { File.WriteAllText(temp, JsonSerializer.Serialize(list)); File.Move(temp, path, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
