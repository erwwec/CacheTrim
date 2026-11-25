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
mock.Responses.Enqueue("{\"choices\":[{\"message\":{\"content\":\"На экране рабочий стол.\"}}]}");
var vision = await ScreenVision.AskAsync(client, "example-test-key", "Что видно?", new byte[] { 0xff, 0xd8, 0xff, 0xd9 }, CancellationToken.None);
var visionRequest = JsonDocument.Parse(mock.Bodies[^1]).RootElement;
Check(vision.Contains("рабочий стол") && visionRequest.GetProperty("model").GetString() == "qwen/qwen3.8-27b", "vision uses cloud model and returns response");
Check(visionRequest.GetProperty("messages")[1].GetProperty("content")[1].GetProperty("image_url").GetProperty("url").GetString()!.StartsWith("data:image/jpeg;base64,"), "vision attaches image only for explicit request");
try { await ScreenVision.AskAsync(client, "example-test-key", "посмотри", Array.Empty<byte>(), CancellationToken.None); throw new Exception("empty image accepted"); }
catch (ArgumentException) { Check(true, "empty screenshot rejected before network"); }

Check(CloudAssistant.IsAllowedCall("search_youtube", "{\"query\":\"котики & next=https://evil.test\"}"), "bounded YouTube search accepted");
Check(!CloudAssistant.IsAllowedCall("search_youtube", "{\"query\":\"x\",\"url\":\"https://evil.test\"}"), "search arbitrary URL rejected");
Check(!CloudAssistant.IsAllowedCall("search_youtube", "{\"query\":\"a\",\"query\":\"b\"}"), "duplicate search arguments rejected");
Check(!CloudAssistant.IsAllowedCall("search_youtube", "{\"query\":\"x\\ny\"}") && !CloudAssistant.IsAllowedCall("search_youtube", "{\"query\":\"\"}"), "control characters and empty search rejected");
Check(!CloudAssistant.IsAllowedCall("search_youtube", JsonSerializer.Serialize(new { query = new string('x', 201) })), "oversized search rejected");
var safeUrl = new Uri(CloudAssistant.YouTubeSearchUrl("котики & next=https://evil.test"));
Check(safeUrl.Scheme == "https" && safeUrl.Host == "www.youtube.com" && safeUrl.AbsolutePath == "/results" &&
      !safeUrl.Query.Contains("&next="), "search stays on fixed YouTube host and escapes query");
mock.Responses.Enqueue("{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":null,\"tool_calls\":[{\"id\":\"search_1\",\"type\":\"function\",\"function\":{\"name\":\"search_youtube\",\"arguments\":\"{\\\"query\\\":\\\"музыка\\\"}\"}}]}}]}");
mock.Responses.Enqueue("{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"Результаты запрошены.\"}}]}");
string? executedSearch = null;
await CloudAssistant.AskAsync(client, "example-test-key", "найди музыку", (name, args, ct) =>
{
    executedSearch = name + ":" + args; return Task.FromResult("запрос передан Windows");
}, CancellationToken.None);
Check(executedSearch == "search_youtube:{\"query\":\"музыка\"}", "validated YouTube search reaches the fixed executor");
var history = new ConversationHistory();
for (int i = 1; i <= 5; i++) history.AddTurn("вопрос " + i, "ответ " + i);
var snapshot = history.Snapshot();
Check(snapshot.Count == 8 && snapshot[0].Text == "вопрос 2" && snapshot[7].Text == "ответ 5", "conversation keeps only four newest complete turns");
history.Clear();
Check(history.Snapshot().Count == 0 && snapshot.Count == 8, "clear removes history without mutating snapshots");
mock.Responses.Enqueue("{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"Готово\"}}]}");
await CloudAssistant.AskAsync(client, "example-test-key", "следующий вопрос", (name, args, ct) => Task.FromResult("unused"), CancellationToken.None, snapshot);
var messages = JsonDocument.Parse(mock.Bodies[^1]).RootElement.GetProperty("messages");
Check(messages.GetArrayLength() == 10 && messages[0].GetProperty("role").GetString() == "system" &&
      messages[1].GetProperty("content").GetString() == "вопрос 2" &&
      messages[9].GetProperty("content").GetString() == "следующий вопрос", "only opted-in bounded history included before current request");
