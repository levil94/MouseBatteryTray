using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Windows.Forms;

namespace MouseBatteryTray
{
    /// <summary>图标绘制：三种样式共用同一套调色与尺寸逻辑。</summary>
    internal static class IconRenderer
    {
        private static Bitmap _customCache;
        private static string _customCachePath;
        private static DateTime _customCacheStamp;

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        private static extern bool DestroyIcon(IntPtr handle);

        public static Color ColorFor(int percent)
        {
            if (percent <= 10) return Color.FromArgb(214, 48, 49);
            if (percent <= 25) return Color.FromArgb(230, 126, 34);
            if (percent <= 50) return Color.FromArgb(241, 196, 15);
            return Color.FromArgb(46, 204, 113);
        }

        public static void DrawDigits(Graphics g, Rectangle rect, int percent)
        {
            using (var brush = new SolidBrush(ColorFor(percent)))
            using (var path = RoundedRect(rect, Math.Max(2, rect.Width / 5)))
            {
                g.FillPath(brush, path);
            }
            DrawText(g, rect, percent.ToString());
        }

        private static void DrawText(Graphics g, Rectangle rect, string text)
        {
            using (var font = new Font("Segoe UI", rect.Height * 0.52f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var fmt = new StringFormat())
            using (var textBrush = new SolidBrush(Color.White))
            {
                fmt.Alignment = StringAlignment.Center;
                fmt.LineAlignment = StringAlignment.Center;
                g.DrawString(text, font, textBrush, new RectangleF(rect.X, rect.Y, rect.Width, rect.Height + 1), fmt);
            }
        }

        /// <summary>电池外框 + 按比例填充，可选择在电池内绘制百分比数字。</summary>
        public static void DrawBattery(Graphics g, Rectangle body, int percent, bool showText)
        {
            Color color = ColorFor(percent);
            int nubWidth = Math.Max(2, body.Width / 8);
            int nubHeight = Math.Max(3, body.Height / 3);
            var nub = new Rectangle(body.Right, body.Y + (body.Height - nubHeight) / 2, nubWidth, nubHeight);

            using (var outline = new Pen(Color.FromArgb(230, 230, 230), Math.Max(1f, body.Height / 12f)))
            using (var bodyPath = RoundedRect(body, Math.Max(1, body.Height / 6)))
            {
                using (var back = new SolidBrush(Color.FromArgb(70, 70, 70)))
                    g.FillPath(back, bodyPath);

                int innerPad = Math.Max(1, body.Width / 12);
                var inner = Rectangle.Inflate(body, -innerPad, -innerPad);
                int fillWidth = (int)Math.Round(inner.Width * Math.Max(0, Math.Min(100, percent)) / 100.0);
                if (fillWidth > 0)
                {
                    var fillRect = new Rectangle(inner.X, inner.Y, fillWidth, inner.Height);
                    using (var fill = new SolidBrush(color))
                        g.FillRectangle(fill, fillRect);
                }

                g.DrawPath(outline, bodyPath);
                using (var nubBrush = new SolidBrush(Color.FromArgb(200, 200, 200)))
                    g.FillRectangle(nubBrush, nub);
            }

            if (showText)
            {
                using (var font = new Font("Segoe UI", body.Height * 0.5f, FontStyle.Bold, GraphicsUnit.Pixel))
                using (var fmt = new StringFormat())
                using (var textBrush = new SolidBrush(Color.White))
                {
                    fmt.Alignment = StringAlignment.Center;
                    fmt.LineAlignment = StringAlignment.Center;
                    g.DrawString(percent.ToString(), font, textBrush,
                        new RectangleF(body.X, body.Y, body.Width, body.Height + 1), fmt);
                }
            }
        }

        public static GraphicsPath RoundedRect(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            if (d > rect.Width) d = rect.Width;
            if (d > rect.Height) d = rect.Height;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static Bitmap LoadCustom(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            DateTime stamp = File.GetLastWriteTimeUtc(path);
            if (_customCache != null && _customCachePath == path && _customCacheStamp == stamp)
                return _customCache;

            try
            {
                if (_customCache != null) { _customCache.Dispose(); _customCache = null; }
                using (var src = Image.FromFile(path))
                {
                    _customCache = new Bitmap(src);
                }
                _customCachePath = path;
                _customCacheStamp = stamp;
                return _customCache;
            }
            catch { return null; }
        }

        /// <summary>生成托盘图标。percent 为 null 表示未连接。</summary>
        public static Icon Render(AppSettings settings, int? percent)
        {
            int size = Math.Max(16, SystemInformation.SmallIconSize.Width);
            var bmp = new Bitmap(size, size);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                g.Clear(Color.Transparent);

                if (!percent.HasValue)
                {
                    using (var gray = new SolidBrush(Color.FromArgb(120, 120, 120)))
                    using (var path = RoundedRect(new Rectangle(0, 0, size, size), Math.Max(2, size / 5)))
                        g.FillPath(gray, path);
                    DrawText(g, new Rectangle(0, 0, size, size), "--");
                }
                else if (settings != null && settings.IconMode == "custom" && LoadCustom(settings.CustomIconPath) != null)
                {
                    Bitmap img = LoadCustom(settings.CustomIconPath);
                    g.DrawImage(img, new Rectangle(0, 0, size, size));
                }
                else if (settings != null && settings.IconMode == "battery")
                {
                    var body = new Rectangle(1, size / 4, size - Math.Max(3, size / 8) - 2, size / 2);
                    DrawBattery(g, body, percent.Value, size >= 24);
                }
                else
                {
                    DrawDigits(g, new Rectangle(0, 0, size, size), percent.Value);
                }
            }

            IntPtr hIcon = bmp.GetHicon();
            Icon icon = (Icon)Icon.FromHandle(hIcon).Clone();
            DestroyIcon(hIcon);
            bmp.Dispose();
            return icon;
        }
    }
}
