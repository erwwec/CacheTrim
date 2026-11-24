using DesktopPet;
var lyrics = LyricsTimeline.Parse("[00:01.10] first\n[00:03.50][00:05.500] repeat\n[00:06.00] \n[00:08.00] last\n[00:88.00] bad\n[ar:artist]");
void Check(bool value, string message) { if (!value) throw new Exception(message); Console.WriteLine("OK: " + message); }
Check(lyrics.Count == 5, "parse timestamps, duplicate times and blank line");
Check(LyricsTimeline.At(lyrics, TimeSpan.FromSeconds(1.09)) == "", "nothing before first line");
Check(LyricsTimeline.At(lyrics, TimeSpan.FromSeconds(1.1)) == "first", "line starts at timestamp");
Check(LyricsTimeline.At(lyrics, TimeSpan.FromSeconds(4)) == "repeat", "line remains until next timestamp");
Check(LyricsTimeline.At(lyrics, TimeSpan.FromSeconds(6)) == "", "blank line clears text");
Check(LyricsTimeline.At(lyrics, TimeSpan.FromSeconds(8)) == "last", "seek uses current position");

Check(LyricsTimeline.Position(TimeSpan.FromSeconds(10), TimeSpan.Zero, TimeSpan.FromSeconds(120), TimeSpan.FromSeconds(30), 1) == TimeSpan.FromSeconds(40), "interpolate old timeline");
Check(LyricsTimeline.Position(TimeSpan.FromSeconds(10), TimeSpan.Zero, TimeSpan.FromSeconds(120), TimeSpan.FromSeconds(10), 2) == TimeSpan.FromSeconds(30), "account for playback speed");
Check(LyricsTimeline.Position(TimeSpan.FromSeconds(10), TimeSpan.Zero, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(30), 1) == TimeSpan.FromSeconds(15), "clamp to track end");

var now = new DateTimeOffset(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);
Check(LyricsRetry.Deadline(now, TimeSpan.FromMinutes(12), null) >= now.AddMinutes(12), "server Retry-After delay is honored");
Check(LyricsRetry.Deadline(now, null, now.AddHours(1)) >= now.AddHours(1), "server Retry-After date is honored");
Check(LyricsRetry.Deadline(now, TimeSpan.FromSeconds(1), null) >= now.AddMinutes(5), "short retry is slowed down");
Check(LyricsRetry.Deadline(now, TimeSpan.MaxValue, null) == DateTimeOffset.MaxValue, "extreme retry does not overflow");

Check(MediaSessionSelection.Choose(new[] { "chrome.exe", "Spotify.exe" }, "chrome.exe") == 1, "playing Spotify takes precedence over other players");
Check(MediaSessionSelection.Choose(new[] { "chrome.exe", "SpotifyAB.SpotifyMusic_abc!App" }, "chrome.exe") == 1, "Spotify Store session is recognized");
Check(MediaSessionSelection.Choose(new[] { "chrome.exe", "other.exe" }, "other.exe") == 1, "otherwise prefer Windows current session");
Check(MediaSessionSelection.Choose(new[] { "chrome.exe", "other.exe" }, null) == 0, "otherwise use first playing session");
Check(MediaSessionSelection.Choose(Array.Empty<string>(), null) == -1, "no playing session is handled");
