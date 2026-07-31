using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.ComponentModel;
using System.Windows.Forms;

namespace EasyCPDLC
{
    public static class DcduTheme
    {
        // V5: closer to real DCDU photos: blue/grey housing, black-blue LCD,
        // white/cyan message text, green header/status, amber hardware key labels.
        // Use a transparency key color that does not occur anywhere in the rendered DCDU artwork.
        // The old value (1,2,3) also appeared in very dark edge/shadow pixels of the PNG assets,
        // which made WinForms punch holes into the bezel and caused the apparent clipping/cut-off borders.
        public static readonly Color TransparentKey = Color.FromArgb(1, 2, 3);
        public static readonly Color Back = Color.FromArgb(10, 13, 16);
        public static readonly Color BezelTop = Color.FromArgb(92, 109, 119);
        public static readonly Color BezelMid = Color.FromArgb(66, 80, 89);
        public static readonly Color BezelBottom = Color.FromArgb(38, 48, 54);
        public static readonly Color BezelEdge = Color.FromArgb(126, 139, 146);
        public static readonly Color BezelDarkEdge = Color.FromArgb(12, 15, 17);
        public static readonly Color Screen = Color.FromArgb(5, 9, 15);
        public static readonly Color ScreenAlt = Color.FromArgb(8, 13, 22);
        public static readonly Color Green = Color.FromArgb(86, 255, 103);
        public static readonly Color Cyan = Color.FromArgb(45, 231, 245);
        public static readonly Color CyanWhite = Color.FromArgb(224, 232, 238);
        public static readonly Color Amber = Color.FromArgb(255, 210, 76);
        public static readonly Color AmberDim = Color.FromArgb(178, 130, 38);
        public static readonly Color SoftKeyBack = Color.FromArgb(7, 8, 9);
        public static readonly Color SoftKeyTop = Color.FromArgb(31, 34, 34);
        public static readonly Color SoftKeyBorder = Color.FromArgb(80, 86, 86);
        public static readonly Color TextDim = Color.FromArgb(120, 136, 144);

        public static Font Mono(float size, FontStyle style = FontStyle.Regular)
            => new Font("Consolas", size, style, GraphicsUnit.Point);

        public static Font Ui(float size, FontStyle style = FontStyle.Regular)
            => new Font("Segoe UI", size, style, GraphicsUnit.Point);
    }



    public class DcduMenuColorTable : ProfessionalColorTable
    {
        public override Color MenuItemSelected => Color.FromArgb(20, 58, 74);
        public override Color MenuItemBorder => DcduTheme.Cyan;
        public override Color ToolStripDropDownBackground => Color.FromArgb(5, 9, 15);
        public override Color ImageMarginGradientBegin => Color.FromArgb(5, 9, 15);
        public override Color ImageMarginGradientMiddle => Color.FromArgb(5, 9, 15);
        public override Color ImageMarginGradientEnd => Color.FromArgb(5, 9, 15);
        public override Color MenuBorder => DcduTheme.Cyan;
        public override Color SeparatorDark => Color.FromArgb(55, 88, 95);
        public override Color SeparatorLight => Color.FromArgb(55, 88, 95);
    }

    public class DcduMenuRenderer : ToolStripProfessionalRenderer
    {
        public DcduMenuRenderer() : base(new DcduMenuColorTable()) { }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            using SolidBrush b = new SolidBrush(Color.FromArgb(5, 9, 15));
            e.Graphics.FillRectangle(b, e.AffectedBounds);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            Rectangle r = new Rectangle(0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
            using Pen border = new Pen(DcduTheme.Cyan, 1.0f);
            e.Graphics.DrawRectangle(border, r);
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            Rectangle r = new Rectangle(Point.Empty, e.Item.Size);
            using SolidBrush back = new SolidBrush(e.Item.Selected ? Color.FromArgb(18, 46, 56) : Color.FromArgb(5, 9, 15));
            e.Graphics.FillRectangle(back, r);
            if (e.Item.Selected)
            {
                using Pen p = new Pen(DcduTheme.Cyan, 1.0f);
                e.Graphics.DrawRectangle(p, 1, 1, r.Width - 3, r.Height - 3);
            }
        }
    }


    public static class DcduStyleManager
    {
        public const string Airbus = "AIRBUS";
        public const string Boeing = "BOEING";
        public const string A220 = "A220";

        private static string currentStyle = LoadStyle();

        public static string CurrentStyle
        {
            get => currentStyle;
            set
            {
                currentStyle = NormalizeStyle(value);
                SaveStyle(currentStyle);
            }
        }

        public static bool IsBoeing => string.Equals(CurrentStyle, Boeing, StringComparison.OrdinalIgnoreCase);
        public static bool IsA220 => string.Equals(CurrentStyle, A220, StringComparison.OrdinalIgnoreCase);

        public static string NormalizeStyle(string style)
        {
            if (string.Equals(style, Boeing, StringComparison.OrdinalIgnoreCase))
            {
                return Boeing;
            }

            if (string.Equals(style, A220, StringComparison.OrdinalIgnoreCase))
            {
                return A220;
            }

            return Airbus;
        }

