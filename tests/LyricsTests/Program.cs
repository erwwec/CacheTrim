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
