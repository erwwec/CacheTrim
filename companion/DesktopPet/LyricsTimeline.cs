using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace DesktopPet;

internal static class LyricsTimeline
{
    private static readonly Regex Stamp = new(@"\[(\d{1,3}):(\d{2})(?:[.:](\d{1,3}))?\]", RegexOptions.Compiled);
    internal static IReadOnlyList<(TimeSpan At, string Text)> Parse(string lrc)
    {
        var result = new List<(TimeSpan At, string Text)>();
        foreach (string line in (lrc ?? "").Split('\n'))
        {
            var matches = Stamp.Matches(line);
            if (matches.Count == 0) continue;
            string text = Stamp.Replace(line, "").Trim();
            foreach (Match match in matches)
            {
                int minutes = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                int seconds = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
                if (seconds >= 60) continue;
                string fraction = match.Groups[3].Value;
                int milliseconds = fraction.Length == 0 ? 0 : int.Parse(fraction.PadRight(3, '0'), CultureInfo.InvariantCulture);
                result.Add((TimeSpan.FromMilliseconds(minutes * 60000L + seconds * 1000L + milliseconds), text));
            }
        }
        return result.OrderBy(x => x.At).ToList();
    }
    internal static TimeSpan Position(TimeSpan current, TimeSpan start, TimeSpan end, TimeSpan elapsed, double rate)
    {
        double duration = Math.Max(0, (end - start).TotalSeconds);
        double position = Math.Max(0, (current - start).TotalSeconds);
        if (elapsed > TimeSpan.Zero && elapsed < TimeSpan.FromDays(1) && double.IsFinite(rate) && rate > 0 && rate <= 8)
            position += elapsed.TotalSeconds * rate;
        if (duration > 0) position = Math.Min(position, duration);
        return TimeSpan.FromSeconds(position);
    }
    internal static string At(IReadOnlyList<(TimeSpan At, string Text)> lines, TimeSpan position)
    {
        string current = "";
        foreach (var line in lines) { if (line.At > position) break; current = line.Text; }
        return current;
    }
}
