using System;
using System.Collections.Generic;
using System.Linq;

namespace DesktopPet;

internal enum PetVoiceCommand { None, Hello, Time, Date, Jump, Secret, Downloads, Desktop }

// Fixed commands only: never pass user speech to a shell or use fuzzy matching.
internal static class VoiceCommandCatalog
{
    private static readonly Dictionary<string, PetVoiceCommand> Commands = new(StringComparer.OrdinalIgnoreCase)
    {
        ["привет клод"] = PetVoiceCommand.Hello,
        ["который час"] = PetVoiceCommand.Time,
        ["сколько времени"] = PetVoiceCommand.Time,
        ["какое сегодня число"] = PetVoiceCommand.Date,
        ["прыгни"] = PetVoiceCommand.Jump,
        ["принеси секрет"] = PetVoiceCommand.Secret,
        ["открой загрузки"] = PetVoiceCommand.Downloads,
        ["покажи рабочий стол"] = PetVoiceCommand.Desktop
    };
    public static string[] Phrases => Commands.Keys.ToArray();
    public static PetVoiceCommand Parse(string? text, float confidence = 1f)
    {
        if (string.IsNullOrWhiteSpace(text) || !float.IsFinite(confidence) || confidence < 0.75f) return PetVoiceCommand.None;
        var normalized = string.Join(" ", text.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return Commands.TryGetValue(normalized, out var command) ? command : PetVoiceCommand.None;
    }
}
