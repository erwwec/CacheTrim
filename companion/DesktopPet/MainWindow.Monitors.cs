using System;
using System.Runtime.InteropServices;
using System.Windows;

namespace DesktopPet;

public partial class MainWindow
{
    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorPoint { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect Work;
        public int Flags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(MonitorPoint point, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    // Use the monitor containing the sprite centre, not always the primary monitor.
    // PointToScreen/PointFromScreen translate through the current HWND's DPI transform.
    private Rect GetCurrentMonitorWorkArea()
    {
        try
        {
            var center = PointToScreen(new Point((ModelLeft + ModelRight) * _uiScale / 2, (ModelTop + ModelBottom) * _uiScale / 2));
            var monitor = MonitorFromPoint(new MonitorPoint { X = (int)Math.Round(center.X), Y = (int)Math.Round(center.Y) }, 2); // nearest monitor
            var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
            if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref info)) return SystemParameters.WorkArea;
            var a = PointFromScreen(new Point(info.Work.Left, info.Work.Top));
            var b = PointFromScreen(new Point(info.Work.Right, info.Work.Bottom));
            if (b.X <= a.X || b.Y <= a.Y) return SystemParameters.WorkArea;
            return new Rect(Left + a.X, Top + a.Y, b.X - a.X, b.Y - a.Y);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or OverflowException)
        {
            return SystemParameters.WorkArea;
        }
    }

    private void ConstrainToCurrentMonitor()
    {
        var area = GetCurrentMonitorWorkArea();
        var minLeft = area.Left - ModelLeft * _uiScale;
        var maxLeft = Math.Max(minLeft, area.Right - ModelRight * _uiScale);
        var minTop = area.Top - ModelTop * _uiScale;
        var maxTop = Math.Max(minTop, area.Bottom - ModelBottom * _uiScale);
        Left = Math.Clamp(Left, minLeft, maxLeft);
        Top = Math.Clamp(Top, minTop, maxTop);
    }
}
