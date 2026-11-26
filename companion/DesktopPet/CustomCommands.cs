using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace DesktopPet;

internal sealed record CustomAction(string Type, string Target)
{
    public override string ToString() => (Type == "app" ? "Запустить программу: " : "Открыть ссылку: ") + Target;
}
internal sealed record CustomCommand(string Name, List<string> Phrases, bool Enabled, List<CustomAction> Actions)
{
    public override string ToString() => Name + (Enabled ? "" : " (выключена)");
}

// User-defined commands are exact matches only. Data is never interpreted as shell syntax.
internal static class CustomCommands
{
    internal static string DefaultPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClaudeDesktopPet", "custom-commands.json");
    internal static string NormalizePhrase(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "";
        return string.Join(" ", new string(input.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : ' ').ToArray()).Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
    internal static string[] SupportedVoicePhrases(IEnumerable<string> phrases, Func<string, bool> isKnown) =>
        phrases.Where(p => p.Split(' ', StringSplitOptions.RemoveEmptyEntries).All(isKnown)).ToArray();
    internal static List<CustomCommand> Validate(IEnumerable<CustomCommand> commands)
    {
        var list = new List<CustomCommand>(); var phrases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in commands)
        {
            if (c == null || string.IsNullOrWhiteSpace(c.Name) || c.Name.Trim().Length > 80 || c.Name.Any(char.IsControl)) throw new InvalidDataException("Укажи название до 80 символов.");
            if (c.Phrases == null || c.Phrases.Count is < 1 or > 10) throw new InvalidDataException("Добавь от 1 до 10 фраз активации.");
            var normalized = new List<string>();
            foreach (var phrase in c.Phrases)
            {
                if (phrase == null || phrase.Length > 80 || phrase.Any(char.IsControl)) throw new InvalidDataException("Фраза должна быть не длиннее 80 символов и без переносов.");
                string n = NormalizePhrase(phrase);
                if (n.Length == 0 || !phrases.Add(n) || VoiceCommandCatalog.Parse(n) != PetVoiceCommand.None) throw new InvalidDataException("Фраза пуста или уже используется другой командой.");
                normalized.Add(n);
            }
            if (c.Actions == null || c.Actions.Count is < 1 or > 10) throw new InvalidDataException("Добавь от 1 до 10 действий.");
            var actions = new List<CustomAction>();
            foreach (var a in c.Actions)
            {
                if (a == null || string.IsNullOrWhiteSpace(a.Target) || a.Target.Any(char.IsControl)) throw new InvalidDataException("Укажи программу или ссылку.");
                if (a.Type == "app")
                {
                    if (a.Target.Length > 1000 || !Path.IsPathFullyQualified(a.Target) || a.Target.StartsWith(@"\\", StringComparison.Ordinal) || !string.Equals(Path.GetExtension(a.Target), ".exe", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Программа должна быть локальным EXE с полным путём (без аргументов).");
                    actions.Add(new CustomAction("app", Path.GetFullPath(a.Target)));
                }
                else if (a.Type == "url")
                {
                    if (a.Target.Length > 2048 || !Uri.TryCreate(a.Target, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) || string.IsNullOrEmpty(uri.Host) || !string.IsNullOrEmpty(uri.UserInfo)) throw new InvalidDataException("Ссылка должна быть обычным адресом http или https без логина и пароля.");
                    actions.Add(new CustomAction("url", uri.AbsoluteUri));
                }
                else throw new InvalidDataException("Неизвестный тип действия.");
            }
            list.Add(new CustomCommand(c.Name.Trim(), normalized, c.Enabled, actions));
            if (list.Count > 50) throw new InvalidDataException("Можно сохранить не более 50 команд.");
        }
        return list;
    }
    internal static CustomCommand? Find(IEnumerable<CustomCommand> commands, string? phrase, float confidence = 1f)
    {
        if (!float.IsFinite(confidence) || confidence < .75f) return null;
        var normalized = NormalizePhrase(phrase);
        return normalized.Length == 0 ? null : commands.FirstOrDefault(c => c.Enabled && c.Phrases.Contains(normalized, StringComparer.OrdinalIgnoreCase));
    }
    internal static List<CustomCommand> Load(string path)
    {
        if (!File.Exists(path)) return new();
        try
        {
            if (new FileInfo(path).Length > 1024 * 1024) throw new InvalidDataException("Файл слишком большой.");
            return Validate(JsonSerializer.Deserialize<List<CustomCommand>>(File.ReadAllText(path)) ?? throw new InvalidDataException("Файл пуст."));
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException or ArgumentException or NotSupportedException)
        { throw new InvalidDataException("Не удалось прочитать свои команды. Файл не будет перезаписан: " + ex.Message, ex); }
    }
    internal static void Save(string path, IEnumerable<CustomCommand> commands)
    {
        if (File.Exists(path)) _ = Load(path);
        var valid = Validate(commands); string dir = Path.GetDirectoryName(path) ?? throw new InvalidOperationException("Не указана папка команд.");
        Directory.CreateDirectory(dir); string temp = Path.Combine(dir, ".commands-" + Guid.NewGuid().ToString("N") + ".tmp");
        try { File.WriteAllText(temp, JsonSerializer.Serialize(valid)); File.Move(temp, path, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
