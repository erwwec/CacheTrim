using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CacheTrim;

internal static class ReleaseHistory
{
    private static Version ParseVersion(string value)
    {
        if (!Version.TryParse(value?.TrimStart('v', 'V'), out var v)) return null;
        return new Version(v.Major, v.Minor, Math.Max(0, v.Build), Math.Max(0, v.Revision));
    }

    internal static string Format(IEnumerable<(string Version, string Notes)> releases, string previous, string current)
    {
        Version old = ParseVersion(previous), now = ParseVersion(current);
        if (old == null || now == null || old >= now) return null;
        var selected = releases.Select(r => (Number: ParseVersion(r.Version), r.Notes))
            .Where(r => r.Number != null && r.Number > old && r.Number <= now)
            .OrderByDescending(r => r.Number).ToList();
        if (!selected.Any(r => r.Number == now)) return null;
        return string.Join("\n\n", selected.Select(r =>
            "Версия " + (r.Number.Revision == 0 ? r.Number.ToString(3) : r.Number.ToString(4)) + ":\n" +
            (string.IsNullOrWhiteSpace(r.Notes) ? "Подробное описание этого релиза отсутствует." : r.Notes.Trim())));
    }

    internal static async Task<string> FetchAsync(string api, string previous, string current, CancellationToken cancellationToken)
    {
        Version old = ParseVersion(previous);
        if (old == null || ParseVersion(current) == null) return null;
        using var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("CacheTrim/" + current);
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        var releases = new List<(string Version, string Notes)>();
        string endpoint = api.EndsWith("/latest", StringComparison.OrdinalIgnoreCase) ? api.Substring(0, api.Length - 7) : api;
        for (int page = 1; page <= 10; page++)
        {
            using var response = await client.GetAsync(endpoint + "?per_page=100&page=" + page, cancellationToken);
            response.EnsureSuccessStatusCode();
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            int count = 0;
            foreach (var item in json.RootElement.EnumerateArray())
            {
                count++;
                if ((item.TryGetProperty("draft", out var draft) && draft.GetBoolean()) ||
                    (item.TryGetProperty("prerelease", out var pre) && pre.GetBoolean())) continue;
                string tag = item.GetProperty("tag_name").GetString();
                Version number = ParseVersion(tag);
                if (number == null) continue;
                releases.Add((tag, item.TryGetProperty("body", out var body) && body.ValueKind == JsonValueKind.String ? body.GetString() : ""));
            }
            if (count < 100) return Format(releases, previous, current);
        }
        return null; // Do not claim a complete history if pagination was truncated.
    }
}
