using System;
using System.Linq;
using System.Threading;

namespace DesktopPet;

// One explicitly requested short command; processing is local and never uploaded.
internal sealed class VoiceCommandSession : IDisposable
{
    private readonly OfflineKeywordSession _session;
    private readonly Action<string?, float, string?> _completed;
    private Timer? _timeout;
    private int _finished;
    public VoiceCommandSession(Action<string?, float, string?> completed)
    {
        _completed = completed;
        _session = new OfflineKeywordSession(VoiceCommandCatalog.Phrases, text => VoiceCommandCatalog.Parse(Normalize(text)) != PetVoiceCommand.None,
            (ok, text) =>
            {
                if (Interlocked.Exchange(ref _finished, 1) != 0) return;
                _completed(ok ? Normalize(text) : null, ok ? 1 : 0, ok ? null : "Микрофон или локальное распознавание остановились.");
            });
    }
    private static string Normalize(string? text) => string.Join(" ", (text ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(x => x != "[unk]"));
    public void Start()
    {
        _session.Start();
        _timeout = new Timer(_ =>
        {
            if (Interlocked.Exchange(ref _finished, 1) == 0) _completed(null, 0, "Команда не распознана за 8 секунд.");
        }, null, TimeSpan.FromSeconds(8), Timeout.InfiniteTimeSpan);
    }
    public void Dispose()
    {
        Interlocked.Exchange(ref _finished, 1);
        _timeout?.Dispose();
        _session.Dispose();
    }
}
