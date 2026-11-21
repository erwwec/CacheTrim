using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CacheTrim
{
    /// <summary>Single source of truth for colours, fonts and card painting.</summary>
    internal static class Theme
    {
        public static readonly Color Bg = Color.FromArgb(10, 13, 17);
        public static readonly Color Surface = Color.FromArgb(17, 22, 29);
        public static readonly Color SurfaceAlt = Color.FromArgb(24, 31, 40);
        public static readonly Color Hover = Color.FromArgb(31, 40, 51);
        public static readonly Color Line = Color.FromArgb(39, 50, 63);
        public static readonly Color RowLine = Color.FromArgb(28, 37, 47);
        public static readonly Color Text = Color.FromArgb(237, 242, 248);
        public static readonly Color Muted = Color.FromArgb(145, 158, 174);
        public static readonly Color Faint = Color.FromArgb(94, 108, 125);
        public static readonly Color Accent = Color.FromArgb(139, 140, 255);
        public static readonly Color AccentSoft = Color.FromArgb(39, 39, 76);
        public static readonly Color Red = Color.FromArgb(226, 88, 74);
        public static readonly Color Amber = Color.FromArgb(240, 180, 41);
        public static readonly Color Blue = Color.FromArgb(83, 177, 255);
        public static readonly Color Violet = Color.FromArgb(190, 139, 255);

        public static Font H1
        {
            get { return new Font("Segoe UI Semibold", 18f); }
        }

        public static Font H2
        {
            get { return new Font("Segoe UI Semibold", 11.5f); }
        }

        public static Font Body
        {
            get { return new Font("Segoe UI", 9.5f); }
        }

        public static Font Small
        {
            get { return new Font("Segoe UI", 8.25f); }
        }

        public static Font Mono
        {
            get { return new Font("Consolas", 8.75f); }
        }

        public static Font MonoBold
        {
            get { return new Font("Consolas", 8.75f, FontStyle.Bold); }
        }

        public static Font Value
        {
            get { return new Font("Consolas", 15f, FontStyle.Bold); }
        }

        public static GraphicsPath Rounded(Rectangle r, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = radius * 2;
            if (d > r.Width) d = r.Width;
            if (d > r.Height) d = r.Height;
            if (d <= 0)
            {
                path.AddRectangle(r);
                return path;
            }

            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static void Card(Graphics g, Rectangle r, Color fill)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = Rounded(r, 10))
            {
                using (SolidBrush b = new SolidBrush(fill))
                    g.FillPath(b, path);
                using (Pen p = new Pen(Line, 1f))
                    g.DrawPath(p, path);
            }
        }

        public static void Bar(Graphics g, Rectangle track, double ratio, Color color)
        {
            using (SolidBrush back = new SolidBrush(Color.FromArgb(34, 39, 47)))
                g.FillRectangle(back, track);

            if (ratio <= 0)
                return;

            if (ratio > 1) ratio = 1;
            int w = (int)Math.Round(track.Width * ratio);
            if (w < 2 && ratio > 0) w = 2;
            using (SolidBrush fill = new SolidBrush(color))
                g.FillRectangle(fill, new Rectangle(track.X, track.Y, w, track.Height));
        }

        public static Color Blend(Color a, Color b, double t)
        {
            if (t < 0) t = 0;
            if (t > 1) t = 1;
            return Color.FromArgb(
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }
    }

    /// <summary>Paints a rounded surface; children must use Surface as their BackColor.</summary>
    internal class CardPanel : Panel
    {
        public CardPanel()
        {
            DoubleBuffered = true;
            BackColor = Theme.Surface;
            SetStyle(ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Theme.Bg);
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            Theme.Card(e.Graphics, r, Theme.Surface);
        }
    }

    /// <summary>Section header used inside cards.</summary>
    internal sealed class CardTitle : Label
    {
        public CardTitle(string text)
        {
            Text = text.ToUpperInvariant();
            Font = new Font("Segoe UI Semibold", 8.25f);
            ForeColor = Theme.Faint;
            BackColor = Theme.Surface;
            AutoSize = false;
            Height = 20;
        }
    }
}
