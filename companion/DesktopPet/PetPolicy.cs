using System;

namespace DesktopPet;

internal static class PetPolicy
{
    public static double NearestSize(double value) => value < 0.75 ? 0.5 : value < 1.25 ? 1.0 : 1.5;

    public static bool FeetBelowWorkArea(double top, double footOffset, double workBottom) => top + footOffset > workBottom;

    public static bool ReminderDue(int minutes, DateTime next, DateTime now, bool quiet, bool menuOpen) =>
        minutes > 0 && now >= next && !quiet && !menuOpen;

    public static bool CanChangeAutostart(string? current, string own) =>
        current == null || string.Equals(current, own, StringComparison.OrdinalIgnoreCase);
}
