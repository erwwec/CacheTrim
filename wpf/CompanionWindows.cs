using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CacheTrim;

internal static class CompanionWindows
{
    private delegate bool EnumCallback(IntPtr window, IntPtr parameter);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumCallback callback, IntPtr parameter);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

    // WPF pets may be tool windows with no Process.MainWindowHandle.
    // Only call after verifying that the process executable is our companion's path.
    internal static bool RequestClose(Process process)
    {
        bool sent = false;
        uint expected = (uint)process.Id;
        EnumWindows((window, _) => {
            GetWindowThreadProcessId(window, out uint id);
            if (id == expected && IsWindowVisible(window))
                sent = PostMessage(window, 0x0010, IntPtr.Zero, IntPtr.Zero) || sent; // WM_CLOSE
            return true;
        }, IntPtr.Zero);
        return sent || process.CloseMainWindow();
    }
}
