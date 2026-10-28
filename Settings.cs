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

    /// <summary>设置窗口（深色主题，卡片式布局）。</summary>
    internal sealed class SettingsForm : Form
    {
        private readonly NumericUpDown _interval;
        private readonly NumericUpDown _threshold;
        private readonly ComboBox _iconMode;
        private readonly Label _iconPathLabel;
        private readonly Button _browseIcon;
        private readonly PictureBox _preview;
        private readonly CheckBox _soundEnabled;
        private readonly Label _soundPathLabel;
        private readonly Button _browseSound;
        private readonly Button _testSound;
        private readonly CheckBox _autoStart;
        private readonly AppSettings _draft;
        private string _pendingIconPath;
        private string _pendingSoundPath;

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
            _pendingIconPath = current.CustomIconPath;
            _pendingSoundPath = current.SoundFile;

            Text = "鼠标电量 — 设置";
            FormBorderStyle = FormBorderStyle.FixedSingle;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(468, 546);
            BackColor = Theme.Background;
            ForeColor = Theme.Text;
            Font = Theme.BodyFont;
            DoubleBuffered = true;

            // ---------- 标题 ----------
            var title = new Label
            {
                Text = "设置",
                Font = Theme.TitleFont,
                ForeColor = Theme.Text,
                BackColor = Theme.Background,
                AutoSize = true,
                Location = new Point(24, 16)
            };
            var subtitle = new Label
            {
                Text = "MouseBatteryTray · 托盘电量监控",
                Font = Theme.SmallFont,
                ForeColor = Theme.SubText,
                BackColor = Theme.Background,
                AutoSize = true,
                Location = new Point(26, 46)
            };

            // ---------- 卡片 1：刷新与阈值 ----------
            var card1 = new CardPanel { Location = new Point(24, 78), Size = new Size(420, 120) };
            var l1 = MakeLabel("刷新频率", new Point(16, 16), card1);
            _interval = MakeNumeric(new Point(150, 13), AppSettings.MinInterval, AppSettings.MaxInterval, 5, _draft.IntervalSeconds, card1);
            var u1 = MakeUnit("秒", new Point(248, 16), card1);
            var h1 = MakeHint("10 – 3600", new Point(288, 16), card1);

            var l2 = MakeLabel("低电量阈值", new Point(16, 54), card1);
            _threshold = MakeNumeric(new Point(150, 51), AppSettings.MinThreshold, AppSettings.MaxThreshold, 1, _draft.LowThreshold, card1);
            var u2 = MakeUnit("%", new Point(248, 54), card1);
            var h2 = MakeHint("5 – 90", new Point(288, 54), card1);

            var l3 = MakeLabel("图标样式", new Point(16, 90), card1);
            _iconMode = new ComboBox
            {
                Location = new Point(150, 87),
                Width = 246,
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(24, 24, 24),
                ForeColor = Theme.Text,
                Font = Theme.BodyFont
            };
            _iconMode.Items.AddRange(new object[] { "数字（默认）", "电池图形", "自定义图片" });
            _iconMode.SelectedIndex = _draft.IconMode == "battery" ? 1 : (_draft.IconMode == "custom" ? 2 : 0);
            _iconMode.SelectedIndexChanged += delegate { SyncIconControls(); };
            card1.Controls.Add(_iconMode);

            // ---------- 卡片 2：图标预览与自定义图片 ----------
            var card2 = new CardPanel { Location = new Point(24, 206), Size = new Size(420, 96) };
            _preview = new PictureBox
            {
                Location = new Point(16, 16),
                Size = new Size(64, 64),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.FromArgb(24, 24, 24)
            };
            card2.Controls.Add(_preview);

            _iconPathLabel = new Label
            {
                Location = new Point(96, 22),
                Size = new Size(220, 20),
                Font = Theme.SmallFont,
                ForeColor = Theme.SubText,
                BackColor = Theme.Card,
                AutoEllipsis = true,
                Text = "使用内置样式"
            };
            card2.Controls.Add(_iconPathLabel);

            _browseIcon = Theme.CreateSecondaryButton("选择图片…", 96);
            _browseIcon.Location = new Point(96, 50);
            _browseIcon.Click += BrowseIcon;
            card2.Controls.Add(_browseIcon);

            var iconHint = MakeHint("支持 PNG / ICO / JPG / BMP，建议正方形", new Point(204, 56), card2);
            iconHint.AutoSize = true;

            // ---------- 卡片 3：提示音 ----------
            var card3 = new CardPanel { Location = new Point(24, 310), Size = new Size(420, 116) };
            _soundEnabled = new CheckBox
            {
                Text = "低电量时播放提示音",
                Location = new Point(16, 14),
                AutoSize = true,
                ForeColor = Theme.Text,
                BackColor = Theme.Card,
                Font = Theme.BodyFont
            };
            _soundEnabled.Checked = _draft.SoundEnabled;
            _soundEnabled.CheckedChanged += delegate { SyncSoundControls(); };
            card3.Controls.Add(_soundEnabled);

            _soundPathLabel = new Label
            {
                Location = new Point(16, 50),
                Size = new Size(296, 20),
                Font = Theme.SmallFont,
                ForeColor = Theme.SubText,
                BackColor = Theme.Card,
                AutoEllipsis = true
            };
            card3.Controls.Add(_soundPathLabel);

            _browseSound = Theme.CreateSecondaryButton("选择音频…", 96);
            _browseSound.Location = new Point(16, 78);
            _browseSound.Click += BrowseSound;
            card3.Controls.Add(_browseSound);

            _testSound = Theme.CreateSecondaryButton("试听", 64);
            _testSound.Location = new Point(118, 78);
            _testSound.Click += delegate
            {
                var temp = new AppSettings { SoundEnabled = true, SoundFile = _pendingSoundPath };
                temp.PlayAlertSound();
            };
            card3.Controls.Add(_testSound);

            var soundHint = MakeHint("留空则使用 Windows 警告音", new Point(190, 84), card3);
            soundHint.AutoSize = true;

            // ---------- 卡片 4：启动 ----------
            var card4 = new CardPanel { Location = new Point(24, 434), Size = new Size(420, 56) };
            _autoStart = new CheckBox
            {
                Text = "开机自动启动",
                Location = new Point(16, 18),
                AutoSize = true,
                ForeColor = Theme.Text,
                BackColor = Theme.Card,
                Font = Theme.BodyFont
            };
            _autoStart.Checked = autoStartEnabled;
            card4.Controls.Add(_autoStart);

            var versionLabel = new Label
            {
                Text = "v" + Program.AppVersion,
                Location = new Point(320, 20),
                AutoSize = true,
                Font = Theme.SmallFont,
                ForeColor = Theme.SubText,
                BackColor = Theme.Card
            };
            card4.Controls.Add(versionLabel);

            // ---------- 底部按钮 ----------
            var cancel = Theme.CreateSecondaryButton("取消", 88);
            cancel.Location = new Point(252, 504);
            cancel.DialogResult = DialogResult.Cancel;

            var ok = Theme.CreatePrimaryButton("保存");
            ok.Location = new Point(356, 502);
            ok.DialogResult = DialogResult.OK;
            ok.Click += delegate { ApplyToDraft(); };

            Controls.AddRange(new Control[] { title, subtitle, card1, card2, card3, card4, cancel, ok });
            AcceptButton = ok;
            CancelButton = cancel;

            Load += delegate { Theme.ApplyDarkTitleBar(this); };
            SyncIconControls();
            SyncSoundControls();
        }

        private static Label MakeLabel(string text, Point location, Control parent)
        {
            var label = new Label
            {
                Text = text,
                Location = location,
                AutoSize = true,
                ForeColor = Theme.Text,
                BackColor = Theme.Card,
                Font = Theme.BodyFont
            };
            parent.Controls.Add(label);
            return label;
        }

        private static Label MakeUnit(string text, Point location, Control parent)
        {
            var label = new Label
            {
                Text = text,
                Location = location,
                AutoSize = true,
                ForeColor = Theme.SubText,
                BackColor = Theme.Card,
                Font = Theme.SmallFont
            };
            parent.Controls.Add(label);
            return label;
        }

        private static Label MakeHint(string text, Point location, Control parent)
        {
            var label = new Label
            {
                Text = text,
                Location = location,
                AutoSize = false,
                Size = new Size(120, 20),
                ForeColor = Color.FromArgb(110, 110, 110),
                BackColor = Theme.Card,
                Font = Theme.SmallFont
            };
            parent.Controls.Add(label);
            return label;
        }

        private static NumericUpDown MakeNumeric(Point location, int min, int max, int step, int value, Control parent)
        {
            var box = new NumericUpDown
            {
                Location = location,
                Width = 88,
                Minimum = min,
                Maximum = max,
                Increment = step,
                Value = value,
                BorderStyle = BorderStyle.FixedSingle,
                TextAlign = HorizontalAlignment.Center
            };
            Theme.StyleInput(box);
            parent.Controls.Add(box);
            return box;
        }

        private void SyncIconControls()
        {
            bool custom = _iconMode.SelectedIndex == 2;
            _browseIcon.Enabled = custom;
            _iconPathLabel.Text = custom
                ? (string.IsNullOrEmpty(_pendingIconPath) ? "尚未选择图片" : Path.GetFileName(_pendingIconPath))
                : "使用内置样式";
            UpdatePreview();
        }

        private void SyncSoundControls()
        {
            bool on = _soundEnabled.Checked;
            _soundPathLabel.Text = string.IsNullOrEmpty(_pendingSoundPath)
                ? "提示音：Windows 警告音"
                : "提示音：" + Path.GetFileName(_pendingSoundPath);
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
                    _pendingIconPath = dlg.FileName;
                    SyncIconControls();
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
                    _pendingSoundPath = dlg.FileName;
                    SyncSoundControls();
                }
            }
        }

        private void UpdatePreview()
        {
            try
            {
                var settings = new AppSettings
                {
                    IconMode = _iconMode.SelectedIndex == 1 ? "battery" : (_iconMode.SelectedIndex == 2 ? "custom" : "digits"),
                    CustomIconPath = _pendingIconPath
                };

                var bmp = new Bitmap(64, 64);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.FromArgb(24, 24, 24));
                    Icon drawn = IconRenderer.Render(settings, 72);
                    if (drawn != null)
                    {
                        g.DrawIcon(drawn, new Rectangle(14, 14, 36, 36));
                        drawn.Dispose();
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
            _draft.CustomIconPath = _pendingIconPath;
            _draft.SoundEnabled = _soundEnabled.Checked;
            _draft.SoundFile = _pendingSoundPath;
        }
    }
}
