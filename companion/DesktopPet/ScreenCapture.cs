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
    internal static (int Width, int Height) PrimarySize() => (GetSystemMetrics(0), GetSystemMetrics(1));
    // Reject a click when the pixels at its target changed after approval. This is not a
    // guarantee that the rest of the screen or the effect of the click is safe.
    internal static bool ClickAreaStable(byte[] before, byte[] after, int x, int y)
    {
        using var aStream = new MemoryStream(before); using var bStream = new MemoryStream(after);
        using var a = new Bitmap(aStream); using var b = new Bitmap(bStream);
        if (a.Width != b.Width || a.Height != b.Height) return false;
        int cx = (int)((long)x * (a.Width - 1) / 999), cy = (int)((long)y * (a.Height - 1) / 999);
        int count = 0, changed = 0;
        for (int dy = -12; dy <= 12; dy += 2) for (int dx = -12; dx <= 12; dx += 2)
        {
            int px = cx + dx, py = cy + dy; if (px < 0 || py < 0 || px >= a.Width || py >= a.Height) continue;
            var ca = a.GetPixel(px, py); var cb = b.GetPixel(px, py); count++;
            if (Math.Abs(ca.R - cb.R) + Math.Abs(ca.G - cb.G) + Math.Abs(ca.B - cb.B) > 48) changed++;
        }
        return count > 0 && changed * 10 <= count;
    }
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
