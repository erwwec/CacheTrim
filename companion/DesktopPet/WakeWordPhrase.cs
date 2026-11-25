using System;

namespace DesktopPet;

internal static class WakeWordPhrase
{
    internal static readonly string[] Phrases = { "джарвис", "джервис" };
    internal static bool MatchesOffline(string? text) =>
        Array.Exists((text ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries), t => t.Equals("джарвис", StringComparison.OrdinalIgnoreCase) || t.Equals("джервис", StringComparison.OrdinalIgnoreCase));
    internal static bool Matches(string? text, float confidence) =>
        float.IsFinite(confidence) && confidence >= 0.55f && string.Equals(text?.Trim(), "джарвис", StringComparison.OrdinalIgnoreCase);
}
