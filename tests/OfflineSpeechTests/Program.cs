using DesktopPet;
using System.Text.Json;
using System.Text.Encodings.Web;
static void Check(bool value, string label) { if (!value) throw new Exception(label); Console.WriteLine("OK: " + label); }
if (args.Length != 1 && args.Length != 4) throw new Exception("Pass the Russian model directory and optionally wake/stop/command raw PCM files.");
using var model = VoskNative.Load(args[0]);
foreach (var phrase in WakeWordPhrase.Phrases.Concat(EmergencyStopPhrase.Phrases).Concat(VoiceCommandCatalog.Phrases))
  foreach (string word in phrase.Split(' ', StringSplitOptions.RemoveEmptyEntries))
    Check(VoskNative.FindWord(model, word) >= 0, "model dictionary includes " + word);
Check(VoskNative.FindWord(model, "несуществующеесловоазбука") < 0, "unknown word rejected");
foreach (var phrases in new[] { WakeWordPhrase.Phrases, EmergencyStopPhrase.Phrases, VoiceCommandCatalog.Phrases })
{
    string grammar = JsonSerializer.Serialize(phrases.Append("[unk]").ToArray(), new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
    Check(grammar.Contains(phrases[0], StringComparison.Ordinal), "grammar preserves UTF-8 words");
    using var recognizer = VoskNative.Create(model, grammar);
    var silence = new byte[3200];
    int result = VoskNative.Accept(recognizer, silence, silence.Length);
    using var json = JsonDocument.Parse(VoskNative.Result(recognizer, result != 0));
    Check(json.RootElement.ValueKind == JsonValueKind.Object, "local recognizer accepts PCM silence and returns JSON");
}
Check(WakeWordPhrase.MatchesOffline("[unk] джарвис"), "local wake match");
Check(EmergencyStopPhrase.MatchesOffline("клод стоп"), "local stop match");
if (args.Length == 4)
{
    for (int sample = 0; sample < 3; sample++)
    {
        var phrases = sample == 0 ? WakeWordPhrase.Phrases : sample == 1 ? EmergencyStopPhrase.Phrases : VoiceCommandCatalog.Phrases;
        string grammar = JsonSerializer.Serialize(phrases.Append("[unk]").ToArray(), new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        using var recognizer = VoskNative.Create(model, grammar);
        byte[] pcm = File.ReadAllBytes(args[sample + 1]); bool matched = false; string last = "";
        for (int offset = 0; offset < pcm.Length; offset += 3200)
        {
            byte[] chunk = pcm.Skip(offset).Take(3200).ToArray();
            bool final = VoskNative.Accept(recognizer, chunk, chunk.Length) != 0;
            using var doc = JsonDocument.Parse(VoskNative.Result(recognizer, final));
            last = doc.RootElement.GetProperty(final ? "text" : "partial").GetString() ?? "";
            string normalized = string.Join(" ", last.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(x => x != "[unk]"));
            matched |= sample == 0 ? WakeWordPhrase.MatchesOffline(last) : sample == 1 ? EmergencyStopPhrase.MatchesOffline(last) : VoiceCommandCatalog.Parse(normalized) == PetVoiceCommand.Hello;
        }
        Check(matched, "synthetic speech " + (sample == 0 ? "wake" : sample == 1 ? "stop" : "quick command") + " detected; last=" + last);
    }
}
using (var full = VoskNative.CreateFull(model))
{
    Check(VoskNative.Accept(full, new byte[3200], 3200) >= 0, "unrestricted recognizer accepts audio");
    using var final = JsonDocument.Parse(VoskNative.FinalResult(full));
    Check(final.RootElement.TryGetProperty("text", out _), "unrestricted recognizer returns final text");
}
using (var stream = new MemoryStream())
{
    using (var writer = new NAudio.Wave.WaveFileWriter(new NAudio.Utils.IgnoreDisposeStream(stream), new NAudio.Wave.WaveFormat(16000, 16, 1))) writer.Write(new byte[6400], 0, 6400);
    try { OfflineSpeech.Transcribe(stream.ToArray(), args[0], CancellationToken.None); throw new Exception("silence accepted as speech"); }
    catch (InvalidOperationException e) { Check(e.Message.Contains("Не удалось распознать"), "silence rejected without cloud fallback"); }
    try { OfflineSpeech.Transcribe(stream.ToArray(), args[0], new CancellationToken(true)); throw new Exception("cancellation ignored"); }
    catch (OperationCanceledException) { Check(true, "local transcription cancels"); }
}
var speechSample = Environment.GetEnvironmentVariable("CACHETRIM_TEST_SPEECH_WAV");
if (!string.IsNullOrEmpty(speechSample))
{
    var recognized = OfflineSpeech.Transcribe(File.ReadAllBytes(speechSample), args[0], CancellationToken.None);
    Console.WriteLine("Synthetic speech transcription: " + recognized);
    Check(recognized.Contains("открой") && recognized.Contains("громкость"), "free-form Russian synthetic speech recognized locally");
}
Console.WriteLine("Offline model and native API tests passed; microphone and Windows not tested.");
