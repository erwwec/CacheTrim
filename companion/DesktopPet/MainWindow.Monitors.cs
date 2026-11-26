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

    private static IntPtr MonitorAt(Point point) => MonitorFromPoint(new MonitorPoint { X = (int)Math.Round(point.X), Y = (int)Math.Round(point.Y) }, 2);

    // Convert the chosen monitor's physical work area through the active WPF window.
    private Rect GetCurrentMonitorWorkArea() => GetMonitorWorkArea(IntPtr.Zero);
    private Rect GetMonitorWorkArea(IntPtr monitor)
    {
        try
        {
            if (monitor == IntPtr.Zero)
            {
                var center = PointToScreen(new Point((ModelLeft + ModelRight) * _uiScale / 2, (ModelTop + ModelBottom) * _uiScale / 2));
                monitor = MonitorAt(center);
            }
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

    private void ConstrainToCurrentMonitor() => ConstrainToMonitor(IntPtr.Zero);
    private void ConstrainToMonitor(IntPtr monitor)
    {
        var area = GetMonitorWorkArea(monitor);
        var position = PetPolicy.ClampPosition(Left, Top, area.Left, area.Top, area.Right, area.Bottom,
            ModelLeft * _uiScale, ModelTop * _uiScale, ModelRight * _uiScale, ModelBottom * _uiScale);
        Left = position.X;
        Top = position.Y;
    }

    protected override void OnDpiChanged(System.Windows.DpiScale oldDpi, System.Windows.DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        // WPF may finish applying a new transform after the DPI notification.
        Dispatcher.BeginInvoke(new Action(() => { if (!_dragging && !_isClosing && IsLoaded) ConstrainToCurrentMonitor(); }), System.Windows.Threading.DispatcherPriority.Loaded);
    }
}
