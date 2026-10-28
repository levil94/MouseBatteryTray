using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace MouseBatteryTray
{
    internal static class Program
    {
        private const string AppName = "MouseBatteryTray";
        private const string MutexName = "Global\\MouseBatteryTray_SingleInstance";
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const int PollSeconds = 60;
        private const int LowBatteryThreshold = 20;

        [STAThread]
        private static void Main()
        {
            bool createdNew;
            using (var mutex = new Mutex(true, MutexName, out createdNew))
            {
                if (!createdNew)
                {
                    MessageBox.Show("鼠标电量监控已经在运行。", AppName,
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new TrayContext());
            }
        }

        internal static string LogPath
        {
            get
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName);
                Directory.CreateDirectory(dir);
                return Path.Combine(dir, "log.txt");
            }
        }

        internal static void Log(string message)
        {
            try
            {
                File.AppendAllText(LogPath,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + message + Environment.NewLine);
            }
            catch { }
        }

        internal static string ExecutablePath
        {
            get { return System.Reflection.Assembly.GetExecutingAssembly().Location; }
        }

        internal static bool IsAutoStartEnabled()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey, false))
            {
                if (key == null) return false;
                return key.GetValue(AppName) != null;
            }
        }

        internal static void SetAutoStart(bool enabled)
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey, true))
            {
                if (key == null) return;
                if (enabled) key.SetValue(AppName, "\"" + ExecutablePath + "\"");
                else key.DeleteValue(AppName, false);
            }
        }

        internal static int LowThreshold { get { return LowBatteryThreshold; } }
        internal static int PollIntervalSeconds { get { return PollSeconds; } }
    }

    internal sealed class TrayContext : ApplicationContext
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool DestroyIcon(IntPtr handle);

        private readonly NotifyIcon _icon;
        private readonly System.Windows.Forms.Timer _timer;
        private readonly ToolStripMenuItem _autoStartItem;
        private LogitechBattery.Reading _last;
        private DateTime _lastUpdated = DateTime.MinValue;
        private bool _warned;

        public TrayContext()
        {
            _icon = new NotifyIcon();
            _icon.Visible = true;
            _icon.Text = "鼠标电量：正在读取…";
            _icon.Icon = RenderIcon(null);
            _icon.MouseClick += OnIconClick;

            var menu = new ContextMenuStrip();
            var refreshItem = new ToolStripMenuItem("立即刷新");
            refreshItem.Click += delegate { Refresh(true); };
            menu.Items.Add(refreshItem);

            _autoStartItem = new ToolStripMenuItem("开机自动启动");
            _autoStartItem.CheckOnClick = true;
            _autoStartItem.Checked = Program.IsAutoStartEnabled();
            _autoStartItem.Click += delegate
            {
                try { Program.SetAutoStart(_autoStartItem.Checked); }
                catch (Exception ex) { Program.Log("设置自启失败: " + ex.Message); }
            };
            menu.Items.Add(_autoStartItem);

            menu.Items.Add(new ToolStripSeparator());
            var exitItem = new ToolStripMenuItem("退出");
            exitItem.Click += delegate { ExitApp(); };
            menu.Items.Add(exitItem);

            _icon.ContextMenuStrip = menu;

            _timer = new System.Windows.Forms.Timer();
            _timer.Interval = Program.PollIntervalSeconds * 1000;
            _timer.Tick += delegate { Refresh(false); };
            _timer.Start();

            Refresh(true);
        }

        private void OnIconClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) Refresh(true);
        }

        private void Refresh(bool verbose)
        {
            try
            {
                LogitechBattery.Reading reading = LogitechBattery.Read();
                if (reading != null && reading.Success)
                {
                    _last = reading;
                    _lastUpdated = DateTime.Now;
                    Program.Log(string.Format("OK {0} idx=0x{1:X2} {2}mV -> {3}%",
                        reading.DeviceName, reading.DeviceIndex, reading.Millivolts, reading.Percent));

                    UpdateIcon(reading.Percent, string.Format("{0} {1}% · {2:0.00}V · {3:HH:mm}",
                        ShortName(reading.DeviceName), reading.Percent, reading.Millivolts / 1000.0, _lastUpdated));

                    if (reading.Percent <= Program.LowThreshold && !_warned)
                    {
                        _warned = true;
                        _icon.ShowBalloonTip(5000, "鼠标电量偏低",
                            string.Format("剩余 {0}%，请及时充电。", reading.Percent), ToolTipIcon.Warning);
                    }
                    if (reading.Percent > Program.LowThreshold + 5) _warned = false;
                }
                else
                {
                    Program.Log("读取失败：" + (reading != null ? reading.Message : "null"));
                    if (_last != null)
                    {
                        UpdateIcon(null, string.Format("未连接 · 上次 {0}% ({1:HH:mm})", _last.Percent, _lastUpdated));
                    }
                    else
                    {
                        UpdateIcon(null, "未检测到罗技鼠标");
                    }
                }
            }
            catch (Exception ex)
            {
                Program.Log("异常：" + ex);
            }
        }

        private static string ShortName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "鼠标";
            string n = name.Replace("Wireless Gaming Mouse", "").Replace("Gaming Mouse", "").Trim();
            return n.Length > 0 ? n : "鼠标";
        }

        private void UpdateIcon(int? percent, string tooltip)
        {
            Icon old = _icon.Icon;
            _icon.Icon = RenderIcon(percent);
            _icon.Text = tooltip.Length > 63 ? tooltip.Substring(0, 63) : tooltip;
            if (old != null) old.Dispose();
        }

        /// <summary>把百分比画成图标：整块底色 + 大号数字，16px 下也能辨认。</summary>
        private static Icon RenderIcon(int? percent)
        {
            int size = Math.Max(16, SystemInformation.SmallIconSize.Width);
            var bmp = new Bitmap(size, size);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                g.Clear(Color.Transparent);

                Color fill;
                string text;
                if (!percent.HasValue)
                {
                    fill = Color.FromArgb(120, 120, 120);
                    text = "--";
                }
                else
                {
                    int p = percent.Value;
                    text = p.ToString();
                    if (p <= 10) fill = Color.FromArgb(214, 48, 49);
                    else if (p <= 25) fill = Color.FromArgb(230, 126, 34);
                    else if (p <= 50) fill = Color.FromArgb(241, 196, 15);
                    else fill = Color.FromArgb(46, 204, 113);
                }

                using (var brush = new SolidBrush(fill))
                using (var path = RoundedRect(new Rectangle(0, 0, size, size), Math.Max(2, size / 5)))
                {
                    g.FillPath(brush, path);
                }

                using (var font = new Font("Segoe UI", size * 0.52f, FontStyle.Bold, GraphicsUnit.Pixel))
                using (var fmt = new StringFormat())
                {
                    fmt.Alignment = StringAlignment.Center;
                    fmt.LineAlignment = StringAlignment.Center;
                    using (var textBrush = new SolidBrush(Color.White))
                    {
                        g.DrawString(text, font, textBrush, new RectangleF(0, 0, size, size + 1), fmt);
                    }
                }
            }

            IntPtr hIcon = bmp.GetHicon();
            Icon icon = (Icon)Icon.FromHandle(hIcon).Clone();
            DestroyIcon(hIcon);
            bmp.Dispose();
            return icon;
        }

        private static GraphicsPath RoundedRect(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private void ExitApp()
        {
            _timer.Stop();
            _icon.Visible = false;
            _icon.Dispose();
            ExitThread();
        }
    }
}
