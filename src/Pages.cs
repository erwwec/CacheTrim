using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace CacheTrim
{
    // ------------------------------------------------------------------ shared bits

    internal static class Ui
    {
        public static Button Primary(string text)
        {
            UiButton b = new UiButton();
            b.Text = text;
            b.Accent = true;
            return b;
        }

        public static Button Ghost(string text)
        {
            UiButton b = new UiButton();
            b.Text = text;
            b.Accent = false;
            return b;
        }

        public static Label Text(string value, Font font, Color color)
        {
            Label l = new Label();
            l.Text = value;
            l.Font = font;
            l.ForeColor = color;
            l.BackColor = Color.Transparent;
            l.AutoSize = false;
            return l;
        }
    }

    /// <summary>
    /// Vertical stack with a dark, hand-drawn scrollbar. WinForms' native scrollbar
    /// stays light on dark themes, so pages that overflow draw their own instead.
    /// </summary>
    internal sealed class ScrollHost : Panel
    {
        private readonly List<Control> _items = new List<Control>();
        private int _offset;
        private bool _dragging;
        private int _dragStartY;
        private int _dragStartOffset;

        public int Gap = 14;
        public int Gutter = 22;
        public int Columns = 1;
        public Predicate<Control> Include;

        /// <summary>
        /// Extra scrollable space under the last card, so a docked footer never covers it.
        /// </summary>
        public int BottomPad = 0;

        public ScrollHost()
        {
            DoubleBuffered = true;
            BackColor = Theme.Bg;
            SetStyle(ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true);
        }

        private int ContentHeight
        {
            get
            {
                int columns = EffectiveColumns;
                int max = BottomPad;
                for (int column = 0; column < columns; column++)
                {
                    int total = BottomPad;
                    int count = 0;
                    int visibleIndex = 0;
                    for (int i = 0; i < _items.Count; i++)
                    {
                        if (!IsIncluded(_items[i])) continue;
                        if (visibleIndex % columns == column)
                        {
                            total += _items[i].Height + (count > 0 ? Gap : 0);
                            count++;
                        }
                        visibleIndex++;
                    }
                    if (total > max) max = total;
                }
                return max;
            }
        }

        private int EffectiveColumns
        {
            get { return Columns > 1 && ClientSize.Width >= 600 ? Columns : 1; }
        }

        private bool IsIncluded(Control item)
        {
            return Include == null || Include(item);
        }

        public void Clear()
        {
            foreach (Control c in _items)
            {
                Controls.Remove(c);
                c.Dispose();
            }
            _items.Clear();
            _offset = 0;
            Invalidate();
        }

        public void Add(Control item)
        {
            _items.Add(item);
            Controls.Add(item);
            item.MouseWheel += Forward;
            Relayout();
        }

        private void Forward(object sender, MouseEventArgs e)
        {
            ScrollBy(e.Delta);
        }

        public void Relayout()
        {
            int columns = EffectiveColumns;
            _offset = Math.Min(_offset, Math.Max(0, ContentHeight - ClientSize.Height));
            int available = Math.Max(200, ClientSize.Width - Gutter - Gap * (columns - 1));
            int width = Math.Max(200, available / columns);
            int[] tops = new int[columns];
            for (int i = 0; i < columns; i++) tops[i] = -_offset;
            int visibleIndex = 0;
            for (int i = 0; i < _items.Count; i++)
            {
                Control c = _items[i];
                if (!IsIncluded(c)) continue;
                int column = visibleIndex % columns;
                c.SetBounds(column * (width + Gap), tops[column], width, c.Height);
                tops[column] += c.Height + Gap;
                visibleIndex++;
            }
            Invalidate();
        }

        public void ScrollBy(int delta)
        {
            int max = Math.Max(0, ContentHeight - ClientSize.Height);
            int next = _offset - delta * 60 / 120;
            if (next < 0) next = 0;
            if (next > max) next = max;
            if (next == _offset)
                return;

            _offset = next;
            Relayout();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            ScrollBy(e.Delta);
        }

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            Relayout();
        }

        private Rectangle Thumb()
        {
            int content = ContentHeight;
            if (content <= ClientSize.Height)
                return Rectangle.Empty;

            int trackHeight = ClientSize.Height - 8;
            int height = Math.Max(30, trackHeight * ClientSize.Height / content);
            int max = content - ClientSize.Height;
            int y = 4 + (int)((trackHeight - height) * (double)_offset / Math.Max(1, max));
            return new Rectangle(Width - 9, y, 5, height);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Theme.Bg);

            int content = ContentHeight;
            if (content <= ClientSize.Height)
                return;

            using (SolidBrush track = new SolidBrush(Color.FromArgb(26, 30, 36)))
                g.FillRectangle(track, new Rectangle(Width - 9, 4, 5, ClientSize.Height - 8));

            Rectangle thumb = Thumb();
            if (thumb.IsEmpty)
                return;

            using (GraphicsPath path = Theme.Rounded(thumb, 2))
            using (SolidBrush b = new SolidBrush(_dragging ? Theme.Accent : Color.FromArgb(62, 70, 82)))
                g.FillPath(b, path);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (Thumb().Contains(e.Location))
            {
                _dragging = true;
                _dragStartY = e.Y;
                _dragStartOffset = _offset;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!_dragging)
                return;

            int content = ContentHeight;
            int max = Math.Max(1, content - ClientSize.Height);
            int trackHeight = Math.Max(1, ClientSize.Height - 8 - Thumb().Height);
            int delta = (e.Y - _dragStartY) * max / trackHeight;
            int next = _dragStartOffset + delta;
            if (next < 0) next = 0;
            if (next > max) next = max;
            _offset = next;
            Relayout();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _dragging = false;
            Invalidate();
        }
    }

    internal abstract class Page : Panel
    {
        protected readonly MainForm Host;
        protected readonly Panel Body;
        protected readonly Panel Content;

        protected Page(MainForm host)
        {
            Host = host;
            Dock = DockStyle.Fill;
            BackColor = Theme.Bg;
            Visible = false;
            Padding = new Padding(30, 16, 30, 16);
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);

            Body = new Panel();
            Body.Dock = DockStyle.Fill;
            Body.BackColor = Theme.Bg;
            Body.Padding = new Padding(0, 76, 0, 0);
            Body.Paint += delegate(object sender, PaintEventArgs e) { DrawHeader(e.Graphics); };
            Controls.Add(Body);

            Content = new Panel();
            Content.Dock = DockStyle.Fill;
            Content.BackColor = Theme.Bg;
            Body.Controls.Add(Content);
        }

        public abstract string Title { get; }

        protected virtual string Subtitle
        {
            get { return string.Empty; }
        }

        public void ShowPage()
        {
            Visible = true;
            BringToFront();
            OnActivated();
            RefreshData();
        }

        public void HidePage()
        {
            Visible = false;
        }

        protected virtual void OnActivated()
        {
        }

        public virtual void RefreshData()
        {
        }

        /// <summary>
        /// Docking order between a Fill child and a Bottom child is z-order dependent and
        /// easy to get wrong, so the scroll viewport is sized by hand instead: it always
        /// ends where the footer begins, never underneath it.
        /// </summary>
        protected void FitAboveFooter(Control viewport, Panel footer)
        {
            viewport.Dock = DockStyle.None;
            EventHandler fit = delegate
            {
                viewport.SetBounds(0, 0, Content.ClientSize.Width,
                    Math.Max(0, Content.ClientSize.Height - footer.Height));
            };
            Content.Resize += fit;
            fit(null, EventArgs.Empty);
        }

        private void DrawHeader(Graphics g)
        {
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            using (GraphicsPath mark = Theme.Rounded(new Rectangle(0, 5, 3, 23), 2))
            using (SolidBrush accent = new SolidBrush(Theme.Accent))
                g.FillPath(accent, mark);

            using (Font h = Theme.H1)
            using (SolidBrush b = new SolidBrush(Theme.Text))
                g.DrawString(Title, h, b, 12, 2);

            if (Subtitle.Length > 0)
            {
                using (Font s = Theme.Body)
                using (SolidBrush b = new SolidBrush(Theme.Muted))
                    g.DrawString(Subtitle, s, b, 14, 40);
            }

            using (LinearGradientBrush line = new LinearGradientBrush(
                new Rectangle(0, 70, Math.Max(1, Body.Width), 1), Theme.Accent, Theme.Line, 0f))
                g.FillRectangle(line, 0, 70, Body.Width, 1);
        }
    }

    // ------------------------------------------------------------------ memory page

    internal sealed class StatCard : Control
    {
        public string Caption = "";
        public string Value = "";
        public string Detail = "";
        public Color Accent = Theme.Accent;
        public double Fraction;

        public StatCard()
        {
            DoubleBuffered = true;
            BackColor = Theme.Bg;
            SetStyle(ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            Theme.Card(g, new Rectangle(0, 0, Width - 1, Height - 1), Theme.Surface);

            using (Font cap = new Font("Segoe UI Semibold", 8f))
            using (SolidBrush b = new SolidBrush(Theme.Faint))
                g.DrawString(Caption.ToUpperInvariant(), cap, b, 14, 12);

            using (Font vf = Theme.Value)
            using (SolidBrush b = new SolidBrush(Theme.Text))
                g.DrawString(Value, vf, b, 12, 30);

            using (Font df = Theme.Small)
            using (SolidBrush b = new SolidBrush(Theme.Muted))
                g.DrawString(Detail, df, b, 14, Height - 36);

            Theme.Bar(g, new Rectangle(14, Height - 16, Width - 28, 4), Fraction, Accent);
        }
    }

    internal sealed class Sparkline : Control
    {
        private readonly List<double> _samples = new List<double>();
        private const int Capacity = 120;

        public Sparkline()
        {
            DoubleBuffered = true;
            BackColor = Theme.Bg;
            SetStyle(ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        }

        public void Add(double value)
        {
            _samples.Add(value < 0 ? 0 : (value > 1 ? 1 : value));
            if (_samples.Count > Capacity)
                _samples.RemoveAt(0);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            Theme.Card(g, new Rectangle(0, 0, Width - 1, Height - 1), Theme.Surface);

            using (Font cap = new Font("Segoe UI Semibold", 8f))
            using (SolidBrush b = new SolidBrush(Theme.Faint))
                g.DrawString("ЗАГРУЗКА ПАМЯТИ · ПОСЛЕДНИЕ 3 МИНУТЫ", cap, b, 14, 12);

            Rectangle plot = new Rectangle(42, 40, Width - 74, Height - 58);

            using (Font lf = Theme.Small)
            using (SolidBrush b = new SolidBrush(Theme.Faint))
            {
                g.DrawString("100%", lf, b, 10, plot.Y - 2);
                g.DrawString("75%", lf, b, 13, plot.Y + plot.Height / 4 - 6);
                g.DrawString("50%", lf, b, 13, plot.Y + plot.Height / 2 - 6);
                g.DrawString("25%", lf, b, 13, plot.Y + plot.Height * 3 / 4 - 6);
                g.DrawString("0%", lf, b, 19, plot.Bottom - 11);
                g.DrawString("3 мин", lf, b, plot.X, plot.Bottom + 5);
                g.DrawString("сейчас", lf, b, plot.Right - 40, plot.Bottom + 5);
            }

            using (Pen grid = new Pen(Color.FromArgb(30, 35, 42)))
            {
                for (int i = 0; i <= 4; i++)
                {
                    int y = plot.Y + plot.Height * i / 4;
                    g.DrawLine(grid, plot.X, y, plot.Right, y);
                }
            }

            if (_samples.Count == 0)
                return;

            // Pad the unobserved part of the three-minute window with the first sample,
            // so the graph remains readable immediately after launch.
            PointF[] points = new PointF[Capacity];
            float step = (float)plot.Width / (Capacity - 1);
            int padding = Capacity - _samples.Count;
            for (int i = 0; i < Capacity; i++)
            {
                int sample = Math.Max(0, i - padding);
                double value = _samples[sample];
                float x = plot.X + i * step;
                float y = plot.Bottom - (float)(value * plot.Height);
                points[i] = new PointF(x, y);
            }

            using (GraphicsPath area = new GraphicsPath())
            {
                area.AddLines(points);
                area.AddLine(points[points.Length - 1].X, plot.Bottom, points[0].X, plot.Bottom);
                area.CloseFigure();
                using (LinearGradientBrush fill = new LinearGradientBrush(
                    new Rectangle(plot.X, plot.Y, Math.Max(1, plot.Width), Math.Max(1, plot.Height)),
                    Color.FromArgb(70, Theme.Accent), Color.FromArgb(0, Theme.Accent), 90f))
                {
                    g.FillPath(fill, area);
                }
            }

            using (Pen line = new Pen(Theme.Accent, 1.8f))
            {
                line.LineJoin = LineJoin.Round;
                g.DrawLines(line, points);
            }

            double last = _samples[_samples.Count - 1];
            using (Font vf = new Font("Consolas", 11f, FontStyle.Bold))
            using (SolidBrush b = new SolidBrush(Theme.Accent))
            {
                SizeF size = g.MeasureString(
                    (last * 100).ToString("0", CultureInfo.InvariantCulture) + "%", vf);
                g.DrawString((last * 100).ToString("0", CultureInfo.InvariantCulture) + "%",
                    vf, b, Width - 18 - size.Width, 8);
            }
        }
    }

    internal sealed class MemoryPage : Page
    {
        private readonly StatCard _used;
        private readonly StatCard _standby;
        private readonly StatCard _cache;
        private readonly StatCard _commit;
        private readonly Sparkline _spark;
        private readonly Panel _actions;
        private readonly Button _btnMemory;
        private readonly Button _btnFull;
        private readonly Button _btnDiagnostic;

        public MemoryPage(MainForm host) : base(host)
        {
            _used = new StatCard { Caption = "Занято", Accent = Theme.Red };
            _standby = new StatCard { Caption = "Stand-by", Accent = Theme.Amber };
            _cache = new StatCard { Caption = "Системный кэш", Accent = Theme.Accent };
            _commit = new StatCard { Caption = "Commit", Accent = Theme.Blue };

            Content.Controls.Add(_used);
            Content.Controls.Add(_standby);
            Content.Controls.Add(_cache);
            Content.Controls.Add(_commit);

            _spark = new Sparkline();
            Content.Controls.Add(_spark);

            _actions = new Panel();
            _actions.Dock = DockStyle.Bottom;
            _actions.Height = 54;
            _actions.BackColor = Theme.Bg;
            _actions.Paint += delegate(object sender, PaintEventArgs e)
            {
                using (Pen p = new Pen(Theme.Line)) e.Graphics.DrawLine(p, 0, 0, _actions.Width, 0);
            };
            Content.Controls.Add(_actions);

            _btnMemory = Ui.Primary("ОЧИСТИТЬ ПАМЯТЬ");
            _btnMemory.Width = 198;
            _btnMemory.Location = new Point(0, 6);
            _btnMemory.Click += delegate { Host.RunMemoryPurge(); };
            _actions.Controls.Add(_btnMemory);

            _btnFull = Ui.Ghost("ПОЛНЫЙ ПРОГОН");
            _btnFull.Width = 190;
            _btnFull.Location = new Point(210, 6);
            _btnFull.Click += delegate { Host.RunFullPass(); };
            _actions.Controls.Add(_btnFull);

            _btnDiagnostic = Ui.Ghost("ДИАГНОСТИКА СИСТЕМЫ");
            _btnDiagnostic.Width = 210;
            _btnDiagnostic.Location = new Point(412, 6);
            _btnDiagnostic.Click += delegate { Host.ShowDiagnostics(); };
            _actions.Controls.Add(_btnDiagnostic);

            Content.Resize += delegate { LayoutCards(); };
            LayoutCards();
        }

        public override string Title
        {
            get { return "Память"; }
        }

        protected override string Subtitle
        {
            get { return "живые счётчики и освобождение standby-списка — те же вызовы, что у RAMMap"; }
        }

        private void LayoutCards()
        {
            int gap = 12;
            int width = Content.ClientSize.Width;
            StatCard[] cards = { _used, _standby, _cache, _commit };
            int columns = width < 780 ? 2 : 4;
            int cardWidth = Math.Max(120, (width - gap * (columns - 1)) / columns);
            for (int i = 0; i < cards.Length; i++)
                cards[i].SetBounds((i % columns) * (cardWidth + gap), (i / columns) * 104, cardWidth, 92);

            int sparkTop = columns == 2 ? 196 : 104;
            int sparkHeight = Math.Max(140, Content.ClientSize.Height - sparkTop - _actions.Height - 10);
            _spark.SetBounds(0, sparkTop, width, sparkHeight);
        }

        public override void RefreshData()
        {
            Native.MemStats s = Native.GetMemoryStats();
            if (s.TotalPhys == 0)
                return;

            double usedRatio = (double)(s.TotalPhys - s.AvailPhys) / s.TotalPhys;

            _used.Value = Cleaner.FormatBytes((long)(s.TotalPhys - s.AvailPhys));
            _used.Detail = "из " + Cleaner.FormatBytes((long)s.TotalPhys) + " всего";
            _used.Fraction = usedRatio;
            _used.Invalidate();

            _standby.Value = Cleaner.FormatBytes((long)s.Standby);
            _standby.Detail = "доступно " + Cleaner.FormatBytes((long)s.AvailPhys);
            _standby.Fraction = s.TotalPhys == 0 ? 0 : (double)s.Standby / s.TotalPhys;
            _standby.Invalidate();

            _cache.Value = Cleaner.FormatBytes((long)s.Cache);
            _cache.Detail = s.TotalPhys == 0 ? "" :
                ((double)s.Cache / s.TotalPhys * 100).ToString("0.0", CultureInfo.InvariantCulture) + "% от RAM";
            _cache.Fraction = s.TotalPhys == 0 ? 0 : (double)s.Cache / s.TotalPhys;
            _cache.Invalidate();

            _commit.Value = Cleaner.FormatBytes((long)s.CommitUsed);
            _commit.Detail = "лимит " + Cleaner.FormatBytes((long)s.CommitLimit);
            _commit.Fraction = s.CommitLimit == 0 ? 0 : (double)s.CommitUsed / s.CommitLimit;
            _commit.Invalidate();

            _spark.Add(usedRatio);
        }
    }

    // ------------------------------------------------------------------ clean page

    internal sealed class CleanPage : Page
    {
        private sealed class Binding
        {
            public CheckBox Box;
            public Action<Config, bool> Apply;
            public Func<Config, bool> Read;
        }

        private readonly ScrollHost _scroll;
        private readonly List<Binding> _bindings = new List<Binding>();
        private readonly List<CardPanel> _cards = new List<CardPanel>();
        private readonly Panel _footer;

        public CleanPage(MainForm host) : base(host)
        {
            _footer = new Panel();
            _footer.Dock = DockStyle.Bottom;
            _footer.Height = 54;
            _footer.BackColor = Theme.Bg;
            Content.Controls.Add(_footer);

            Button save = Ui.Primary("СОХРАНИТЬ ГАЛОЧКИ");
            save.Width = 210;
            save.Location = new Point(0, 6);
            save.Click += delegate
            {
                ApplyTo(Host.Settings);
                Host.Settings.Save();
                Host.LogLine("настройки сохранены: " + Config.ConfigPath);
            };
            _footer.Controls.Add(save);

            Button reset = Ui.Ghost("ПО УМОЛЧАНИЮ");
            reset.Width = 170;
            reset.Location = new Point(222, 6);
            reset.Click += delegate
            {
                LoadFrom(new Config());
                Host.LogLine("галочки сброшены к рекомендуемым (не сохранены).");
            };
            _footer.Controls.Add(reset);

            _scroll = new ScrollHost();
            _scroll.Dock = DockStyle.Fill;
            _scroll.Columns = 2;
            _scroll.Gap = 12;
            _scroll.BottomPad = 16;
            Content.Controls.Add(_scroll);
            Content.Controls.SetChildIndex(_scroll, 0);
            FitAboveFooter(_scroll, _footer);

            BuildCards();
            LoadFrom(host.Settings);
        }

        public override string Title
        {
            get { return "Очистка"; }
        }

        protected override string Subtitle
        {
            get { return "что выгребать при ручном запуске и на входе в систему"; }
        }

        private void BuildCards()
        {
            CardPanel memory = NewCard("Оперативная память");
            int y = 46;

            y = Option(memory, y, "Обрезать рабочие наборы",
                "Working set всех процессов сжимается, горячие страницы остаются",
                delegate(Config c) { return c.WorkingSets; },
                delegate(Config c, bool v) { c.WorkingSets = v; });

            y = Option(memory, y, "Очистить standby-список",
                "Страницы ожидания отдаются под новые задачи — самый заметный прирост",
                delegate(Config c) { return c.Standby; },
                delegate(Config c, bool v) { c.Standby = v; });

            y = Option(memory, y, "Очистить low-priority standby",
                "Агрессивнее: трогает и страницы низкого приоритета",
                delegate(Config c) { return c.LowStandby; },
                delegate(Config c, bool v) { c.LowStandby = v; });

            y = Option(memory, y, "Сбросить modified page list",
                "Записывает грязные страницы на диск и освобождает их",
                delegate(Config c) { return c.Modified; },
                delegate(Config c, bool v) { c.Modified = v; });

            y = Option(memory, y, "Обрезать системный кэш ФС",
                "SetSystemFileCacheSize(-1,-1,0), нужен SeIncreaseQuotaPrivilege",
                delegate(Config c) { return c.FileCache; },
                delegate(Config c, bool v) { c.FileCache = v; });

            Finish(memory, y);

            CardPanel disk = NewCard("Диск и кэши");
            y = 46;

            y = Option(disk, y, "Временные файлы",
                "%TEMP%, Windows\\Temp, CrashDumps, INetCache, WebCache",
                delegate(Config c) { return c.Temp; },
                delegate(Config c, bool v) { c.Temp = v; });

            y = Option(disk, y, "Кэши браузеров",
                "Chrome, Edge, Brave, Vivaldi, Chromium, Yandex, Opera",
                delegate(Config c) { return c.Browsers; },
                delegate(Config c, bool v) { c.Browsers = v; });

            y = Option(disk, y, "Кэш шейдеров",
                "D3DSCache, NVIDIA DXCache/GLCache, AMD, Intel",
                delegate(Config c) { return c.D3dCache; },
                delegate(Config c, bool v) { c.D3dCache = v; });

            y = Option(disk, y, "Эскизы проводника",
                "thumbcache_*.db и iconcache_*.db; занятые файлы пропускаются",
                delegate(Config c) { return c.Thumbs; },
                delegate(Config c, bool v) { c.Thumbs = v; });

            y = Option(disk, y, "Отчёты о сбоях",
                "Windows Error Reporting: очереди и архив",
                delegate(Config c) { return c.Wer; },
                delegate(Config c, bool v) { c.Wer = v; });

            y = Option(disk, y, "Дампы памяти",
                "MEMORY.DMP, Minidump, LiveKernelReports",
                delegate(Config c) { return c.Dumps; },
                delegate(Config c, bool v) { c.Dumps = v; });

            y = Option(disk, y, "Кэш Windows Update",
                "Останавливает wuauserv и bits, чистит Download и Delivery Optimization",
                delegate(Config c) { return c.UpdateCache; },
                delegate(Config c, bool v) { c.UpdateCache = v; });

            y = Option(disk, y, "Кэши UWP-приложений",
                "LocalCache и TempState пакетов, до 300 штук за проход",
                delegate(Config c) { return c.UwpCache; },
                delegate(Config c, bool v) { c.UwpCache = v; });

            y = Option(disk, y, "Prefetch старше 7 дней",
                "Windows заново построит прогрев при следующем запуске программ",
                delegate(Config c) { return c.Prefetch; },
                delegate(Config c, bool v) { c.Prefetch = v; });

            y = Option(disk, y, "Старые логи Windows",
                "C:\\Windows\\Logs: CBS, DISM, MoSetup, WindowsUpdate — старше 7 дней",
                delegate(Config c) { return c.WinLogs; },
                delegate(Config c, bool v) { c.WinLogs = v; });

            y = Option(disk, y, "Корзина",
                "SHEmptyRecycleBin без подтверждения — файлы не вернуть",
                delegate(Config c) { return c.Recycle; },
                delegate(Config c, bool v) { c.Recycle = v; });

            Finish(disk, y);

            CardPanel net = NewCard("Сеть");
            y = 46;

            y = Option(net, y, "DNS-кэш",
                "DnsFlushResolverCache — полезно после смены DNS или VPN",
                delegate(Config c) { return c.Dns; },
                delegate(Config c, bool v) { c.Dns = v; });

            y = Option(net, y, "ARP-кэш",
                "netsh interface ip delete arpcache",
                delegate(Config c) { return c.Arp; },
                delegate(Config c, bool v) { c.Arp = v; });

            y = Option(net, y, "NetBIOS-кэш",
                "nbtstat -R — сбрасывает имена NetBIOS",
                delegate(Config c) { return c.Netbios; },
                delegate(Config c, bool v) { c.Netbios = v; });

            Finish(net, y);
        }

        private CardPanel NewCard(string title)
        {
            CardPanel card = new CardPanel();
            card.Width = 800;
            card.Height = 120;
            _scroll.Add(card);
            _cards.Add(card);

            Label caption = new Label();
            caption.Text = title.ToUpperInvariant();
            caption.Font = new Font("Segoe UI Semibold", 8.25f);
            caption.ForeColor = Theme.Accent;
            caption.BackColor = Theme.Surface;
            caption.SetBounds(20, 16, 500, 18);
            card.Controls.Add(caption);

            Panel rule = new Panel();
            rule.BackColor = Theme.RowLine;
            rule.SetBounds(20, 42, card.Width - 40, 1);
            rule.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            card.Controls.Add(rule);

            return card;
        }

        private int Option(CardPanel card, int top, string text, string hint,
            Func<Config, bool> read, Action<Config, bool> apply)
        {
            RowPanel row = new RowPanel();
            row.SetBounds(20, top, Math.Max(200, card.Width - 40), 48);
            row.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            card.Controls.Add(row);

            DarkCheck box = new DarkCheck();
            box.Text = text;
            box.SetBounds(0, 1, Math.Max(120, row.Width - 8), 22);
            box.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            row.Controls.Add(box);

            Label note = new Label();
            note.Text = hint;
            note.ForeColor = Theme.Faint;
            note.BackColor = Theme.Surface;
            note.Font = Theme.Small;
            note.AutoEllipsis = true;
            note.SetBounds(24, 25, Math.Max(120, row.Width - 32), 15);
            note.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            row.Controls.Add(note);

            Binding b = new Binding();
            b.Box = box;
            b.Apply = apply;
            b.Read = read;
            _bindings.Add(b);

            return top + 48;
        }

        private static void Finish(CardPanel card, int height)
        {
            card.Height = height + 10;
        }

        public void LoadFrom(Config c)
        {
            foreach (Binding b in _bindings)
                b.Box.Checked = b.Read(c);
        }

        public void ApplyTo(Config c)
        {
            foreach (Binding b in _bindings)
                b.Apply(c, b.Box.Checked);
        }
    }

    // ------------------------------------------------------------------ startup page

    internal sealed class StartupPage : Page
    {
        private readonly ScrollHost _scroll;
        private readonly Label _summary;

        public StartupPage(MainForm host) : base(host)
        {
            Panel tools = new Panel();
            tools.Dock = DockStyle.Bottom;
            tools.Height = 54;
            tools.BackColor = Theme.Bg;
            Content.Controls.Add(tools);

            Button refresh = Ui.Ghost("ОБНОВИТЬ СПИСОК");
            refresh.Width = 190;
            refresh.Location = new Point(0, 6);
            refresh.Click += delegate { Load(); };
            tools.Controls.Add(refresh);

            _summary = Ui.Text("", Theme.Small, Theme.Muted);
            _summary.SetBounds(206, 16, 460, 20);
            _summary.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            tools.Controls.Add(_summary);

            _scroll = new ScrollHost();
            _scroll.Dock = DockStyle.Fill;
            _scroll.BottomPad = 16;
            Content.Controls.Add(_scroll);
            Content.Controls.SetChildIndex(_scroll, 0);
            FitAboveFooter(_scroll, tools);
        }

        public override string Title
        {
            get { return "Автозагрузка"; }
        }

        protected override string Subtitle
        {
            get { return "отключение через реестр и папки автозагрузки; ничего не удаляется — запись прячется в резерв"; }
        }

        protected override void OnActivated()
        {
            Load();
        }

        private void Load()
        {
            _scroll.Clear();

            List<Autostart.Entry> entries = Autostart.List();
            int disabled = 0;

            foreach (Autostart.Entry e in entries)
            {
                if (!e.Enabled)
                    disabled++;

                _scroll.Add(BuildRow(e));
            }

            if (entries.Count == 0)
            {
                Label empty = Ui.Text("Записей автозагрузки не найдено.", Theme.Body, Theme.Muted);
                empty.SetBounds(2, 4, 420, 22);
                _scroll.Add(empty);
            }

            _summary.Text = string.Format(CultureInfo.InvariantCulture,
                "всего {0}, отключено {1}", entries.Count, disabled);
        }

        private Control BuildRow(Autostart.Entry entry)
        {
            CardPanel row = new CardPanel();
            row.Width = 800;
            row.Height = 58;

            Label name = new Label();
            name.Text = entry.Name;
            name.ForeColor = entry.Enabled ? Theme.Text : Theme.Muted;
            name.BackColor = Theme.Surface;
            name.Font = new Font("Segoe UI Semibold", 9.5f);
            name.AutoEllipsis = true;
            name.SetBounds(18, 9, Math.Max(120, row.Width - 260), 20);
            name.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            row.Controls.Add(name);

            Label cmd = Ui.Text(Shorten(entry.Command, 110), Theme.Small, Theme.Faint);
            cmd.AutoEllipsis = true;
            cmd.SetBounds(18, 31, Math.Max(120, row.Width - 260), 16);
            cmd.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            row.Controls.Add(cmd);

            Chip chip = new Chip();
            chip.Set(Autostart.SourceLabel(entry.Origin), Theme.Blue);
            chip.SetBounds(row.Width - 214, 19, 150, 20);
            chip.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            row.Controls.Add(chip);

            ToggleSwitch toggle = new ToggleSwitch();
            toggle.Checked = entry.Enabled;
            toggle.SetBounds(row.Width - 52, 18, 42, 22);
            toggle.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            row.Controls.Add(toggle);

            toggle.CheckedChanged += delegate
            {
                string error = Autostart.SetEnabled(entry, toggle.Checked);
                if (error == null)
                {
                    Host.LogLine(string.Format("автозагрузка: \"{0}\" -> {1}",
                        entry.Name, toggle.Checked ? "включено" : "отключено"));
                    Load();
                }
                else
                {
                    Host.LogLine("автозагрузка: ошибка — " + error);
                    toggle.Checked = entry.Enabled;
                }
            };

            return row;
        }

        private static string Shorten(string value, int max)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;
            return value.Length <= max ? value : value.Substring(0, max - 1) + "…";
        }
    }

    // ------------------------------------------------------------------ log page

    internal sealed class LogPage : Page
    {
        private readonly LogView _box;

        public LogPage(MainForm host) : base(host)
        {
            Panel tools = new Panel();
            tools.Dock = DockStyle.Bottom;
            tools.Height = 54;
            tools.BackColor = Theme.Bg;
            Content.Controls.Add(tools);

            Button reload = Ui.Ghost("ЗАГРУЗИТЬ history.log");
            reload.Width = 210;
            reload.Location = new Point(0, 6);
            reload.Click += delegate { LoadHistory(); };
            tools.Controls.Add(reload);

            Button open = Ui.Ghost("ОТКРЫТЬ ФАЙЛ");
            open.Width = 160;
            open.Location = new Point(222, 6);
            open.Click += delegate { OpenPath(Config.HistoryPath); };
            tools.Controls.Add(open);

            Button folder = Ui.Ghost("ПАПКА ЛОГОВ");
            folder.Width = 160;
            folder.Location = new Point(394, 6);
            folder.Click += delegate { OpenPath(Config.LogDir); };
            tools.Controls.Add(folder);

            _box = new LogView();
            _box.Dock = DockStyle.Fill;
            Content.Controls.Add(_box);
            Content.Controls.SetChildIndex(_box, 0);
            FitAboveFooter(_box, tools);
        }

        public override string Title
        {
            get { return "Журнал"; }
        }

        protected override string Subtitle
        {
            get { return "каждый проход пишется в файл — включая запуски из задачи планировщика"; }
        }

        public void Append(string line)
        {
            if (InvokeRequired)
            {
                BeginInvoke((MethodInvoker)delegate { Append(line); });
                return;
            }

            string stamp = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
            _box.Append("[" + stamp + "] " + line + "\n");
        }

        private void LoadHistory()
        {
            try
            {
                if (!File.Exists(Config.HistoryPath))
                {
                    _box.SetText("Журнал ещё пуст: " + Config.HistoryPath);
                    return;
                }

                string[] lines = File.ReadAllLines(Config.HistoryPath, Encoding.UTF8);
                int start = Math.Max(0, lines.Length - 500);
                StringBuilder sb = new StringBuilder();
                for (int i = start; i < lines.Length; i++)
                    sb.AppendLine(lines[i]);
                _box.SetText(sb.ToString());
            }
            catch (Exception ex)
            {
                _box.SetText("не удалось прочитать журнал: " + ex.Message);
            }
        }

        private static void OpenPath(string path)
        {
            try
            {
                if (!File.Exists(path) && !Directory.Exists(path))
                {
                    Directory.CreateDirectory(Config.LogDir);
                    path = Config.LogDir;
                }
                System.Diagnostics.Process.Start("explorer.exe", "\"" + path + "\"");
            }
            catch (Exception)
            {
            }
        }
    }

    // ------------------------------------------------------------------ settings page

    internal sealed class SettingsPage : Page
    {
        private readonly Stepper _delay;
        private readonly Stepper _age;
        private Label _taskState;
        private Label _updateState;
        private UiButton _updateButton;
        private UpdateInfo _pendingUpdate;
        private bool _updateBusy;

        public SettingsPage(MainForm host) : base(host)
        {
            CardPanel card = new CardPanel();
            card.SetBounds(0, 0, Math.Max(640, Content.ClientSize.Width), 250);
            card.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Content.Controls.Add(card);

            Label caption = new Label();
            caption.Text = "ЗАПУСК И ВОЗРАСТ ФАЙЛОВ";
            caption.Font = new Font("Segoe UI Semibold", 8.25f);
            caption.ForeColor = Theme.Accent;
            caption.BackColor = Theme.Surface;
            caption.SetBounds(18, 14, 420, 18);
            card.Controls.Add(caption);

            Label l1 = Ui.Text("Задержка автоочистки после входа, секунд", Theme.Body, Theme.Text);
            l1.SetBounds(18, 56, 360, 22);
            card.Controls.Add(l1);

            _delay = MakeNumber(card, 380, 52);

            Label n1 = Ui.Text("0 — сразу при входе; 20–60 безопаснее", Theme.Small, Theme.Faint);
            n1.SetBounds(18, 80, 420, 18);
            card.Controls.Add(n1);

            Label l2 = Ui.Text("Трогать temp-файлы старше, часов", Theme.Body, Theme.Text);
            l2.SetBounds(18, 118, 360, 22);
            card.Controls.Add(l2);

            _age = MakeNumber(card, 380, 114);

            Label n2 = Ui.Text("0 — удалять всё незанятое, включая свежий temp", Theme.Small, Theme.Faint);
            n2.SetBounds(18, 142, 420, 18);
            card.Controls.Add(n2);

            Button save = Ui.Primary("СОХРАНИТЬ");
            save.SetBounds(18, 180, 210, 38);
            save.Click += delegate
            {
                Host.Settings.DelaySeconds = (int)_delay.Value;
                Host.Settings.TempAgeHours = (int)_age.Value;
                Host.Settings.Save();
                Host.LogLine(string.Format("настройки: задержка {0} с, temp старше {1} ч",
                    Host.Settings.DelaySeconds, Host.Settings.TempAgeHours));
            };
            card.Controls.Add(save);

            CardPanel paths = new CardPanel();
            paths.SetBounds(0, 264, Math.Max(640, Content.ClientSize.Width), 150);
            paths.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Content.Controls.Add(paths);

            Label pc = new Label();
            pc.Text = "ПУТИ";
            pc.Font = new Font("Segoe UI Semibold", 8.25f);
            pc.ForeColor = Theme.Accent;
            pc.BackColor = Theme.Surface;
            pc.SetBounds(18, 14, 200, 18);
            paths.Controls.Add(pc);

            AddPath(paths, 48, "Настройки", Config.ConfigPath);
            AddPath(paths, 74, "Журнал", Config.HistoryPath);
            AddPath(paths, 100, "Резерв автозагрузки", Path.Combine(Config.ConfigDir, "StartupBackup"));

            CardPanel task = new CardPanel();
            task.SetBounds(0, 428, Math.Max(640, Content.ClientSize.Width), 150);
            task.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Content.Controls.Add(task);

            Label tc = new Label();
            tc.Text = "АВТООЧИСТКА ПРИ ВХОДЕ";
            tc.Font = new Font("Segoe UI Semibold", 8.25f);
            tc.ForeColor = Theme.Accent;
            tc.BackColor = Theme.Surface;
            tc.SetBounds(18, 14, 260, 18);
            task.Controls.Add(tc);

            _taskState = Ui.Text("", Theme.Body, Theme.Muted);
            _taskState.SetBounds(18, 42, 600, 20);
            task.Controls.Add(_taskState);

            _updateState = Ui.Text("Обновление: проверка вручную", Theme.Small, Theme.Muted);
            _updateState.SetBounds(task.Width - 218, 44, 198, 20);
            _updateState.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            task.Controls.Add(_updateState);

            Button install = Ui.Primary("ВКЛЮЧИТЬ АВТООЧИСТКУ");
            install.SetBounds(18, 78, 230, 38);
            install.Click += delegate { Host.InstallTask(); };
            task.Controls.Add(install);

            Button remove = Ui.Ghost("СНЯТЬ");
            remove.SetBounds(258, 78, 140, 38);
            remove.Click += delegate { Host.RemoveTask(); };
            task.Controls.Add(remove);

            _updateButton = (UiButton)Ui.Ghost("ПРОВЕРИТЬ ОБНОВЛЕНИЯ");
            _updateButton.SetBounds(task.Width - 218, 78, 198, 38);
            _updateButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _updateButton.Click += delegate { UpdateButtonClick(); };
            task.Controls.Add(_updateButton);

            LoadValues(host.Settings);
        }

        public override string Title
        {
            get { return "Настройки"; }
        }

        protected override string Subtitle
        {
            get { return "поведение задачи планировщика и глубина чистки temp"; }
        }

        public void SetTaskState(bool installed)
        {
            if (_taskState == null)
                return;

            _taskState.Text = installed
                ? "Задача «" + TaskSetup.TaskName + "» стоит: запуск при входе, HighestAvailable, --silent."
                : "Задача не создана: чистка при входе выполняться не будет.";
            _taskState.ForeColor = installed ? Theme.Accent : Theme.Muted;
        }

        protected override void OnActivated()
        {
            LoadValues(Host.Settings);
            SetTaskState(TaskSetup.IsInstalled());
        }

        private void UpdateButtonClick()
        {
            if (_updateBusy) return;
            if (_pendingUpdate != null && _pendingUpdate.Available)
            {
                InstallUpdate();
                return;
            }
            CheckForUpdates();
        }

        private void CheckForUpdates()
        {
            _updateBusy = true;
            _updateButton.Enabled = false;
            _updateState.Text = "Проверяю GitHub Releases…";
            _updateState.ForeColor = Theme.Muted;
            System.Threading.Thread worker = new System.Threading.Thread(delegate()
            {
                UpdateInfo info = null;
                string error = null;
                try { info = UpdateService.CheckLatest(); }
                catch (Exception ex) { error = ex.Message; }
                BeginInvoke((MethodInvoker)delegate
                {
                    _updateBusy = false;
                    _updateButton.Enabled = true;
                    if (error != null)
                    {
                        _pendingUpdate = null;
                        _updateState.Text = "Проверка не удалась: " + error;
                        _updateState.ForeColor = Theme.Amber;
                        Host.LogLine("обновление: " + error);
                        return;
                    }

                    _pendingUpdate = info;
                    if (info.Available)
                    {
                        _updateState.Text = "Доступна версия " + info.Version;
                        _updateState.ForeColor = Theme.Accent;
                        _updateButton.Text = "СКАЧАТЬ И УСТАНОВИТЬ";
                        _updateButton.Accent = true;
                        _updateButton.Invalidate();
                        Host.LogLine("обновление: доступна версия " + info.Version);
                    }
                    else
                    {
                        _updateState.Text = "Установлена актуальная версия " + AppInfo.Version;
                        _updateState.ForeColor = Theme.Muted;
                        _updateButton.Text = "ПРОВЕРИТЬ ОБНОВЛЕНИЯ";
                        _updateButton.Accent = false;
                        _updateButton.Invalidate();
                        Host.LogLine("обновление: установлена актуальная версия.");
                    }
                });
            });
            worker.IsBackground = true;
            worker.Start();
        }

        private void InstallUpdate()
        {
            _updateBusy = true;
            _updateButton.Enabled = false;
            _updateState.Text = "Загружаю и проверяю контрольную сумму…";
            System.Threading.Thread worker = new System.Threading.Thread(delegate()
            {
                string error = null;
                try
                {
                    UpdateService.DownloadAndLaunch(_pendingUpdate, Application.ExecutablePath,
                        System.Diagnostics.Process.GetCurrentProcess().Id);
                }
                catch (Exception ex) { error = ex.Message; }
                BeginInvoke((MethodInvoker)delegate
                {
                    _updateBusy = false;
                    if (error != null)
                    {
                        _updateButton.Enabled = true;
                        _updateState.Text = "Не удалось установить: " + error;
                        _updateState.ForeColor = Theme.Red;
                        Host.LogLine("обновление: " + error);
                        return;
                    }
                    _updateState.Text = "Обновляю приложение…";
                    Host.LogLine("обновление: файл проверен, приложение перезапустится с новой версией.");
                    Host.Close();
                });
            });
            worker.IsBackground = true;
            worker.Start();
        }

        private static Stepper MakeNumber(Control parent, int x, int y)
        {
            Stepper box = new Stepper();
            box.Minimum = 0;
            box.Maximum = 900;
            box.SetBounds(x, y - 4, 132, 32);
            parent.Controls.Add(box);
            return box;
        }

        private void LoadValues(Config c)
        {
            _delay.Value = Math.Min(_delay.Maximum, Math.Max(_delay.Minimum, c.DelaySeconds));
            _age.Value = Math.Min(_age.Maximum, Math.Max(_age.Minimum, c.TempAgeHours));
        }

        private static void AddPath(CardPanel card, int top, string caption, string path)
        {
            Label name = Ui.Text(caption, Theme.Small, Theme.Faint);
            name.SetBounds(18, top, 150, 18);
            card.Controls.Add(name);

            Label value = Ui.Text(path, Theme.Mono, Theme.Muted);
            value.SetBounds(172, top - 2, 450, 18);
            value.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            card.Controls.Add(value);
        }
    }
}
