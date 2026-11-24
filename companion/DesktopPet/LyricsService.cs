using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media.Control;

namespace DesktopPet;

// Reads metadata exposed by Windows media controls; never uploads captured audio.
internal sealed class LyricsService : IDisposable
{
    private readonly HttpClient _client = new() { Timeout = TimeSpan.FromSeconds(8), MaxResponseContentBufferSize = 1024 * 1024 };
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private string _song = "";
    private DateTime _retryAfter = DateTime.MinValue;
    private IReadOnlyList<(TimeSpan At, string Text)> _lines = Array.Empty<(TimeSpan, string)>();

    public LyricsService() => _client.DefaultRequestHeaders.UserAgent.ParseAdd("CacheTrimDesktopPet/2.1 (https://github.com/erwwec/CacheTrim)");

    public async Task<string?> CurrentLineAsync(CancellationToken cancellation)
    {
        _manager ??= await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        cancellation.ThrowIfCancellationRequested();
        var session = _manager.GetCurrentSession();
        if (session == null || session.GetPlaybackInfo().PlaybackStatus != GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing) return null;
        var properties = await session.TryGetMediaPropertiesAsync();
        cancellation.ThrowIfCancellationRequested();
        string title = properties?.Title?.Trim() ?? "";
        string artist = properties?.Artist?.Trim() ?? "";
        if (title.Length == 0 || artist.Length == 0) return null;
        var timeline = session.GetTimelineProperties();
        double duration = (timeline.EndTime - timeline.StartTime).TotalSeconds;
        string album = properties?.AlbumTitle?.Trim() ?? "";
        string key = title + "\n" + artist + "\n" + album + "\n" + Math.Round(duration);
        if (_song != key) { _song = key; _lines = Array.Empty<(TimeSpan, string)>(); _retryAfter = DateTime.MinValue; }
        if (_lines.Count == 0 && DateTime.UtcNow >= _retryAfter)
        {
            // Retry missing lyrics and transient network errors sparingly; no requests for every tick.
            _retryAfter = DateTime.UtcNow.AddMinutes(5);
            var query = "https://lrclib.net/api/get?track_name=" + Uri.EscapeDataString(title) + "&artist_name=" + Uri.EscapeDataString(artist);
            if (album.Length > 0) query += "&album_name=" + Uri.EscapeDataString(album);
            if (duration >= 1 && duration <= 3600) query += "&duration=" + ((int)Math.Round(duration)).ToString(System.Globalization.CultureInfo.InvariantCulture);
            using var response = await _client.GetAsync(query, cancellation);
            if (response.StatusCode == HttpStatusCode.NotFound) return null;
            response.EnsureSuccessStatusCode();
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellation));
            if (json.RootElement.TryGetProperty("syncedLyrics", out var lyrics) && lyrics.ValueKind == JsonValueKind.String)
                _lines = LyricsTimeline.Parse(lyrics.GetString() ?? "");
        }
        // Refresh position after network latency rather than using the old pre-request snapshot.
        var current = session.GetTimelineProperties();
        var delta = DateTimeOffset.UtcNow - current.LastUpdatedTime;
        double rate = session.GetPlaybackInfo().PlaybackRate ?? 1.0;
        var position = LyricsTimeline.Position(current.Position, current.StartTime, current.EndTime, delta, rate);
        string line = LyricsTimeline.At(_lines, position);
        return string.IsNullOrWhiteSpace(line) ? null : line;
    }
    public void Dispose() => _client.Dispose();
}
