using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace CacheTrim
{
    // Separate settings: corruption must stop cleanup rather than silently discard exclusions.
    internal sealed class CleanupOptions
    {
        public List<string> ExcludedPaths { get; set; } = new List<string>();
        public Dictionary<string, int> MinimumAgeHours { get; set; } = new Dictionary<string, int>();
        public List<string> Browsers { get; set; } = new List<string> { "Chrome", "Edge", "Brave", "Vivaldi", "Chromium", "Yandex", "Opera" };
        public static string PathName => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CacheTrim", "cleanup-options.json");
        public static readonly string[] BrowserNames = { "Chrome", "Edge", "Brave", "Vivaldi", "Chromium", "Yandex", "Opera" };
        public static readonly string[] Categories = { "temp", "browsers", "shaders", "thumbnails", "reports", "dumps", "updates", "uwp", "prefetch", "logs" };

        public static CleanupOptions Load() => LoadFromPath(PathName);

        internal static CleanupOptions LoadFromPath(string path)
        {
            try
            {
                string json;
                try { json = File.ReadAllText(path); }
                catch (FileNotFoundException) { return new CleanupOptions(); }
                catch (DirectoryNotFoundException) { return new CleanupOptions(); }
                using var document = JsonDocument.Parse(json);
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                    throw new InvalidDataException("Настройки очистки должны быть объектом.");
                foreach (string field in new[] { "ExcludedPaths", "MinimumAgeHours", "Browsers" })
                {
                    if (document.RootElement.EnumerateObject().Count(p => p.NameEquals(field)) != 1)
                        throw new InvalidDataException("Отсутствует или повторяется поле настроек: " + field);
                }
                var options = JsonSerializer.Deserialize<CleanupOptions>(json);
                if (options == null || options.ExcludedPaths == null || options.MinimumAgeHours == null || options.Browsers == null)
                    throw new InvalidDataException("Настройки очистки неполные.");
                options.Validate();
                return options;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is JsonException || ex is ArgumentException || ex is NotSupportedException)
            {
                throw new InvalidDataException("Не удалось прочитать правила очистки. Очистка остановлена. Сохрани копию и восстанови файл " + path + ": " + ex.Message, ex);
            }
        }

        public void Validate()
        {
            if (ExcludedPaths == null || MinimumAgeHours == null || Browsers == null) throw new ArgumentException("Настройки не заполнены.");
            ExcludedPaths = ExcludedPaths.Select(NormalizePath).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (MinimumAgeHours.Any(x => !Categories.Contains(x.Key) || x.Value < 0 || x.Value > 87600))
                throw new ArgumentException("Возраст файлов должен быть от 0 до 87600 часов.");
            if (Browsers.Any(x => !BrowserNames.Contains(x))) throw new ArgumentException("Неизвестный браузер.");
        }

        public void Save()
        {
            Validate();
            Directory.CreateDirectory(Path.GetDirectoryName(PathName));
            string temp = PathName + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                File.WriteAllText(temp, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
                if (File.Exists(PathName)) File.Replace(temp, PathName, null); else File.Move(temp, PathName);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }

        public int AgeFor(string category) => MinimumAgeHours.TryGetValue(category, out int hours) ? hours : 0;
        public bool IncludesBrowser(string browser) => Browsers.Contains(browser);
        public bool IsExcluded(string path)
        {
            string value = NormalizePath(path);
            foreach (string exclude in ExcludedPaths)
            {
                if (string.Equals(value, exclude, StringComparison.OrdinalIgnoreCase)) return true;
                string prefix = exclude.EndsWith(Path.DirectorySeparatorChar) ? exclude : exclude + Path.DirectorySeparatorChar;
                if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }
        private static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
                throw new ArgumentException("Укажи полный путь к файлу или папке: " + path);
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        }
    }
}
