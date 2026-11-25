using System.IO;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using CacheTrim;

internal static class Program
{
    private delegate bool Callback(IntPtr hwnd, IntPtr parameter);
    [DllImport("user32.dll")] private static extern bool EnumWindows(Callback callback, IntPtr parameter);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hwnd);
    private static bool HasWindow(int pid) {
        bool found = false;
        EnumWindows((hwnd, _) => { GetWindowThreadProcessId(hwnd, out uint id); if (id == pid && IsWindowVisible(hwnd)) found = true; return true; }, IntPtr.Zero);
        return found;
    }
    [STAThread] static void Main(string[] args)
    {
        if (args.Length != 1) throw new ArgumentException("Expected DesktopPet.exe path");
        string root = Path.Combine(Path.GetTempPath(), "CacheTrimRestartTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "target")); Directory.CreateDirectory(Path.Combine(root, "decoy"));
        string target = Path.Combine(root, "target", "DesktopPet.exe");
        string decoy = Path.Combine(root, "decoy", "DesktopPet.exe");
        File.Copy(args[0], target); File.Copy(args[0], decoy);
        Process a = null, b = null;
        try {
            a = Process.Start(new ProcessStartInfo(target) { UseShellExecute = false })!;
            b = Process.Start(new ProcessStartInfo(decoy) { UseShellExecute = false })!;
            for (int i = 0; i < 80 && (!HasWindow(a.Id) || !HasWindow(b.Id)); i++) { if (a.HasExited || b.HasExited) throw new Exception("Test pet exited early"); Thread.Sleep(500); }
            if (!HasWindow(a.Id) || !HasWindow(b.Id)) throw new Exception("Pet windows not found");
            var isRunning = typeof(MainWindow).GetMethod("IsKnownCompanionRunning", BindingFlags.Static | BindingFlags.NonPublic)!;
            if (!(bool)isRunning.Invoke(null, new object[] { target })!) throw new Exception("Matching companion was not detected");
            if ((bool)isRunning.Invoke(null, new object[] { Path.Combine(root, "missing", "DesktopPet.exe") })!) throw new Exception("Unrelated same-named process caused a false match");
            var close = typeof(MainWindow).GetMethod("CloseKnownCompanion", BindingFlags.Static | BindingFlags.NonPublic)!;
            close.Invoke(null, new object[] { target });
            if (!a.WaitForExit(3000)) throw new Exception("Matching pet did not close");
            if (b.HasExited) throw new Exception("Unrelated same-named pet was closed");
            if ((bool)isRunning.Invoke(null, new object[] { target })!) throw new Exception("Unrelated pet falsely blocks target after restart");
            Console.WriteLine("Restart closes only the companion at the expected path.");
        }
        finally {
            foreach (var p in new[] { a, b }) if (p != null) { try { if (!p.HasExited) { p.Kill(); p.WaitForExit(); } } finally { p.Dispose(); } }
            Directory.Delete(root, true);
        }
    }
}
