using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace DesktopPet;

internal sealed record CustomAction(string Type, string Target)
{
    [JsonIgnore] public string DisplayLabel => Type switch { "app" => "▣  Запустить программу", "file" => "▤  Открыть файл", "folder" => "▣  Открыть папку", "url" => "◎  Открыть ссылку", "search" => "⌕  Поиск в интернете", "delay" => "◷  Пауза (сек.)", "sound" => "♫  Проиграть звук", "cmd" => "▸  Команда CMD", "powershell" => "▸  PowerShell", _ => "Действие" };
    public override string ToString() => DisplayLabel + ": " + Target;
}
internal sealed record CustomCommand(string Name, List<string> Phrases, bool Enabled, List<CustomAction> Actions)
{
    public string Id { get; init; } = "";
    public bool Confirm { get; init; } = true;
    public List<string> OptionalPhrases { get; init; } = new();
    public bool ScheduleEnabled { get; init; }
    public string ScheduleTime { get; init; } = "09:00";
    public string ScheduleDays { get; init; } = "daily";
    public override string ToString() => Name + (Enabled ? "" : " (выключена)");
}

// A command matches an exact activation phrase, optionally with one prefix or suffix.
internal static class CustomCommands
{
    internal static string DefaultPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClaudeDesktopPet", "custom-commands.json");
    internal static string ScheduleStatePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClaudeDesktopPet", "custom-command-schedule.json");
    internal static readonly string[] ScheduleOptions = { "daily", "weekdays", "weekends", "monday", "tuesday", "wednesday", "thursday", "friday", "saturday", "sunday" };
    internal static string NormalizePhrase(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "";
        return string.Join(" ", new string(input.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : ' ').ToArray()).Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
    internal static string[] SupportedVoicePhrases(IEnumerable<string> phrases, Func<string, bool> isKnown) => phrases.Where(p => p.Split(' ', StringSplitOptions.RemoveEmptyEntries).All(isKnown)).ToArray();
    private static List<string> NormalizeLines(List<string>? values, int max, bool required)
    {
        if (values == null || values.Count > max || (required && values.Count == 0)) throw new InvalidDataException(required ? "Добавь от 1 до 10 фраз активации." : "Добавь не более 5 необязательных фраз.");
        var result = new List<string>();
        foreach (var value in values)
        {
            if (value == null || value.Length > 80 || value.Any(char.IsControl)) throw new InvalidDataException("Фраза должна быть не длиннее 80 символов и без переносов.");
            string n = NormalizePhrase(value);
            if (n.Length == 0 || result.Contains(n, StringComparer.OrdinalIgnoreCase)) throw new InvalidDataException("Фраза пуста или повторяется.");
            result.Add(n);
        }
        return result;
    }
    internal static IEnumerable<string> Variants(CustomCommand c)
    {
        foreach (string phrase in c.Phrases)
        {
            yield return phrase;
            foreach (string extra in c.OptionalPhrases) { yield return extra + " " + phrase; yield return phrase + " " + extra; }
        }
    }
    private static string LocalFile(string target, string[]? extensions)
    {
        if (target.Length > 1000 || !Path.IsPathFullyQualified(target) || target.StartsWith(@"\\", StringComparison.Ordinal) || (extensions != null && !extensions.Contains(Path.GetExtension(target), StringComparer.OrdinalIgnoreCase))) throw new InvalidDataException("Укажи локальный файл с полным путём и поддерживаемым расширением.");
        string full = Path.GetFullPath(target);
        if (full.StartsWith(@"\\", StringComparison.Ordinal)) throw new InvalidDataException("Сетевые и специальные пути не поддерживаются.");
        return full;
    }
    internal static List<CustomCommand> Validate(IEnumerable<CustomCommand> commands)
    {
        var list = new List<CustomCommand>(); var phrases = new HashSet<string>(StringComparer.OrdinalIgnoreCase); var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in commands)
        {
            if (c == null || string.IsNullOrWhiteSpace(c.Name) || c.Name.Trim().Length > 80 || c.Name.Any(char.IsControl)) throw new InvalidDataException("Укажи название до 80 символов.");
            var normalized = NormalizeLines(c.Phrases, 10, true); var optional = NormalizeLines(c.OptionalPhrases, 5, false);
            string id = string.IsNullOrEmpty(c.Id) ? Guid.NewGuid().ToString("N") : c.Id;
            if (!Guid.TryParseExact(id, "N", out _) || !ids.Add(id)) throw new InvalidDataException("Повторяющийся или неверный идентификатор команды.");
            if (c.ScheduleTime == null || !DateTime.TryParseExact(c.ScheduleTime, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _) || !ScheduleOptions.Contains(c.ScheduleDays)) throw new InvalidDataException("Укажи время расписания в формате ЧЧ:ММ и дни запуска.");
            if (c.Actions == null || c.Actions.Count is < 1 or > 10) throw new InvalidDataException("Добавь от 1 до 10 действий.");
            var actions = new List<CustomAction>();
            foreach (var a in c.Actions)
            {
                if (a == null || string.IsNullOrWhiteSpace(a.Target) || a.Target.Any(char.IsControl)) throw new InvalidDataException("Заполни параметры действия без переносов строк.");
                string target = a.Target.Trim();
                switch (a.Type)
                {
                    case "app": target = LocalFile(target, new[] { ".exe" }); break;
                    case "sound": target = LocalFile(target, new[] { ".wav", ".mp3" }); break;
                    case "file": case "folder": target = LocalFile(target, null); break;
                    case "url":
                        if (target.Length > 2048 || !Uri.TryCreate(target, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) || string.IsNullOrEmpty(uri.Host) || !string.IsNullOrEmpty(uri.UserInfo)) throw new InvalidDataException("Ссылка должна быть обычным адресом http или https без логина и пароля.");
                        target = uri.AbsoluteUri; break;
                    case "delay":
                        if (!int.TryParse(target, NumberStyles.None, CultureInfo.InvariantCulture, out int seconds) || seconds is < 1 or > 3600) throw new InvalidDataException("Пауза: целое число от 1 до 3600 секунд.");
                        target = seconds.ToString(CultureInfo.InvariantCulture); break;
                    case "search": if (target.Length > 300) throw new InvalidDataException("Поисковый запрос слишком длинный."); break;
                    case "cmd": case "powershell": if (target.Length > 2000) throw new InvalidDataException("Команда должна быть не длиннее 2000 символов."); break;
                    default: throw new InvalidDataException("Неизвестный тип действия.");
                }
                actions.Add(new CustomAction(a.Type, target));
            }
            var valid = c with { Name = c.Name.Trim(), Id = id, Phrases = normalized, OptionalPhrases = optional, Actions = actions };
            foreach (string v in Variants(valid))
                if (v.Length > 80 || !phrases.Add(v) || VoiceCommandCatalog.Parse(v) != PetVoiceCommand.None) throw new InvalidDataException("Фраза или её вариант слишком длинный или совпадает с другой командой.");
            list.Add(valid);
            if (list.Count > 50) throw new InvalidDataException("Можно сохранить не более 50 команд.");
        }
        return list;
    }
    internal static CustomCommand? Find(IEnumerable<CustomCommand> commands, string? phrase, float confidence = 1f)
    {
        if (!float.IsFinite(confidence) || confidence < .75f) return null;
        var normalized = NormalizePhrase(phrase);
        return normalized.Length == 0 ? null : commands.FirstOrDefault(c => c.Enabled && Variants(c).Contains(normalized, StringComparer.OrdinalIgnoreCase));
    }
    internal static bool IsDue(CustomCommand c, DateTime localNow, string? lastDate)
    {
        if (!c.Enabled || !c.ScheduleEnabled || !ScheduleOptions.Contains(c.ScheduleDays)) return false;
        if (!DateTime.TryParseExact(c.ScheduleTime, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time) || localNow.Hour != time.Hour || localNow.Minute != time.Minute) return false;
        if (lastDate == localNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)) return false;
        return c.ScheduleDays switch {
            "daily" => true, "weekdays" => localNow.DayOfWeek is >= DayOfWeek.Monday and <= DayOfWeek.Friday,
            "weekends" => localNow.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday,
            _ => string.Equals(c.ScheduleDays, localNow.DayOfWeek.ToString(), StringComparison.OrdinalIgnoreCase)
        };
    }
    internal static List<CustomCommand> Load(string path)
    {
        if (!File.Exists(path)) return new();
        try
        {
            if (new FileInfo(path).Length > 1024 * 1024) throw new InvalidDataException("Файл слишком большой.");
            var stored = JsonSerializer.Deserialize<List<CustomCommand>>(File.ReadAllText(path)) ?? throw new InvalidDataException("Файл пуст.");
            if (stored.Any(c => c != null && c.ScheduleEnabled && string.IsNullOrEmpty(c.Id))) throw new InvalidDataException("У команды с расписанием нет постоянного идентификатора; автозапуск остановлен.");
            return Validate(stored);
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException or ArgumentException or NotSupportedException)
        { throw new InvalidDataException("Не удалось прочитать свои команды. Файл не будет перезаписан: " + ex.Message, ex); }
    }
    internal static void WriteAtomic(string path, string text)
    {
        string dir = Path.GetDirectoryName(path) ?? throw new InvalidOperationException("Не указана папка команд.");
        Directory.CreateDirectory(dir); string temp = Path.Combine(dir, ".commands-" + Guid.NewGuid().ToString("N") + ".tmp");
        try { File.WriteAllText(temp, text); File.Move(temp, path, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    internal static void Save(string path, IEnumerable<CustomCommand> commands)
    {
        if (File.Exists(path)) _ = Load(path);
        WriteAtomic(path, JsonSerializer.Serialize(Validate(commands)));
    }
}

// Record before launch: restart during the same minute cannot launch the same command twice.
internal sealed class CustomScheduleTracker
{
    private readonly string _path;
    private readonly Dictionary<string, string> _last;
    internal CustomScheduleTracker(string path)
    {
        _path = path;
        try
        {
            if (!File.Exists(path)) { _last = new(); return; }
            if (new FileInfo(path).Length > 1024 * 1024) throw new InvalidDataException("Файл состояния расписания слишком большой.");
            _last = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path)) ?? throw new InvalidDataException("Файл состояния расписания пуст.");
            if (_last.Count > 5000 || _last.Any(p => !Guid.TryParseExact(p.Key, "N", out _) || !DateTime.TryParseExact(p.Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))) throw new InvalidDataException("Неверный файл состояния расписания.");
        }
        catch (Exception e) when (e is JsonException or ArgumentException) { throw new InvalidDataException("Не удалось прочитать состояние расписания; автозапуск остановлен.", e); }
    }
    internal bool Due(CustomCommand c, DateTime now) => CustomCommands.IsDue(c, now, _last.GetValueOrDefault(c.Id));
    internal void Mark(CustomCommand c, DateTime now)
    {
        var next = new Dictionary<string, string>(_last) { [c.Id] = now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) };
        // Remove old entries before saving, without clearing today's deduplication.
        foreach (var key in next.Where(p => DateTime.TryParseExact(p.Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) && d < now.Date.AddDays(-45)).Select(p => p.Key).ToArray()) next.Remove(key);
        CustomCommands.WriteAtomic(_path, JsonSerializer.Serialize(next)); _last.Clear(); foreach (var p in next) _last.Add(p.Key, p.Value);
    }
}

internal static class CustomCommandExecutor
{
    internal static async Task RunAsync(IEnumerable<CustomAction> actions, Func<CustomAction, CancellationToken, Task> perform, CancellationToken token)
    {
        foreach (var action in actions) { token.ThrowIfCancellationRequested(); await perform(action, token); }
    }
}
