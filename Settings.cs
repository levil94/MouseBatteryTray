using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Media;
using System.Windows.Forms;

namespace MouseBatteryTray
{
    /// <summary>程序配置，存放在 %LOCALAPPDATA%\MouseBatteryTray\settings.ini。</summary>
    internal sealed class AppSettings
    {
        public const int MinInterval = 10;
        public const int MaxInterval = 3600;
        public const int MinThreshold = 5;
        public const int MaxThreshold = 90;

        public int IntervalSeconds = 60;
        public int LowThreshold = 20;
        public string IconMode = "digits";      // digits | battery | custom
        public string CustomIconPath = "";
        public bool SoundEnabled = true;
        public string SoundFile = "";           // 空 = 使用系统警告音

        private static string SettingsPath
        {
            get
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MouseBatteryTray");
                Directory.CreateDirectory(dir);
                return Path.Combine(dir, "settings.ini");
            }
        }

        public static AppSettings Load()
        {
            var s = new AppSettings();
            try
            {
                string path = SettingsPath;
                if (!File.Exists(path)) return s;

                foreach (string raw in File.ReadAllLines(path))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";")) continue;
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;

                    string key = line.Substring(0, eq).Trim().ToLowerInvariant();
                    string val = line.Substring(eq + 1).Trim();

