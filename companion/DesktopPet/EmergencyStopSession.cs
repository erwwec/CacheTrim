using System;

namespace DesktopPet;

// The stop listener is active only while the explicit PC task is running.
internal sealed class EmergencyStopSession : IDisposable
{
    private readonly OfflineKeywordSession _session;
    internal EmergencyStopSession(Action<bool> result) => _session = new OfflineKeywordSession(EmergencyStopPhrase.Phrases, EmergencyStopPhrase.MatchesOffline, result);
    internal void Start() => _session.Start();
    public void Dispose() => _session.Dispose();
}
