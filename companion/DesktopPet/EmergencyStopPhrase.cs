using System;

namespace DesktopPet;

// Stop is fail-safe: a false positive only stops further actions.
internal static class EmergencyStopPhrase
{
    internal static readonly string[] Phrases = { "стоп", "джарвис стоп", "клод стоп", "остановись" };
    internal static bool MatchesOffline(string? text) =>
        Array.Exists((text ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries), t => t.Equals("стоп", StringComparison.OrdinalIgnoreCase) || t.Equals("остановись", StringComparison.OrdinalIgnoreCase));
    internal static bool Matches(string? text, float confidence)
    {
        if (string.IsNullOrWhiteSpace(text) || !float.IsFinite(confidence) || confidence < 0.35f) return false;
        string normalized = string.Join(" ", text.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        foreach (string phrase in Phrases) if (string.Equals(normalized, phrase, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
}
