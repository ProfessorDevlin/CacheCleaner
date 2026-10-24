using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace CacheCleaner
{
    internal class Palette
    {
        public bool IsDark;
        public Color Back, Surface, Text, SubText, Border, Accent, AccentHover, AccentText, ButtonBack, ButtonHover;
    }

    /// <summary>
    /// Light and dark theme in black/gray/white; follows the Windows "app mode" setting.
    /// </summary>
    internal static class Theme
    {
        // The first installed font is used (Segoe UI Variable is available from Windows 11).
        private static readonly string[] Fonts = { "Segoe UI Variable Text", "Segoe UI" };
        private static readonly string[] TitleFonts = { "Segoe UI Variable Text Semibold", "Segoe UI Semibold" };
        private static readonly FontFamily[] Installed = new System.Drawing.Text.InstalledFontCollection().Families;

        public static readonly Font UiFont = FirstInstalled(Fonts, 10f);
        public static readonly Font TitleFont = FirstInstalled(TitleFonts, 10.5f);

        private static Font FirstInstalled(string[] names, float size)
        {
            foreach (string name in names)
                if (Array.Exists(Installed, f => f.Name == name))
                    return new Font(name, size);
            return new Font(FontFamily.GenericSansSerif, size);
        }

        public static readonly Palette Light = new Palette
        {
            IsDark = false,
            Back = Color.FromArgb(0xF5, 0xF4, 0xED),
            Surface = Color.FromArgb(0xFF, 0xFF, 0xFF),
            Text = Color.FromArgb(0x14, 0x14, 0x13),
            SubText = Color.FromArgb(0x73, 0x72, 0x6C),
            Border = Color.FromArgb(0xE0, 0xDE, 0xD5),
            Accent = Color.FromArgb(0x14, 0x14, 0x13),
            AccentHover = Color.FromArgb(0x3D, 0x3D, 0x3A),
            AccentText = Color.FromArgb(0xFA, 0xF9, 0xF5),
            ButtonBack = Color.FromArgb(0xFF, 0xFF, 0xFF),
            ButtonHover = Color.FromArgb(0xF0, 0xEE, 0xE6),
        };

        public static readonly Palette Dark = new Palette
        {
            IsDark = true,
            Back = Color.FromArgb(0x26, 0x26, 0x24),
            Surface = Color.FromArgb(0x30, 0x30, 0x2E),
            Text = Color.FromArgb(0xFA, 0xF9, 0xF5),
            SubText = Color.FromArgb(0xA6, 0xA3, 0x9A),
            Border = Color.FromArgb(0x45, 0x44, 0x3F),
            Accent = Color.FromArgb(0xFA, 0xF9, 0xF5),
            AccentHover = Color.FromArgb(0xDE, 0xDC, 0xD4),
            AccentText = Color.FromArgb(0x1F, 0x1E, 0x1D),
            ButtonBack = Color.FromArgb(0x3A, 0x3A, 0x37),
            ButtonHover = Color.FromArgb(0x45, 0x44, 0x40),
        };

        public static Palette Current { get; private set; } = Light;

        /// <summary>Re-reads the Windows theme. Returns true if it has changed.</summary>
        public static bool Refresh()
        {
            bool dark = false;
            using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                dark = key?.GetValue("AppsUseLightTheme") is int value && value == 0;

            var next = dark ? Dark : Light;
            bool changed = next != Current;
            Current = next;
            return changed;
        }

        public static void Apply(Control root) => ApplyTo(root, Current.Back);

        private static void ApplyTo(Control c, Color background)
        {
            var p = Current;
            Color childBackground = background;

            switch (c)
            {
                case Form form:
                    form.BackColor = p.Back;
                    form.ForeColor = p.Text;
                    ApplyTitleBar(form);
                    break;
                case CardPanel card:
                    card.BackColor = background;
                    card.ForeColor = p.Text;
                    childBackground = p.Surface;
                    break;
                case Button button:
                    StyleButton(button);
                    break;
                default:
                    c.BackColor = background;
                    // Labels tagged "sub" are secondary text (descriptions, sizes, status)
                    c.ForeColor = (c.Tag as string) == "sub" ? p.SubText : p.Text;
                    break;
            }

            c.Invalidate();
            foreach (Control child in c.Controls)
                ApplyTo(child, childBackground);
        }

        private static void StyleButton(Button b)
        {
            var p = Current;
            bool primary = (b.Tag as string) == "primary";
            b.FlatStyle = FlatStyle.Flat;
            b.UseVisualStyleBackColor = false;
            b.Cursor = Cursors.Hand;
            b.BackColor = primary ? p.Accent : p.ButtonBack;
            b.ForeColor = primary ? p.AccentText : p.Text;
            b.FlatAppearance.BorderColor = primary ? p.Accent : p.Border;
            b.FlatAppearance.MouseOverBackColor = primary ? p.AccentHover : p.ButtonHover;
            b.FlatAppearance.MouseDownBackColor = primary ? p.AccentHover : p.Border;
        }

        // ---------------------------------------------------------------- Windows window frame

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        /// <summary>Dark title bar and (from Windows 11) a title bar in the background color.</summary>
        public static void ApplyTitleBar(Form form)
        {
            if (!form.IsHandleCreated)
                return;

            int dark = Current.IsDark ? 1 : 0;
            if (DwmSetWindowAttribute(form.Handle, 20, ref dark, 4) != 0)
                DwmSetWindowAttribute(form.Handle, 19, ref dark, 4);

            int caption = ColorRef(Current.Back);
            int text = ColorRef(Current.Text);
            DwmSetWindowAttribute(form.Handle, 35, ref caption, 4);
            DwmSetWindowAttribute(form.Handle, 36, ref text, 4);
        }

        private static int ColorRef(Color c) => c.R | (c.G << 8) | (c.B << 16);

        public static GraphicsPath RoundedRect(RectangleF r, float radius)
        {
            float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            var path = new GraphicsPath();
            path.AddArc(r.Left, r.Top, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    /// <summary>Rounded card with a heading (replaces the GroupBox).</summary>
    [DesignerCategory("")]
    internal class CardPanel : Panel
    {
        private const int TitleHeight = 26;

        public CardPanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            Padding = new Padding(12, TitleHeight + 8, 12, 8);
        }

        [Browsable(true)]
        public override string Text
        {
            get => base.Text;
            set { base.Text = value; Invalidate(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var p = Theme.Current;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            TextRenderer.DrawText(g, Text, Theme.TitleFont, new Point(2, 4), p.Text);

            var rect = new RectangleF(0.5f, TitleHeight + 0.5f, Width - 1.5f, Height - TitleHeight - 1.5f);
            using (var path = Theme.RoundedRect(rect, 8))
            using (var fill = new SolidBrush(p.Surface))
            using (var pen = new Pen(p.Border))
            {
                g.FillPath(fill, path);
                g.DrawPath(pen, path);
            }
        }
    }

    /// <summary>Checkbox with a rounded box in the accent color.</summary>
    [DesignerCategory("")]
    internal class ThemedCheckBox : CheckBox
    {
        private bool hover;

        public ThemedCheckBox()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnEnabledChanged(EventArgs e) { Cursor = Enabled ? Cursors.Hand : Cursors.Default; base.OnEnabledChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var p = Theme.Current;
            var g = e.Graphics;
            g.Clear(BackColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            const float size = 16;
            var box = new RectangleF(1, (Height - size) / 2f, size, size);
            using (var path = Theme.RoundedRect(box, 4))
            {
                if (Checked)
                {
                    using (var fill = new SolidBrush(hover ? p.AccentHover : p.Accent))
                        g.FillPath(fill, path);
                    using (var pen = new Pen(p.AccentText, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                        g.DrawLines(pen, new[]
                        {
                            new PointF(box.Left + 4, box.Top + 8.5f),
                            new PointF(box.Left + 7, box.Top + 11.5f),
                            new PointF(box.Left + 12, box.Top + 5),
                        });
                }
                else
                {
                    using (var fill = new SolidBrush(p.Back))
                        g.FillPath(fill, path);
                    using (var pen = new Pen(Enabled && hover ? p.Accent : p.SubText, 1.2f))
                        g.DrawPath(pen, path);
                }
            }

            var textRect = new Rectangle((int)size + 8, 0, Width - (int)size - 8, Height);
            TextRenderer.DrawText(g, Text, Font, textRect, Enabled ? p.Text : p.SubText,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
        }
    }

    /// <summary>Thin rounded progress bar; in marquee mode a block slides back and forth.</summary>
    [DesignerCategory("")]
    internal class ThemedProgressBar : Control
    {
        private readonly Timer marqueeTimer = new Timer { Interval = 20 };
        private int value;
        private bool marquee;
        private float marqueePos;

        public ThemedProgressBar()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            marqueeTimer.Tick += (s, e) =>
            {
                marqueePos = (marqueePos + 0.015f) % 1.3f;
                Invalidate();
            };
        }

        public int Value
        {
            get => value;
            set { this.value = Math.Max(0, Math.Min(100, value)); Invalidate(); }
        }

        public bool Marquee
        {
            get => marquee;
            set
            {
                marquee = value;
                marqueeTimer.Enabled = value;
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var p = Theme.Current;
            var g = e.Graphics;
            g.Clear(BackColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var track = new RectangleF(0, 0, Width - 1, Height - 1);
            float radius = Height / 2f;
            using (var path = Theme.RoundedRect(track, radius))
            using (var fill = new SolidBrush(p.Border))
                g.FillPath(fill, path);

            RectangleF bar;
            if (marquee)
            {
                float width = track.Width * 0.3f;
                float x = (marqueePos - 0.3f) * track.Width;
                bar = RectangleF.Intersect(new RectangleF(x, 0, width, track.Height), track);
            }
            else
            {
                bar = new RectangleF(0, 0, track.Width * value / 100f, track.Height);
            }

            if (bar.Width < 1)
                return;
            using (var path = Theme.RoundedRect(bar, radius))
            using (var fill = new SolidBrush(p.Accent))
                g.FillPath(fill, path);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                marqueeTimer.Dispose();
            base.Dispose(disposing);
        }
    }

    /// <summary>Message and confirmation dialog in the theme colors (the standard MessageBox is always light).</summary>
    [DesignerCategory("")]
    internal class ThemedDialog : Form
    {
        private readonly Control focusOnShow;

        private ThemedDialog(string title, string message, string okText, string cancelText)
        {
            Text = title;
            Font = Theme.UiFont;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ShowIcon = false;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(20, 18, 20, 16);

            var layout = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Dock = DockStyle.Fill };
            var label = new Label { Text = message, AutoSize = true, MaximumSize = new Size(420, 0), Margin = new Padding(0, 0, 0, 18) };
            var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill, Margin = Padding.Empty };

            var ok = new Button { Text = okText, AutoSize = true, Padding = new Padding(10, 3, 10, 3), Tag = "primary", DialogResult = DialogResult.OK };
            buttons.Controls.Add(ok);
            AcceptButton = ok;
            if (cancelText != null)
            {
                var cancel = new Button { Text = cancelText, AutoSize = true, Padding = new Padding(10, 3, 10, 3), DialogResult = DialogResult.Cancel };
                buttons.Controls.Add(cancel);
                CancelButton = cancel;
                focusOnShow = cancel; // Enter alone should not delete anything
            }

            layout.Controls.Add(label);
            layout.Controls.Add(buttons);
            Controls.Add(layout);
            Theme.Apply(this);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.ApplyTitleBar(this);
        }

        // Focus can only be set once the dialog is visible
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            focusOnShow?.Focus();
        }

        public static void Info(IWin32Window owner, string title, string message)
        {
            using (var dialog = new ThemedDialog(title, message, "OK", null))
                dialog.ShowDialog(owner);
        }

        public static bool Confirm(IWin32Window owner, string title, string message, string okText, string cancelText)
        {
            using (var dialog = new ThemedDialog(title, message, okText, cancelText))
                return dialog.ShowDialog(owner) == DialogResult.OK;
        }
    }
}
