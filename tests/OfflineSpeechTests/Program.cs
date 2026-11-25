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
Console.WriteLine("Offline model and native API tests passed; microphone and Windows not tested.");
