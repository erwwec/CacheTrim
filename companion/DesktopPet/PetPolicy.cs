using System;

namespace DesktopPet;

internal static class PetPolicy
{
    public static bool ReminderDue(int minutes, DateTime next, DateTime now, bool quiet, bool menuOpen) =>
        minutes > 0 && now >= next && !quiet && !menuOpen;

    public static bool CanChangeAutostart(string? current, string own) =>
        current == null || string.Equals(current, own, StringComparison.OrdinalIgnoreCase);
}
