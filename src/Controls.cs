using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CacheTrim
{
    /// <summary>
    /// Rounded button drawn by hand: Windows' own chrome stays light-grey on a dark
    /// surface, and a flat rectangle with square corners reads as a placeholder.
    /// </summary>
    internal sealed class UiButton : Button
    {
        public bool Accent;
        private bool _hover;
        private bool _down;

        public UiButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Font = new Font("Segoe UI Semibold", 9f);
            Height = 38;
            TabStop = false;
            Cursor = Cursors.Hand;
        }

        protected override bool ShowFocusCues
        {
            get { return false; }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _hover = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hover = false;
            _down = false;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            _down = true;
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _down = false;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Bg);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            Rectangle r = new Rectangle(1, 1, Math.Max(1, Width - 3), Math.Max(1, Height - 3));

            Color fill;
            Color border;
            Color text;

            if (Accent)
            {
                fill = _down ? Theme.Blend(Theme.Accent, Color.Black, 0.18)
                    : (_hover ? Theme.Blend(Theme.Accent, Color.White, 0.08) : Theme.Accent);
                border = Theme.Blend(fill, Color.White, 0.12);
                text = Color.FromArgb(13, 14, 27);
            }
            else
            {
                fill = _down ? Theme.SurfaceAlt : (_hover ? Theme.Hover : Theme.Surface);
                border = Theme.Line;
                text = Enabled ? Theme.Text : Theme.Muted;
            }

            using (GraphicsPath path = Theme.Rounded(r, 9))
            {
                using (LinearGradientBrush b = new LinearGradientBrush(r,
                    Accent ? Theme.Blend(fill, Color.White, 0.05) : Theme.Blend(fill, Color.White, 0.035),
                    fill, LinearGradientMode.Vertical))
                    g.FillPath(b, path);
                using (Pen p = new Pen(border))
                    g.DrawPath(p, path);
                if (!Accent && Width > 4 && Height > 12)
                {
                    using (Pen edge = new Pen(Theme.Line))
                        g.DrawLine(edge, r.Right, r.Y + 8, r.Right, r.Bottom - 8);
                }
            }

            Rectangle label = new Rectangle(r.X + 8, r.Y + 1, Math.Max(1, r.Width - 16), r.Height - 2);
            TextRenderer.DrawText(g, Text, Font, label, text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
        }
    }

    /// <summary>Pill switch. No caption — the label lives next to it in the row.</summary>
    internal sealed class ToggleSwitch : CheckBox
    {
        private bool _hover;

        public ToggleSwitch()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            AutoSize = false;
            Size = new Size(42, 22);
            Cursor = Cursors.Hand;
            TabStop = false;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _hover = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hover = false;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Theme.Surface);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle track = new Rectangle(0, 2, Width - 1, Height - 5);

            Color off = _hover ? Color.FromArgb(56, 64, 76) : Color.FromArgb(44, 51, 61);
            Color on = _hover ? Theme.Blend(Theme.Accent, Color.White, 0.10) : Theme.Accent;

            using (GraphicsPath path = Theme.Rounded(track, track.Height / 2))
            using (SolidBrush b = new SolidBrush(Checked ? on : off))
                g.FillPath(b, path);

            int d = track.Height - 6;
            int x = Checked ? track.Right - d - 3 : track.X + 3;
            Rectangle knob = new Rectangle(x, track.Y + 3, d, d);

            using (SolidBrush b = new SolidBrush(Checked ? Color.FromArgb(13, 14, 27) : Color.FromArgb(154, 166, 180)))
                g.FillEllipse(b, knob);
        }
    }

    /// <summary>Checkbox with a hand-drawn box so the system blue glyph never shows up.</summary>
    internal sealed class DarkCheck : CheckBox
    {
        private bool _hover;

        public DarkCheck()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            AutoSize = false;
            Height = 22;
            BackColor = Theme.Surface;
            Font = new Font("Segoe UI", 9.5f);
            Cursor = Cursors.Hand;
            TabStop = false;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _hover = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hover = false;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.Clear(BackColor);

            Rectangle box = new Rectangle(0, (Height - 16) / 2, 16, 16);

            using (GraphicsPath path = Theme.Rounded(box, 4))
            {
                Color fill = Checked
                    ? Theme.Accent
                    : (_hover ? Color.FromArgb(30, 36, 44) : Color.FromArgb(20, 24, 30));
                using (SolidBrush b = new SolidBrush(fill))
                    g.FillPath(b, path);

                if (!Checked)
                {
                    using (Pen p = new Pen(_hover ? Color.FromArgb(78, 88, 102) : Theme.Line))
                        g.DrawPath(p, path);
                }
            }

            if (Checked)
            {
                using (Pen p = new Pen(Color.FromArgb(13, 14, 27), 1.8f))
                {
                    p.StartCap = LineCap.Round;
                    p.EndCap = LineCap.Round;
                    p.LineJoin = LineJoin.Round;
                    g.DrawLines(p, new Point[]
                    {
                        new Point(box.X + 4, box.Y + 8),
                        new Point(box.X + 7, box.Y + 11),
                        new Point(box.X + 12, box.Y + 5)
                    });
                }
            }

            Rectangle text = new Rectangle(24, 0, Math.Max(10, Width - 24), Height);
            TextRenderer.DrawText(g, Text, Font, text, Enabled ? Theme.Text : Theme.Muted,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left |
                TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
        }
    }

    /// <summary>Small rounded badge: state, source, tags.</summary>
    internal sealed class Chip : Control
    {
        public Color Tone = Theme.Faint;

        public Chip()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Surface;
            Font = new Font("Consolas", 7.25f, FontStyle.Bold);
            Size = new Size(96, 20);
        }

        public void Set(string caption, Color tone)
        {
            Tone = tone;
            Text = caption;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.Clear(BackColor);

            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = Theme.Rounded(r, Height / 2))
            {
                using (SolidBrush b = new SolidBrush(Color.FromArgb(26, Tone)))
                    g.FillPath(b, path);
                using (Pen p = new Pen(Color.FromArgb(64, Tone)))
                    g.DrawPath(p, path);
            }

            TextRenderer.DrawText(g, Text, Font, r, Tone,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
        }
    }

    /// <summary>− value + field; the system NumericUpDown keeps white spinner buttons.</summary>
    internal sealed class Stepper : Control
    {
        private int _value;
        private int _minimum;
        private int _maximum = 100;
        private int _zone = -1;

        public event EventHandler ValueChanged;

        public Stepper()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Bg;
            Font = new Font("Consolas", 10f, FontStyle.Bold);
            Size = new Size(150, 32);
            Cursor = Cursors.Hand;
            TabStop = false;
        }

        public int Minimum
        {
            get { return _minimum; }
            set { _minimum = value; Clamp(); }
        }

        public int Maximum
        {
            get { return _maximum; }
            set { _maximum = value; Clamp(); }
        }

        public int Value
        {
            get { return _value; }
            set
            {
                int next = value < _minimum ? _minimum : (value > _maximum ? _maximum : value);
                if (next == _value)
                    return;
                _value = next;
                Invalidate();
                if (ValueChanged != null)
                    ValueChanged(this, EventArgs.Empty);
            }
        }

        private void Clamp()
        {
            if (_value < _minimum) _value = _minimum;
            if (_value > _maximum) _value = _maximum;
            Invalidate();
        }

        private Rectangle Minus
        {
            get { return new Rectangle(1, 1, 30, Height - 3); }
        }

        private Rectangle Plus
        {
            get { return new Rectangle(Width - 31, 1, 30, Height - 3); }
        }

        private Rectangle Field
        {
            get { return new Rectangle(0, 0, Width - 1, Height - 1); }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int zone = Minus.Contains(e.Location) ? 0 : (Plus.Contains(e.Location) ? 2 : 1);
            if (zone != _zone)
            {
                _zone = zone;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _zone = -1;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (Minus.Contains(e.Location))
                Value = _value - 1;
            else if (Plus.Contains(e.Location))
                Value = _value + 1;
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            Value = _value + (e.Delta > 0 ? 1 : -1);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            using (GraphicsPath path = Theme.Rounded(Field, 7))
            {
                using (SolidBrush b = new SolidBrush(Theme.SurfaceAlt))
                    g.FillPath(b, path);
                using (Pen p = new Pen(Theme.Line))
                    g.DrawPath(p, path);
            }

            if (_zone == 0)
                FillZone(g, Minus);
            else if (_zone == 2)
                FillZone(g, Plus);

            DrawSign(g, Minus, false);
            DrawSign(g, Plus, true);

            Rectangle middle = new Rectangle(32, 0, Width - 64, Height);
            TextRenderer.DrawText(g, _value.ToString(), Font, middle, Theme.Text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding);
        }

        private static void FillZone(Graphics g, Rectangle zone)
        {
            using (GraphicsPath path = Theme.Rounded(zone, 6))
            using (SolidBrush b = new SolidBrush(Theme.Hover))
                g.FillPath(b, path);
        }

        private static void DrawSign(Graphics g, Rectangle zone, bool plus)
        {
            int cx = zone.X + zone.Width / 2;
            int cy = zone.Y + zone.Height / 2;

            using (Pen p = new Pen(Theme.Muted, 1.6f))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                g.DrawLine(p, cx - 5, cy, cx + 5, cy);
                if (plus)
                    g.DrawLine(p, cx, cy - 5, cx, cy + 5);
            }
        }
    }

    /// <summary>Row inside a card: bottom hairline separates options without a table.</summary>
    internal sealed class RowPanel : Panel
    {
        public bool ShowDivider = true;

        public RowPanel()
        {
            DoubleBuffered = true;
            BackColor = Theme.Surface;
            SetStyle(ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Theme.Surface);

            if (!ShowDivider)
                return;

            using (Pen p = new Pen(Theme.RowLine))
                e.Graphics.DrawLine(p, 0, Height - 1, Width, Height - 1);
        }
    }

    /// <summary>
    /// Read-only log surface with a dark scrollbar of its own. RichTextBox keeps its
    /// native light scrollbar on a dark theme, so the bar is switched off and drawn here;
    /// scrolling goes through EM_LINESCROLL.
    /// </summary>
    internal sealed class LogView : Panel
    {
        private readonly RichTextBox _box;
        private bool _dragging;
        private int _dragStartY;
        private int _dragStartLine;

        public LogView()
        {
            DoubleBuffered = true;
            BackColor = Theme.Bg;
            SetStyle(ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true);

            _box = new RichTextBox();
            _box.BackColor = Color.FromArgb(9, 11, 14);
            _box.ForeColor = Color.FromArgb(198, 208, 222);
            _box.BorderStyle = BorderStyle.None;
            _box.Font = Theme.Mono;
            _box.ReadOnly = true;
            _box.WordWrap = true;
            _box.DetectUrls = false;
            _box.ScrollBars = RichTextBoxScrollBars.None;
            _box.MouseWheel += OnWheel;
            _box.MouseMove += OnBoxMove;
            _box.MouseDown += OnBoxDown;
            _box.MouseUp += OnBoxUp;
            Controls.Add(_box);
        }

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            _box.SetBounds(0, 0, Math.Max(0, Width - 14), Height);
            Invalidate();
        }

        public void Append(string text)
        {
            _box.AppendText(text);
            ScrollToEnd();
        }

        public void SetText(string text)
        {
            _box.Text = text;
            ScrollToEnd();
        }

        private void ScrollToEnd()
        {
            _box.SelectionStart = _box.TextLength;
            _box.ScrollToCaret();
            Invalidate();
        }

        private void OnWheel(object sender, MouseEventArgs e)
        {
            Native.ScrollLines(_box.Handle, e.Delta > 0 ? -3 : 3);
            Invalidate();
        }

        private int LineHeight
        {
            get { return Math.Max(1, _box.Font.Height); }
        }

        private int VisibleLines
        {
            get { return Math.Max(1, _box.ClientSize.Height / LineHeight); }
        }

        private Rectangle Thumb()
        {
            int total = Native.LineCount(_box.Handle);
            if (total <= VisibleLines || Height < 20)
                return Rectangle.Empty;

            int track = Height - 8;
            int height = Math.Max(30, track * VisibleLines / total);
            int first = Native.FirstVisibleLine(_box.Handle);
            int span = Math.Max(1, total - VisibleLines);
            int y = 4 + (int)((long)(track - height) * Math.Min(first, span) / span);
            return new Rectangle(Width - 9, y, 5, height);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Theme.Bg);

            if (Thumb().IsEmpty)
                return;

            using (SolidBrush track = new SolidBrush(Color.FromArgb(26, 30, 36)))
                g.FillRectangle(track, new Rectangle(Width - 9, 4, 5, Height - 8));

            using (System.Drawing.Drawing2D.GraphicsPath path = Theme.Rounded(Thumb(), 2))
            using (SolidBrush b = new SolidBrush(_dragging ? Theme.Accent : Color.FromArgb(62, 70, 82)))
                g.FillPath(b, path);
        }

        private void OnBoxDown(object sender, MouseEventArgs e)
        {
            if (!Thumb().Contains(ToHost(e.Location)))
                return;
            _dragging = true;
            _dragStartY = e.Y;
            _dragStartLine = Native.FirstVisibleLine(_box.Handle);
        }

        private void OnBoxMove(object sender, MouseEventArgs e)
        {
            if (!_dragging)
                return;

            int total = Native.LineCount(_box.Handle);
            int span = Math.Max(1, total - VisibleLines);
            int track = Math.Max(1, Height - 8 - Thumb().Height);
            int target = _dragStartLine + (e.Y - _dragStartY) * span / track;
            if (target < 0) target = 0;
            if (target > span) target = span;

            Native.ScrollLines(_box.Handle, target - Native.FirstVisibleLine(_box.Handle));
            Invalidate();
        }

        private void OnBoxUp(object sender, MouseEventArgs e)
        {
            _dragging = false;
            Invalidate();
        }

        private Point ToHost(Point p)
        {
            return new Point(p.X + _box.Left, p.Y + _box.Top);
        }
    }
}
