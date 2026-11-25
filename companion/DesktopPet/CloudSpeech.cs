using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace DesktopPet;

internal static class CloudSpeech
{
    public static async Task<string> TranscribeAsync(HttpClient client, string key, byte[] wav, CancellationToken ct)
    {
        if (wav.Length < 3200) throw new InvalidOperationException("Запись слишком короткая. Попробуй ещё раз.");
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/audio/transcriptions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        using var form = new MultipartFormDataContent();
        using var file = new ByteArrayContent(wav);
        file.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        form.Add(file, "file", "command.wav");
        form.Add(new StringContent("whisper-large-v3-turbo"), "model");
        form.Add(new StringContent("json"), "response_format");
        request.Content = form;
        using var response = await client.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.TooManyRequests) throw new InvalidOperationException("Лимит Groq исчерпан. Попробуй позже.");
        if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden) throw new InvalidOperationException("Ключ Groq не принят или распознавание недоступно.");
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException("Groq не распознал запись (HTTP " + (int)response.StatusCode + ").");
        var json = await response.Content.ReadAsStringAsync(ct);
        if (json.Length > 65536) throw new InvalidOperationException("Ответ распознавания слишком большой.");
        string text = (JsonNode.Parse(json)?["text"]?.GetValue<string>() ?? "").Trim();
        if (text.Length == 0 || text.Length > 1000) throw new InvalidOperationException("Не удалось распознать короткую команду. Попробуй ещё раз.");
        return text;
    }
}
