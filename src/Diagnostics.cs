using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace CacheTrim
{
    internal sealed class DiagnosticsForm : Form
    {
        private readonly Native.MemStats _stats;

        public DiagnosticsForm()
        {
            _stats = Native.GetMemoryStats();
            Text = "CacheTrim — диагностика системы";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(700, 590);
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = Theme.Body;
            DoubleBuffered = true;

            Panel header = new Panel();
            header.SetBounds(0, 0, ClientSize.Width, 112);
            header.BackColor = Theme.Surface;
            header.Paint += PaintHeader;
            Controls.Add(header);

            Label title = new Label();
            title.Text = "Диагностика системы";
            title.Font = new Font("Segoe UI Semibold", 18f);
            title.ForeColor = Theme.Text;
            title.BackColor = Theme.Surface;
            title.SetBounds(28, 22, 450, 34);
            header.Controls.Add(title);

            Label subtitle = new Label();
            subtitle.Text = "Снимок состояния Windows · только чтение";
            subtitle.Font = Theme.Small;
            subtitle.ForeColor = Theme.Muted;
            subtitle.BackColor = Theme.Surface;
            subtitle.SetBounds(30, 62, 480, 20);
            header.Controls.Add(subtitle);

            Chip state = new Chip();
            state.Set(Native.IsElevated ? "АДМИН" : "ОГРАНИЧЕННЫЙ РЕЖИМ",
                Native.IsElevated ? Theme.Accent : Theme.Amber);
            state.Size = new Size(148, 24);
            state.Location = new Point(518, 35);
            header.Controls.Add(state);

            int gap = 12;
            int left = 24;
            int top = 132;
            int width = (ClientSize.Width - left * 2 - gap) / 2;
            AddMetric("ОПЕРАЦИОННАЯ СИСТЕМА", Environment.OSVersion.VersionString,
                Environment.Is64BitOperatingSystem ? "64-разрядная Windows" : "32-разрядная Windows",
                left, top, width, Theme.Blue);
            AddMetric("ПРОЦЕССОРЫ", Environment.ProcessorCount.ToString(),
                "логических процессоров доступно системе", left + width + gap, top, width, Theme.Violet);
            top += 104;
            ulong used = _stats.TotalPhys > _stats.AvailPhys ? _stats.TotalPhys - _stats.AvailPhys : 0;
            AddMetric("ОПЕРАТИВНАЯ ПАМЯТЬ", Format(used) + " занято",
                Format(_stats.TotalPhys) + " всего  ·  " + Format(_stats.AvailPhys) + " доступно",
                left, top, width, Theme.Red);
            AddMetric("COMMIT", Format(_stats.CommitUsed) + " использовано",
                "лимит " + Format(_stats.CommitLimit), left + width + gap, top, width, Theme.Blue);
            top += 104;
            AddMetric("STAND-BY", Format(_stats.Standby), "страницы в standby-списке",
                left, top, width, Theme.Amber);
            AddMetric("СИСТЕМНЫЕ РЕСУРСЫ", _stats.ProcessCount + " процессов",
                _stats.HandleCount.ToString("N0") + " дескрипторов открыто", left + width + gap, top, width, Theme.Accent);
            top += 104;
            AddMetric("СИСТЕМНЫЙ ДИСК", GetSystemDriveInfo(), "диск с каталогом Windows",
                left, top, width, Theme.Accent);
            AddMetric("СРЕДА", Environment.MachineName,
                Environment.UserName + "  ·  " + (Native.IsElevated ? "администратор" : "обычные права"),
                left + width + gap, top, width, Theme.Violet);

            UiButton close = new UiButton();
            close.Text = "ЗАКРЫТЬ";
            close.Accent = true;
            close.Width = 132;
            close.Location = new Point(ClientSize.Width - 156, ClientSize.Height - 52);
            close.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            close.Click += delegate { Close(); };
            Controls.Add(close);
        }

        private void AddMetric(string caption, string value, string detail,
            int x, int y, int width, Color tone)
        {
            CardPanel card = new CardPanel();
            card.SetBounds(x, y, width, 88);
            Controls.Add(card);

            Panel stripe = new Panel();
            stripe.BackColor = tone;
            stripe.SetBounds(0, 12, 3, 64);
            card.Controls.Add(stripe);

            Label cap = new Label();
            cap.Text = caption;
            cap.Font = new Font("Segoe UI Semibold", 7.5f);
            cap.ForeColor = Theme.Faint;
            cap.BackColor = Theme.Surface;
            cap.SetBounds(16, 10, width - 30, 16);
            card.Controls.Add(cap);

            Label val = new Label();
            val.Text = value;
            val.Font = new Font("Segoe UI Semibold", 11f);
            val.ForeColor = Theme.Text;
            val.BackColor = Theme.Surface;
            val.AutoEllipsis = true;
            val.SetBounds(16, 30, width - 30, 24);
            card.Controls.Add(val);

            Label sub = new Label();
            sub.Text = detail;
            sub.Font = new Font("Segoe UI", 7.75f);
            sub.ForeColor = Theme.Muted;
            sub.BackColor = Theme.Surface;
            sub.AutoEllipsis = true;
            sub.SetBounds(16, 58, width - 30, 17);
            card.Controls.Add(sub);
        }

        private static string GetSystemDriveInfo()
        {
            try
            {
                string root = Path.GetPathRoot(Environment.SystemDirectory);
                DriveInfo drive = new DriveInfo(root);
                return Format((ulong)drive.AvailableFreeSpace) + " / " + Format((ulong)drive.TotalSize);
            }
            catch
            {
                return "недоступно";
            }
        }

        private static string Format(ulong bytes)
        {
            return Cleaner.FormatBytes(bytes > long.MaxValue ? long.MaxValue : (long)bytes);
        }

        private void PaintHeader(object sender, PaintEventArgs e)
        {
            using (LinearGradientBrush line = new LinearGradientBrush(
                new Rectangle(0, 109, ClientSize.Width, 3), Theme.Accent, Theme.Line, 0f))
                e.Graphics.FillRectangle(line, 0, 109, ClientSize.Width, 3);
        }
    }
}