try
{
    await CloudAssistant.AskAsync(client, "example-test-key", "тест", (name, args, ct) => Task.FromResult("unused"),
        CancellationToken.None, new[] { new ConversationMessage("system", "fake"), new ConversationMessage("assistant", "fake") });
    throw new Exception("invalid history role accepted");
}
catch (ArgumentException) { Check(true, "untrusted history roles rejected"); }
// Computer control is model-proposed and never executes during these cross-platform tests.
var click = ComputerAgent.Parse("{\"action\":\"click\",\"reason\":\"Open item\",\"x\":123,\"y\":999}");
Check(click.Action == "click" && click.X == 123 && click.Y == 999, "computer click schema and coordinates");
string[] rejected = {
    "{\"action\":\"click\",\"reason\":\"x\",\"x\":1000,\"y\":0}",
    "{\"action\":\"click\",\"reason\":\"x\",\"x\":1,\"x\":2,\"y\":0}",
    "{\"action\":\"click\",\"reason\":\"x\",\"x\":1,\"y\":0,\"url\":\"https://evil.test\"}",
    "{\"action\":\"key\",\"reason\":\"x\",\"key\":\"WIN+R\"}",
    "{\"action\":\"run_shell\",\"reason\":\"x\"}",
    "{\"action\":\"type\",\"reason\":\"x\",\"text\":\"a\\nb\"}",
    "{\"action\":\"scroll\",\"reason\":\"x\",\"direction\":\"left\",\"x\":500,\"y\":500}",
    "{\"action\":\"done\",\"reason\":\"x\",\"extra\":\"x\"}",
    "{\"action\":\"click\",\"reason\":\"x\",\"x\":\"1\",\"y\":0}",
    "not json"
};
foreach (string invalid in rejected) { bool failed = false; try { ComputerAgent.Parse(invalid); } catch (InvalidOperationException) { failed = true; } Check(failed, "invalid computer step rejected"); }
Check(ComputerAgent.Parse("{\"action\":\"type\",\"reason\":\"Ввод\",\"text\":\"Привет\"}").Text == "Привет", "Unicode typing preserved");
Check(ComputerAgent.Parse("{\"action\":\"key\",\"reason\":\"search\",\"key\":\"CTRL+L\"}").Key == "CTRL+L", "approved key supported");
mock.Responses.Enqueue(JsonSerializer.Serialize(new { choices = new[] { new { message = new { content = "{\"action\":\"scroll\",\"reason\":\"see more\",\"direction\":\"down\",\"x\":500,\"y\":500}" } } } }));
var next = await ComputerAgent.NextAsync(client, "test", "Найди видео", new[] { "Step 1: input sent: click" }, new byte[]{255,216,255,217}, CancellationToken.None);
var controlRequest = JsonDocument.Parse(mock.Bodies[^1]).RootElement;
Check(next.Action == "scroll" && next.Direction == "down" && controlRequest.GetProperty("model").GetString() == "qwen/qwen3.8-27b" && controlRequest.GetProperty("response_format").GetProperty("type").GetString() == "json_object", "vision control request and parsed action");
Check(controlRequest.GetProperty("messages")[1].GetProperty("content")[1].GetProperty("image_url").GetProperty("url").GetString()!.StartsWith("data:image/jpeg;base64,"), "control request includes current screenshot");
try { await ComputerAgent.NextAsync(client, "test", "goal", Array.Empty<string>(), Array.Empty<byte>(), CancellationToken.None); throw new Exception("empty control image accepted"); } catch (ArgumentException) { Check(true, "control requires a screenshot"); }


