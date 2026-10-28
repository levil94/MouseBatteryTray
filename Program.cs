using System;
using System.Drawing;
using System.IO;
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

        internal static string DataDir
        {
            get
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName);
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        internal static string LogPath { get { return Path.Combine(DataDir, "log.txt"); } }

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
    }

    internal sealed class TrayContext : ApplicationContext
    {
        private readonly NotifyIcon _icon;
        private readonly System.Windows.Forms.Timer _timer;
        private readonly ToolStripMenuItem _infoItem;
        private AppSettings _settings;
        private LogitechBattery.Reading _last;
        private DateTime _lastUpdated = DateTime.MinValue;
        private bool _warned;

        public TrayContext()
        {
            _settings = AppSettings.Load();

            _icon = new NotifyIcon { Visible = true, Text = "鼠标电量：正在读取…" };
            _icon.Icon = IconRenderer.Render(_settings, null);
            _icon.MouseClick += OnIconClick;

            var menu = new ContextMenuStrip();
            _infoItem = new ToolStripMenuItem("正在读取…") { Enabled = false };
            menu.Items.Add(_infoItem);
            menu.Items.Add(new ToolStripSeparator());

            var refreshItem = new ToolStripMenuItem("立即刷新");
            refreshItem.Click += delegate { Refresh(true); };
            menu.Items.Add(refreshItem);

            var settingsItem = new ToolStripMenuItem("设置…");
            settingsItem.Click += delegate { OpenSettings(); };
            menu.Items.Add(settingsItem);

            menu.Items.Add(new ToolStripSeparator());
            var exitItem = new ToolStripMenuItem("退出");
            exitItem.Click += delegate { ExitApp(); };
            menu.Items.Add(exitItem);

            _icon.ContextMenuStrip = menu;

            _timer = new System.Windows.Forms.Timer();
            _timer.Interval = _settings.IntervalSeconds * 1000;
            _timer.Tick += delegate { Refresh(false); };
            _timer.Start();

            Refresh(true);
        }

        private void OnIconClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) Refresh(true);
        }

        private void OpenSettings()
        {
            using (var form = new SettingsForm(_settings, Program.IsAutoStartEnabled()))
            {
                if (form.ShowDialog() != DialogResult.OK) return;

                AppSettings updated = form.Result;
                bool autoStartChanged = form.AutoStartChecked != Program.IsAutoStartEnabled();
                _settings = updated;
                _settings.Save();

                if (autoStartChanged)
                {
                    try { Program.SetAutoStart(form.AutoStartChecked); }
                    catch (Exception ex) { Program.Log("设置自启失败: " + ex.Message); }
                }

                _timer.Interval = _settings.IntervalSeconds * 1000;
                Program.Log(string.Format("设置已更新：间隔={0}s 阈值={1}% 图标={2}",
                    _settings.IntervalSeconds, _settings.LowThreshold, _settings.IconMode));

                _warned = false;
                Refresh(true);
            }
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

                    SetIcon(reading.Percent, string.Format("{0} {1}% · {2:0.00}V · {3:HH:mm}",
                        ShortName(reading.DeviceName), reading.Percent, reading.Millivolts / 1000.0, _lastUpdated));
                    _infoItem.Text = string.Format("{0}：{1}%（{2:0.00}V）",
                        ShortName(reading.DeviceName), reading.Percent, reading.Millivolts / 1000.0);

                    if (reading.Percent <= _settings.LowThreshold && !_warned)
                    {
                        _warned = true;
                        _icon.ShowBalloonTip(5000, "鼠标电量偏低",
                            string.Format("剩余 {0}%，已低于设定的 {1}%。", reading.Percent, _settings.LowThreshold),
                            ToolTipIcon.Warning);
                    }
                    if (reading.Percent > _settings.LowThreshold + 5) _warned = false;
                }
                else
                {
                    Program.Log("读取失败：" + (reading != null ? reading.Message : "null"));
                    if (_last != null)
                    {
                        SetIcon(null, string.Format("未连接 · 上次 {0}% ({1:HH:mm})", _last.Percent, _lastUpdated));
                        _infoItem.Text = string.Format("未连接 · 上次 {0}%（{1:HH:mm}）", _last.Percent, _lastUpdated);
                    }
                    else
                    {
                        SetIcon(null, "未检测到罗技鼠标");
                        _infoItem.Text = "未检测到罗技鼠标";
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

        private void SetIcon(int? percent, string tooltip)
        {
            Icon old = _icon.Icon;
            _icon.Icon = IconRenderer.Render(_settings, percent);
            _icon.Text = tooltip.Length > 63 ? tooltip.Substring(0, 63) : tooltip;
            if (old != null) old.Dispose();
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
