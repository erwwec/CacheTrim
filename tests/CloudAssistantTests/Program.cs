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
var answer = await CloudAssistant.AskAsync(client, "example-test-key", "открой ютуб", (name, ct) => { actions.Add(name); return Task.FromResult("Открыт YouTube"); }, CancellationToken.None);
Check(actions.SequenceEqual(new[] { "open_youtube" }) && answer.Contains("YouTube"), "model tool result and response");
Check(mock.Targets.All(u => u == "https://api.groq.com/openai/v1/chat/completions"), "fixed HTTPS endpoint");
Check(JsonDocument.Parse(mock.Bodies[1]).RootElement.GetProperty("messages")[3].GetProperty("content").GetString() == "Открыт YouTube", "actual tool result sent back");
mock.Responses.Enqueue("{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":null,\"tool_calls\":[{\"id\":\"call_2\",\"type\":\"function\",\"function\":{\"name\":\"open_youtube\",\"arguments\":\"{\\\"url\\\":\\\"https://evil.test\\\"}\"}}]}}]}");
mock.Responses.Enqueue("{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"Недоступно.\"}}]}");
await CloudAssistant.AskAsync(client, "example-test-key", "задача", (name, ct) => { actions.Add(name); return Task.FromResult("executed"); }, CancellationToken.None);
Check(actions.Count == 1 && JsonDocument.Parse(mock.Bodies[^1]).RootElement.GetProperty("messages")[3].GetProperty("content").GetString()!.Contains("запрещено"), "malicious tool arguments not executed");
var cancelled = new CancellationToken(true);
try { await CloudAssistant.AskAsync(client, "example-test-key", "тест", (name, ct) => { actions.Add(name); return Task.FromResult("executed"); }, cancelled); throw new Exception("cancellation ignored"); }
catch (OperationCanceledException) { Check(actions.Count == 1, "cancellation blocks actions"); }
mock.Responses.Enqueue("{\"text\":\"открой ютуб\"}");
string transcript = await CloudSpeech.TranscribeAsync(client, "example-test-key", new byte[6400], CancellationToken.None);
Check(transcript == "открой ютуб" && mock.Targets[^1] == "https://api.groq.com/openai/v1/audio/transcriptions", "voice transcription uses fixed Groq endpoint");
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
