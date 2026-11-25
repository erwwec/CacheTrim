using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Encodings.Web;
using System.Threading;
using NAudio.Wave;

namespace DesktopPet;

// Only a tiny fixed vocabulary, processed in memory on this PC. No network upload.
internal sealed class OfflineKeywordSession : IDisposable
{
    private readonly object _gate = new();
    private readonly Func<string?, bool> _matches;
    private readonly Action<bool, string?> _callback;
    private readonly VoskNative.ModelHandle _model;
    private readonly VoskNative.RecognizerHandle _recognizer;
    private readonly WaveInEvent _input;
    private int _finished, _disposed;

    internal OfflineKeywordSession(string[] phrases, Func<string?, bool> matches, Action<bool> callback)
        : this(phrases, matches, (matched, _) => callback(matched)) { }

    internal OfflineKeywordSession(string[] phrases, Func<string?, bool> matches, Action<bool, string?> callback)
    {
        _matches = matches; _callback = callback;
        string path = Path.Combine(AppContext.BaseDirectory, "VoiceModel", "vosk-model-small-ru-0.22");
        if (!File.Exists(Path.Combine(path, "am", "final.mdl")))
            throw new InvalidOperationException("Локальная русская модель не найдена. Распакуй весь архив и держи папку VoiceModel рядом с DesktopPet.exe.");
        _model = VoskNative.Load(path);
        try
        {
            // Reject incomplete/corrupted models instead of pretending a stop word can work.
            if (phrases.SelectMany(p => p.Split(' ', StringSplitOptions.RemoveEmptyEntries)).Distinct().Any(w => VoskNative.FindWord(_model, w) < 0))
                throw new InvalidOperationException("В локальной модели отсутствуют нужные слова. Голосовой режим не запущен.");
            string grammar = JsonSerializer.Serialize(phrases.Append("[unk]").ToArray(), new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
            _recognizer = VoskNative.Create(_model, grammar);
            try
            {
                _input = new WaveInEvent { WaveFormat = new WaveFormat(16000, 16, 1), BufferMilliseconds = 100 };
                _input.DataAvailable += OnAudio; _input.RecordingStopped += OnStopped;
            }
            catch { _recognizer.Dispose(); throw; }
        }
        catch { _model.Dispose(); throw; }
    }
    internal void Start() => _input.StartRecording();
    private void OnAudio(object? sender, WaveInEventArgs e)
    {
        bool matched = false, failed = false;
        string? recognized = null;
        lock (_gate)
        {
            if (Volatile.Read(ref _disposed) != 0 || Volatile.Read(ref _finished) != 0) return;
            try
            {
                bool final = VoskNative.Accept(_recognizer, e.Buffer, e.BytesRecorded) != 0;
                using var json = JsonDocument.Parse(VoskNative.Result(_recognizer, final));
                string field = final ? "text" : "partial";
                recognized = json.RootElement.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
                matched = _matches(recognized);
            }
            catch { failed = true; }
        }
        if ((matched || failed) && Interlocked.Exchange(ref _finished, 1) == 0) _callback(matched && !failed, recognized);
    }
    private void OnStopped(object? sender, StoppedEventArgs e)
    {
        if (Volatile.Read(ref _disposed) == 0 && Interlocked.Exchange(ref _finished, 1) == 0) _callback(false, null);
    }
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        Interlocked.Exchange(ref _finished, 1);
        _input.DataAvailable -= OnAudio; _input.RecordingStopped -= OnStopped;
        try { _input.StopRecording(); } catch { }
        try { _input.Dispose(); } catch { }
        lock (_gate) { _recognizer.Dispose(); _model.Dispose(); }
    }
}
