using System;
using System.Linq;
using System.Speech.Recognition;

namespace DesktopPet;

// One explicitly requested utterance. No wake word, continuous listening or app-side upload.
internal sealed class VoiceCommandSession : IDisposable
{
    private readonly SpeechRecognitionEngine _engine;
    private readonly Action<string?, float, string?> _completed;
    public VoiceCommandSession(Action<string?, float, string?> completed)
    {
        _completed = completed;
        var recognizer = SpeechRecognitionEngine.InstalledRecognizers()
            .FirstOrDefault(r => r.Culture.TwoLetterISOLanguageName.Equals("ru", StringComparison.OrdinalIgnoreCase));
        if (recognizer == null) throw new InvalidOperationException("Русский модуль распознавания речи SAPI не найден. Проверь установленные модули Windows; можно вводить команды текстом.");
        _engine = new SpeechRecognitionEngine(recognizer);
        try
        {
            var builder = new GrammarBuilder { Culture = recognizer.Culture };
            builder.Append(new Choices(VoiceCommandCatalog.Phrases));
            _engine.LoadGrammar(new Grammar(builder));
            _engine.InitialSilenceTimeout = TimeSpan.FromSeconds(8);
            _engine.BabbleTimeout = TimeSpan.FromSeconds(3);
            _engine.EndSilenceTimeout = TimeSpan.FromSeconds(1);
            _engine.RecognizeCompleted += OnCompleted;
        }
        catch { _engine.Dispose(); throw; }
    }
    public void Start()
    {
        _engine.SetInputToDefaultAudioDevice();
        _engine.RecognizeAsync(RecognizeMode.Single);
    }
    private void OnCompleted(object? sender, RecognizeCompletedEventArgs e)
    {
        if (e.Cancelled) { _completed(null, 0, "Прослушивание остановлено."); return; }
        if (e.Error != null) { _completed(null, 0, "Не удалось распознать команду. Проверь микрофон и разрешения Windows."); return; }
        _completed(e.Result?.Text, e.Result?.Confidence ?? 0, null);
    }
    public void Dispose()
    {
        _engine.RecognizeCompleted -= OnCompleted;
        try { _engine.RecognizeAsyncCancel(); } catch (InvalidOperationException) { }
        _engine.Dispose();
    }
}