        public static string AssetFile(string airbusAssetFile)
        {
            if (IsA220 || !IsBoeing || string.IsNullOrWhiteSpace(airbusAssetFile))
            {
                return airbusAssetFile;
            }

            string fileName = Path.GetFileNameWithoutExtension(airbusAssetFile);
            string extension = Path.GetExtension(airbusAssetFile);
            return fileName + "_Boeing" + extension;
        }

        private static string StyleFilePath
        {
            get
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EasyCPDLC");
                return Path.Combine(dir, "DcduStyle.txt");
            }
        }

        private static string LoadStyle()
        {
            try
            {
                string path = StyleFilePath;
                if (File.Exists(path))
                {
                    return NormalizeStyle(File.ReadAllText(path).Trim());
                }
            }
            catch
            {
                // Fall back to Airbus if config cannot be read.
            }

            return Airbus;
        }

        private static void SaveStyle(string style)
        {
            try
            {
                string path = StyleFilePath;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, NormalizeStyle(style));
            }
            catch
            {
                // Keep runtime style even if persistence fails.
            }
        }
    }

    public static class DcduWindowHelper
    {
        public static void ApplyDeviceWindow(Form form, Control frame, int radius = 18)
        {
            if (form == null || frame == null) return;
            form.BackColor = DcduTheme.TransparentKey;
            form.TransparencyKey = DcduTheme.TransparentKey;
            form.FormBorderStyle = FormBorderStyle.None;

            // Bitmap-based DCDU frames need a few transparent pixels around the artwork.
            // Otherwise the rounded Form.Region clips screws/corners at the form edge.
            // For normal drawn panels we keep the old behavior and fill the client area.
            bool preserveDesignedFrameBounds = frame is DcduAssetPanel;

            void UpdateRegion()
            {
                if (form.Width <= 0 || form.Height <= 0) return;
                using GraphicsPath path = DcduPanel.RoundedRect(new Rectangle(0, 0, form.Width - 1, form.Height - 1), radius);
                form.Region?.Dispose();
                form.Region = new Region(path);

                if (!preserveDesignedFrameBounds)
                {
                    frame.Location = new Point(0, 0);
                    frame.Size = form.ClientSize;
                }
            }

            UpdateRegion();
            form.Resize += (_, __) => UpdateRegion();
        }
    }


    public static class DcduAssets
    {
        public static Image LoadImage(string fileName)
        {
            // Prefer embedded resources for single-file publishing.
            Image embedded = EmbeddedAssets.LoadImage("Resources", fileName);
            if (embedded != null)
            {
                return embedded;
            }

            // Developer fallback: allow loose resources while running from the IDE/source tree.
            foreach (string path in CandidatePaths(fileName))
            {
                if (File.Exists(path))
                {
                    using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using Image image = Image.FromStream(stream);
                    return new Bitmap(image);
                }
            }

            return null;
        }

        private static System.Collections.Generic.IEnumerable<string> CandidatePaths(string fileName)
        {
            string baseDir = AppContext.BaseDirectory;
            yield return Path.Combine(baseDir, "Resources", fileName);
            yield return Path.Combine(Application.StartupPath, "Resources", fileName);
            yield return Path.Combine(Environment.CurrentDirectory, "Resources", fileName);

            DirectoryInfo dir = new DirectoryInfo(baseDir);
            for (int i = 0; i < 8 && dir != null; i++, dir = dir.Parent)
            {
                yield return Path.Combine(dir.FullName, "Resources", fileName);
                yield return Path.Combine(dir.FullName, fileName);
            }
        }
    }

    public class DcduAssetPanel : Panel
    {
        private Image cachedImage;
        private string assetFileName;
        private Rectangle highlightRectangle = Rectangle.Empty;
        private bool highlightPressed;
        private bool drawA220Bezel;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool DrawA220Bezel
        {
            get => drawA220Bezel;
            set
            {
                if (drawA220Bezel == value) return;
                drawA220Bezel = value;
                Invalidate();
            }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string AssetFileName
        {
            get => assetFileName;
            set
            {
                if (assetFileName == value) return;
                assetFileName = value;
                cachedImage?.Dispose();
                cachedImage = null;
                Invalidate();
            }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool ShowHotspotHighlight { get; set; } = false;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Rectangle HighlightRectangle
        {
            get => highlightRectangle;
            set
            {
                if (highlightRectangle == value) return;
                Rectangle old = highlightRectangle;
                highlightRectangle = value;
                if (!old.IsEmpty) Invalidate(Rectangle.Inflate(old, 5, 5));
                if (!highlightRectangle.IsEmpty) Invalidate(Rectangle.Inflate(highlightRectangle, 5, 5));
            }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool HighlightPressed
        {
            get => highlightPressed;
            set
            {
                if (highlightPressed == value) return;
                highlightPressed = value;
                if (!highlightRectangle.IsEmpty) Invalidate(Rectangle.Inflate(highlightRectangle, 5, 5));
            }
        }

        public DcduAssetPanel()
        {
            DoubleBuffered = true;
            BackColor = Color.Transparent;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(DcduTheme.TransparentKey);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

            if (DrawA220Bezel)
            {
                DrawA220Background(e.Graphics);
            }
            else
            {
                cachedImage ??= DcduAssets.LoadImage(AssetFileName);
                if (cachedImage != null)
                {
                    e.Graphics.DrawImage(cachedImage, new Rectangle(0, 0, Width, Height));
                }
                else
                {
                    using SolidBrush fallback = new SolidBrush(DcduTheme.BezelMid);
                    e.Graphics.FillRectangle(fallback, ClientRectangle);
                }
            }

            DrawHotspotHighlight(e.Graphics);
        }

        private void DrawA220Background(Graphics graphics)
        {
            if (Width <= 2 || Height <= 2)
            {
                return;
            }

            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

            Rectangle outer = new Rectangle(1, 1, Width - 3, Height - 3);
            int outerRadius = Math.Max(12, Math.Min(28, Math.Min(Width, Height) / 18));

            using (GraphicsPath outerPath = DcduPanel.RoundedRect(outer, outerRadius))
            using (LinearGradientBrush bezelBrush = new LinearGradientBrush(
                outer,
                Color.FromArgb(105, 124, 145),
                Color.FromArgb(48, 64, 82),
                LinearGradientMode.Vertical))
            using (Pen brightEdge = new Pen(Color.FromArgb(145, 163, 183), 1.2f))
            using (Pen darkEdge = new Pen(Color.FromArgb(20, 29, 39), 2.0f))
            {
                graphics.FillPath(bezelBrush, outerPath);
                graphics.DrawPath(darkEdge, outerPath);

                Rectangle highlight = Rectangle.Inflate(outer, -3, -3);
                using GraphicsPath highlightPath = DcduPanel.RoundedRect(
                    highlight,
                    Math.Max(8, outerRadius - 3));
                graphics.DrawPath(brightEdge, highlightPath);
            }

            // Dark recessed touchscreen area. The actual navigation and content controls
            // are placed over this surface by MainForm.
            Rectangle recess = new Rectangle(
                Math.Max(14, Width / 50),
                Math.Max(12, Height / 54),
                Width - (Math.Max(14, Width / 50) * 2),
                Height - (Math.Max(12, Height / 54) * 2));

            using (GraphicsPath recessPath = DcduPanel.RoundedRect(
                recess,
                Math.Max(10, outerRadius - 7)))
            using (LinearGradientBrush recessBrush = new LinearGradientBrush(
                recess,
                Color.FromArgb(10, 17, 25),
                Color.FromArgb(2, 5, 9),
                LinearGradientMode.Vertical))
            using (Pen recessOuter = new Pen(Color.FromArgb(22, 31, 42), 5.0f))
            using (Pen recessInner = new Pen(Color.FromArgb(122, 142, 161), 1.0f))
            {
                graphics.FillPath(recessBrush, recessPath);
                graphics.DrawPath(recessOuter, recessPath);

                Rectangle innerLine = Rectangle.Inflate(recess, -5, -5);
                using GraphicsPath innerPath = DcduPanel.RoundedRect(
                    innerLine,
                    Math.Max(7, outerRadius - 10));
                graphics.DrawPath(recessInner, innerPath);
            }

            // Very subtle blue reflection along the top edge, similar to the A220 display.
            Rectangle reflection = new Rectangle(
                recess.Left + 10,
                recess.Top + 7,
                Math.Max(1, recess.Width - 20),
                Math.Max(1, recess.Height / 5));

            using (LinearGradientBrush reflectionBrush = new LinearGradientBrush(
                reflection,
                Color.FromArgb(22, 72, 106, 142),
                Color.FromArgb(0, 72, 106, 142),
                LinearGradientMode.Vertical))
            {
                graphics.FillRectangle(reflectionBrush, reflection);
            }
        }

        private void DrawHotspotHighlight(Graphics g)
        {
            // Hardware-style assets already contain their own button states.
            // Runtime mouseover halos are intentionally disabled.
            return;
        }
    }

    public class DcduHotspotButton : Control
    {
        public DcduHotspotButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            ForeColor = Color.Transparent;
            Cursor = Cursors.Hand;
            TabStop = false;
            Text = string.Empty;
        }

        protected override void OnPaintBackground(PaintEventArgs pevent)
        {
            // Intentionally empty. This class is now mostly used as a rectangle holder
            // for hit testing. It should never cover the bitmap artwork.
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            // No direct drawing. The parent DcduAssetPanel draws hover highlights.
        }

        public void PerformClick()
        {
            if (Enabled)
            {
                OnClick(EventArgs.Empty);
            }
        }
    }

    public class DcduActionButton : Control
    {
        private bool isHovered;
        private bool isPressed;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool Accent { get; set; } = true;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool Blue { get; set; } = false;

        public DcduActionButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            ForeColor = DcduTheme.CyanWhite;
            Font = DcduTheme.Mono(14f, FontStyle.Bold);
            Cursor = Cursors.Hand;
            TabStop = true;
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); isHovered = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); isHovered = false; isPressed = false; Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (e.Button == MouseButtons.Left) { isPressed = true; Invalidate(); } }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); isPressed = false; Invalidate(); }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); isHovered = false; isPressed = false; Invalidate(); }

        protected override void OnPaintBackground(PaintEventArgs pevent)
        {
            // Draw complete background ourselves.
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            using GraphicsPath path = DcduPanel.RoundedRect(r, 8);

            Color top;
            Color bottom;
            Color borderColor;

            if (!Enabled)
            {
                top = Color.FromArgb(20, 24, 22);
                bottom = Color.FromArgb(10, 12, 12);
                borderColor = Color.FromArgb(80, 90, 90);
            }
            else if (Blue)
            {
                top = isPressed ? Color.FromArgb(8, 42, 64) : isHovered ? Color.FromArgb(18, 94, 132) : Color.FromArgb(12, 68, 106);
                bottom = isPressed ? Color.FromArgb(22, 92, 132) : isHovered ? Color.FromArgb(8, 58, 92) : Color.FromArgb(5, 38, 68);
                borderColor = isHovered || isPressed ? DcduTheme.Cyan : Color.FromArgb(70, 200, 230);
            }
            else
            {
                top = Color.FromArgb(28, 74, 37);
                bottom = Color.FromArgb(8, 34, 13);
                borderColor = isHovered ? DcduTheme.Cyan : DcduTheme.Green;
                if (isHovered)
                {
                    top = Color.FromArgb(36, 92, 46);
                    bottom = Color.FromArgb(10, 47, 16);
                }
                if (isPressed)
                {
                    top = Color.FromArgb(8, 38, 15);
                    bottom = Color.FromArgb(42, 96, 49);
                }
            }

            using LinearGradientBrush brush = new LinearGradientBrush(r, top, bottom, LinearGradientMode.Vertical);
            using Pen border = new Pen(borderColor, isHovered ? 2.0f : 1.3f);
            e.Graphics.FillPath(brush, path);
            e.Graphics.DrawPath(border, path);

            // Avoid a bright white artifact in the upper-left corner of the small blue CONNECT button.
            // The cyan border is enough for the avionics-style highlight.
            if (!Blue)
            {
                using Pen shine = new Pen(Color.FromArgb(isHovered ? 85 : 55, 255, 255, 255), 1.0f);
                e.Graphics.DrawLine(shine, 12, 6, Width - 13, 6);
            }

            TextRenderer.DrawText(
                e.Graphics,
                Text,
                Font,
                ClientRectangle,
                Enabled ? DcduTheme.Amber : Color.FromArgb(95, 105, 105),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    public class DcduPanel : Panel
    {
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int Radius { get; set; } = 16;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool DrawScrews { get; set; } = true;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool DrawWear { get; set; } = true;

        public DcduPanel()
        {
            DoubleBuffered = true;
            BackColor = Color.Transparent;
            Padding = new Padding(22);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using GraphicsPath path = RoundedRect(rect, Radius);
            using LinearGradientBrush body = new LinearGradientBrush(rect, DcduTheme.BezelTop, DcduTheme.BezelBottom, LinearGradientMode.Vertical);
            e.Graphics.FillPath(body, path);

            // Fine rough powder-coated material, not a fake grid.
            using HatchBrush grain = new HatchBrush(HatchStyle.Percent05, Color.FromArgb(10, 255, 255, 255), Color.FromArgb(0, 0, 0, 0));
            e.Graphics.FillPath(grain, path);

            if (DrawWear)
            {
                DrawDeterministicWear(e.Graphics, rect);
            }

            using Pen outerDark = new Pen(DcduTheme.BezelDarkEdge, 3f);
            using Pen topLight = new Pen(Color.FromArgb(150, 145, 158, 166), 1f);
            using Pen innerDark = new Pen(Color.FromArgb(95, 0, 0, 0), 5f);
            e.Graphics.DrawPath(outerDark, path);
            e.Graphics.DrawLine(topLight, 24, 15, Width - 25, 15);

            Rectangle recess = new Rectangle(18, 18, Width - 37, Height - 37);
            using GraphicsPath recessPath = RoundedRect(recess, Math.Max(4, Radius - 4));
            e.Graphics.DrawPath(innerDark, recessPath);

            if (DrawScrews)
            {
                DrawScrew(e.Graphics, 19, 20);
                DrawScrew(e.Graphics, Width - 38, 20);
                DrawScrew(e.Graphics, 19, Height - 39);
                DrawScrew(e.Graphics, Width - 38, Height - 39);
            }
        }

        private static void DrawDeterministicWear(Graphics g, Rectangle rect)
        {
            // Small edge scuffs like the reference photos, deterministic so it won't flicker.
            using Pen scratch = new Pen(Color.FromArgb(42, 230, 230, 220), 1f);
            Point[] p =
            {
                new Point(86, 22), new Point(105, 20),
                new Point(190, 18), new Point(212, 19),
                new Point(rect.Right - 210, 21), new Point(rect.Right - 188, 19),
                new Point(rect.Right - 92, 55), new Point(rect.Right - 75, 57),
                new Point(42, rect.Bottom - 76), new Point(62, rect.Bottom - 74),
                new Point(rect.Right - 165, rect.Bottom - 28), new Point(rect.Right - 142, rect.Bottom - 27)
            };
            for (int i = 0; i + 1 < p.Length; i += 2)
            {
                if (p[i].X > 0 && p[i].X < rect.Width && p[i].Y > 0 && p[i].Y < rect.Height)
                    g.DrawLine(scratch, p[i], p[i + 1]);
            }
        }

        private static void DrawScrew(Graphics g, int x, int y)
        {
            Rectangle shadowRect = new Rectangle(x + 2, y + 3, 20, 20);
            using SolidBrush shadow = new SolidBrush(Color.FromArgb(125, 0, 0, 0));
            g.FillEllipse(shadow, shadowRect);

            Rectangle r = new Rectangle(x, y, 20, 20);
            using LinearGradientBrush b = new LinearGradientBrush(r, Color.FromArgb(100, 110, 112), Color.FromArgb(10, 11, 12), LinearGradientMode.ForwardDiagonal);
            using Pen p = new Pen(Color.FromArgb(3, 3, 4), 2);
            g.FillEllipse(b, r);
            g.DrawEllipse(p, r);
            using Pen slot = new Pen(Color.FromArgb(2, 2, 2), 2.3f);
            g.DrawLine(slot, x + 5, y + 10, x + 15, y + 10);
        }

        internal static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            radius = Math.Max(1, radius);
            int d = radius * 2;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    public class DcduScreenPanel : Panel
    {
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int Radius { get; set; } = 8;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool DrawScreenBackground { get; set; } = true;

        public DcduScreenPanel()
        {
            DoubleBuffered = true;
            BackColor = DcduTheme.Screen;
            ForeColor = DcduTheme.CyanWhite;
            Padding = new Padding(18);
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
        }

        protected override void OnPaintBackground(PaintEventArgs pevent)
        {
            if (!DrawScreenBackground && Parent != null)
            {
                GraphicsState state = pevent.Graphics.Save();
                pevent.Graphics.TranslateTransform(-Left, -Top);
                Rectangle parentClip = new Rectangle(Left, Top, Width, Height);
                using PaintEventArgs parentArgs = new PaintEventArgs(pevent.Graphics, parentClip);
                InvokePaintBackground(Parent, parentArgs);
                InvokePaint(Parent, parentArgs);
                pevent.Graphics.Restore(state);
                return;
            }

            base.OnPaintBackground(pevent);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (!DrawScreenBackground)
            {
                base.OnPaint(e);
                return;
            }
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

            Rectangle dropShadow = new Rectangle(3, 4, Width - 3, Height - 3);
            using GraphicsPath shadowPath = DcduPanel.RoundedRect(dropShadow, Radius);
            using SolidBrush shadow = new SolidBrush(Color.FromArgb(190, 0, 0, 0));
            e.Graphics.FillPath(shadow, shadowPath);

            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using GraphicsPath path = DcduPanel.RoundedRect(rect, Radius);
            using LinearGradientBrush b = new LinearGradientBrush(rect, DcduTheme.ScreenAlt, DcduTheme.Screen, LinearGradientMode.Vertical);
            using Pen border = new Pen(Color.FromArgb(72, 88, 100), 1.2f);
            e.Graphics.FillPath(b, path);
            e.Graphics.DrawPath(border, path);

            // Real DCDU: no green grid. Only slight glass/vignette.
            Rectangle vignette = new Rectangle(2, 2, Width - 5, Height - 5);
            using GraphicsPath vignettePath = DcduPanel.RoundedRect(vignette, Math.Max(3, Radius - 2));
            using PathGradientBrush glow = new PathGradientBrush(vignettePath)
            {
                CenterColor = Color.FromArgb(0, 0, 0, 0),
                SurroundColors = new[] { Color.FromArgb(92, 0, 0, 0) }
            };
            e.Graphics.FillPath(glow, vignettePath);

            base.OnPaint(e);
        }
    }

    public class DcduButton : Button
    {
        private bool isHovered;
        private bool isPressed;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool Accent { get; set; }

        public DcduButton()
        {
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            FlatAppearance.MouseOverBackColor = Color.Transparent;
            FlatAppearance.MouseDownBackColor = Color.Transparent;
            BackColor = Color.Transparent;
            ForeColor = DcduTheme.Amber;
            Font = DcduTheme.Mono(11f, FontStyle.Bold);
            TextAlign = ContentAlignment.MiddleCenter;
            Cursor = Cursors.Hand;
            UseVisualStyleBackColor = false;
            Height = 48;
            Width = 86;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            if (!Enabled) return;
            isHovered = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            isHovered = false;
            isPressed = false;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs mevent)
        {
            base.OnMouseDown(mevent);
            if (!Enabled || mevent.Button != MouseButtons.Left) return;
            isPressed = true;
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs mevent)
        {
            base.OnMouseUp(mevent);
            if (!Enabled) return;
            isPressed = false;
            Invalidate();
        }

        protected override void OnPaintBackground(PaintEventArgs pevent)
        {
            // Draw full button ourselves. Prevent WinForms from adding odd cyan/focus strips.
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            pevent.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            pevent.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

            Rectangle shadowRect = new Rectangle(4, 5, Width - 8, Height - 7);
            using GraphicsPath shadowPath = DcduPanel.RoundedRect(shadowRect, 7);
            using SolidBrush shadow = new SolidBrush(Color.FromArgb(115, 0, 0, 0));
            pevent.Graphics.FillPath(shadow, shadowPath);

            Rectangle rect = new Rectangle(3, 2, Width - 8, Height - 8);
            using GraphicsPath path = DcduPanel.RoundedRect(rect, 7);

            Color top;
            Color bottom;
            Color borderColor;
            float borderWidth;

            if (!Enabled)
            {
                top = Color.FromArgb(24, 26, 26);
                bottom = Color.FromArgb(7, 8, 9);
                borderColor = Color.FromArgb(64, 70, 70);
                borderWidth = 1.1f;
            }
            else if (isPressed)
            {
                top = Color.FromArgb(9, 14, 15);
                bottom = Color.FromArgb(27, 31, 31);
                borderColor = DcduTheme.Amber;
                borderWidth = 1.6f;
            }
            else if (isHovered)
            {
                top = Color.FromArgb(36, 42, 42);
                bottom = Color.FromArgb(12, 14, 15);
                borderColor = DcduTheme.Cyan;
                borderWidth = 1.5f;
            }
            else
            {
                top = Color.FromArgb(31, 34, 34);
                bottom = Color.FromArgb(5, 6, 7);
                borderColor = Accent ? Color.FromArgb(72, 170, 88) : Color.FromArgb(86, 92, 92);
                borderWidth = 1.1f;
            }

            using LinearGradientBrush b = new LinearGradientBrush(rect, top, bottom, LinearGradientMode.Vertical);
            using Pen border = new Pen(borderColor, borderWidth);
            pevent.Graphics.FillPath(b, path);
            pevent.Graphics.DrawPath(border, path);

            using Pen shine = new Pen(Color.FromArgb(35, 255, 255, 230), 1f);
            pevent.Graphics.DrawLine(shine, rect.Left + 7, rect.Top + 4, rect.Right - 7, rect.Top + 4);

            TextRenderer.DrawText(
                pevent.Graphics,
                Text,
                Font,
                rect,
                Enabled ? ForeColor : Color.FromArgb(90, 94, 92),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    public class DcduCheckBox : Control
    {
        private bool isChecked;
        private bool isHovered;

        public event EventHandler CheckedChanged;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool Checked
        {
            get => isChecked;
            set
            {
                if (isChecked == value) return;
                isChecked = value;
                Invalidate();
                CheckedChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public DcduCheckBox()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = DcduTheme.Screen;
            ForeColor = DcduTheme.CyanWhite;
            Font = DcduTheme.Mono(12f, FontStyle.Bold);
            Cursor = Cursors.Hand;
            Height = 32;
            Width = 300;
            TabStop = true;
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); isHovered = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); isHovered = false; Invalidate(); }
        protected override void OnClick(EventArgs e) { base.OnClick(e); Checked = !Checked; }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter)
            {
                Checked = !Checked;
                e.Handled = true;
            }
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (BackColor == Color.Transparent && Parent != null)
            {
                GraphicsState state = e.Graphics.Save();
                e.Graphics.TranslateTransform(-Left, -Top);
                using PaintEventArgs parentArgs = new PaintEventArgs(e.Graphics, new Rectangle(Left, Top, Width, Height));
                InvokePaintBackground(Parent, parentArgs);
                InvokePaint(Parent, parentArgs);
                e.Graphics.Restore(state);
                return;
            }

            using SolidBrush b = new SolidBrush(BackColor);
            e.Graphics.FillRectangle(b, ClientRectangle);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            int boxSize = Math.Min(30, Math.Max(22, Height - 8));
            Rectangle box = new Rectangle(0, (Height - boxSize) / 2, boxSize, boxSize);
            using SolidBrush fill = new SolidBrush(Checked ? Color.FromArgb(60, 132, 70) : Color.FromArgb(5, 9, 15));
            using Pen border = new Pen(Checked ? Color.White : (isHovered ? DcduTheme.Cyan : DcduTheme.CyanWhite), 2.0f);
            e.Graphics.FillRectangle(fill, box);
            e.Graphics.DrawRectangle(border, box);

            if (Checked)
            {
                using Pen tick = new Pen(Color.White, 3.3f);
                e.Graphics.DrawLines(tick, new[] { new Point(box.Left + 7, box.Top + boxSize / 2), new Point(box.Left + boxSize / 2 - 1, box.Bottom - 6), new Point(box.Right - 6, box.Top + 7) });
            }

            TextRenderer.DrawText(
                e.Graphics,
                Text,
                Font,
                new Rectangle(box.Right + 12, 0, Width - box.Right - 12, Height),
                ForeColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    public class DcduSmallButton : Button
    {
        public DcduSmallButton()
        {
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            FlatAppearance.MouseOverBackColor = Color.Transparent;
            FlatAppearance.MouseDownBackColor = Color.Transparent;
            BackColor = Color.Transparent;
            ForeColor = DcduTheme.Amber;
            Font = DcduTheme.Mono(10f, FontStyle.Bold);
            Cursor = Cursors.Hand;
            UseVisualStyleBackColor = false;
        }
    }

    public class CustomUI
    {
        public static UITextBox CreateTextBox(string _text, int _maxLength, Color _controlFrontColor, Color _controlBackColor, Font _font)
        {
            UITextBox _temp = new(_controlFrontColor)
            {
                BackColor = _controlBackColor,
                ForeColor = _controlFrontColor,
                Font = _font,
                MaxLength = _maxLength,
                BorderStyle = BorderStyle.None,
                Text = _text,
                CharacterCasing = CharacterCasing.Upper,
                Top = 10,
                Padding = new Padding(3, 0, 3, -10),
                Margin = new Padding(3, 5, 3, -10),
                Height = 20,
                TextAlign = HorizontalAlignment.Center
            };

            using (Graphics G = _temp.CreateGraphics())
            {
                _temp.Width = (int)(_temp.MaxLength * G.MeasureString("x", _temp.Font).Width);
            }

            return _temp;
        }

        public static Label CreateTemplate(string _text, Color _controlFrontColor, Color _controlBackColor, Font _font)
        {
            Label _temp = new()
            {
                BackColor = Color.Transparent,
                ForeColor = _controlFrontColor,
                Font = _font,
                AutoSize = true,
                Text = _text,
                Top = 10,
                Height = 20,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(0, 10, 0, 0),
                Margin = new Padding(0, 0, 0, 0)
            };

            return _temp;
        }
    }

    public class DcduScrollOverlay : Control
    {
        private const int ScrollBarBoth = 3;
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ShowScrollBar(IntPtr hWnd, int wBar, bool bShow);

        private readonly Timer refreshTimer;
        private bool dragging;
        private int dragOffset;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public ScrollableControl Target { get; set; }

        public DcduScrollOverlay()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            Width = 18;
            Visible = false;

            refreshTimer = new Timer { Interval = 120 };
            refreshTimer.Tick += (sender, args) =>
            {
                UpdateVisibility();
                if (Visible)
                {
                    HideNativeTargetScrollbars();
                    Invalidate();
                }
            };
            refreshTimer.Start();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                refreshTimer?.Stop();
                refreshTimer?.Dispose();
            }
            base.Dispose(disposing);
        }

        private void UpdateVisibility()
        {
            bool shouldBeVisible = Target != null && !Target.IsDisposed && MaxScroll > 0;
            if (Visible != shouldBeVisible)
            {
                Visible = shouldBeVisible;
                Parent?.Invalidate(Bounds, true);
                Target?.Parent?.Invalidate(Target.Bounds, true);
            }
        }

        private void HideNativeTargetScrollbars()
        {
            if (Target == null || Target.IsDisposed || !Target.IsHandleCreated) return;
            try
            {
                ShowScrollBar(Target.Handle, ScrollBarBoth, false);
            }
            catch
            {
                // Keep custom overlay best-effort only.
            }
        }

        private int CurrentScroll => Target == null ? 0 : Math.Max(0, -Target.AutoScrollPosition.Y);

        private int MaxScroll
        {
            get
            {
                if (Target == null || Target.IsDisposed) return 0;
                return Math.Max(0, Target.DisplayRectangle.Height - Target.ClientSize.Height);
            }
        }

        private Rectangle GetRailRectangle()
        {
            return new Rectangle(4, 2, Math.Max(8, Width - 8), Math.Max(8, Height - 4));
        }

        private Rectangle GetThumbRectangle()
        {
            Rectangle rail = GetRailRectangle();
            if (Target == null || MaxScroll <= 0)
            {
                return Rectangle.Empty;
            }

            int visible = Math.Max(1, Target.ClientSize.Height);
            int content = Math.Max(visible, Target.DisplayRectangle.Height);
            int thumbHeight = Math.Max(26, (int)Math.Round(rail.Height * (visible / (float)content)));
            thumbHeight = Math.Min(rail.Height, thumbHeight);
            int travel = Math.Max(1, rail.Height - thumbHeight);
            int thumbY = rail.Y + (int)Math.Round(travel * (CurrentScroll / (float)MaxScroll));
            return new Rectangle(rail.X + 1, thumbY + 2, Math.Max(4, rail.Width - 2), Math.Max(12, thumbHeight - 4));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (MaxScroll <= 0)
            {
                return;
            }

            HideNativeTargetScrollbars();

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

            // Screen-colored strip behind the custom rail, only visible while scrolling is needed.
            using SolidBrush screenFill = new SolidBrush(Color.FromArgb(8, 10, 14));
            e.Graphics.FillRectangle(screenFill, ClientRectangle);

            Rectangle rail = GetRailRectangle();
            using GraphicsPath railPath = DcduPanel.RoundedRect(rail, 5);
            using LinearGradientBrush railBrush = new LinearGradientBrush(
                rail,
                Color.FromArgb(236, 6, 10, 14),
                Color.FromArgb(236, 18, 27, 33),
                LinearGradientMode.Horizontal);
            using Pen railPen = new Pen(Color.FromArgb(130, 88, 106, 114), 1f);
            using Pen railHighlight = new Pen(Color.FromArgb(38, Color.White), 1f);
            e.Graphics.FillPath(railBrush, railPath);
            e.Graphics.DrawPath(railPen, railPath);
            e.Graphics.DrawLine(railHighlight, rail.Left + 2, rail.Top + 1, rail.Right - 3, rail.Top + 1);

            Rectangle innerRail = Rectangle.Inflate(rail, -3, -3);
            if (innerRail.Width > 0 && innerRail.Height > 0)
            {
                using GraphicsPath innerRailPath = DcduPanel.RoundedRect(innerRail, 4);
                using LinearGradientBrush innerRailBrush = new LinearGradientBrush(
                    innerRail,
                    Color.FromArgb(110, 2, 4, 6),
                    Color.FromArgb(120, 11, 17, 22),
                    LinearGradientMode.Horizontal);
                e.Graphics.FillPath(innerRailBrush, innerRailPath);
            }

            Rectangle thumb = GetThumbRectangle();
            if (thumb.IsEmpty)
            {
                return;
            }

            using GraphicsPath thumbPath = DcduPanel.RoundedRect(thumb, 4);
            using LinearGradientBrush thumbBrush = new LinearGradientBrush(
                thumb,
                Color.FromArgb(248, 116, 126, 130),
                Color.FromArgb(248, 58, 67, 72),
                LinearGradientMode.Horizontal);
            using Pen thumbOuter = new Pen(Color.FromArgb(210, 190, 198, 202), 1f);
            using Pen thumbHighlight = new Pen(Color.FromArgb(72, Color.White), 1f);

            e.Graphics.FillPath(thumbBrush, thumbPath);
            e.Graphics.DrawPath(thumbOuter, thumbPath);
            e.Graphics.DrawLine(thumbHighlight, thumb.Left + 2, thumb.Top + 1, thumb.Right - 3, thumb.Top + 1);

            int lineX = thumb.Left + thumb.Width / 2;
            using Pen centerLine = new Pen(Color.FromArgb(190, 228, 236, 238), 1.2f);
            using Pen gripOne = new Pen(Color.FromArgb(210, 255, 210, 76), 1.0f);
            using Pen gripTwo = new Pen(Color.FromArgb(140, 255, 210, 76), 1.0f);
            e.Graphics.DrawLine(centerLine, lineX, thumb.Top + 4, lineX, thumb.Bottom - 4);
            e.Graphics.DrawLine(gripOne, thumb.Left + 2, thumb.Top + Math.Max(5, thumb.Height / 3), thumb.Right - 3, thumb.Top + Math.Max(5, thumb.Height / 3));
            e.Graphics.DrawLine(gripTwo, thumb.Left + 2, thumb.Bottom - Math.Max(6, thumb.Height / 3), thumb.Right - 3, thumb.Bottom - Math.Max(6, thumb.Height / 3));
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (Target == null || MaxScroll <= 0) return;
            int delta = e.Delta > 0 ? -24 : 24;
            SetScroll(CurrentScroll + delta);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left || Target == null || MaxScroll <= 0) return;

            Rectangle thumb = GetThumbRectangle();
            if (thumb.Contains(e.Location))
            {
                dragging = true;
                dragOffset = e.Y - thumb.Y;
                Capture = true;
            }
            else
            {
                SetScrollFromY(e.Y);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!dragging || Target == null || MaxScroll <= 0) return;
            SetScrollFromY(e.Y - dragOffset + GetThumbRectangle().Height / 2);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            dragging = false;
            Capture = false;
        }

        private void SetScrollFromY(int y)
        {
            if (MaxScroll <= 0) return;

            Rectangle rail = GetRailRectangle();
            Rectangle thumb = GetThumbRectangle();
            int thumbHeight = thumb.IsEmpty ? 26 : thumb.Height;
            int travel = Math.Max(1, rail.Height - thumbHeight);
            int clampedY = Math.Max(rail.Y, Math.Min(rail.Bottom - thumbHeight, y - thumbHeight / 2));
            int value = (int)Math.Round(MaxScroll * ((clampedY - rail.Y) / (float)travel));
            SetScroll(value);
        }

        private void SetScroll(int value)
        {
            if (Target == null || MaxScroll <= 0) return;
            int max = MaxScroll;
            int clamped = Math.Max(0, Math.Min(max, value));
            Target.AutoScrollPosition = new Point(Math.Max(0, -Target.AutoScrollPosition.X), clamped);
            Target.Invalidate(true);
            Target.Parent?.Invalidate(Target.Bounds, true);
            Target.Update();
            Invalidate();
            Parent?.Invalidate(Bounds, true);
        }
    }


}