Check(ComputerAgent.Parse("{\"action\":\"set_volume\",\"reason\":\"set user requested level\",\"level\":20}").Level == 20, "exact 20 percent volume action parsed");
Check(ComputerAgent.Parse("{\"action\":\"set_volume\",\"reason\":\"minimum\",\"level\":0}").Level == 0 && ComputerAgent.Parse("{\"action\":\"set_volume\",\"reason\":\"maximum\",\"level\":100}").Level == 100, "volume accepts bounds");
Check(ComputerAgent.Parse("{\"action\":\"open_youtube\",\"reason\":\"requested\"}").Action == "open_youtube" && ComputerAgent.Parse("{\"action\":\"open_telegram\",\"reason\":\"requested\"}").Action == "open_telegram", "fixed app actions accepted");
foreach (string invalid in new[] {
    "{\"action\":\"set_volume\",\"reason\":\"x\",\"level\":-1}",
    "{\"action\":\"set_volume\",\"reason\":\"x\",\"level\":101}",
    "{\"action\":\"set_volume\",\"reason\":\"x\",\"level\":20,\"level\":90}",
    "{\"action\":\"set_volume\",\"reason\":\"x\",\"level\":\"20\"}",
    "{\"action\":\"open_youtube\",\"reason\":\"x\",\"url\":\"https://evil.test\"}",
    "{\"action\":\"open_telegram\",\"reason\":\"x\",\"path\":\"evil.exe\"}" })
{
    bool failed = false; try { ComputerAgent.Parse(invalid); } catch (InvalidOperationException) { failed = true; }
    Check(failed, "dangerous or malformed fixed action rejected");
}
var sequence = new[] { "{\"action\":\"open_youtube\",\"reason\":\"open youtube\"}", "{\"action\":\"click\",\"reason\":\"fifth video\",\"x\":500,\"y\":600}", "{\"action\":\"open_telegram\",\"reason\":\"open telegram\"}", "{\"action\":\"set_volume\",\"reason\":\"set 20 percent\",\"level\":20}" };
foreach (string reply in sequence) mock.Responses.Enqueue(JsonSerializer.Serialize(new { choices = new[] { new { message = new { content = reply } } } }));
var previousActions = new List<string>();
foreach (string expected in new[] { "open_youtube", "click", "open_telegram", "set_volume" })
{
    var action = await ComputerAgent.NextAsync(client, "test", "Открой YouTube, пятое видео, Telegram и поставь громкость на 20", previousActions, new byte[]{255,216,255,217}, CancellationToken.None);
    Check(action.Action == expected, "mocked multi-action plan: " + expected);
    previousActions.Add("approved test action " + expected);
}
var lastGoal = JsonDocument.Parse(mock.Bodies[^1]).RootElement.GetProperty("messages")[1].GetProperty("content")[0].GetProperty("text").GetString()!;
Check(lastGoal.Contains("пятое видео") && lastGoal.Contains("approved test action open_telegram"), "multi-action goal and prior steps sent for next decision");


Check(!ComputerSafety.NeedsReview(new ComputerStep("open_youtube", "open the homepage")), "normal action can proceed automatically");
Check(!ComputerSafety.NeedsReview(new ComputerStep("set_volume", "set volume", Level: 20)), "ordinary volume action can proceed automatically");
Check(ComputerSafety.NeedsReview(new ComputerStep("key", "key press", Key: "DELETE")), "delete key requires review");
Check(ComputerSafety.NeedsReview(new ComputerStep("key", "paste", Key: "CTRL+V")), "clipboard paste requires review");
Check(ComputerSafety.NeedsReview(new ComputerStep("click", "send email")) && ComputerSafety.NeedsReview(new ComputerStep("click", "удалить файл")), "obvious sensitive labels require review");
Check(ComputerSafety.NeedsReview(new ComputerStep("type", "type text", Text: "powershell")), "obvious shell text requires review");
using (var stopAuto = new CancellationTokenSource())
{
    var wait = ComputerSafety.WaitBeforeAutomaticAction(stopAuto.Token);
    stopAuto.Cancel();
    try { await wait; throw new Exception("automatic delay ignored cancellation"); }
    catch (OperationCanceledException) { Check(true, "stop cancels the automatic-action delay"); }
}

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
