using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace MouseBatteryTray
{
    /// <summary>一套完整调色板。明暗两套共用同一组语义槽位。</summary>
    internal sealed class ThemePalette
    {
        public string Id;
        public bool IsDark;

        public Color Window;            // 窗口背景
        public Color Card;              // 卡片底色
        public Color CardBorder;        // 卡片边框
        public Color Header;            // 卡片内分区标题
        public Color Input;             // 输入框底色
        public Color InputBorder;       // 输入框边框
        public Color Text;              // 主文字
        public Color TextSecondary;     // 正文
        public Color TextMuted;         // 说明文字
        public Color Accent;            // 强调色
        public Color AccentHover;
        public Color AccentText;        // 强调色上的文字
        public Color Hover;             // 悬停背景
        public Color MenuBackground;
        public Color MenuBorder;
        public Color Separator;
        public Color ChartBackground;
        public Color ChartGrid;
        public Color ChartLine;
        public Color ChartFillTop;
        public Color ChartFillBottom;

        public static readonly ThemePalette Dark = new ThemePalette
        {
            Id = "dark",
            IsDark = true,
            Window = Color.FromArgb(26, 26, 26),
            Card = Color.FromArgb(38, 38, 38),
            CardBorder = Color.FromArgb(61, 61, 61),
            Header = Color.FromArgb(245, 245, 245),
            Input = Color.FromArgb(51, 51, 51),
            InputBorder = Color.FromArgb(74, 74, 74),
            Text = Color.FromArgb(242, 242, 242),
            TextSecondary = Color.FromArgb(214, 214, 214),
            TextMuted = Color.FromArgb(160, 160, 160),
            Accent = Color.FromArgb(76, 194, 255),
            AccentHover = Color.FromArgb(111, 208, 255),
            AccentText = Color.FromArgb(0, 36, 61),
            Hover = Color.FromArgb(51, 51, 51),
            MenuBackground = Color.FromArgb(38, 38, 38),
            MenuBorder = Color.FromArgb(64, 64, 64),
            Separator = Color.FromArgb(58, 58, 58),
            ChartBackground = Color.FromArgb(26, 26, 26),
            ChartGrid = Color.FromArgb(48, 48, 48),
            ChartLine = Color.FromArgb(76, 194, 255),
            ChartFillTop = Color.FromArgb(90, 76, 194, 255),
            ChartFillBottom = Color.FromArgb(8, 76, 194, 255)
        };

        public static readonly ThemePalette Light = new ThemePalette
        {
            Id = "light",
            IsDark = false,
            Window = Color.FromArgb(243, 243, 243),
            Card = Color.FromArgb(255, 255, 255),
            CardBorder = Color.FromArgb(220, 220, 220),
            Header = Color.FromArgb(28, 28, 28),
            Input = Color.FromArgb(242, 242, 242),
            InputBorder = Color.FromArgb(200, 200, 200),
            Text = Color.FromArgb(26, 26, 26),
            TextSecondary = Color.FromArgb(70, 70, 70),
            TextMuted = Color.FromArgb(95, 95, 95),
            Accent = Color.FromArgb(0, 103, 192),
            AccentHover = Color.FromArgb(0, 90, 158),
            AccentText = Color.FromArgb(255, 255, 255),
            Hover = Color.FromArgb(234, 234, 234),
            MenuBackground = Color.FromArgb(252, 252, 252),
            MenuBorder = Color.FromArgb(214, 214, 214),
            Separator = Color.FromArgb(228, 228, 228),
            ChartBackground = Color.FromArgb(255, 255, 255),
            ChartGrid = Color.FromArgb(232, 232, 232),
            ChartLine = Color.FromArgb(0, 103, 192),
            ChartFillTop = Color.FromArgb(70, 0, 103, 192),
            ChartFillBottom = Color.FromArgb(6, 0, 103, 192)
        };

        public static ThemePalette ById(string id)
        {
            return string.Equals(id, "light", StringComparison.OrdinalIgnoreCase) ? Light : Dark;
        }
    }

    /// <summary>主题解析与变更通知：system / light / dark。</summary>
    internal static class ThemeManager
    {
        private const string PersonalizeKey =
            @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

        public static string Mode = "system";
        public static ThemePalette Current { get; private set; }

        public static event EventHandler Changed;

        static ThemeManager()
        {
            Current = ThemePalette.Dark;
        }

        public static bool SystemUsesLightTheme()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(PersonalizeKey, false))
                {
                    if (key == null) return false;
                    object value = key.GetValue("AppsUseLightTheme");
                    if (value is int) return (int)value == 1;
                }
            }
            catch { }
            return false;
        }

        public static void Apply(string mode)
        {
            if (mode != "light" && mode != "dark" && mode != "system") mode = "system";
            Mode = mode;

            ThemePalette next = mode == "system"
                ? (SystemUsesLightTheme() ? ThemePalette.Light : ThemePalette.Dark)
                : ThemePalette.ById(mode);

            bool changed = Current == null || Current.Id != next.Id;
            Current = next;
            if (changed && Changed != null) Changed(null, EventArgs.Empty);
        }

        /// <summary>系统主题变化时（仅 system 模式）重新解析。</summary>
        public static void RefreshIfFollowing()
        {
            if (Mode != "system") return;
            ThemePalette next = SystemUsesLightTheme() ? ThemePalette.Light : ThemePalette.Dark;
            if (next.Id == Current.Id) return;
            Current = next;
            if (Changed != null) Changed(null, EventArgs.Empty);
        }

        // ---------- 深色标题栏 / 云母效果 ----------

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
        private const uint SWP_FLAGS = 0x0020 | 0x0002 | 0x0001 | 0x0004;   // FRAMECHANGED|NOMOVE|NOSIZE|NOZORDER

        /// <summary>云母效果需要 Windows 11 22H2（build 22621）以上。</summary>
        public static bool MicaSupported
        {
            get { return Environment.OSVersion.Version.Build >= 22621; }
        }

        public static void ApplyTitleBar(Form form)
        {
            try
            {
                int dark = Current.IsDark ? 1 : 0;
                if (DwmSetWindowAttribute(form.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int)) != 0)
                    DwmSetWindowAttribute(form.Handle, 19, ref dark, sizeof(int));

                if (MicaSupported)
                {
                    // 2 = Mica（桌面壁纸采样），4 = Tabbed；暗色下用 Mica 更稳
                    int backdrop = Current.IsDark ? 2 : 2;
                    DwmSetWindowAttribute(form.Handle, DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int));
                }

                // 强制重绘标题栏，否则切换主题后颜色不会刷新
                SetWindowPos(form.Handle, IntPtr.Zero, 0, 0, 0, 0, SWP_FLAGS);
            }
            catch { }
        }

        // ---------- 工厂 ----------

        public static Button CreatePrimaryButton(string text, ThemePalette p)
        {
            var btn = new Button
            {
                Text = text,
                FlatStyle = FlatStyle.Flat,
                BackColor = p.Accent,
                ForeColor = p.AccentText,
                Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold),
                Size = new Size(88, 32),
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = p.AccentHover;
            btn.FlatAppearance.MouseDownBackColor = p.AccentHover;
            btn.Tag = "primary";
            return btn;
        }

        public static Button CreateSecondaryButton(string text, int width, ThemePalette p)
        {
            var btn = new Button
            {
                Text = text,
                FlatStyle = FlatStyle.Flat,
                BackColor = p.Card,
                ForeColor = p.TextSecondary,
                Font = new Font("Segoe UI", 9.5f),
                Size = new Size(width, 28),
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            btn.FlatAppearance.BorderColor = p.InputBorder;
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.MouseOverBackColor = p.Hover;
            btn.FlatAppearance.MouseDownBackColor = p.Hover;
            btn.Tag = "secondary";
            return btn;
        }
    }

    /// <summary>卡片容器：圆角、边框、可随主题重绘。</summary>
    internal sealed class CardPanel : Panel
    {
        private ThemePalette _palette;

        public CardPanel(ThemePalette palette)
        {
            _palette = palette;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            BackColor = palette.Window;
        }

        public void ApplyPalette(ThemePalette palette)
        {
            _palette = palette;
            BackColor = palette.Window;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(_palette.Window);

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = IconRenderer.RoundedRect(rect, 10))
            using (var fill = new SolidBrush(_palette.Card))
            using (var pen = new Pen(_palette.CardBorder))
            {
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(pen, path);
            }
        }
    }

    /// <summary>随主题变化的右键菜单渲染器。</summary>
    internal sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
    {
        private readonly ThemePalette _p;

        public DarkMenuRenderer(ThemePalette palette) : base(new MenuColorTable(palette))
        {
            _p = palette;
        }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
            using (var path = IconRenderer.RoundedRect(rect, 8))
            using (var brush = new SolidBrush(_p.MenuBackground))
                e.Graphics.FillPath(brush, path);
            using (var path = IconRenderer.RoundedRect(rect, 8))
            using (var pen = new Pen(_p.MenuBorder))
                e.Graphics.DrawPath(pen, path);
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (!e.Item.Selected || !e.Item.Enabled) return;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(4, 1, e.Item.Width - 8, e.Item.Height - 2);
            using (var path = IconRenderer.RoundedRect(rect, 5))
            using (var brush = new SolidBrush(_p.Hover))
                e.Graphics.FillPath(brush, path);
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? _p.Text : _p.TextMuted;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            int y = e.Item.Height / 2;
            using (var pen = new Pen(_p.Separator))
                e.Graphics.DrawLine(pen, 12, y, e.Item.Width - 12, y);
        }

        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e) { }

        private sealed class MenuColorTable : ProfessionalColorTable
        {
            private readonly ThemePalette _p;
            public MenuColorTable(ThemePalette p) { _p = p; }

            public override Color ToolStripDropDownBackground { get { return _p.MenuBackground; } }
            public override Color ImageMarginGradientBegin { get { return _p.MenuBackground; } }
            public override Color ImageMarginGradientMiddle { get { return _p.MenuBackground; } }
            public override Color ImageMarginGradientEnd { get { return _p.MenuBackground; } }
            public override Color MenuBorder { get { return _p.MenuBorder; } }
            public override Color MenuItemBorder { get { return Color.Transparent; } }
            public override Color MenuItemSelected { get { return _p.Hover; } }
            public override Color SeparatorDark { get { return _p.Separator; } }
            public override Color SeparatorLight { get { return _p.MenuBackground; } }
        }
    }
}
