using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MouseBatteryTray
{
    /// <summary>电量历史曲线窗口：可选 1 小时 / 6 小时 / 24 小时 / 7 天。</summary>
    internal sealed class ChartForm : Form
    {
        private readonly Button[] _rangeButtons;
        private readonly TimeSpan[] _ranges = { TimeSpan.FromHours(1), TimeSpan.FromHours(6), TimeSpan.FromHours(24), TimeSpan.FromDays(7) };
        private readonly string[] _rangeLabels = { "1 小时", "6 小时", "24 小时", "7 天" };
        private int _selected = 1;

        private static readonly Color Background = Color.FromArgb(30, 30, 30);
        private static readonly Color GridColor = Color.FromArgb(60, 60, 60);
        private static readonly Color TextColor = Color.FromArgb(200, 200, 200);
        private static readonly Color LineColor = Color.FromArgb(46, 204, 113);

        public ChartForm()
        {
            Text = "鼠标电量 — 历史曲线";
            ClientSize = new Size(720, 400);
            MinimumSize = new Size(520, 320);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Background;
            ForeColor = TextColor;
            Font = new Font("Microsoft YaHei UI", 9f);
            DoubleBuffered = true;

            _rangeButtons = new Button[_ranges.Length];
            for (int i = 0; i < _ranges.Length; i++)
            {
                var btn = new Button
                {
                    Text = _rangeLabels[i],
                    Location = new Point(12 + i * 92, 10),
                    Size = new Size(84, 26),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = i == _selected ? Color.FromArgb(70, 70, 70) : Color.FromArgb(45, 45, 45),
                    ForeColor = TextColor
                };
                int index = i;
                btn.Click += delegate { _selected = index; UpdateButtons(); Invalidate(); };
                _rangeButtons[i] = btn;
                Controls.Add(btn);
            }

            var refresh = new Button
            {
                Text = "刷新",
                Location = new Point(12 + _ranges.Length * 92, 10),
                Size = new Size(64, 26),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = TextColor
            };
            refresh.Click += delegate { Invalidate(); };
            Controls.Add(refresh);

            Resize += delegate { Invalidate(); };
            Paint += OnPaint;
        }

        private void UpdateButtons()
        {
            for (int i = 0; i < _rangeButtons.Length; i++)
            {
                _rangeButtons[i].BackColor = i == _selected ? Color.FromArgb(70, 70, 70) : Color.FromArgb(45, 45, 45);
            }
        }

        private void OnPaint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var plot = new Rectangle(52, 56, ClientSize.Width - 76, ClientSize.Height - 100);
            if (plot.Width < 40 || plot.Height < 40) return;

            List<Sample> data = HistoryStore.Query(_ranges[_selected]);

            using (var gridPen = new Pen(GridColor))
            {
                for (int p = 0; p <= 100; p += 25)
                {
                    int y = plot.Bottom - (int)(plot.Height * p / 100.0);
                    g.DrawLine(gridPen, plot.Left, y, plot.Right, y);
                    using (var tb = new SolidBrush(TextColor))
                        g.DrawString(p + "%", new Font("Segoe UI", 7.5f), tb, plot.Left - 34, y - 7);
                }
                g.DrawRectangle(new Pen(Color.FromArgb(80, 80, 80)), plot);
            }

            if (data.Count < 2)
            {
                string msg = data.Count == 0
                    ? "还没有历史数据，运行一段时间后这里会出现曲线"
                    : "样本太少，至少需要 2 个采样点";
                using (var tb = new SolidBrush(TextColor))
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
                Color.FromArgb(90, LineColor), Color.FromArgb(10, LineColor), LinearGradientMode.Vertical))
            {
                var area = new List<PointF>(points);
                area.Add(new PointF(points[points.Count - 1].X, plot.Bottom));
                area.Add(new PointF(points[0].X, plot.Bottom));
                g.FillPolygon(fill, area.ToArray());
            }

            using (var pen = new Pen(LineColor, 2f))
                g.DrawLines(pen, points.ToArray());

            int min = 100, max = 0;
            foreach (Sample s in data)
            {
                if (s.Percent < min) min = s.Percent;
                if (s.Percent > max) max = s.Percent;
            }

            string title = string.Format("{0} · {1} 个采样点 · 当前 {2}% · 区间 {3}%–{4}%",
                _rangeLabels[_selected], data.Count, data[data.Count - 1].Percent, min, max);
            using (var tb = new SolidBrush(TextColor))
                g.DrawString(title, new Font("Microsoft YaHei UI", 9f), tb, plot.Left, plot.Top - 24);

            using (var tb = new SolidBrush(Color.FromArgb(150, 150, 150)))
            {
                g.DrawString(from.ToString("MM-dd HH:mm"), new Font("Segoe UI", 7.5f), tb, plot.Left, plot.Bottom + 6);
                string endText = to.ToString("MM-dd HH:mm");
                SizeF size = g.MeasureString(endText, new Font("Segoe UI", 7.5f));
                g.DrawString(endText, new Font("Segoe UI", 7.5f), tb, plot.Right - size.Width, plot.Bottom + 6);
            }
        }
    }
}
