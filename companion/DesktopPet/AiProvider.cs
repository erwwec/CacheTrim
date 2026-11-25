using System;
using System.Text.Json.Nodes;

namespace DesktopPet;

internal enum AiProvider { Groq, OpenRouter }
internal static class AiProviderConfig
{
    internal static string Name(AiProvider p) => p switch { AiProvider.Groq => "Groq", AiProvider.OpenRouter => "OpenRouter", _ => throw new ArgumentOutOfRangeException(nameof(p)) };
    internal static string Endpoint(AiProvider p) => p switch { AiProvider.Groq => "https://api.groq.com/openai/v1/chat/completions", AiProvider.OpenRouter => "https://openrouter.ai/api/v1/chat/completions", _ => throw new ArgumentOutOfRangeException(nameof(p)) };
    internal static JsonObject Body(AiProvider p, JsonArray messages, bool vision, int tokens)
    {
        var model = p switch { AiProvider.Groq => vision ? "qwen/qwen3.8-27b" : "openai/gpt-oss-20b", AiProvider.OpenRouter => "openrouter/free", _ => throw new ArgumentOutOfRangeException(nameof(p)) };
        var body = new JsonObject { ["model"] = model, ["messages"] = messages };
        body[p == AiProvider.Groq ? "max_completion_tokens" : "max_tokens"] = tokens;
        return body;
    }
}