                    switch (key)
                    {
                        case "interval_seconds":
                            s.IntervalSeconds = Clamp(ParseInt(val, 60), MinInterval, MaxInterval);
                            break;
                        case "low_threshold":
                            s.LowThreshold = Clamp(ParseInt(val, 20), MinThreshold, MaxThreshold);
                            break;
                        case "icon_mode":
                            if (val == "digits" || val == "battery" || val == "custom") s.IconMode = val;
                            break;
                        case "custom_icon_path":
                            s.CustomIconPath = val;
                            break;
                        case "sound_enabled":
                            s.SoundEnabled = val != "0" && !val.Equals("false", StringComparison.OrdinalIgnoreCase);
                            break;
                        case "sound_file":
                            s.SoundFile = val;
                            break;
                    }
                }
            }
            catch { }
            return s;
        }

        public void Save()
        {
            try
            {
                IntervalSeconds = Clamp(IntervalSeconds, MinInterval, MaxInterval);
                LowThreshold = Clamp(LowThreshold, MinThreshold, MaxThreshold);

                var lines = new[]
                {
                    "# MouseBatteryTray 配置",
                    "interval_seconds=" + IntervalSeconds.ToString(CultureInfo.InvariantCulture),
                    "low_threshold=" + LowThreshold.ToString(CultureInfo.InvariantCulture),
                    "icon_mode=" + IconMode,
                    "custom_icon_path=" + CustomIconPath,
                    "sound_enabled=" + (SoundEnabled ? "1" : "0"),
                    "sound_file=" + SoundFile
                };
                File.WriteAllLines(SettingsPath, lines);
            }
            catch { }
        }

        private static int ParseInt(string text, int fallback)
        {
            int v;
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out v) ? v : fallback;
        }

        private static int Clamp(int v, int min, int max)
        {
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }

        /// <summary>播放低电量提示音：优先自定义 wav，否则系统警告音。</summary>
        public void PlayAlertSound()
        {
            if (!SoundEnabled) return;
            try
            {
                if (!string.IsNullOrEmpty(SoundFile) && File.Exists(SoundFile))
                {
                    using (var player = new SoundPlayer(SoundFile))
                    {
                        player.Play();
                    }
                }
                else
                {
                    SystemSounds.Exclamation.Play();
                }
            }
            catch (Exception ex)
            {
                Program.Log("播放提示音失败: " + ex.Message);
            }
        }
    }

    /// <summary>设置窗口：刷新频率、低电量阈值、图标样式、提示音。</summary>
    internal sealed class SettingsForm : Form
    {
        private readonly NumericUpDown _interval;
        private readonly NumericUpDown _threshold;
        private readonly ComboBox _iconMode;
        private readonly TextBox _iconPath;
        private readonly Button _browseIcon;
        private readonly CheckBox _autoStart;
        private readonly CheckBox _soundEnabled;
        private readonly TextBox _soundPath;
        private readonly Button _browseSound;
        private readonly Button _testSound;
        private readonly Label _preview;
        private readonly AppSettings _draft;

        public AppSettings Result { get { return _draft; } }
        public bool AutoStartChecked { get { return _autoStart.Checked; } }

        public SettingsForm(AppSettings current, bool autoStartEnabled)
        {
            _draft = new AppSettings
            {
                IntervalSeconds = current.IntervalSeconds,
                LowThreshold = current.LowThreshold,
                IconMode = current.IconMode,
                CustomIconPath = current.CustomIconPath,
                SoundEnabled = current.SoundEnabled,
                SoundFile = current.SoundFile
            };

            Text = "鼠标电量 — 设置";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(440, 396);
            Font = new Font("Microsoft YaHei UI", 9f);

            var lblInterval = new Label { Text = "刷新频率（秒）", Location = new Point(18, 22), AutoSize = true };
            _interval = new NumericUpDown
            {
                Location = new Point(170, 19),
                Width = 90,
                Minimum = AppSettings.MinInterval,
                Maximum = AppSettings.MaxInterval,
                Increment = 5,
                Value = _draft.IntervalSeconds
            };
            var hintInterval = new Label { Text = "10 – 3600", Location = new Point(270, 22), AutoSize = true, ForeColor = Color.Gray };

            var lblThreshold = new Label { Text = "低电量阈值（%）", Location = new Point(18, 60), AutoSize = true };
            _threshold = new NumericUpDown
            {
                Location = new Point(170, 57),
                Width = 90,
                Minimum = AppSettings.MinThreshold,
                Maximum = AppSettings.MaxThreshold,
                Increment = 1,
                Value = _draft.LowThreshold
            };
            var hintThreshold = new Label { Text = "5 – 90", Location = new Point(270, 60), AutoSize = true, ForeColor = Color.Gray };

            var lblIcon = new Label { Text = "图标样式", Location = new Point(18, 98), AutoSize = true };
            _iconMode = new ComboBox
            {
                Location = new Point(170, 95),
                Width = 160,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _iconMode.Items.AddRange(new object[] { "数字（默认）", "电池图形", "自定义图片" });
            _iconMode.SelectedIndex = _draft.IconMode == "battery" ? 1 : (_draft.IconMode == "custom" ? 2 : 0);
            _iconMode.SelectedIndexChanged += delegate { SyncIconControls(); };

            _iconPath = new TextBox { Location = new Point(18, 134), Width = 310, Text = _draft.CustomIconPath, ReadOnly = true };
            _browseIcon = new Button { Text = "选择…", Location = new Point(336, 132), Width = 74, Height = 25 };
            _browseIcon.Click += BrowseIcon;

            _preview = new Label
            {
                Location = new Point(18, 172),
                Size = new Size(64, 64),
                BorderStyle = BorderStyle.FixedSingle,
                TextAlign = ContentAlignment.MiddleCenter
            };

            _autoStart = new CheckBox { Text = "开机自动启动", Location = new Point(96, 186), AutoSize = true, Checked = autoStartEnabled };

            _soundEnabled = new CheckBox
            {
                Text = "低电量时播放提示音",
                Location = new Point(96, 210),
                AutoSize = true,
                Checked = _draft.SoundEnabled
            };
            _soundEnabled.CheckedChanged += delegate { SyncSoundControls(); };

            _soundPath = new TextBox { Location = new Point(18, 264), Width = 310, Text = _draft.SoundFile, ReadOnly = true };
            _browseSound = new Button { Text = "选择…", Location = new Point(336, 262), Width = 74, Height = 25 };
            _browseSound.Click += BrowseSound;
            _testSound = new Button { Text = "试听", Location = new Point(18, 296), Width = 74, Height = 25 };
            _testSound.Click += delegate
            {
                var temp = new AppSettings { SoundEnabled = true, SoundFile = _soundPath.Text };
                temp.PlayAlertSound();
            };

            var hintSound = new Label
            {
                Text = "留空 = Windows 警告音；可选自定义 .wav",
                Location = new Point(100, 272),
                AutoSize = true,
                ForeColor = Color.Gray
            };
            hintSound.Location = new Point(18, 330);

            var ok = new Button { Text = "确定", Location = new Point(252, 356), Width = 80, DialogResult = DialogResult.OK };
            var cancel = new Button { Text = "取消", Location = new Point(340, 356), Width = 80, DialogResult = DialogResult.Cancel };
            ok.Click += delegate { ApplyToDraft(); };

            Controls.AddRange(new Control[]
            {
                lblInterval, _interval, hintInterval,
                lblThreshold, _threshold, hintThreshold,
                lblIcon, _iconMode,
                _iconPath, _browseIcon,
                _preview, _autoStart, _soundEnabled,
                _soundPath, _browseSound, _testSound, hintSound,
                ok, cancel
            });

            AcceptButton = ok;
            CancelButton = cancel;
            SyncIconControls();
            SyncSoundControls();
        }

        private void SyncIconControls()
        {
            bool custom = _iconMode.SelectedIndex == 2;
            _iconPath.Enabled = custom;
            _browseIcon.Enabled = custom;
            UpdatePreview();
        }

        private void SyncSoundControls()
        {
            bool on = _soundEnabled.Checked;
            _soundPath.Enabled = on;
            _browseSound.Enabled = on;
            _testSound.Enabled = on;
        }

        private void BrowseIcon(object sender, EventArgs e)
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = "选择图标图片";
                dlg.Filter = "图片文件|*.png;*.ico;*.jpg;*.jpeg;*.bmp|所有文件|*.*";
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    _iconPath.Text = dlg.FileName;
                    UpdatePreview();
                }
            }
        }

        private void BrowseSound(object sender, EventArgs e)
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = "选择提示音";
                dlg.Filter = "WAV 音频|*.wav|所有文件|*.*";
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    _soundPath.Text = dlg.FileName;
                }
            }
        }

        private void UpdatePreview()
        {
            try
            {
                int size = 48;
                var bmp = new Bitmap(size, size);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);

                    int mode = _iconMode.SelectedIndex;
                    if (mode == 2 && File.Exists(_iconPath.Text))
                    {
                        using (var img = Image.FromFile(_iconPath.Text))
                            g.DrawImage(img, new Rectangle(0, 0, size, size));
                    }
                    else if (mode == 1)
                    {
                        IconRenderer.DrawBattery(g, new Rectangle(2, 14, size - 8, size - 28), 72, false);
                    }
                    else
                    {
                        IconRenderer.DrawDigits(g, new Rectangle(0, 0, size, size), 72);
                    }
                }
                if (_preview.Image != null) _preview.Image.Dispose();
                _preview.Image = bmp;
            }
            catch { }
        }

        private void ApplyToDraft()
        {
            _draft.IntervalSeconds = (int)_interval.Value;
            _draft.LowThreshold = (int)_threshold.Value;
            _draft.IconMode = _iconMode.SelectedIndex == 1 ? "battery" : (_iconMode.SelectedIndex == 2 ? "custom" : "digits");
            _draft.CustomIconPath = _iconPath.Text;
            _draft.SoundEnabled = _soundEnabled.Checked;
            _draft.SoundFile = _soundPath.Text;
        }
    }
}
