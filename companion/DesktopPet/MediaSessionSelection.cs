using System;
using System.Collections.Generic;

namespace DesktopPet;

internal static class MediaSessionSelection
{
    // Prefer the playing Spotify desktop session when several applications play at once.
    // Browser Spotify sessions identify the browser, so they follow the normal Windows choice.
    public static int Choose(IReadOnlyList<string> playingSources, string? currentSource)
    {
        for (int i = 0; i < playingSources.Count; i++)
            if (playingSources[i].IndexOf("spotify", StringComparison.OrdinalIgnoreCase) >= 0) return i;
        for (int i = 0; i < playingSources.Count; i++)
            if (string.Equals(playingSources[i], currentSource, StringComparison.OrdinalIgnoreCase)) return i;
        return playingSources.Count > 0 ? 0 : -1;
    }
}
