using DesktopPet;
using System.Net;
using System.Net.Http;
using System.Text.Json;

static void Check(bool ok, string label) { if (!ok) throw new Exception(label); Console.WriteLine("OK: " + label); }
Check(CloudAssistant.IsAllowedCall("open_youtube", "{}"), "fixed action allowed");
Check(!CloudAssistant.IsAllowedCall("run_shell", "{}"), "unknown action blocked");
Check(!CloudAssistant.IsAllowedCall("open_youtube", "{\"url\":\"https://evil.test\"}"), "unexpected arguments blocked");
Check(!CloudAssistant.IsAllowedCall("open_youtube", "null") && !CloudAssistant.IsAllowedCall("open_youtube", "bad"), "invalid arguments blocked");
var mock = new FakeHandler(); using var client = new HttpClient(mock);
var actions = new List<string>();
mock.Responses.Enqueue("{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":null,\"tool_calls\":[{\"id\":\"call_1\",\"type\":\"function\",\"function\":{\"name\":\"open_youtube\",\"arguments\":\"{}\"}}]}}]}");
mock.Responses.Enqueue("{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"Открыл YouTube.\"}}]}");
var answer = await CloudAssistant.AskAsync(client, "example-test-key", "открой ютуб", (name, args, ct) => { actions.Add(name); return Task.FromResult("Открыт YouTube"); }, CancellationToken.None);
Check(actions.SequenceEqual(new[] { "open_youtube" }) && answer.Contains("YouTube"), "model tool result and response");
Check(mock.Targets.All(u => u == "https://api.groq.com/openai/v1/chat/completions"), "fixed HTTPS endpoint");
Check(JsonDocument.Parse(mock.Bodies[1]).RootElement.GetProperty("messages")[3].GetProperty("content").GetString() == "Открыт YouTube", "actual tool result sent back");
mock.Responses.Enqueue("{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":null,\"tool_calls\":[{\"id\":\"call_2\",\"type\":\"function\",\"function\":{\"name\":\"open_youtube\",\"arguments\":\"{\\\"url\\\":\\\"https://evil.test\\\"}\"}}]}}]}");
mock.Responses.Enqueue("{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"Недоступно.\"}}]}");
await CloudAssistant.AskAsync(client, "example-test-key", "задача", (name, args, ct) => { actions.Add(name); return Task.FromResult("executed"); }, CancellationToken.None);
Check(actions.Count == 1 && JsonDocument.Parse(mock.Bodies[^1]).RootElement.GetProperty("messages")[3].GetProperty("content").GetString()!.Contains("запрещено"), "malicious tool arguments not executed");
var cancelled = new CancellationToken(true);
try { await CloudAssistant.AskAsync(client, "example-test-key", "тест", (name, args, ct) => { actions.Add(name); return Task.FromResult("executed"); }, cancelled); throw new Exception("cancellation ignored"); }
catch (OperationCanceledException) { Check(actions.Count == 1, "cancellation blocks actions"); }
mock.Responses.Enqueue("{\"text\":\"открой ютуб\"}");
string transcript = await CloudSpeech.TranscribeAsync(client, "example-test-key", new byte[6400], CancellationToken.None);
Check(transcript == "открой ютуб" && mock.Targets[^1] == "https://api.groq.com/openai/v1/audio/transcriptions", "voice transcription uses fixed Groq endpoint");
using var deckData = JsonDocument.Parse("""
{"title":"Вторая мировая война","subtitle":"Основные этапы и последствия","slides":[
{"title":"Начало войны","left":"1 сентября 1939 года Германия вторглась в Польшу.","right":"Вскоре Великобритания и Франция объявили Германии войну."},
{"title":"Перелом и окончание","left":"Победа под Сталинградом стала важным этапом перелома войны.","right":"В 1945 году капитулировали Германия и Япония."}]}
""");
Check(PresentationBuilder.IsValid(deckData.RootElement), "valid deck content accepted");
Check(!CloudAssistant.IsAllowedCall("create_presentation", "{\"title\":\"x\",\"path\":\"../evil\"}"), "reject paths and malformed decks");
var deckArgs = deckData.RootElement.GetRawText();
Check(CloudAssistant.IsAllowedCall("create_presentation", deckArgs), "valid presentation tool call allowed");
mock.Responses.Enqueue(JsonSerializer.Serialize(new { choices = new[] { new { message = new { role = "assistant", content = (string?)null, tool_calls = new[] { new { id = "deck_1", type = "function", function = new { name = "create_presentation", arguments = deckArgs } } } } } } }));
mock.Responses.Enqueue("{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"Создано.\"}}]}");
string? received = null;
await CloudAssistant.AskAsync(client, "example-test-key", "создай презентацию", (name, arguments, ct) => { received = name + ":" + arguments; return Task.FromResult("Проверено"); }, CancellationToken.None);
Check(received == "create_presentation:" + deckArgs, "validated presentation arguments reach executor");

var temp = Path.Combine(Path.GetTempPath(), "CacheTrimDeckTest-" + Guid.NewGuid().ToString("N"));
try
{
    var pptx = PresentationBuilder.Create(deckData.RootElement, temp);
    var second = PresentationBuilder.Create(deckData.RootElement, temp);
    Check(pptx != second && File.Exists(pptx) && File.Exists(second), "new decks never overwrite prior files");
    using (var presentation = DocumentFormat.OpenXml.Packaging.PresentationDocument.Open(pptx, false))
    {
        var part = presentation.PresentationPart!;
        var slideIds = part.Presentation.SlideIdList!.Elements<DocumentFormat.OpenXml.Presentation.SlideId>().ToArray();
        Check(slideIds.Length == 3, "cover plus requested slides");
        var texts = slideIds.SelectMany(x => ((DocumentFormat.OpenXml.Packaging.SlidePart)part.GetPartById(x.RelationshipId!)).Slide.Descendants<DocumentFormat.OpenXml.Drawing.Text>()).Select(x => x.Text).ToArray();
        Check(texts.Contains("Вторая мировая война") && texts.Contains("Перелом и окончание") && !texts.Contains("SLIDE_TITLE"), "slide text is editable and populated");
        var errors = new DocumentFormat.OpenXml.Validation.OpenXmlValidator().Validate(presentation).Take(4).ToArray();
        Check(errors.Length == 0, "presentation passes Open XML validation: " + string.Join("; ", errors.Select(e => e.Description)));
    }
    var preview = Environment.GetEnvironmentVariable("CACHETRIM_TEST_DECK");
    if (!string.IsNullOrWhiteSpace(preview)) File.Copy(pptx, preview, true);
}
finally { if (Directory.Exists(temp)) Directory.Delete(temp, true); }
Console.WriteLine("All cloud assistant tests passed.");

sealed class FakeHandler : HttpMessageHandler
{
    public Queue<string> Responses { get; } = new();
    public List<string> Targets { get; } = new();
    public List<string> Bodies { get; } = new();
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); Targets.Add(request.RequestUri!.ToString());
        Bodies.Add(await request.Content!.ReadAsStringAsync(ct));
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Responses.Dequeue()) };
    }
}
