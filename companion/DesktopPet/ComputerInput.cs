using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace DesktopPet;

// No shell or clipboard access. This emits only the input already approved in the UI.
internal static class ComputerInput
{
    [StructLayout(LayoutKind.Sequential)] private struct Input { public uint type; public InputUnion data; }
    [StructLayout(LayoutKind.Explicit)] private struct InputUnion { [FieldOffset(0)] public Mouse mouse; [FieldOffset(0)] public Keyboard keyboard; }
    [StructLayout(LayoutKind.Sequential)] private struct Mouse { public int dx, dy; public uint mouseData, flags, time; public UIntPtr extraInfo; }
    [StructLayout(LayoutKind.Sequential)] private struct Keyboard { public ushort vk, scan; public uint flags, time; public UIntPtr extraInfo; }
    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint nInputs, Input[] inputs, int size);
    [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll")] internal static extern bool IsWindow(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
    internal static bool IsOurWindow(IntPtr hwnd) { GetWindowThreadProcessId(hwnd, out uint pid); return pid == Environment.ProcessId; }
    private static Input K(ushort vk, ushort scan, uint flags) => new() { type = 1, data = new InputUnion { keyboard = new Keyboard { vk = vk, scan = scan, flags = flags } } };
    private static Input M(uint flags, uint data = 0) => new() { type = 0, data = new InputUnion { mouse = new Mouse { flags = flags, mouseData = data } } };
    private static void Send(List<Input> list)
    {
        if (SendInput((uint)list.Count, list.ToArray(), Marshal.SizeOf<Input>()) != list.Count)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows не приняла ввод (возможно, окно защищено или имеет другой уровень прав).");
    }
    internal static void Execute(ComputerStep step)
    {
        var inputs = new List<Input>();
        switch (step.Action)
        {
            case "click": case "double_click":
                int w = GetSystemMetrics(0), h = GetSystemMetrics(1);
                if (w < 1 || h < 1 || !SetCursorPos((int)((long)step.X * (w - 1) / 999), (int)((long)step.Y * (h - 1) / 999))) throw new InvalidOperationException("Не удалось переместить указатель.");
                inputs.Add(M(0x0002)); inputs.Add(M(0x0004));
                if (step.Action == "double_click") { inputs.Add(M(0x0002)); inputs.Add(M(0x0004)); }
                break;
            case "scroll":
                int sw = GetSystemMetrics(0), sh = GetSystemMetrics(1);
                if (sw < 1 || sh < 1 || !SetCursorPos((int)((long)step.X * (sw - 1) / 999), (int)((long)step.Y * (sh - 1) / 999))) throw new InvalidOperationException("Не удалось переместить указатель.");
                inputs.Add(M(0x0800, unchecked((uint)(step.Direction == "up" ? 360 : -360)))); break;
            case "type":
                foreach (char c in step.Text) { inputs.Add(K(0, c, 0x0004)); inputs.Add(K(0, c, 0x0004 | 0x0002)); } break;
            case "key":
                string[] parts = step.Key.Split('+');
                ushort modifier = parts.Length == 2 ? (ushort)(parts[0] == "CTRL" ? 0x11 : 0x12) : (ushort)0;
                string name = parts[^1];
                ushort vk = name switch { "ENTER" => 0x0D, "TAB" => 0x09, "ESC" => 0x1B, "BACKSPACE" => 0x08, "DELETE" => 0x2E, "SPACE" => 0x20, "UP" => 0x26, "DOWN" => 0x28, "LEFT" => 0x25, "RIGHT" => 0x27, "HOME" => 0x24, "END" => 0x23, "PAGEDOWN" => 0x22, "PAGEUP" => 0x21, _ => (ushort)name[0] };
                if (modifier != 0) inputs.Add(K(modifier, 0, 0));
                bool extended = name is "DELETE" or "UP" or "DOWN" or "LEFT" or "RIGHT" or "HOME" or "END" or "PAGEDOWN" or "PAGEUP";
                inputs.Add(K(vk, 0, extended ? 1u : 0u)); inputs.Add(K(vk, 0, (extended ? 1u : 0u) | 2u));
                if (modifier != 0) inputs.Add(K(modifier, 0, 2));
                break;
            default: throw new InvalidOperationException("Это действие нельзя выполнить как ввод.");
        }
        Send(inputs);
    }
}
