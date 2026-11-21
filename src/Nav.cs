using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CacheTrim
{
    /// <summary>Left navigation rail with hand-drawn icons and a hover pill.</summary>
    internal sealed class NavSidebar : Panel
    {
        private static readonly string[] Items = { "Память", "Очистка", "Оптимизация", "Автозагрузка", "Журнал", "Настройки" };

        private readonly List<Rectangle> _hit = new List<Rectangle>();
        private int _hover = -1;
        private int _selected;

        public event EventHandler SelectionChanged;

        public NavSidebar()
        {
            DoubleBuffered = true;
            BackColor = Theme.Surface;
            SetStyle(ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            Width = 224;
        }

        public int Selected
        {
            get { return _selected; }
            set
            {
                if (value < 0 || value >= Items.Length || value == _selected)
                    return;
                _selected = value;
                Invalidate();
                if (SelectionChanged != null)
                    SelectionChanged(this, EventArgs.Empty);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int found = -1;
            for (int i = 0; i < _hit.Count; i++)
            {
                if (_hit[i].Contains(e.Location))
                {
                    found = i;
                    break;
                }
            }

            if (found != _hover)
            {
                _hover = found;
                Cursor = found >= 0 ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hover = -1;
            Cursor = Cursors.Default;
            Invalidate();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            for (int i = 0; i < _hit.Count; i++)
            {
                if (_hit[i].Contains(e.Location))
                {
                    Selected = i;
                    return;
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.Clear(Theme.Surface);

            using (Pen edge = new Pen(Theme.Line))
                g.DrawLine(edge, Width - 1, 0, Width - 1, Height);

            // ---- brand block ----
            Rectangle mark = new Rectangle(22, 22, 38, 38);
            using (GraphicsPath p = Theme.Rounded(mark, 9))
            using (SolidBrush b = new SolidBrush(Theme.AccentSoft))
                g.FillPath(b, p);
            using (Font mf = new Font("Consolas", 12f, FontStyle.Bold))
            using (SolidBrush b = new SolidBrush(Theme.Accent))
            {
                StringFormat sf = new StringFormat();
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                g.DrawString("CT", mf, b, new RectangleF(mark.X, mark.Y + 1, mark.Width, mark.Height), sf);
            }

            using (Font bf = new Font("Segoe UI Semibold", 12f))
            using (SolidBrush b = new SolidBrush(Theme.Text))
                g.DrawString("CacheTrim", bf, b, 70, 22);
            using (Font sf = new Font("Segoe UI", 7.75f))
            using (SolidBrush b = new SolidBrush(Theme.Faint))
                g.DrawString("SYSTEM TOOLS  ·  " + AppInfo.Version, sf, b, 70, 44);

            using (Pen line = new Pen(Theme.Line))
                g.DrawLine(line, 22, 78, Width - 23, 78);

            // ---- items ----
            _hit.Clear();
            using (Font section = new Font("Segoe UI Semibold", 7.5f))
            using (SolidBrush sectionBrush = new SolidBrush(Theme.Faint))
                g.DrawString("ИНСТРУМЕНТЫ", section, sectionBrush, 24, 94);
            int top = 112;
            for (int i = 0; i < Items.Length; i++)
            {
                Rectangle row = new Rectangle(12, top, Width - 24, 44);
                _hit.Add(row);

                bool active = i == _selected;
                if (active)
                {
                    using (GraphicsPath p = Theme.Rounded(row, 7))
                    using (SolidBrush b = new SolidBrush(Theme.SurfaceAlt))
                        g.FillPath(b, p);
                    using (GraphicsPath glow = Theme.Rounded(new Rectangle(row.X, row.Y + 10, 3, 24), 2))
                    using (SolidBrush b = new SolidBrush(Theme.Accent))
                        g.FillPath(b, glow);
                }
                else if (i == _hover)
                {
                    using (GraphicsPath p = Theme.Rounded(row, 7))
                    using (SolidBrush b = new SolidBrush(Theme.Hover))
                        g.FillPath(b, p);
                }

                Color fg = active ? Theme.Text : (i == _hover ? Theme.Text : Theme.Muted);
                DrawIcon(g, i, new Rectangle(row.X + 14, row.Y + 14, 16, 16), active ? Theme.Accent : fg);

                using (Font f = new Font("Segoe UI", 9.5f, active ? FontStyle.Bold : FontStyle.Regular))
                using (SolidBrush b = new SolidBrush(fg))
                    g.DrawString(Items[i], f, b, row.X + 42, row.Y + 12);

                top += 49;
            }

            // ---- footer ----
            using (Font f = new Font("Segoe UI", 7.75f))
            using (SolidBrush b = new SolidBrush(Theme.Faint))
                g.DrawString(Native.IsElevated ? "права администратора" : "без прав администратора",
                    f, b, 20, Height - 44);
            using (SolidBrush b = new SolidBrush(Native.IsElevated ? Theme.Accent : Theme.Red))
                g.FillEllipse(b, new Rectangle(Width - 34, Height - 41, 7, 7));
        }

        private static void DrawIcon(Graphics g, int index, Rectangle r, Color color)
        {
            using (Pen p = new Pen(color, 1.6f))
            using (SolidBrush b = new SolidBrush(color))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;

                switch (index)
                {
                    case 0: // memory: three bars
                        g.FillRectangle(b, r.X, r.Y + 2, 16, 3);
                        g.FillRectangle(b, r.X, r.Y + 7, 11, 3);
                        g.FillRectangle(b, r.X, r.Y + 12, 14, 3);
                        break;

                    case 1: // clean: droplet
                        using (GraphicsPath drop = new GraphicsPath())
                        {
                            drop.AddBezier(r.X + 8, r.Y, r.X + 15, r.Y + 8, r.X + 15, r.Y + 11, r.X + 8, r.Y + 15);
                            drop.AddBezier(r.X + 8, r.Y + 15, r.X + 1, r.Y + 11, r.X + 1, r.Y + 8, r.X + 8, r.Y);
                            drop.CloseFigure();
                            using (Pen dp = new Pen(color, 1.5f))
                                g.DrawPath(dp, drop);
                        }
                        break;

                    case 2: // optimize: three sliders
                        g.DrawLine(p, r.X, r.Y + 3, r.X + 16, r.Y + 3);
                        g.DrawLine(p, r.X, r.Y + 8, r.X + 16, r.Y + 8);
                        g.DrawLine(p, r.X, r.Y + 13, r.X + 16, r.Y + 13);
                        g.FillEllipse(b, r.X + 3, r.Y + 1, 4, 4);
                        g.FillEllipse(b, r.X + 9, r.Y + 6, 4, 4);
                        g.FillEllipse(b, r.X + 5, r.Y + 11, 4, 4);
                        break;

                    case 3: // power
                        g.DrawArc(p, r.X + 1, r.Y + 2, 14, 14, -60, 300);
                        g.DrawLine(p, r.X + 8, r.Y, r.X + 8, r.Y + 7);
                        break;

                    case 4: // log lines
                        g.DrawLine(p, r.X, r.Y + 2, r.X + 16, r.Y + 2);
                        g.DrawLine(p, r.X, r.Y + 8, r.X + 12, r.Y + 8);
                        g.DrawLine(p, r.X, r.Y + 14, r.X + 14, r.Y + 14);
                        break;

                    default: // gear
                        g.DrawEllipse(p, r.X + 3, r.Y + 3, 10, 10);
                        g.FillEllipse(b, r.X + 6.5f, r.Y + 6.5f, 3, 3);
                        for (int k = 0; k < 6; k++)
                        {
                            double a = Math.PI * k / 3.0;
                            float x1 = r.X + 8 + (float)(Math.Cos(a) * 6.2);
                            float y1 = r.Y + 8 + (float)(Math.Sin(a) * 6.2);
                            float x2 = r.X + 8 + (float)(Math.Cos(a) * 8.0);
                            float y2 = r.Y + 8 + (float)(Math.Sin(a) * 8.0);
                            g.DrawLine(p, x1, y1, x2, y2);
                        }
                        break;
                }
            }
        }
    }
}
