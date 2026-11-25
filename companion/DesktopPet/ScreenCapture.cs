using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace DesktopPet;

internal static class ScreenCapture
{
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
    public static byte[] CapturePrimaryJpeg()
    {
        int width = GetSystemMetrics(0), height = GetSystemMetrics(1);
        if (width < 1 || height < 1 || (long)width * height > 50_000_000) throw new InvalidOperationException("Не удалось определить безопасный размер экрана.");
        using var original = new Bitmap(width, height, PixelFormat.Format24bppRgb);
        using (var g = Graphics.FromImage(original)) g.CopyFromScreen(0, 0, 0, 0, original.Size, CopyPixelOperation.SourceCopy);
        // Limit image size and transfer. It stays in memory and is never written by CacheTrim to disk.
        double scale = Math.Min(1.0, Math.Min(1280.0 / width, 1280.0 / height));
        using var resized = new Bitmap(Math.Max(1, (int)(width * scale)), Math.Max(1, (int)(height * scale)), PixelFormat.Format24bppRgb);
        using (var g = Graphics.FromImage(resized)) { g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.DrawImage(original, 0, 0, resized.Width, resized.Height); }
        using var stream = new MemoryStream();
        resized.Save(stream, ImageFormat.Jpeg);
        return stream.ToArray();
    }
}
