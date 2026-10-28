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
        public string SoundFile = "";
        public string Appearance = "system";    // system | light | dark

        private static string SettingsPath
        {
            get { return Path.Combine(Program.DataDir, "settings.ini"); }
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
                        case "appearance":
                            if (val == "system" || val == "light" || val == "dark") s.Appearance = val;
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
                    "sound_file=" + SoundFile,
                    "appearance=" + Appearance
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

        public void PlayAlertSound()
        {
            if (!SoundEnabled) return;
            try
            {
                if (!string.IsNullOrEmpty(SoundFile) && File.Exists(SoundFile))
                {
                    using (var player = new SoundPlayer(SoundFile)) player.Play();
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

    /// <summary>设置窗口：浅色 / 深色双主题，跟随系统可实时切换。</summary>
    internal sealed class SettingsForm : Form
    {
        private readonly NumericUpDown _interval;
        private readonly NumericUpDown _threshold;
        private readonly ComboBox _iconMode;
        private readonly ComboBox _appearance;
        private readonly Label _iconPathLabel;
        private readonly Button _browseIcon;
        private readonly PictureBox _preview;
        private readonly CheckBox _soundEnabled;
        private readonly Label _soundPathLabel;
        private readonly Button _browseSound;
        private readonly Button _testSound;
        private readonly CheckBox _autoStart;
        private readonly Button _ok;
        private readonly Button _cancel;
        private readonly Label _title;
        private readonly Label _subtitle;
        private readonly AppSettings _draft;
        private ThemePalette _palette;
        private string _pendingIconPath;
        private string _pendingSoundPath;
        private bool _suppressAppearanceEvent;

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
                SoundFile = current.SoundFile,
                Appearance = current.Appearance
            };
            _pendingIconPath = current.CustomIconPath;
            _pendingSoundPath = current.SoundFile;
            _palette = ThemeManager.Current;

            Text = "鼠标电量 — 设置";
            FormBorderStyle = FormBorderStyle.FixedSingle;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(468, 592);
            BackColor = _palette.Window;
            Font = new Font("Segoe UI", 9.5f);

            _title = new Label
            {
                Text = "设置",
                Font = new Font("Segoe UI Semibold", 15f, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(24, 16),
                Tag = "title"
            };
            _subtitle = new Label
            {
                Text = "MouseBatteryTray · 托盘电量监控",
                Font = new Font("Segoe UI", 8.5f),
                AutoSize = true,
                Location = new Point(26, 46),
                Tag = "muted"
            };

            // ---------- 卡片 1：外观与行为 ----------
            var card1 = new CardPanel(_palette) { Location = new Point(24, 78), Size = new Size(420, 158) };

            var header1 = SectionHeader("外观与刷新", new Point(16, 12), card1, _palette);

            var lApp = FieldLabel("外观", new Point(16, 48), card1, _palette);
            _appearance = MakeCombo(new Point(150, 45), 246, card1, _palette);
            _appearance.Items.AddRange(new object[] { "跟随系统", "亮色", "暗色" });
            _suppressAppearanceEvent = true;
            _appearance.SelectedIndex = _draft.Appearance == "light" ? 1 : (_draft.Appearance == "dark" ? 2 : 0);
            _suppressAppearanceEvent = false;
            _appearance.SelectedIndexChanged += AppearanceChanged;

            var lInt = FieldLabel("刷新频率", new Point(16, 84), card1, _palette);
            _interval = MakeNumeric(new Point(150, 81), AppSettings.MinInterval, AppSettings.MaxInterval, 5, _draft.IntervalSeconds, card1, _palette);
            var u1 = MakeUnit("秒", new Point(248, 84), card1, _palette);
            var h1 = MakeHint("10 – 3600", new Point(288, 84), card1, _palette);

            var lTh = FieldLabel("低电量阈值", new Point(16, 120), card1, _palette);
            _threshold = MakeNumeric(new Point(150, 117), AppSettings.MinThreshold, AppSettings.MaxThreshold, 1, _draft.LowThreshold, card1, _palette);
            var u2 = MakeUnit("%", new Point(248, 120), card1, _palette);
            var h2 = MakeHint("5 – 90", new Point(288, 120), card1, _palette);

            // ---------- 卡片 2：图标 ----------
            var card2 = new CardPanel(_palette) { Location = new Point(24, 244), Size = new Size(420, 108) };
            var header2 = SectionHeader("托盘图标", new Point(16, 12), card2, _palette);

            _iconMode = MakeCombo(new Point(16, 46), 200, card2, _palette);
            _iconMode.Items.AddRange(new object[] { "数字（默认）", "电池图形", "自定义图片" });
            _iconMode.SelectedIndex = _draft.IconMode == "battery" ? 1 : (_draft.IconMode == "custom" ? 2 : 0);
            _iconMode.SelectedIndexChanged += delegate { SyncIconControls(); };

            _preview = new PictureBox
            {
                Location = new Point(240, 40),
                Size = new Size(48, 48),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = _palette.Input
            };
            card2.Controls.Add(_preview);

            _iconPathLabel = new Label
            {
                Location = new Point(300, 46),
                Size = new Size(100, 36),
                Font = new Font("Segoe UI", 8.5f),
                AutoEllipsis = true,
                Tag = "muted"
            };
            card2.Controls.Add(_iconPathLabel);

            _browseIcon = ThemeManager.CreateSecondaryButton("选择图片…", 96, _palette);
            _browseIcon.Location = new Point(16, 76);
            _browseIcon.Click += BrowseIcon;
            card2.Controls.Add(_browseIcon);

            // ---------- 卡片 3：提示音 ----------
            var card3 = new CardPanel(_palette) { Location = new Point(24, 360), Size = new Size(420, 116) };
            var header3 = SectionHeader("低电量提示音", new Point(16, 12), card3, _palette);

            _soundEnabled = new CheckBox
            {
                Text = "播放提示音",
                Location = new Point(16, 44),
                AutoSize = true,
                Font = new Font("Segoe UI", 9.5f),
                Tag = "check"
            };
            _soundEnabled.Checked = _draft.SoundEnabled;
            _soundEnabled.CheckedChanged += delegate { SyncSoundControls(); };
            card3.Controls.Add(_soundEnabled);

            _soundPathLabel = new Label
            {
                Location = new Point(150, 47),
                Size = new Size(250, 20),
                Font = new Font("Segoe UI", 8.5f),
                AutoEllipsis = true,
                Tag = "muted"
            };
            card3.Controls.Add(_soundPathLabel);

            _browseSound = ThemeManager.CreateSecondaryButton("选择音频…", 96, _palette);
            _browseSound.Location = new Point(16, 78);
            _browseSound.Click += BrowseSound;
            card3.Controls.Add(_browseSound);

            _testSound = ThemeManager.CreateSecondaryButton("试听", 64, _palette);
            _testSound.Location = new Point(118, 78);
            _testSound.Click += delegate
            {
                var temp = new AppSettings { SoundEnabled = true, SoundFile = _pendingSoundPath };
                temp.PlayAlertSound();
            };
            card3.Controls.Add(_testSound);

            var soundHint = new Label
            {
                Text = "留空则使用 Windows 警告音",
                Location = new Point(192, 84),
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5f),
                Tag = "muted"
            };
            card3.Controls.Add(soundHint);

            // ---------- 卡片 4：启动 ----------
            var card4 = new CardPanel(_palette) { Location = new Point(24, 484), Size = new Size(420, 56) };
            _autoStart = new CheckBox
            {
                Text = "开机自动启动",
                Location = new Point(16, 18),
                AutoSize = true,
                Font = new Font("Segoe UI", 9.5f),
                Tag = "check"
            };
            _autoStart.Checked = autoStartEnabled;
            card4.Controls.Add(_autoStart);

            var versionLabel = new Label
            {
                Text = "v" + Program.AppVersion,
                Location = new Point(352, 20),
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5f),
                Tag = "muted"
            };
            card4.Controls.Add(versionLabel);

            // ---------- 底部按钮 ----------
            _cancel = ThemeManager.CreateSecondaryButton("取消", 88, _palette);
            _cancel.Location = new Point(252, 550);
            _cancel.DialogResult = DialogResult.Cancel;

            _ok = ThemeManager.CreatePrimaryButton("保存", _palette);
            _ok.Location = new Point(356, 548);
            _ok.DialogResult = DialogResult.OK;
            _ok.Click += delegate { ApplyToDraft(); };

            Controls.AddRange(new Control[] { _title, _subtitle, card1, card2, card3, card4, _cancel, _ok });

            AcceptButton = _ok;
            CancelButton = _cancel;

            Load += delegate { ThemeManager.ApplyTitleBar(this); };
            ThemeManager.Changed += OnThemeChanged;

            ApplyPalette(_palette);
            SyncIconControls();
            SyncSoundControls();
        }

        private void OnThemeChanged(object sender, EventArgs e)
        {
            if (IsDisposed) return;
            // 用户在弹窗里手动选了模式时不跟随系统
            if (!_suppressAppearanceEvent && _appearance.SelectedIndex == 0)
            {
                ApplyPalette(ThemeManager.Current);
            }
        }

        private void AppearanceChanged(object sender, EventArgs e)
        {
            if (_suppressAppearanceEvent) return;
            string mode = _appearance.SelectedIndex == 1 ? "light" : (_appearance.SelectedIndex == 2 ? "dark" : "system");
            ThemeManager.Apply(mode);
            ApplyPalette(ThemeManager.Current);
        }

        // ---------- 主题应用 ----------

        private void ApplyPalette(ThemePalette p)
        {
            _palette = p;
            BackColor = p.Window;
            ForeColor = p.Text;
            ThemeManager.ApplyTitleBar(this);

            foreach (Control control in Controls) ApplyToControl(control, p);
        }

        private static void ApplyToControl(Control control, ThemePalette p)
        {
            var card = control as CardPanel;
            if (card != null) card.ApplyPalette(p);

            var label = control as Label;
            if (label != null)
            {
                string tag = label.Tag as string;
                if (tag == "title") { label.ForeColor = p.Text; label.BackColor = p.Window; }
                else if (tag == "muted") { label.ForeColor = p.TextMuted; label.BackColor = label.Parent is CardPanel ? p.Card : p.Window; }
                else if (tag == "header") { label.ForeColor = p.Accent; label.BackColor = p.Card; }
                else { label.ForeColor = p.TextSecondary; label.BackColor = p.Card; }
            }

            var input = control as NumericUpDown;
            if (input != null)
            {
                input.BackColor = p.Input;
                input.ForeColor = p.Text;
                input.BorderStyle = BorderStyle.FixedSingle;
            }

            var combo = control as ComboBox;
            if (combo != null)
            {
                combo.BackColor = p.Input;
                combo.ForeColor = p.Text;
                combo.FlatStyle = FlatStyle.Flat;
            }

            var check = control as CheckBox;
            if (check != null)
            {
                check.ForeColor = p.TextSecondary;
                check.BackColor = check.Parent is CardPanel ? p.Card : p.Window;
            }

            var button = control as Button;
            if (button != null)
            {
                string kind = button.Tag as string;
                if (kind == "primary")
                {
                    button.BackColor = p.Accent;
                    button.ForeColor = p.AccentText;
                    button.FlatAppearance.BorderSize = 0;
                    button.FlatAppearance.MouseOverBackColor = p.AccentHover;
                    button.FlatAppearance.MouseDownBackColor = p.AccentHover;
                }
                else
                {
                    button.BackColor = p.Card;
                    button.ForeColor = p.TextSecondary;
                    button.FlatAppearance.BorderSize = 1;
                    button.FlatAppearance.BorderColor = p.InputBorder;
                    button.FlatAppearance.MouseOverBackColor = p.Hover;
                    button.FlatAppearance.MouseDownBackColor = p.Hover;
                }
            }

            foreach (Control child in control.Controls) ApplyToControl(child, p);
        }

        // ---------- 控件工厂 ----------

        private static Label SectionHeader(string text, Point location, Control parent, ThemePalette p)
        {
            var label = new Label
            {
                Text = "▍ " + text,
                Location = location,
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold),
                ForeColor = p.Accent,
                BackColor = p.Card,
                Tag = "header"
            };
            parent.Controls.Add(label);
            return label;
        }

        private static Label FieldLabel(string text, Point location, Control parent, ThemePalette p)
        {
            var label = new Label
            {
                Text = text,
                Location = location,
                AutoSize = true,
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = p.TextSecondary,
                BackColor = p.Card
            };
            parent.Controls.Add(label);
            return label;
        }

        private static Label MakeUnit(string text, Point location, Control parent, ThemePalette p)
        {
            var label = new Label
            {
                Text = text,
                Location = location,
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = p.TextMuted,
                BackColor = p.Card
            };
            parent.Controls.Add(label);
            return label;
        }

        private static Label MakeHint(string text, Point location, Control parent, ThemePalette p)
        {
            var label = new Label
            {
                Text = text,
                Location = location,
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = p.TextMuted,
                BackColor = p.Card
            };
            parent.Controls.Add(label);
            return label;
        }

        private static ComboBox MakeCombo(Point location, int width, Control parent, ThemePalette p)
        {
            var combo = new ComboBox
            {
                Location = location,
                Width = width,
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                BackColor = p.Input,
                ForeColor = p.Text,
                Font = new Font("Segoe UI", 9.5f)
            };
            parent.Controls.Add(combo);
            return combo;
        }

        private static NumericUpDown MakeNumeric(Point location, int min, int max, int step, int value, Control parent, ThemePalette p)
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
                TextAlign = HorizontalAlignment.Center,
                BackColor = p.Input,
                ForeColor = p.Text,
                Font = new Font("Segoe UI", 9.5f)
            };
            parent.Controls.Add(box);
            return box;
        }

        // ---------- 交互 ----------

        private void SyncIconControls()
        {
            bool custom = _iconMode.SelectedIndex == 2;
            _browseIcon.Enabled = custom;
            _iconPathLabel.Text = custom
                ? (string.IsNullOrEmpty(_pendingIconPath) ? "尚未选择" : Path.GetFileName(_pendingIconPath))
                : "内置样式";
            UpdatePreview();
        }

        private void SyncSoundControls()
        {
            bool on = _soundEnabled.Checked;
            _soundPathLabel.Text = string.IsNullOrEmpty(_pendingSoundPath) ? "Windows 警告音" : Path.GetFileName(_pendingSoundPath);
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

                var bmp = new Bitmap(48, 48);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(_palette.Input);
                    Icon drawn = IconRenderer.Render(settings, 72);
                    if (drawn != null)
                    {
                        g.DrawIcon(drawn, new Rectangle(6, 6, 36, 36));
                        drawn.Dispose();
                    }
                }
                if (_preview.Image != null) _preview.Image.Dispose();
                _preview.Image = bmp;
                _preview.BackColor = _palette.Input;
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
            _draft.Appearance = _appearance.SelectedIndex == 1 ? "light" : (_appearance.SelectedIndex == 2 ? "dark" : "system");
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            ThemeManager.Changed -= OnThemeChanged;
            base.OnFormClosed(e);
        }
    }
}
