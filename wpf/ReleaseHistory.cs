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

    internal sealed record ReleaseEntry(string Version, string Notes, DateTimeOffset? PublishedAt);
    internal sealed record HistoryResult(IReadOnlyList<ReleaseEntry> Releases, bool Complete);

    // Only numbered, public, stable GitHub releases are displayed. Never execute
    // markup or links from a release description; it is untrusted text.
    internal static List<ReleaseEntry> ReadPage(JsonElement root)
    {
        var result = new List<ReleaseEntry>();
        foreach (var item in root.EnumerateArray())
        {
            if ((item.TryGetProperty("draft", out var draft) && draft.GetBoolean()) ||
                (item.TryGetProperty("prerelease", out var pre) && pre.GetBoolean())) continue;
            string tag = item.GetProperty("tag_name").GetString();
            if (ParseVersion(tag) == null) continue;
            string notes = item.TryGetProperty("body", out var body) && body.ValueKind == JsonValueKind.String ? body.GetString() : "";
            DateTimeOffset? published = item.TryGetProperty("published_at", out var date) && date.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(date.GetString(), out var parsed) ? parsed : null;
            result.Add(new ReleaseEntry(tag, notes ?? "", published));
        }
        return result;
    }

    internal static IReadOnlyList<ReleaseEntry> SortReleases(IEnumerable<ReleaseEntry> releases) =>
        releases.OrderByDescending(r => ParseVersion(r.Version)).ToList();

    internal static async Task<HistoryResult> FetchAllAsync(string api, string userAgentVersion, CancellationToken cancellationToken)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("CacheTrim/" + userAgentVersion);
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        var releases = new List<ReleaseEntry>();
        string endpoint = api.EndsWith("/latest", StringComparison.OrdinalIgnoreCase) ? api.Substring(0, api.Length - 7) : api;
        for (int page = 1; page <= 10; page++)
        {
            using var response = await client.GetAsync(endpoint + "?per_page=100&page=" + page, cancellationToken);
            response.EnsureSuccessStatusCode();
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            int count = json.RootElement.GetArrayLength();
            releases.AddRange(ReadPage(json.RootElement));
            if (count < 100) return new HistoryResult(SortReleases(releases), true);
        }
        return new HistoryResult(SortReleases(releases), false);
    }

    internal static async Task<string> FetchAsync(string api, string previous, string current, CancellationToken cancellationToken)
    {
        if (ParseVersion(previous) == null || ParseVersion(current) == null) return null;
        HistoryResult history = await FetchAllAsync(api, current, cancellationToken);
        // Do not claim a complete history if pagination was truncated.
        return history.Complete ? Format(history.Releases.Select(r => (r.Version, r.Notes)), previous, current) : null;
    }
}
