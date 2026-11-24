using System;
namespace DesktopPet;
internal static class LyricsRetry
{
    // Preserve the server's Retry-After, even when the track changes or the feature is toggled.
    internal static DateTimeOffset Deadline(DateTimeOffset now, TimeSpan? delta, DateTimeOffset? date)
    {
        var minimum = TimeSpan.FromMinutes(5);
        var wait = delta ?? (date.HasValue ? date.Value - now : minimum);
        if (wait < minimum) wait = minimum;
        // Add jitter without risking overflow for pathological response headers.
        var available = DateTimeOffset.MaxValue - now;
        if (wait >= available) return DateTimeOffset.MaxValue;
        var jitter = TimeSpan.FromSeconds(Math.Min(wait.TotalSeconds * 0.3 * Random.Shared.NextDouble(), 30));
        if (wait >= available - jitter) return DateTimeOffset.MaxValue;
        return now + wait + jitter;
    }
}
