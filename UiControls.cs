using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MouseBatteryTray
{
    /// <summary>圆角扁平按钮：主/次两种样式，禁用态也保持可读。</summary>
    internal sealed class FlatButton : Button
    {
        public bool Primary;
        public ThemePalette Palette;
        public int CornerRadius = 7;
        public bool ShowBorder = true;

        private bool _hover;
        private bool _pressed;

        public FlatButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            UseVisualStyleBackColor = false;
            Cursor = Cursors.Hand;
            Font = new Font("Segoe UI", 9.5f);
            TabStop = true;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { _pressed = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            ThemePalette p = Palette ?? ThemeManager.Current;
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : p.Card);

            Color fill, border, text;

            if (Primary)
            {
                fill = Enabled ? (_pressed ? p.AccentHover : (_hover ? p.AccentHover : p.Accent)) : Blend(p.Accent, p.Card, 0.6);
                border = fill;
                text = Enabled ? p.AccentText : p.TextMuted;
            }
            else
            {
                fill = !Enabled ? p.Card : (_pressed ? p.Hover : (_hover ? p.Hover : p.Card));
                border = Enabled ? p.InputBorder : Blend(p.InputBorder, p.Card, 0.5);
                text = Enabled ? p.TextSecondary : p.TextMuted;
            }

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = IconRenderer.RoundedRect(rect, CornerRadius))
            using (var brush = new SolidBrush(fill))
            {
                g.FillPath(brush, path);
                if (ShowBorder)
                {
                    using (var pen = new Pen(border))
                        g.DrawPath(pen, path);
                }
            }

            var textRect = new Rectangle(2, 0, Width - 4, Height);
            TextRenderer.DrawText(g, Text, Font, textRect, text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }

        internal static Color Blend(Color a, Color b, double ratio)
        {
            int r = (int)Math.Round(a.R * ratio + b.R * (1 - ratio));
            int g = (int)Math.Round(a.G * ratio + b.G * (1 - ratio));
            int bl = (int)Math.Round(a.B * ratio + b.B * (1 - ratio));
            return Color.FromArgb(r, g, bl);
        }
    }

    /// <summary>圆角输入容器：内部控件无边框，由容器负责圆角与描边。</summary>
    internal sealed class RoundedField : Panel
    {
        public ThemePalette Palette;
        public int CornerRadius = 6;

        public RoundedField(ThemePalette palette)
        {
            Palette = palette;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = palette.Card;
        }

        public void ApplyPalette(ThemePalette p)
        {
            Palette = p;
            BackColor = p.Card;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            ThemePalette p = Palette ?? ThemeManager.Current;
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(p.Card);

            var rect = new Rectangle(1, 1, Width - 3, Height - 3);
            using (var path = IconRenderer.RoundedRect(rect, CornerRadius))
            using (var brush = new SolidBrush(p.Input))
            using (var pen = new Pen(p.InputBorder))
            {
                g.FillPath(brush, path);
                g.DrawPath(pen, path);
            }
        }
    }

    /// <summary>圆角下拉选择：自绘，点击弹出主题化菜单。</summary>
    internal sealed class RoundedSelect : Control
    {
        private readonly List<string> _items = new List<string>();
        private int _selected;
        private bool _hover;
        private bool _open;
        public ThemePalette Palette;
        public int CornerRadius = 6;
        public event EventHandler SelectionChanged;

        public RoundedSelect(ThemePalette palette)
        {
            Palette = palette;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            Cursor = Cursors.Hand;
            Font = new Font("Segoe UI", 9.5f);
            Height = 28;
            TabStop = true;
        }

        public int SelectedIndex
        {
            get { return _selected; }
            set
            {
                if (value < 0 || value >= _items.Count || value == _selected) return;
                _selected = value;
                Invalidate();
                if (SelectionChanged != null) SelectionChanged(this, EventArgs.Empty);
            }
        }

        public string SelectedText
        {
            get { return _items.Count > 0 ? _items[_selected] : ""; }
        }

        public void SetItems(params string[] items)
        {
            _items.Clear();
            _items.AddRange(items);
            _selected = 0;
            Invalidate();
        }

        public void ApplyPalette(ThemePalette p)
        {
            Palette = p;
            BackColor = p.Card;
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            ShowMenu();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Down || e.KeyCode == Keys.Right)
            {
                if (_selected < _items.Count - 1) SelectedIndex = _selected + 1;
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Up || e.KeyCode == Keys.Left)
            {
                if (_selected > 0) SelectedIndex = _selected - 1;
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter)
            {
                ShowMenu();
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if (e.Delta > 0 && _selected > 0) SelectedIndex = _selected - 1;
            else if (e.Delta < 0 && _selected < _items.Count - 1) SelectedIndex = _selected + 1;
            base.OnMouseWheel(e);
        }

        private void ShowMenu()
        {
            if (_open || _items.Count == 0) return;
            _open = true;

            ThemePalette p = Palette ?? ThemeManager.Current;
            var menu = new ContextMenuStrip();
            menu.Renderer = new DarkMenuRenderer(p);
            menu.Font = Font;
            menu.ShowImageMargin = false;
            menu.Padding = new Padding(2, 5, 2, 5);

            for (int i = 0; i < _items.Count; i++)
            {
                int index = i;
                var item = new ToolStripMenuItem(_items[i]);
                item.Checked = i == _selected;
                item.Click += delegate
                {
                    SelectedIndex = index;
                    _open = false;
                };
                menu.Items.Add(item);
            }

            menu.Closed += delegate { _open = false; };
            menu.Show(this, new Point(0, Height + 2));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            ThemePalette p = Palette ?? ThemeManager.Current;
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : p.Card);

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = IconRenderer.RoundedRect(rect, CornerRadius))
            using (var brush = new SolidBrush(_hover ? p.Hover : p.Input))
            using (var pen = new Pen(Focused ? p.Accent : p.InputBorder))
            {
                g.FillPath(brush, path);
                g.DrawPath(pen, path);
            }

            var textRect = new Rectangle(10, 0, Width - 32, Height);
            TextRenderer.DrawText(g, SelectedText, Font, textRect, p.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
                TextFormatFlags.EndEllipsis);

            // 右侧折角箭头
            int cx = Width - 16, cy = Height / 2;
            using (var pen = new Pen(p.TextMuted, 1.6f))
            {
                g.DrawLine(pen, cx - 4, cy - 2, cx, cy + 2);
                g.DrawLine(pen, cx, cy + 2, cx + 4, cy - 2);
            }
        }
    }

    /// <summary>圆角步进器：替代 NumericUpDown，保持统一视觉与可用性。</summary>
    internal sealed class RoundedStepper : Control
    {
        private int _value;
        public int Minimum = 0;
        public int Maximum = 100;
        public int Increment = 1;
        public ThemePalette Palette;
        public int CornerRadius = 6;
        public event EventHandler ValueChanged;

        private bool _hoverUp;
        private bool _hoverDown;

        public RoundedStepper(ThemePalette palette, int value)
        {
            Palette = palette;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            Font = new Font("Segoe UI", 9.5f);
            Cursor = Cursors.Hand;
            Height = 28;
            _value = value;
            TabStop = true;
        }

        public int Value
        {
            get { return _value; }
            set
            {
                int clamped = Math.Max(Minimum, Math.Min(Maximum, value));
                if (clamped == _value) return;
                _value = clamped;
                Invalidate();
                if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
            }
        }

        public void ApplyPalette(ThemePalette p)
        {
            Palette = p;
            BackColor = p.Card;
            Invalidate();
        }

        private Rectangle UpRect { get { return new Rectangle(Width - 26, 1, 25, Height / 2 - 1); } }
        private Rectangle DownRect { get { return new Rectangle(Width - 26, Height / 2, 25, Height / 2 - 1); } }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            bool up = UpRect.Contains(e.Location);
            bool down = DownRect.Contains(e.Location);
            if (up != _hoverUp || down != _hoverDown) { _hoverUp = up; _hoverDown = down; Invalidate(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hoverUp = _hoverDown = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (UpRect.Contains(e.Location)) Value = _value + Increment;
            else if (DownRect.Contains(e.Location)) Value = _value - Increment;
            base.OnMouseDown(e);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            Value = _value + (e.Delta > 0 ? Increment : -Increment);
            base.OnMouseWheel(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Up || e.KeyCode == Keys.Right) { Value = _value + Increment; e.Handled = true; }
            else if (e.KeyCode == Keys.Down || e.KeyCode == Keys.Left) { Value = _value - Increment; e.Handled = true; }
            base.OnKeyDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            ThemePalette p = Palette ?? ThemeManager.Current;
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : p.Card);

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = IconRenderer.RoundedRect(rect, CornerRadius))
            using (var brush = new SolidBrush(p.Input))
            using (var pen = new Pen(Focused ? p.Accent : p.InputBorder))
            {
                g.FillPath(brush, path);
                g.DrawPath(pen, path);
            }

            var textRect = new Rectangle(8, 0, Width - 36, Height);
            TextRenderer.DrawText(g, _value.ToString(), Font, textRect, p.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);

            using (var pen = new Pen(p.TextMuted, 1.5f))
            {
                Rectangle u = UpRect;
                int ux = u.X + u.Width / 2, uy = u.Y + u.Height / 2;
                g.DrawLine(pen, ux - 3, uy + 1, ux, uy - 2);
                g.DrawLine(pen, ux, uy - 2, ux + 3, uy + 1);

                Rectangle d = DownRect;
                int dx = d.X + d.Width / 2, dy = d.Y + d.Height / 2;
                g.DrawLine(pen, dx - 3, dy - 1, dx, dy + 2);
                g.DrawLine(pen, dx, dy + 2, dx + 3, dy - 1);
            }
        }
    }
}
