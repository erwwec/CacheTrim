using System;

namespace DesktopPet;

internal static class PetPolicy
{
    // Slider range 50–200%, in 5% steps; preserve old saved values within this range.
    public static double NormalizeSize(double value) => !double.IsFinite(value) ? 0.5 : Math.Round(Math.Clamp(value, 0.5, 2.0) * 20, MidpointRounding.AwayFromZero) / 20;

    public static (double X, double Y) ClampPosition(double x, double y, double workLeft, double workTop, double workRight, double workBottom, double visualLeft, double visualTop, double visualRight, double visualBottom)
    {
        double minX = workLeft - visualLeft, minY = workTop - visualTop;
        double maxX = Math.Max(minX, workRight - visualRight), maxY = Math.Max(minY, workBottom - visualBottom);
        return (Math.Clamp(x, minX, maxX), Math.Clamp(y, minY, maxY));
    }

    // Keep throwing on the same monitor only; old speed must not send a transferred pet away.
    public static bool KeepThrow(bool sameMonitor, TimeSpan idle) => sameMonitor && idle >= TimeSpan.Zero && idle < TimeSpan.FromMilliseconds(150);

    public static bool FeetBelowWorkArea(double top, double footOffset, double workBottom) => top + footOffset > workBottom;

    public static bool ReminderDue(int minutes, DateTime next, DateTime now, bool quiet, bool menuOpen) =>
        minutes > 0 && now >= next && !quiet && !menuOpen;

    public static bool CanChangeAutostart(string? current, string own) =>
        current == null || string.Equals(current, own, StringComparison.OrdinalIgnoreCase);
}
