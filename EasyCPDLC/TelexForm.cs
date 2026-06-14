/*  EASYCPDLC: CPDLC Client for the VATSIM Network
    Copyright (C) 2021 Joshua Seagrave joshseagrave@googlemail.com

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/



using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using FSUIPC;

namespace EasyCPDLC
{

    public partial class TelexForm : Form
    {

        public const int WM_NCLBUTTONDOWN = 0xA1;
        public const int HT_CAPTION = 0x2;
        private const int cGrip = 16;
        private const int cCaption = 32;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        private bool isReply = false;

        private readonly MainForm parent;
        private readonly Color controlBackColor;
        private readonly Color controlFrontColor;
        private readonly Font textFont;
        private readonly Font textFontBold;
        private readonly string recipient;
        public TelexForm(MainForm _parent, string _recipient = null)
        {
            InitializeComponent();
            telexFrame.AssetFileName = DcduStyleManager.AssetFile("TelexWindowFrame.png");
            ApplyTransparentScreenOverlays();
            ApplyWindowLayout();
            DcduWindowHelper.ApplyDeviceWindow(this, telexFrame, 22);
            InitialiseHotspots();
            parent = _parent;
            controlBackColor = parent.controlBackColor;
            controlFrontColor = parent.controlFrontColor;
            textFont = parent.textFont;
            textFontBold = parent.textFontBold;
            recipient = _recipient is null ? null : _recipient;
            isReply = _recipient is not null;

            this.TopMost = parent.TopMost;
            sendButton.Enabled = false;
        }



        private void ApplyWindowLayout()
        {
            bool isBoeing = DcduStyleManager.IsBoeing;
            Size targetSize = isBoeing ? new Size(800, 258) : new Size(700, 233);
            ClientSize = targetSize;
            Size = targetSize;
            MinimumSize = targetSize;
            MaximumSize = targetSize;

            telexFrame.Location = new Point(0, 0);
            telexFrame.Size = targetSize;

            if (isBoeing)
            {
                // Tuned to the current Boeing telex artwork used by the user (800x258).
                freeTextButton.Bounds = new Rectangle(37, 34, 76, 34);
                metarButton.Bounds = new Rectangle(37, 80, 76, 34);
                atisButton.Bounds = new Rectangle(37, 126, 76, 34);

                exitButton.Bounds = new Rectangle(692, 34, 76, 34);
                clearButton.Bounds = new Rectangle(692, 116, 76, 34);
                sendButton.Bounds = new Rectangle(692, 160, 76, 34);

                telexScreen.Bounds = new Rectangle(131, 17, 529, 210);
                messageFormatPanel.Bounds = new Rectangle(14, 14, 501, 182);
                messageFormatPanel.Padding = new Padding(8, 0, 0, 24);
                radioContainer.Location = new Point(37, 230);
                radioContainer.Size = new Size(100, 20);
            }
            else
            {
                freeTextButton.Bounds = new Rectangle(23, 52, 58, 28);
                metarButton.Bounds = new Rectangle(23, 90, 58, 30);
                atisButton.Bounds = new Rectangle(23, 128, 58, 29);

                exitButton.Bounds = new Rectangle(619, 51, 54, 28);
                clearButton.Bounds = new Rectangle(619, 128, 54, 28);
                sendButton.Bounds = new Rectangle(619, 165, 54, 29);

                telexScreen.Bounds = new Rectangle(104, 37, 496, 157);
                messageFormatPanel.Bounds = new Rectangle(12, 12, 470, 135);
                messageFormatPanel.Padding = new Padding(6, 0, 0, 18);
                radioContainer.Location = new Point(24, 205);
                radioContainer.Size = new Size(100, 18);
            }

            telexFrame.Invalidate();
        }

        private void InitialiseHotspots()
        {
            // The hotspot controls are NOT added to telexFrame.Controls.
            // They exist only as bounds/event containers so they cannot punch transparent holes through the bitmap.
        }

        private Control GetAssetHotspotAt(Point location)
        {
            Control[] hotspots = { freeTextButton, metarButton, atisButton, clearButton, sendButton, exitButton };
            foreach (Control hotspot in hotspots)
            {
                if (hotspot != null && hotspot.Enabled && hotspot.Bounds.Contains(location))
                {
                    return hotspot;
                }
            }
            return null;
        }

        private void AssetFrame_MouseMove(object sender, MouseEventArgs e)
        {
            Control hit = GetAssetHotspotAt(e.Location);
            telexFrame.HighlightRectangle = Rectangle.Empty;
            telexFrame.Cursor = hit == null ? Cursors.Default : Cursors.Hand;
        }

        private void AssetFrame_MouseLeave(object sender, EventArgs e)
        {
            telexFrame.HighlightPressed = false;
            telexFrame.HighlightRectangle = Rectangle.Empty;
            telexFrame.Cursor = Cursors.Default;
        }

        private void AssetFrame_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            Control hit = GetAssetHotspotAt(e.Location);
            if (hit != null)
            {
                telexFrame.HighlightRectangle = Rectangle.Empty;
                telexFrame.HighlightPressed = false;
                return;
            }
            WindowDrag(sender, e);
        }

        private void AssetFrame_MouseUp(object sender, MouseEventArgs e)
        {
            telexFrame.HighlightPressed = false;
        }

        private void AssetFrame_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            if (GetAssetHotspotAt(e.Location) is DcduHotspotButton button)
            {
                button.PerformClick();
            }
        }

        private void ApplyTransparentScreenOverlays()
        {
            // Same visual behavior as the main DCDU: do not paint a separate dark block over the bitmap screen.
            messageFormatPanel.BackColor = Color.Transparent;
            radioContainer.BackColor = Color.Transparent;
        }

        private AccessibleLabel CreateTemplate(string _text)
        {
            AccessibleLabel _temp = new(controlFrontColor)
            {
                BackColor = Color.Transparent,
                ForeColor = controlFrontColor,
                Font = textFont,
                AutoSize = true,
                Text = _text,
                Top = 10,
                Height = 20,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(0, 10, 0, 0),
                Margin = new Padding(0, 0, 0, 0),
                TabStop = true,
                TabIndex = 0
            };

            return _temp;
        }

        private UITextBox CreateTextBox(string _text, int _maxLength)
        {
            UITextBox _temp = new(controlFrontColor)
            {
                BackColor = controlBackColor,
                ForeColor = controlFrontColor,
                Font = textFontBold,
                MaxLength = _maxLength,
                BorderStyle = BorderStyle.None,
                Text = _text,
                CharacterCasing = CharacterCasing.Upper,
                Top = 10,
                PlaceholderText = new string('▯', _maxLength),
                Height = 30,
                TextAlign = HorizontalAlignment.Left,
                TabIndex = 0,
                Anchor = AnchorStyles.Left
            };

            using (Graphics G = _temp.CreateGraphics())
            {
                _temp.Width = (int)(_temp.MaxLength *
                              G.MeasureString("▯", _temp.Font).Width * 1.5);
            }

            return _temp;
        }


        private UITextBox CreateMultiLineBox(string _text)
        {
            UITextBox _temp = new(controlFrontColor)
            {
                BackColor = controlBackColor,
                ForeColor = controlFrontColor,
                Font = textFontBold,
                BorderStyle = BorderStyle.None,
                Width = Math.Max(320, messageFormatPanel.Width - 40),
                Multiline = true,
                WordWrap = true,
                ScrollBars = ScrollBars.Vertical,
                PlaceholderText = "Your message here...",
                Text = _text,
                MaxLength = 255,
                Height = 88,
                TabIndex = 0
            };
            _temp.TextChanged += ExpandMultiLineBox;

            _temp.CharacterCasing = CharacterCasing.Upper;
            _temp.Padding = new Padding(3, 0, 3, -10);
            _temp.Margin = new Padding(3, 5, 3, -10);
            _temp.TextAlign = HorizontalAlignment.Left;

            return _temp;
        }

        private void ExitButton_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void WindowDrag(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                _ = SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        }
        private void ReloadPanel(object sender, EventArgs e)
        {
            if (Properties.Settings.Default.TelexWindowLocation != new Point(0, 0))
            {
                Location = Properties.Settings.Default.TelexWindowLocation;
            }

            ApplyWindowLayout();
            DcduWindowHelper.ApplyDeviceWindow(this, telexFrame, 22);
            FreeTextButton_Click(freeTextButton, EventArgs.Empty);
        }

        private void ResetPanel(object sender, EventArgs e)
        {
            FreeTextButton_Click(freeTextButton, EventArgs.Empty);
        }

        private void ExpandMultiLineBox(object sender, EventArgs e)
        {
            TextBox _sender = (TextBox)sender;
            // amount of padding to add
            const int padding = 3;
            // get number of lines (first line is 0, so add 1)
            int numLines = _sender.GetLineFromCharIndex(_sender.TextLength) + 1;
            // get border thickness
            int border = _sender.Height - _sender.ClientSize.Height;
            // set height (height of one line * number of lines + spacing)
            _sender.Height = _sender.Font.Height * numLines + padding + border;
            ScrollToBottom(messageFormatPanel);
        }

        private static void ScrollToBottom(FlowLayoutPanel p)
        {
            using Control c = new() { Parent = p, Dock = DockStyle.Bottom };
            p.ScrollControlIntoView(c);
            c.Parent = null;
        }
        private AccessibleLabel CreateHeader(string text)
        {
            AccessibleLabel label = CreateTemplate(text);
            label.Font = textFontBold;
            label.ForeColor = Color.FromArgb(120, 220, 255);
            label.Padding = new Padding(0, 2, 0, 0);
            label.Margin = new Padding(0, 0, 0, 8);
            label.AutoSize = true;
            return label;
        }

        private string GetSuggestedStation()
        {
            try
            {
                if (parent.fsuipc.groundspeed < 100)
                {
                    return parent.userVATSIMData.flight_plan.departure?.Trim().ToUpperInvariant() ?? string.Empty;
                }

                return parent.userVATSIMData.flight_plan.arrival?.Trim().ToUpperInvariant() ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static IEnumerable<TextBox> GetTextBoxesRecursive(Control root)
        {
            foreach (Control control in root.Controls)
            {
                if (control is TextBox tb)
                {
                    yield return tb;
                }

                foreach (TextBox nested in GetTextBoxesRecursive(control))
                {
                    yield return nested;
                }
            }
        }


        private static IEnumerable<ComboBox> GetComboBoxesRecursive(Control root)
        {
            foreach (Control control in root.Controls)
            {
                if (control is ComboBox combo)
                {
                    yield return combo;
                }

                foreach (ComboBox nested in GetComboBoxesRecursive(control))
                {
                    yield return nested;
                }
            }
        }

        private string GetSelectedAtisRequestIdentifier(string station)
        {
            string normalizedStation = (station ?? string.Empty).Trim().ToUpperInvariant();

            ComboBox combo = GetComboBoxesRecursive(messageFormatPanel)
                .FirstOrDefault(x => x.Name == "atisTypeComboBox");

            string selectedType = combo?.SelectedItem?.ToString()?.Trim().ToUpperInvariant() ?? "NONE";

            return selectedType switch
            {
                "ARRIVAL" => normalizedStation + "_A",
                "DEPARTURE" => normalizedStation + "_D",
                _ => normalizedStation
            };
        }


        private Color AccentColor()
        {
            return DcduStyleManager.IsBoeing
                ? Color.FromArgb(86, 255, 103)
                : Color.FromArgb(45, 231, 245);
        }

        private Color AccentTitleColor()
        {
            return DcduStyleManager.IsBoeing
                ? Color.FromArgb(178, 255, 188)
                : Color.FromArgb(118, 220, 255);
        }

        private Color AccentMutedColor()
        {
            return DcduStyleManager.IsBoeing
                ? Color.FromArgb(90, 116, 92)
                : Color.FromArgb(78, 102, 120);
        }

        private Color AccentLabelColor()
        {
            return DcduStyleManager.IsBoeing
                ? Color.FromArgb(220, 238, 222)
                : Color.FromArgb(210, 222, 232);
        }

        private Control CreateHeroBanner(string title, string subtitle, bool isAtis)
        {
            int width = Math.Max(320, messageFormatPanel.ClientSize.Width - 24);
            int height = DcduStyleManager.IsBoeing ? 40 : 38;

            Panel panel = new()
            {
                Width = width,
                Height = height,
                Margin = new Padding(0, 0, 0, 7),
                BackColor = Color.Transparent
            };

            panel.Paint += (_, e) => DrawHeroBanner(e.Graphics, panel.ClientRectangle, title, subtitle, isAtis);
            return panel;
        }

        private void DrawHeroBanner(Graphics g, Rectangle bounds, string title, string subtitle, bool isAtis)
        {
            Color accent = AccentColor();

            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            Rectangle r = new Rectangle(0, 0, bounds.Width - 1, bounds.Height - 1);

            Rectangle smallIconRect = new Rectangle(6, 6, 20, 18);
            if (isAtis)
            {
                DrawAtisIcon(g, smallIconRect, Color.FromArgb(165, accent), 1.35f);
            }
            else
            {
                DrawWeatherIcon(g, smallIconRect, Color.FromArgb(165, accent), 1.35f);
            }

            using Font titleFont = new Font(textFontBold.FontFamily, Math.Max(9.0f, textFontBold.Size - 0.8f), FontStyle.Bold);
            using Font subtitleFont = new Font(textFont.FontFamily, Math.Max(7.8f, textFont.Size - 1.6f), FontStyle.Regular);

            TextRenderer.DrawText(
                g,
                title,
                titleFont,
                new Rectangle(33, 2, r.Width - 42, 18),
                AccentTitleColor(),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);

            TextRenderer.DrawText(
                g,
                subtitle,
                subtitleFont,
                new Rectangle(33, 20, r.Width - 42, 14),
                AccentMutedColor(),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);

            using Pen separator = new Pen(Color.FromArgb(24, accent), 1.0f);
            g.DrawLine(separator, 33, r.Height - 4, Math.Max(70, r.Width - 44), r.Height - 4);
        }

        private Control CreateStationRow(string initialValue, string hintText)
        {
            int width = Math.Max(320, messageFormatPanel.ClientSize.Width - 24);

            Panel row = new()
            {
                Width = width,
                Height = 54,
                Margin = new Padding(0, 0, 0, 4),
                BackColor = Color.Transparent
            };

            AccessibleLabel caption = CreateTemplate("STATION:");
            caption.Location = new Point(0, 2);
            caption.Margin = new Padding(0);
            caption.Padding = new Padding(0);
            caption.AutoSize = false;
            caption.Width = DcduStyleManager.IsBoeing ? 90 : 82;
            caption.Height = 18;
            caption.ForeColor = AccentLabelColor();
            row.Controls.Add(caption);

            Panel fieldPanel = new()
            {
                Location = new Point(caption.Right + 8, 0),
                Size = new Size(
                    DcduStyleManager.IsBoeing ? 126 : 118,
                    DcduStyleManager.IsBoeing ? 31 : 30),
                BackColor = Color.FromArgb(4, 10, 18),
                Margin = new Padding(0)
            };
            fieldPanel.Paint += (_, e) => DrawStationFieldChrome(e.Graphics, fieldPanel.ClientRectangle);
            row.Controls.Add(fieldPanel);

            UITextBox stationBox = CreateTextBox(initialValue, 4);
            stationBox.Parent = fieldPanel;
            stationBox.Location = new Point(10, 6);
            stationBox.Size = new Size(84, 18);
            stationBox.Margin = new Padding(0);
            stationBox.Padding = new Padding(0);
            stationBox.BackColor = fieldPanel.BackColor;
            stationBox.BorderStyle = BorderStyle.None;
            stationBox.PlaceholderText = "ICAO";
            stationBox.TextAlign = HorizontalAlignment.Left;
            stationBox.BringToFront();

            Label hint = new()
            {
                AutoSize = true,
                BackColor = Color.Transparent,
                ForeColor = AccentMutedColor(),
                Font = new Font(textFont.FontFamily, Math.Max(8.0f, textFont.Size - 1.2f), FontStyle.Regular),
                Text = hintText,
                Location = new Point(0, 33),
                Margin = new Padding(0)
            };
            row.Controls.Add(hint);

            return row;
        }


        private Control CreateAtisStationRow(string initialValue)
        {
            int width = Math.Max(320, messageFormatPanel.ClientSize.Width - 24);

            Panel row = new()
            {
                Width = width,
                Height = 56,
                Margin = new Padding(0, 0, 0, 4),
                BackColor = Color.Transparent
            };

            AccessibleLabel stationCaption = CreateTemplate("STATION:");
            stationCaption.Location = new Point(0, 2);
            stationCaption.Margin = new Padding(0);
            stationCaption.Padding = new Padding(0);
            stationCaption.AutoSize = false;
            stationCaption.Width = DcduStyleManager.IsBoeing ? 90 : 82;
            stationCaption.Height = 18;
            stationCaption.ForeColor = AccentLabelColor();
            row.Controls.Add(stationCaption);

            Panel fieldPanel = new()
            {
                Location = new Point(stationCaption.Right + 8, 0),
                Size = new Size(
                    DcduStyleManager.IsBoeing ? 126 : 118,
                    DcduStyleManager.IsBoeing ? 31 : 30),
                BackColor = Color.FromArgb(4, 10, 18),
                Margin = new Padding(0)
            };
            fieldPanel.Paint += (_, e) => DrawStationFieldChrome(e.Graphics, fieldPanel.ClientRectangle);
            row.Controls.Add(fieldPanel);

            UITextBox stationBox = CreateTextBox(initialValue, 4);
            stationBox.Parent = fieldPanel;
            stationBox.Location = new Point(10, 6);
            stationBox.Size = new Size(84, 18);
            stationBox.Margin = new Padding(0);
            stationBox.Padding = new Padding(0);
            stationBox.BackColor = fieldPanel.BackColor;
            stationBox.BorderStyle = BorderStyle.None;
            stationBox.PlaceholderText = "ICAO";
            stationBox.TextAlign = HorizontalAlignment.Left;
            stationBox.BringToFront();

            int typeLabelX = fieldPanel.Right + (DcduStyleManager.IsBoeing ? 22 : 16);

            Label typeLabel = new()
            {
                AutoSize = false,
                BackColor = Color.Transparent,
                ForeColor = AccentLabelColor(),
                Font = textFont,
                Text = "TYPE:",
                Location = new Point(typeLabelX, 2),
                Size = new Size(48, 22),
                TextAlign = ContentAlignment.MiddleLeft
            };
            row.Controls.Add(typeLabel);

            ComboBox atisTypeCombo = new()
            {
                Name = "atisTypeComboBox",
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Popup,
                BackColor = Color.FromArgb(4, 10, 18),
                ForeColor = AccentTitleColor(),
                Font = new Font(textFontBold.FontFamily, Math.Max(8.2f, textFontBold.Size - 1.4f), FontStyle.Bold),
                Location = new Point(typeLabel.Right + 6, 0),
                Size = new Size(DcduStyleManager.IsBoeing ? 132 : 126, DcduStyleManager.IsBoeing ? 30 : 28)
            };
            atisTypeCombo.Items.Add("NONE");
            atisTypeCombo.Items.Add("ARRIVAL");
            atisTypeCombo.Items.Add("DEPARTURE");
            atisTypeCombo.SelectedItem = "NONE";
            row.Controls.Add(atisTypeCombo);

            Label hint = new()
            {
                AutoSize = true,
                BackColor = Color.Transparent,
                ForeColor = AccentMutedColor(),
                Font = new Font(textFont.FontFamily, Math.Max(8.0f, textFont.Size - 1.2f), FontStyle.Regular),
                Text = "NONE = ICAO   ARR = ICAO_A   DEP = ICAO_D",
                Location = new Point(0, 34),
                Margin = new Padding(0)
            };
            row.Controls.Add(hint);

            return row;
        }

        private void DrawStationFieldChrome(Graphics g, Rectangle bounds)
        {
            Color accent = AccentColor();

            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, bounds.Width - 1, bounds.Height - 1);

            using GraphicsPath path = DcduPanel.RoundedRect(r, 6);
            using LinearGradientBrush fill = new(r,
                Color.FromArgb(4, 10, 18),
                Color.FromArgb(2, 6, 12),
                LinearGradientMode.Vertical);
            using Pen border = new(Color.FromArgb(DcduStyleManager.IsBoeing ? 74 : 62, accent), 1.0f);

            g.FillPath(fill, path);
            g.DrawPath(border, path);
        }

        private static void DrawWeatherIcon(Graphics g, Rectangle rect, Color color, float strokeWidth)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using Pen pen = new(color, strokeWidth)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round
            };

            int x = rect.X;
            int y = rect.Y;
            int w = rect.Width;
            int h = rect.Height;

            g.DrawArc(pen, x + (int)(0.10f * w), y + (int)(0.34f * h), (int)(0.28f * w), (int)(0.30f * h), 180, 180);
            g.DrawArc(pen, x + (int)(0.28f * w), y + (int)(0.16f * h), (int)(0.28f * w), (int)(0.34f * h), 180, 180);
            g.DrawArc(pen, x + (int)(0.46f * w), y + (int)(0.28f * h), (int)(0.24f * w), (int)(0.24f * h), 180, 180);
            g.DrawLine(pen, x + (int)(0.18f * w), y + (int)(0.64f * h), x + (int)(0.62f * w), y + (int)(0.64f * h));

            g.DrawLine(pen, x + (int)(0.24f * w), y + (int)(0.72f * h), x + (int)(0.20f * w), y + (int)(0.92f * h));
            g.DrawLine(pen, x + (int)(0.40f * w), y + (int)(0.72f * h), x + (int)(0.36f * w), y + (int)(0.92f * h));
            g.DrawLine(pen, x + (int)(0.56f * w), y + (int)(0.72f * h), x + (int)(0.52f * w), y + (int)(0.92f * h));

            g.DrawArc(pen, x + (int)(0.64f * w), y + (int)(0.18f * h), (int)(0.26f * w), (int)(0.26f * h), 290, 115);
        }

        private static void DrawAtisIcon(Graphics g, Rectangle rect, Color color, float strokeWidth)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using Pen pen = new(color, strokeWidth)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round
            };
            using SolidBrush brush = new(color);

            int x = rect.X;
            int y = rect.Y;
            int w = rect.Width;
            int h = rect.Height;

            Point[] tower =
            {
                new Point(x + (int)(0.42f * w), y + (int)(0.20f * h)),
                new Point(x + (int)(0.58f * w), y + (int)(0.20f * h)),
                new Point(x + (int)(0.66f * w), y + (int)(0.76f * h)),
                new Point(x + (int)(0.34f * w), y + (int)(0.76f * h))
            };

            g.DrawPolygon(pen, tower);
            g.FillEllipse(brush, x + (int)(0.47f * w), y + (int)(0.10f * h), Math.Max(3, (int)(0.06f * w)), Math.Max(3, (int)(0.06f * h)));
            g.DrawLine(pen, x + (int)(0.50f * w), y + (int)(0.16f * h), x + (int)(0.50f * w), y + (int)(0.08f * h));
            g.DrawLine(pen, x + (int)(0.34f * w), y + (int)(0.76f * h), x + (int)(0.66f * w), y + (int)(0.76f * h));

            g.DrawArc(pen, x + (int)(0.58f * w), y + (int)(0.08f * h), (int)(0.22f * w), (int)(0.22f * h), 300, 120);
            g.DrawArc(pen, x + (int)(0.66f * w), y + (int)(0.00f * h), (int)(0.28f * w), (int)(0.28f * h), 300, 120);
        }
        private string GetFirstTextBoxText()
        {
            TextBox box = GetTextBoxesRecursive(messageFormatPanel).FirstOrDefault();
            return box == null ? string.Empty : box.Text.Trim().ToUpperInvariant();
        }

        private void MessageInputChanged(object sender, EventArgs e)
        {
            UpdateSendButtonState();
        }
        private void FinalizeMessagePanel()
        {
            foreach (TextBox box in GetTextBoxesRecursive(messageFormatPanel))
            {
                box.TextChanged -= MessageInputChanged;
                box.TextChanged += MessageInputChanged;
            }

            UpdateSendButtonState();
        }

        private void UpdateSendButtonState()
        {
            sendButton.Enabled = IsTelexSendReady();
        }
        private bool IsTelexSendReady()
        {
            RadioButton radioBtn = radioContainer.Controls.OfType<RadioButton>()
                                       .FirstOrDefault(x => x.Checked);

            if (radioBtn == null)
            {
                return false;
            }

            TextBox[] boxes = GetTextBoxesRecursive(messageFormatPanel).ToArray();

            if (boxes.Length == 0)
            {
                return false;
            }

            string stationOrRecipient = boxes[0].Text.Trim();

            if (stationOrRecipient.Length < 4)
            {
                return false;
            }

            if (radioBtn.Name == "freeTextRadioButton")
            {
                TextBox messageBox = boxes.FirstOrDefault(tb => tb.Multiline);
                return messageBox != null && !string.IsNullOrWhiteSpace(messageBox.Text);
            }

            return true;
        }

        private void SendButton_Click(object sender, EventArgs e)
        {
            if (!IsTelexSendReady())
            {
                return;
            }

            RadioButton radioBtn = radioContainer.Controls.OfType<RadioButton>()
                                       .Where(x => x.Checked).FirstOrDefault();

            if (radioBtn == null)
            {
                return;
            }

            string recipientText = GetFirstTextBoxText();

            switch (radioBtn.Name)
            {
                case "freeTextRadioButton":
                    string formatMessage = GetTextBoxesRecursive(messageFormatPanel)
                        .Where(tb => tb.Multiline)
                        .Select(tb => tb.Text.Trim())
                        .FirstOrDefault();

                    if (string.IsNullOrWhiteSpace(formatMessage))
                    {
                        return;
                    }

                    if (!this.parent.TryReserveFreeTextSlot(out TimeSpan remaining))
                    {
                        this.parent.NotifyFreeTextCooldown(remaining);
                        return;
                    }

                    _ = Task.Run(() => this.parent.SendCPDLCMessage(recipientText, "TELEX", formatMessage.Trim()));
                    break;

                case "metarRadioButton":
                    this.parent.WriteMessage("METAR REQUEST", "METAR", recipientText, true);
                    this.parent.ArtificialDelay("METAR " + recipientText, "INFOREQ", "REQUEST");
                    break;

                case "atisRadioButton":
                    string atisRequestIdentifier = GetSelectedAtisRequestIdentifier(recipientText);
                    this.parent.WriteMessage("ATIS REQUEST", "ATIS", atisRequestIdentifier, true);
                    this.parent.ArtificialDelay("VATATIS " + atisRequestIdentifier, "INFOREQ", "REQUEST");
                    break;

                default:
                    return;
            }

            if (isReply)
            {
                parent.ClearPreview();
            }

            this.Close();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x84)
            {  // Trap WM_NCHITTEST
                Point pos = new(m.LParam.ToInt32());
                pos = this.PointToClient(pos);
                if (pos.Y < cCaption)
                {
                    m.Result = (IntPtr)2;  // HTCAPTION
                    return;
                }
                if (pos.X >= this.ClientSize.Width - cGrip && pos.Y >= this.ClientSize.Height - cGrip)
                {
                    m.Result = (IntPtr)17; // HTBOTTOMRIGHT
                    return;
                }
            }
            base.WndProc(ref m);
        }


        private Control CreateFreeTextComposer(string initialRecipient)
        {
            int width = Math.Max(320, messageFormatPanel.ClientSize.Width - 24);
            int height = Math.Max(
                DcduStyleManager.IsBoeing ? 164 : 128,
                messageFormatPanel.ClientSize.Height - (DcduStyleManager.IsBoeing ? 6 : 4));

            Panel panel = new()
            {
                Width = width,
                Height = height,
                Margin = new Padding(0, 0, 0, 0),
                BackColor = Color.Transparent
            };

            panel.Paint += (_, e) => DrawFreeTextComposerChrome(e.Graphics, panel.ClientRectangle);

            Rectangle iconRect = new Rectangle(5, 5, 20, 18);
            panel.Paint += (_, e) => DrawFreeTextIcon(e.Graphics, iconRect, Color.FromArgb(165, AccentColor()), 1.5f);

            Label title = new()
            {
                AutoSize = false,
                BackColor = Color.Transparent,
                ForeColor = AccentTitleColor(),
                Font = new Font(textFontBold.FontFamily, textFontBold.Size, FontStyle.Bold),
                Text = "TELEX  FREE TEXT",
                Location = new Point(32, 3),
                Size = new Size(width - 42, 21),
                TextAlign = ContentAlignment.MiddleLeft
            };
            panel.Controls.Add(title);

            Label subtitle = new()
            {
                AutoSize = false,
                BackColor = Color.Transparent,
                ForeColor = AccentMutedColor(),
                Font = new Font(textFont.FontFamily, Math.Max(8.0f, textFont.Size - 1.4f), FontStyle.Regular),
                Text = "Send a custom message via Hoppie",
                Location = new Point(32, 21),
                Size = new Size(width - 42, 16),
                TextAlign = ContentAlignment.MiddleLeft
            };
            panel.Controls.Add(subtitle);

            Label recipientLabel = new()
            {
                AutoSize = false,
                BackColor = Color.Transparent,
                ForeColor = AccentLabelColor(),
                Font = textFont,
                Text = "TO:",
                Location = new Point(0, 43),
                Size = new Size(38, 22),
                TextAlign = ContentAlignment.MiddleLeft
            };
            panel.Controls.Add(recipientLabel);

            Panel recipientField = new()
            {
                Location = new Point(42, 40),
                Size = new Size(DcduStyleManager.IsBoeing ? 150 : 138, 28),
                BackColor = Color.FromArgb(4, 10, 18)
            };
            recipientField.Paint += (_, e) => DrawStationFieldChrome(e.Graphics, recipientField.ClientRectangle);
            panel.Controls.Add(recipientField);

            UITextBox recipientBox = CreateTextBox(initialRecipient ?? string.Empty, 7);
            recipientBox.Parent = recipientField;
            recipientBox.Location = new Point(10, 5);
            recipientBox.Size = new Size(recipientField.Width - 18, 18);
            recipientBox.Margin = new Padding(0);
            recipientBox.Padding = new Padding(0);
            recipientBox.BackColor = recipientField.BackColor;
            recipientBox.BorderStyle = BorderStyle.None;
            recipientBox.PlaceholderText = "ATC/CALL";
            recipientBox.TextAlign = HorizontalAlignment.Left;
            recipientBox.BringToFront();

            Label messageLabel = new()
            {
                AutoSize = false,
                BackColor = Color.Transparent,
                ForeColor = AccentLabelColor(),
                Font = textFont,
                Text = "MSG:",
                Location = new Point(0, 72),
                Size = new Size(52, 18),
                TextAlign = ContentAlignment.MiddleLeft
            };
            panel.Controls.Add(messageLabel);

            Label hint = new()
            {
                AutoSize = false,
                BackColor = Color.Transparent,
                ForeColor = AccentMutedColor(),
                Font = new Font(textFont.FontFamily, Math.Max(7.5f, textFont.Size - 1.8f), FontStyle.Regular),
                Text = "MAX 255 CHARS",
                Location = new Point(width - 130, 72),
                Size = new Size(122, 18),
                TextAlign = ContentAlignment.MiddleRight
            };
            panel.Controls.Add(hint);

            Panel messageField = new()
            {
                Location = new Point(0, 88),
                Size = new Size(width - 8, Math.Max(34, height - 88)),
                BackColor = Color.FromArgb(3, 8, 15)
            };
            messageField.Paint += (_, e) => DrawMessageFieldChrome(e.Graphics, messageField.ClientRectangle);
            panel.Controls.Add(messageField);

            UITextBox messageBox = new(controlFrontColor)
            {
                Parent = messageField,
                Location = new Point(10, 7),
                Size = new Size(messageField.Width - 22, Math.Max(20, messageField.Height - 14)),
                BackColor = messageField.BackColor,
                ForeColor = controlFrontColor,
                Font = textFontBold,
                BorderStyle = BorderStyle.None,
                Multiline = true,
                WordWrap = true,
                ScrollBars = ScrollBars.Vertical,
                PlaceholderText = "Your message here...",
                MaxLength = 255,
                CharacterCasing = CharacterCasing.Upper,
                TextAlign = HorizontalAlignment.Left,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            messageBox.BringToFront();

            return panel;
        }

        private void DrawFreeTextComposerChrome(Graphics g, Rectangle bounds)
        {
            Color accent = AccentColor();

            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle r = new Rectangle(0, 0, bounds.Width - 1, bounds.Height - 1);

            using Pen topLine = new(Color.FromArgb(24, accent), 1.0f);
            using Pen divider = new(Color.FromArgb(18, accent), 1.0f);

            g.DrawLine(topLine, 32, 36, Math.Min(r.Width - 12, 330), 36);
            g.DrawLine(divider, 0, 68, Math.Min(r.Width - 12, 245), 68);
        }

        private void DrawMessageFieldChrome(Graphics g, Rectangle bounds)
        {
            Color accent = AccentColor();

            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, bounds.Width - 1, bounds.Height - 1);

            using GraphicsPath path = DcduPanel.RoundedRect(r, 6);
            using LinearGradientBrush fill = new(r,
                Color.FromArgb(3, 8, 15),
                Color.FromArgb(1, 5, 10),
                LinearGradientMode.Vertical);
            using Pen border = new(Color.FromArgb(DcduStyleManager.IsBoeing ? 50 : 42, accent), 1.0f);

            g.FillPath(fill, path);
            g.DrawPath(border, path);
        }

        private static void DrawFreeTextIcon(Graphics g, Rectangle rect, Color color, float strokeWidth)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using Pen pen = new(color, strokeWidth)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round
            };

            Rectangle envelope = new Rectangle(rect.X, rect.Y + 3, rect.Width, rect.Height - 5);
            g.DrawRectangle(pen, envelope);
            g.DrawLine(pen, envelope.Left, envelope.Top, envelope.Left + envelope.Width / 2, envelope.Top + envelope.Height / 2);
            g.DrawLine(pen, envelope.Right, envelope.Top, envelope.Left + envelope.Width / 2, envelope.Top + envelope.Height / 2);
            g.DrawLine(pen, envelope.Left, envelope.Bottom, envelope.Left + envelope.Width / 2, envelope.Top + envelope.Height / 2);
            g.DrawLine(pen, envelope.Right, envelope.Bottom, envelope.Left + envelope.Width / 2, envelope.Top + envelope.Height / 2);
        }


        private void FreeTextButton_Click(object sender, EventArgs e)
        {
            messageFormatPanel.Controls.Clear();

            Control composer = CreateFreeTextComposer(recipient is null ? string.Empty : recipient);
            messageFormatPanel.Controls.Add(composer);
            messageFormatPanel.SetFlowBreak(composer, true);

            freeTextRadioButton.Checked = true;
            FinalizeMessagePanel();
        }
        private void MetarButton_Click(object sender, EventArgs e)
        {
            messageFormatPanel.Controls.Clear();

            Control hero = CreateHeroBanner(
                "WX  METAR",
                "Latest airport weather report",
                false);

            messageFormatPanel.Controls.Add(hero);
            messageFormatPanel.SetFlowBreak(hero, true);

            Control stationRow = CreateStationRow(
                GetSuggestedStation(),
                "ENTER ICAO OF DEP / ARR AERODROME");

            messageFormatPanel.Controls.Add(stationRow);
            messageFormatPanel.SetFlowBreak(stationRow, true);

            metarRadioButton.Checked = true;
            FinalizeMessagePanel();
        }
        private void AtisButton_Click(object sender, EventArgs e)
        {
            messageFormatPanel.Controls.Clear();

            Control hero = CreateHeroBanner(
                "WX  ATIS",
                "Airport information broadcast",
                true);

            messageFormatPanel.Controls.Add(hero);
            messageFormatPanel.SetFlowBreak(hero, true);

            Control stationRow = CreateAtisStationRow(GetSuggestedStation());

            messageFormatPanel.Controls.Add(stationRow);
            messageFormatPanel.SetFlowBreak(stationRow, true);

            atisRadioButton.Checked = true;
            FinalizeMessagePanel();
        }

        private void TelexForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            Properties.Settings.Default.TelexWindowLocation = Location;
            Properties.Settings.Default.TelexWindowSize = Size;
            Properties.Settings.Default.Save();
        }
    }
}
