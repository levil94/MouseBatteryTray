using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MouseBatteryTray
{
    /// <summary>统一的深色主题：调色板、标题栏、菜单渲染、按钮与输入控件样式。</summary>
    internal static class Theme
    {
        public static readonly Color Background = Color.FromArgb(32, 32, 32);
        public static readonly Color Card = Color.FromArgb(43, 43, 43);
        public static readonly Color CardHover = Color.FromArgb(50, 50, 50);
        public static readonly Color Border = Color.FromArgb(60, 60, 60);
        public static readonly Color Text = Color.FromArgb(232, 232, 232);
        public static readonly Color SubText = Color.FromArgb(154, 154, 154);
        public static readonly Color Accent = Color.FromArgb(46, 204, 113);
        public static readonly Color AccentHover = Color.FromArgb(39, 174, 96);
        public static readonly Color Danger = Color.FromArgb(214, 48, 49);

        public static readonly Font BodyFont = new Font("Segoe UI", 9.5f, FontStyle.Regular, GraphicsUnit.Point);
        public static readonly Font SmallFont = new Font("Segoe UI", 8.5f, FontStyle.Regular, GraphicsUnit.Point);
        public static readonly Font TitleFont = new Font("Segoe UI Semibold", 15f, FontStyle.Bold, GraphicsUnit.Point);
        public static readonly Font SectionFont = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold, GraphicsUnit.Point);

        // ---------- 深色标题栏 ----------

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        public static void ApplyDarkTitleBar(Form form)
        {
            try
            {
                // 20 = DWMWA_USE_IMMERSIVE_DARK_MODE（Win10 20H1+），19 = 旧版本
                int enabled = 1;
                if (DwmSetWindowAttribute(form.Handle, 20, ref enabled, sizeof(int)) != 0)
                    DwmSetWindowAttribute(form.Handle, 19, ref enabled, sizeof(int));
            }
            catch { }
        }

        // ---------- 控件样式 ----------

        public static Button CreatePrimaryButton(string text)
        {
            var btn = new Button
            {
                Text = text,
                FlatStyle = FlatStyle.Flat,
                BackColor = Accent,
                ForeColor = Color.FromArgb(16, 16, 16),
                Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold),
                Size = new Size(88, 32),
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = AccentHover;
            btn.FlatAppearance.MouseDownBackColor = AccentHover;
            return btn;
        }

        public static Button CreateSecondaryButton(string text, int width)
        {
            var btn = new Button
            {
                Text = text,
                FlatStyle = FlatStyle.Flat,
                BackColor = Card,
                ForeColor = Text,
                Font = BodyFont,
                Size = new Size(width, 28),
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            btn.FlatAppearance.BorderColor = Border;
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.MouseOverBackColor = CardHover;
            btn.FlatAppearance.MouseDownBackColor = CardHover;
            return btn;
        }

        public static void StyleInput(Control control)
        {
            control.BackColor = Color.FromArgb(24, 24, 24);
            control.ForeColor = Text;
            control.Font = BodyFont;
        }

        public static void StyleLabel(Label label, bool sub)
        {
            label.ForeColor = sub ? SubText : Text;
            label.BackColor = Color.Transparent;
            label.Font = sub ? SmallFont : BodyFont;
        }
    }

    /// <summary>卡片式容器：圆角背景 + 细边框。</summary>
    internal sealed class CardPanel : Panel
    {
        public CardPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            BackColor = Theme.Background;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = IconRenderer.RoundedRect(rect, 10))
            using (var fill = new SolidBrush(Theme.Card))
            using (var pen = new Pen(Theme.Border))
            {
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(pen, path);
            }
        }
    }

    /// <summary>深色右键菜单渲染器。</summary>
    internal sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
    {
        public DarkMenuRenderer() : base(new DarkColorTable()) { }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
            using (var path = IconRenderer.RoundedRect(rect, 8))
            using (var brush = new SolidBrush(Color.FromArgb(38, 38, 38)))
            {
                e.Graphics.FillPath(brush, path);
            }
            using (var pen = new Pen(Color.FromArgb(64, 64, 64)))
            using (var path = IconRenderer.RoundedRect(rect, 8))
            {
                e.Graphics.DrawPath(pen, path);
            }
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (!e.Item.Selected || !e.Item.Enabled) return;

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(4, 1, e.Item.Width - 8, e.Item.Height - 2);
            using (var path = IconRenderer.RoundedRect(rect, 5))
            using (var brush = new SolidBrush(Color.FromArgb(58, 58, 58)))
            {
                e.Graphics.FillPath(brush, path);
            }
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? Theme.Text : Color.FromArgb(120, 120, 120);
            base.OnRenderItemText(e);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            int y = e.Item.Height / 2;
            using (var pen = new Pen(Color.FromArgb(58, 58, 58)))
            {
                e.Graphics.DrawLine(pen, 12, y, e.Item.Width - 12, y);
            }
        }

        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e) { }

        private sealed class DarkColorTable : ProfessionalColorTable
        {
            public override Color ToolStripDropDownBackground { get { return Color.FromArgb(38, 38, 38); } }
            public override Color ImageMarginGradientBegin { get { return Color.FromArgb(38, 38, 38); } }
            public override Color ImageMarginGradientMiddle { get { return Color.FromArgb(38, 38, 38); } }
            public override Color ImageMarginGradientEnd { get { return Color.FromArgb(38, 38, 38); } }
            public override Color MenuBorder { get { return Color.FromArgb(64, 64, 64); } }
            public override Color MenuItemBorder { get { return Color.Transparent; } }
            public override Color MenuItemSelected { get { return Color.FromArgb(58, 58, 58); } }
            public override Color SeparatorDark { get { return Color.FromArgb(58, 58, 58); } }
            public override Color SeparatorLight { get { return Color.FromArgb(38, 38, 38); } }
        }
    }
}
