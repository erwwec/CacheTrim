using System;

namespace DesktopPet;

// Explicit opt-in. Waiting audio stays local and is not uploaded by this app.
internal sealed class WakeWordSession : IDisposable
{
    private readonly OfflineKeywordSession _session;
    internal WakeWordSession(Action<bool> result) => _session = new OfflineKeywordSession(WakeWordPhrase.Phrases, WakeWordPhrase.MatchesOffline, result);
    internal void Start() => _session.Start();
    public void Dispose() => _session.Dispose();
}
