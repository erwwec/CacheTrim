using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;

namespace DesktopPet;
// Operates only on the window which is actually foreground after the user countdown.
internal static class InteractiveCommandActions
{
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr hwnd, System.Text.StringBuilder name, int maxCount);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool PostMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool ShowWindowAsync(IntPtr hwnd, int cmdShow);
    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, Input[] inputs, int size);
    [StructLayout(LayoutKind.Sequential)] private struct Input { public uint Type; public InputUnion Data; }
    [StructLayout(LayoutKind.Explicit)] private struct InputUnion { [FieldOffset(0)] public MouseInput Mouse; [FieldOffset(0)] public KeyboardInput Keyboard; }
    [StructLayout(LayoutKind.Sequential)] private struct MouseInput { public int X, Y; public uint MouseData, Flags, Time; public UIntPtr ExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] private struct KeyboardInput { public ushort VirtualKey, Scan; public uint Flags, Time; public UIntPtr ExtraInfo; }
    private static IntPtr Target()
    {
        IntPtr hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero || !IsWindowVisible(hwnd) || GetWindowThreadProcessId(hwnd, out uint pid) == 0 || pid == 0 || pid == (uint)Environment.ProcessId)
            throw new InvalidOperationException("Нужное окно не выбрано. Переключись на него во время отсчёта и попробуй снова.");
        var name = new System.Text.StringBuilder(256);
        GetClassName(hwnd, name, name.Capacity);
        if (name.ToString() is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd")
            throw new InvalidOperationException("Выбери окно приложения, а не рабочий стол или панель задач.");
        return hwnd;
    }
    internal static void WindowAction(string action)
    {
        IntPtr hwnd = Target();
        if (action == "close") { if (!PostMessage(hwnd, 0x0010, IntPtr.Zero, IntPtr.Zero)) throw new Win32Exception(Marshal.GetLastWin32Error(), "Не удалось отправить запрос на закрытие окна."); return; }
        int mode = action switch { "minimize" => 6, "maximize" => 3, "restore" => 9, _ => throw new InvalidDataException("Неизвестное действие с окном.") };
        if (!ShowWindowAsync(hwnd, mode)) throw new InvalidOperationException("Не удалось изменить состояние окна.");
    }
    private static Input Make(ushort key, bool up)
    {
        bool extended = key is 0x21 or 0x22 or 0x23 or 0x24 or 0x25 or 0x26 or 0x27 or 0x28 or 0x2D or 0x2E;
        return new Input { Type = 1, Data = new InputUnion { Keyboard = new KeyboardInput { VirtualKey = key, Flags = (up ? 2u : 0u) | (extended ? 1u : 0u) } } };
    }
    internal static void SendKeys(string chord)
    {
        ushort[] keys = CustomKeyChord.VirtualKeys(chord); var target = Target();
        var input = new Input[keys.Length * 2];
        for (int i = 0; i < keys.Length; i++) { input[i] = Make(keys[i], false); input[keys.Length + i] = Make(keys[keys.Length - i - 1], true); }
        if (target != GetForegroundWindow()) throw new InvalidOperationException("Активное окно изменилось; сочетание не отправлено.");
        if (SendInput((uint)input.Length, input, Marshal.SizeOf<Input>()) != input.Length)
        {
            // Release any modifiers that might have been injected before failure.
            var releases = new Input[keys.Length]; for (int i = 0; i < keys.Length; i++) releases[i] = Make(keys[keys.Length - i - 1], true);
            SendInput((uint)releases.Length, releases, Marshal.SizeOf<Input>());
            throw new InvalidOperationException("Не удалось отправить сочетание: возможно, окно защищено или запущено с более высокими правами.");
        }
    }
}
