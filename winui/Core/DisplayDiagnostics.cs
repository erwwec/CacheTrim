using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
namespace CacheTrim;
internal static class DisplayDiagnostics
{
    internal sealed record Report(string Name, int Width, int Height, int CurrentHz, int[] ReportedHz);
    internal static int[] Rates(IEnumerable<(int Width, int Height, int Hz)> modes, int width, int height) => modes.Where(x => x.Width == width && x.Height == height && x.Hz > 1).Select(x => x.Hz).Distinct().OrderBy(x => x).ToArray();
    internal static Report Read(string deviceName)
    {
        var current = NewMode();
        if (!EnumDisplaySettings(deviceName, -1, ref current)) throw new InvalidOperationException("Windows не вернула текущий режим экрана.");
        var modes = new List<(int Width, int Height, int Hz)>();
        for (int index = 0; index < 8192; index++)
        {
            var mode = NewMode(); if (!EnumDisplaySettings(deviceName, index, ref mode)) break;
            modes.Add(((int)mode.dmPelsWidth, (int)mode.dmPelsHeight, (int)mode.dmDisplayFrequency));
        }
        return new Report(deviceName, (int)current.dmPelsWidth, (int)current.dmPelsHeight, (int)current.dmDisplayFrequency, Rates(modes, (int)current.dmPelsWidth, (int)current.dmPelsHeight));
    }
    internal static int NativeStructureSize => Marshal.SizeOf<DevMode>();
    private static DevMode NewMode() => new DevMode { dmSize = (short)Marshal.SizeOf<DevMode>() };
    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "EnumDisplaySettingsW")]
    private static extern bool EnumDisplaySettings(string device, int mode, ref DevMode data);
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DevMode
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
        public short dmSpecVersion, dmDriverVersion, dmSize, dmDriverExtra;
        public int dmFields, dmPositionX, dmPositionY, dmDisplayOrientation, dmDisplayFixedOutput;
        public short dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
        public short dmLogPixels;
        public uint dmBitsPerPel, dmPelsWidth, dmPelsHeight, dmDisplayFlags, dmDisplayFrequency, dmICMMethod, dmICMIntent, dmMediaType, dmDitherType, dmReserved1, dmReserved2, dmPanningWidth, dmPanningHeight;
    }
}
