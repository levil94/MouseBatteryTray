using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MouseBatteryTray
{
    /// <summary>电量历史曲线窗口，跟随应用主题。</summary>
    internal sealed class ChartForm : Form
    {
        private readonly Button[] _rangeButtons;
        private readonly TimeSpan[] _ranges = { TimeSpan.FromHours(1), TimeSpan.FromHours(6), TimeSpan.FromHours(24), TimeSpan.FromDays(7) };
        private readonly string[] _rangeLabels = { "1 小时", "6 小时", "24 小时", "7 天" };
        private readonly Button _refresh;
        private int _selected = 1;
        private ThemePalette _palette;

        public ChartForm()
        {
            _palette = ThemeManager.Current;

            Text = "鼠标电量 — 历史曲线";
            ClientSize = new Size(760, 420);
            MinimumSize = new Size(520, 320);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = _palette.ChartBackground;
            Font = new Font("Segoe UI", 9.5f);
            DoubleBuffered = true;

            _rangeButtons = new Button[_ranges.Length];
            for (int i = 0; i < _ranges.Length; i++)
            {
                var btn = new Button
                {
                    Text = _rangeLabels[i],
                    Location = new Point(12 + i * 92, 10),
                    Size = new Size(84, 28),
                    FlatStyle = FlatStyle.Flat,
                    Cursor = Cursors.Hand,
                    Font = new Font("Segoe UI", 9f)
                };
                btn.FlatAppearance.BorderSize = 1;
                int index = i;
                btn.Click += delegate { _selected = index; UpdateButtons(); Invalidate(); };
                _rangeButtons[i] = btn;
                Controls.Add(btn);
            }

            _refresh = new Button
            {
                Text = "刷新",
                Location = new Point(12 + _ranges.Length * 92, 10),
                Size = new Size(64, 28),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 9f)
            };
            _refresh.FlatAppearance.BorderSize = 1;
            _refresh.Click += delegate { Invalidate(); };
            Controls.Add(_refresh);

            Resize += delegate { Invalidate(); };
            Paint += OnPaint;
            Load += delegate { ThemeManager.ApplyTitleBar(this); };
            ThemeManager.Changed += OnThemeChanged;
            ApplyPalette(_palette, false);
        }

        private void OnThemeChanged(object sender, EventArgs e)
        {
            if (IsDisposed) return;
            ApplyPalette(ThemeManager.Current, true);
        }

        private void ApplyPalette(ThemePalette p, bool repaint)
        {
            _palette = p;
            BackColor = p.ChartBackground;
            ThemeManager.ApplyTitleBar(this);

            for (int i = 0; i < _rangeButtons.Length; i++)
            {
                bool active = i == _selected;
                _rangeButtons[i].BackColor = active ? p.Accent : p.Card;
                _rangeButtons[i].ForeColor = active ? p.AccentText : p.TextSecondary;
                _rangeButtons[i].FlatAppearance.BorderColor = p.CardBorder;
                _rangeButtons[i].FlatAppearance.MouseOverBackColor = active ? p.AccentHover : p.Hover;
            }
            _refresh.BackColor = p.Card;
            _refresh.ForeColor = p.TextSecondary;
            _refresh.FlatAppearance.BorderColor = p.CardBorder;
            _refresh.FlatAppearance.MouseOverBackColor = p.Hover;

            if (repaint) Invalidate();
        }

        private void UpdateButtons()
        {
            for (int i = 0; i < _rangeButtons.Length; i++)
            {
                bool active = i == _selected;
                _rangeButtons[i].BackColor = active ? _palette.Accent : _palette.Card;
                _rangeButtons[i].ForeColor = active ? _palette.AccentText : _palette.TextSecondary;
            }
        }

        private void OnPaint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(_palette.ChartBackground);

            var plot = new Rectangle(56, 60, ClientSize.Width - 80, ClientSize.Height - 104);
            if (plot.Width < 40 || plot.Height < 40) return;

            List<Sample> data = HistoryStore.Query(_ranges[_selected]);

            using (var gridPen = new Pen(_palette.ChartGrid))
            using (var axisBrush = new SolidBrush(_palette.TextMuted))
            using (var axisFont = new Font("Segoe UI", 7.5f))
            {
                for (int p = 0; p <= 100; p += 25)
                {
                    int y = plot.Bottom - (int)(plot.Height * p / 100.0);
                    g.DrawLine(gridPen, plot.Left, y, plot.Right, y);
                    g.DrawString(p + "%", axisFont, axisBrush, plot.Left - 36, y - 8);
                }
                g.DrawRectangle(new Pen(_palette.CardBorder), plot);
            }

            if (data.Count < 2)
            {
                string msg = data.Count == 0
                    ? "还没有历史数据，运行一段时间后这里会出现曲线"
                    : "样本太少，至少需要 2 个采样点";
                using (var tb = new SolidBrush(_palette.TextMuted))
                using (var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    g.DrawString(msg, new Font("Microsoft YaHei UI", 10f), tb, plot, fmt);
                return;
            }

            DateTime from = DateTime.Now - _ranges[_selected];
            DateTime to = DateTime.Now;
            double totalSeconds = (to - from).TotalSeconds;

            var points = new List<PointF>();
            foreach (Sample s in data)
            {
                double xRatio = (s.Time - from).TotalSeconds / totalSeconds;
                if (xRatio < 0) xRatio = 0;
                if (xRatio > 1) xRatio = 1;
                float x = plot.Left + (float)(plot.Width * xRatio);
                float y = plot.Bottom - (float)(plot.Height * s.Percent / 100.0);
                points.Add(new PointF(x, y));
            }

            using (var fill = new LinearGradientBrush(
                new Rectangle(plot.Left, plot.Top, plot.Width, plot.Height),
                _palette.ChartFillTop, _palette.ChartFillBottom, LinearGradientMode.Vertical))
            {
                var area = new List<PointF>(points);
                area.Add(new PointF(points[points.Count - 1].X, plot.Bottom));
                area.Add(new PointF(points[0].X, plot.Bottom));
                g.FillPolygon(fill, area.ToArray());
            }

            using (var pen = new Pen(_palette.ChartLine, 2f))
                g.DrawLines(pen, points.ToArray());

            int min = 100, max = 0;
            foreach (Sample s in data)
            {
                if (s.Percent < min) min = s.Percent;
                if (s.Percent > max) max = s.Percent;
            }

            string title = string.Format("{0} · {1} 个采样点 · 当前 {2}% · 区间 {3}%–{4}%",
                _rangeLabels[_selected], data.Count, data[data.Count - 1].Percent, min, max);
            using (var tb = new SolidBrush(_palette.TextSecondary))
                g.DrawString(title, new Font("Segoe UI", 9.5f), tb, plot.Left, plot.Top - 26);

            using (var tb = new SolidBrush(_palette.TextMuted))
            using (var small = new Font("Segoe UI", 7.5f))
            {
                g.DrawString(from.ToString("MM-dd HH:mm"), small, tb, plot.Left, plot.Bottom + 6);
                string endText = to.ToString("MM-dd HH:mm");
                SizeF size = g.MeasureString(endText, small);
                g.DrawString(endText, small, tb, plot.Right - size.Width, plot.Bottom + 6);
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            ThemeManager.Changed -= OnThemeChanged;
            base.OnFormClosed(e);
        }
    }
}
