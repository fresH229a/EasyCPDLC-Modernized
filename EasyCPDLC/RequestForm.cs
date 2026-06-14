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
using System.Linq;
using System.Net.Http;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace EasyCPDLC
{
    public partial class RequestForm : Form
    {
        
        private static readonly HttpClient HoppieOnlineClient = new HttpClient() { Timeout = TimeSpan.FromSeconds(5) };
public const int WM_NCLBUTTONDOWN = 0xA1;
        public const int HT_CAPTION = 0x2;
        private const int cGrip = 16;
        private const int cCaption = 32;

        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_APPWINDOW = 0x00040000;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int ScrollBarBoth = 3;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ReleaseCapture();
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ShowScrollBar(IntPtr hWnd, int wBar, bool bShow);

        private bool requestPanelScrollHooksInstalled;
        private readonly ContextMenuStrip popupMenu = new();
        private ToolStripMenuItem directRequestMenu;
        private ToolStripMenuItem levelRequestMenu;
        private ToolStripMenuItem speedRequestMenu;
        private ToolStripMenuItem whenCanWeRequestMenu;

        private readonly ContextMenuStrip clxMenu = new();
        private ToolStripMenuItem depClxMenu;
        private ToolStripMenuItem ocnClxMenu;

        UITextBox fix1;
        UITextBox fix2;
        UITextBox fix3;

        private readonly MainForm MainForm;
        private readonly Pilot userVATSIMData;
        private readonly Color controlBackColor;
        private readonly Color controlFrontColor;

        private readonly Dictionary<string, string> rsnConversion = new();

        private readonly Font controlFontBold;
        private readonly Font textFont;
        private readonly Font textFontBold;

        private bool _needsLogon;

        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public bool NeedsLogon
        {
            get
            {
                return this._needsLogon;
            }

            set
            {
                this._needsLogon = value;

                if (this._needsLogon)
                {
                    logonButton.Text = "LOGON";
                    requestButton.Enabled = false;
                    reportButton.Enabled = false;
                }
                else
                {
                    logonButton.Text = "LOGOFF";
                    requestButton.Enabled = true;
                    reportButton.Enabled = true;
                }
            }
        }

        public RequestForm(MainForm parent)
        {
            InitializeComponent();
            this.ShowInTaskbar = false;
            requestFrame.AssetFileName = DcduStyleManager.AssetFile("RequestWindowFrame.png");
            ApplyTransparentScreenOverlays();
            ApplyWindowLayout();
            DcduWindowHelper.ApplyDeviceWindow(this, requestFrame, 22);
            KeepRequestPanelScrollClean();
            InitialiseHotspots();
            this.MainForm = parent;
            this.TopMost = parent.TopMost;
            if (this.MainForm.CurrentATCUnit != null)
            {
                NeedsLogon = false;
            }
            else
            {
                NeedsLogon = true;
            }

            userVATSIMData = parent.userVATSIMData;
            controlBackColor = parent.controlBackColor;
            controlFrontColor = parent.controlFrontColor;
            controlFontBold = parent.controlFontBold ?? new Font("Consolas", 12.5F, FontStyle.Bold);
            textFont = parent.textFont;
            textFontBold = parent.textFontBold;

            rsnConversion.Add("DUE TO WX", "DUE TO WEATHER");
            rsnConversion.Add("DUE TO A/C PERFORMANCE", "DUE TO PERFORMANCE");

            InitialisePopupMenu();

        }



        private static Rectangle ScaleRect(Rectangle source, Size fromSize, Size toSize)
        {
            int x = (int)Math.Round(source.X * (toSize.Width / (double)fromSize.Width));
            int y = (int)Math.Round(source.Y * (toSize.Height / (double)fromSize.Height));
            int width = (int)Math.Round(source.Width * (toSize.Width / (double)fromSize.Width));
            int height = (int)Math.Round(source.Height * (toSize.Height / (double)fromSize.Height));
            return new Rectangle(x, y, width, height);
        }

        private void ApplyWindowLayout()
        {
            bool isBoeing = DcduStyleManager.IsBoeing;
            Size targetSize = isBoeing ? new Size(800, 252) : new Size(750, 250);
            ClientSize = targetSize;
            Size = targetSize;
            MinimumSize = targetSize;
            MaximumSize = targetSize;

            requestFrame.Location = new Point(0, 0);
            requestFrame.Size = targetSize;

            if (isBoeing)
            {
                // Scale the Boeing request/ATC hitboxes and screen layout to the correct 800x252 form size.
                Size baseSize = new Size(1120, 353);
                pdcButton.Bounds = ScaleRect(new Rectangle(61, 69, 104, 48), baseSize, targetSize);
                logonButton.Bounds = ScaleRect(new Rectangle(61, 129, 104, 48), baseSize, targetSize);
                requestButton.Bounds = ScaleRect(new Rectangle(61, 189, 104, 48), baseSize, targetSize);
                reportButton.Bounds = ScaleRect(new Rectangle(61, 249, 104, 48), baseSize, targetSize);
                exitButton.Bounds = ScaleRect(new Rectangle(960, 69, 98, 48), baseSize, targetSize);
                clearButton.Bounds = ScaleRect(new Rectangle(960, 189, 98, 48), baseSize, targetSize);
                sendButton.Bounds = ScaleRect(new Rectangle(960, 249, 98, 48), baseSize, targetSize);

                requestScreen.Bounds = new Rectangle(92, 24, 596, 186);
                messageFormatPanel.Bounds = new Rectangle(4, 4, requestScreen.Width - 8, requestScreen.Height - 8);
                messageFormatPanel.Padding = new Padding(2, 0, 2, 0);
                ConfigureRequestMessageGrid();
                radioContainer.Location = new Point(44, 228);
                radioContainer.Size = new Size(110, 20);
                requestContainer.Location = new Point(684, 228);
                requestContainer.Size = new Size(110, 20);
            }
            else
            {
                pdcButton.Bounds = new Rectangle(17, 50, 84, 32);
                logonButton.Bounds = new Rectangle(17, 88, 84, 32);
                requestButton.Bounds = new Rectangle(17, 125, 84, 32);
                reportButton.Bounds = new Rectangle(17, 162, 84, 32);
                exitButton.Bounds = new Rectangle(646, 48, 81, 31);
                clearButton.Bounds = new Rectangle(647, 127, 80, 32);
                sendButton.Bounds = new Rectangle(646, 164, 81, 31);
                requestScreen.Bounds = new Rectangle(78, 24, 582, 188);
                messageFormatPanel.Bounds = new Rectangle(4, 4, requestScreen.Width - 8, requestScreen.Height - 8);
                messageFormatPanel.Padding = new Padding(2, 0, 2, 0);
                ConfigureRequestMessageGrid();
                radioContainer.Location = new Point(18, 224);
                radioContainer.Size = new Size(105, 18);
                requestContainer.Location = new Point(628, 224);
                requestContainer.Size = new Size(105, 18);
            }

            messageFormatPanel.AutoScroll = false;
            KeepRequestPanelScrollClean();
            sendButton.Enabled = false;
            requestFrame.Invalidate();
        }

        private void InitialiseHotspots()
        {
            // The hotspot controls are NOT added to requestFrame.Controls.
            // They exist only as bounds/event containers so they cannot punch transparent holes through the bitmap.
        }

        private Control GetAssetHotspotAt(Point location)
        {
            Control[] hotspots = { pdcButton, logonButton, requestButton, reportButton, clearButton, sendButton, exitButton };
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
            requestFrame.HighlightRectangle = Rectangle.Empty;
            requestFrame.Cursor = hit == null ? Cursors.Default : Cursors.Hand;
        }

        private void AssetFrame_MouseLeave(object sender, EventArgs e)
        {
            requestFrame.HighlightPressed = false;
            requestFrame.HighlightRectangle = Rectangle.Empty;
            requestFrame.Cursor = Cursors.Default;
        }

        private void AssetFrame_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            Control hit = GetAssetHotspotAt(e.Location);
            if (hit != null)
            {
                requestFrame.HighlightRectangle = Rectangle.Empty;
                requestFrame.HighlightPressed = false;
                return;
            }
            WindowDrag(sender, e);
        }

        private void AssetFrame_MouseUp(object sender, MouseEventArgs e)
        {
            requestFrame.HighlightPressed = false;
        }

        private void AssetFrame_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            if (GetAssetHotspotAt(e.Location) is DcduHotspotButton button)
            {
                button.PerformClick();
            }
        }

        private void ConfigureRequestMessageGrid()
        {
            if (messageFormatPanel == null)
            {
                return;
            }

            messageFormatPanel.SuspendLayout();

            messageFormatPanel.ColumnStyles.Clear();
            messageFormatPanel.ColumnCount = 6;
            messageFormatPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 8F));
            messageFormatPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, DcduStyleManager.IsBoeing ? 88F : 82F));
            messageFormatPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44F));
            messageFormatPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, DcduStyleManager.IsBoeing ? 105F : 98F));
            messageFormatPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42F));
            messageFormatPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 8F));

            messageFormatPanel.RowStyles.Clear();
            messageFormatPanel.RowCount = 7;
            messageFormatPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));
            messageFormatPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));
            messageFormatPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));
            messageFormatPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));
            messageFormatPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));
            messageFormatPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            messageFormatPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));

            messageFormatPanel.ResumeLayout(false);
        }

        private void KeepRequestPanelScrollClean()
        {
            try
            {
                if (messageFormatPanel == null || messageFormatPanel.IsDisposed)
                {
                    return;
                }

                if (!requestPanelScrollHooksInstalled)
                {
                    requestPanelScrollHooksInstalled = true;
                    messageFormatPanel.ControlAdded += (_, __) => BeginInvoke(new Action(KeepRequestPanelScrollClean));
                    messageFormatPanel.ControlRemoved += (_, __) => BeginInvoke(new Action(KeepRequestPanelScrollClean));
                    messageFormatPanel.SizeChanged += (_, __) => BeginInvoke(new Action(KeepRequestPanelScrollClean));
                    messageFormatPanel.Layout += (_, __) =>
                    {
                        if (messageFormatPanel.IsHandleCreated)
                        {
                            ShowScrollBar(messageFormatPanel.Handle, ScrollBarBoth, false);
                        }
                    };
                }

                messageFormatPanel.HorizontalScroll.Visible = false;
                messageFormatPanel.HorizontalScroll.Enabled = false;
                messageFormatPanel.HorizontalScroll.Maximum = 0;

                // Keep vertical scrolling available for longer templates, but avoid the ugly bottom bar.
                messageFormatPanel.AutoScrollMinSize = new Size(0, 0);

                if (messageFormatPanel.IsHandleCreated)
                {
                    ShowScrollBar(messageFormatPanel.Handle, ScrollBarBoth, false);
                }
            }
            catch
            {
                // Cosmetic only.
            }
        }

        private void ApplyTransparentScreenOverlays()
        {
            // Opaque content panels avoid WinForms transparent repaint artifacts while scrolling.
            requestScreen.BackColor = Color.Transparent;
            messageFormatPanel.BackColor = Color.Transparent;
            radioContainer.BackColor = Color.Transparent;
            requestContainer.BackColor = Color.Transparent;
        }

        private ToolStripMenuItem CreateMenuItem(string name)
        {
            ToolStripMenuItem _temp = new(name)
            {
                AutoSize = false,
                BackColor = Color.FromArgb(5, 9, 15),
                ForeColor = DcduTheme.CyanWhite,
                Font = controlFontBold,
                Size = new Size(142, 34),
                Padding = new Padding(8, 0, 8, 0),
                TextAlign = ContentAlignment.MiddleLeft
            };

            return _temp;
        }

        private void StyleDcduMenu(ContextMenuStrip menu)
        {
            menu.BackColor = Color.FromArgb(5, 9, 15);
            menu.ForeColor = DcduTheme.CyanWhite;
            menu.Font = controlFontBold;
            menu.ShowImageMargin = false;
            menu.ShowCheckMargin = false;
            menu.RenderMode = ToolStripRenderMode.Professional;
            menu.Renderer = new DcduMenuRenderer();
            menu.Padding = new Padding(2);
        }

        private void ShowMenuAtHotspot(ContextMenuStrip menu, Control hotspot)
        {
            if (menu == null || hotspot == null) return;

            Size preferred = menu.GetPreferredSize(Size.Empty);
            int x = hotspot.Left;
            int y = hotspot.Bottom + 5;

            if (x + preferred.Width > requestFrame.Width)
            {
                x = Math.Max(4, hotspot.Right - preferred.Width);
            }

            if (y + preferred.Height > requestFrame.Height)
            {
                y = Math.Max(4, hotspot.Top - preferred.Height - 5);
            }

            menu.Show(requestFrame, new Point(x, y));
        }

        private void InitialisePopupMenu()
        {

            StyleDcduMenu(popupMenu);

            directRequestMenu = CreateMenuItem("DIRECT");
            directRequestMenu.Click += DirectRequestClick;
            levelRequestMenu = CreateMenuItem("LEVEL");
            levelRequestMenu.Click += LevelRequestClick;
            speedRequestMenu = CreateMenuItem("SPEED");
            speedRequestMenu.Click += SpeedRequestClick;
            whenCanWeRequestMenu = CreateMenuItem("WHEN CAN WE?");
            whenCanWeRequestMenu.Click += WhenCanWeRequestClick;

            StyleDcduMenu(clxMenu);

            depClxMenu = CreateMenuItem("DEP CLX");
            depClxMenu.Click += DepClxClick;
            ocnClxMenu = CreateMenuItem("OCN CLX");
            ocnClxMenu.Click += OcnClxClick;
        }

        private void AddRemarksField(TableLayoutPanel _control)
        {
            _control.Controls.Add(CreateTemplate("REMARKS: "), 1, 4);
            UITextBox remarksBox = CreateMultiLineBox("");
            _control.Controls.Add(remarksBox, 1, 5);
            _control.SetColumnSpan(remarksBox, 4);
            _control.SetRowSpan(remarksBox, 2);
            _control.Controls.Add(CreateBoxTemplate("[", AnchorStyles.Left), 0, 5);
            _control.Controls.Add(CreateBoxTemplate("[", AnchorStyles.Left), 0, 6);
            _control.Controls.Add(CreateBoxTemplate("]", AnchorStyles.Right), 5, 5);
            _control.Controls.Add(CreateBoxTemplate("]", AnchorStyles.Right), 5, 6);
        }

        private void DepClxClick(object sender, EventArgs e)
        {
            depClxRadioButton.Checked = true;

            messageFormatPanel.Controls.Clear();
            messageFormatPanel.Controls.Add(CreateTemplate("RECIPIENT:"), 1, 0);
            messageFormatPanel.Controls.Add(CreateTextBox("", 4), 2, 0);
            messageFormatPanel.Controls.Add(CreateTemplate("CALLSIGN: "), 1, 1);
            messageFormatPanel.Controls.Add(CreateTextBox(userVATSIMData.callsign, 7), 2, 1);
            messageFormatPanel.Controls.Add(CreateTemplate("A/C TYPE: "), 3, 1);
            messageFormatPanel.Controls.Add(CreateTextBox(userVATSIMData.flight_plan.aircraft_short, 4), 4, 1);
            messageFormatPanel.Controls.Add(CreateTemplate("DEP ARPT: "), 1, 2);
            messageFormatPanel.Controls.Add(CreateTextBox(userVATSIMData.flight_plan.departure, 4), 2, 2);
            messageFormatPanel.Controls.Add(CreateTemplate("ARR ARPT: "), 3, 2);
            messageFormatPanel.Controls.Add(CreateTextBox(userVATSIMData.flight_plan.arrival, 4), 4, 2);
            messageFormatPanel.Controls.Add(CreateTemplate("STAND: "), 1, 3);
            messageFormatPanel.Controls.Add(CreateTextBox("", 4), 2, 3);
            messageFormatPanel.Controls.Add(CreateTemplate("ATIS: "), 3, 3);
            messageFormatPanel.Controls.Add(CreateTextBox("", 1), 4, 3);

            AddRemarksField(messageFormatPanel);
            FinalizeMessagePanel();
        }
        private void OcnClxClick(object sender, EventArgs e)
        {
            ocnClxRadioButton.Checked = true;

            messageFormatPanel.Controls.Clear();
            messageFormatPanel.Controls.Add(CreateTemplate("RECIPIENT:"), 1, 0);
            messageFormatPanel.Controls.Add(CreateTextBox("", 4), 2, 0);
            messageFormatPanel.Controls.Add(CreateTemplate("CALLSIGN: "), 1, 1);
            messageFormatPanel.Controls.Add(CreateTextBox(userVATSIMData.callsign, 7), 2, 1);
            messageFormatPanel.Controls.Add(CreateTemplate("ENTRY PT: "), 3, 1);
            messageFormatPanel.Controls.Add(CreateTextBox("", 7), 4, 1);
            messageFormatPanel.Controls.Add(CreateTemplate("ETA: "), 1, 2);
            messageFormatPanel.Controls.Add(CreateTextBox("", 4), 2, 2);
            messageFormatPanel.Controls.Add(CreateTemplate("MACH: M0."), 3, 2);
            messageFormatPanel.Controls.Add(CreateTextBox("", 2), 4, 2);
            messageFormatPanel.Controls.Add(CreateTemplate("FLT LVL: "), 1, 3);
            messageFormatPanel.Controls.Add(CreateTextBox("", 3), 2, 3);

            AddRemarksField(messageFormatPanel);
            FinalizeMessagePanel();
        }

        private void DirectRequestClick(object sender, EventArgs e)
        {
            directRadioButton.Checked = true;

            messageFormatPanel.Controls.Clear();
            messageFormatPanel.Controls.Add(CreateTemplate("RECIPIENT:"), 1, 0);
            messageFormatPanel.Controls.Add(CreateTextBox(MainForm.CurrentATCUnit, 4, true), 2, 0);
            messageFormatPanel.Controls.Add(CreateTemplate("REQUEST DIRECT TO "), 1, 1);
            messageFormatPanel.Controls.Add(CreateAutoFillTextBox("", 7, MainForm.reportFixes), 2, 1);
            messageFormatPanel.Controls.Add(CreateCheckBox("DUE TO WX", "rsnParam"), 1, 2);
            messageFormatPanel.Controls.Add(CreateCheckBox("DUE TO A/C PERFORMANCE", "rsnParam"), 3, 2);

            AddRemarksField(messageFormatPanel);
            FinalizeMessagePanel();

        }

        private void LevelRequestClick(object sender, EventArgs e)
        {
            levelRadioButton.Checked = true;

            messageFormatPanel.Controls.Clear();
            messageFormatPanel.Controls.Add(CreateTemplate("RECIPIENT:"), 1, 0);
            messageFormatPanel.Controls.Add(CreateTextBox(MainForm.CurrentATCUnit, 4, true), 2, 0);
            messageFormatPanel.Controls.Add(CreateTemplate("REQUESTED FL: "), 1, 1);
            messageFormatPanel.Controls.Add(CreateTextBox("", 3, false, true), 2, 1);
            messageFormatPanel.Controls.Add(CreateCheckBox("DUE TO WX", "rsnParam"), 1, 2);
            messageFormatPanel.Controls.Add(CreateCheckBox("DUE TO A/C PERFORMANCE", "rsnParam"), 3, 2);

            AddRemarksField(messageFormatPanel);
            FinalizeMessagePanel();
        }

        private void SpeedRequestClick(object sender, EventArgs e)
        {
            speedRadioButton.Checked = true;

            messageFormatPanel.Controls.Clear();
            messageFormatPanel.Controls.Add(CreateTemplate("RECIPIENT:"), 1, 0);
            messageFormatPanel.Controls.Add(CreateTextBox(MainForm.CurrentATCUnit, 4, true), 2, 0);
            messageFormatPanel.Controls.Add(CreateTemplate("REQUEST"), 1, 1);
            messageFormatPanel.Controls.Add(CreateCheckBox("MACH: M0.", "unitParam"), 1, 2);
            messageFormatPanel.Controls.Add(CreateTextBox("", 2, false, true), 2, 2);
            messageFormatPanel.Controls.Add(CreateCheckBox("SPEED: ", "unitParam"), 3, 2);
            messageFormatPanel.Controls.Add(CreateTextBox("", 3, false, true), 4, 2);
            messageFormatPanel.Controls.Add(CreateCheckBox("DUE TO WX", "rsnParam"), 1, 3);
            messageFormatPanel.Controls.Add(CreateCheckBox("DUE TO A/C PERFORMANCE", "rsnParam"), 4, 3);

            AddRemarksField(messageFormatPanel);
            FinalizeMessagePanel();
        }

        private void WhenCanWeRequestClick(object sender, EventArgs e)
        {
            wcwRadioButton.Checked = true;

            messageFormatPanel.Controls.Clear();
            messageFormatPanel.Controls.Add(CreateTemplate("RECIPIENT:"), 1, 0);
            messageFormatPanel.Controls.Add(CreateTextBox(MainForm.CurrentATCUnit, 4, true), 2, 0);
            messageFormatPanel.Controls.Add(CreateTemplate("WHEN CAN WE EXPECT:"), 1, 1);
            messageFormatPanel.Controls.Add(CreateCheckBox("HIGHER LEVEL?", "wcwParam"), 1, 2);
            messageFormatPanel.Controls.Add(CreateCheckBox("LOWER LEVEL?", "wcwParam"), 2, 2);
            messageFormatPanel.Controls.Add(CreateCheckBox("BACK ON ROUTE?", "wcwParam"), 3, 2);
            messageFormatPanel.Controls.Add(CreateCheckBox("CLIMB TO FL: ", "wcwParam"), 1, 3);
            messageFormatPanel.Controls.Add(CreateTextBox("", 3, false, true), 2, 3);
            messageFormatPanel.Controls.Add(CreateCheckBox("DESCENT TO FL:", "wcwParam"), 1, 4);
            messageFormatPanel.Controls.Add(CreateTextBox("", 3, false, true), 2, 4);
            messageFormatPanel.Controls.Add(CreateCheckBox("MACH: M0.", "wcwParam"), 3, 3);
            messageFormatPanel.Controls.Add(CreateTextBox("", 2, false, true), 4, 3);
            messageFormatPanel.Controls.Add(CreateCheckBox("SPEED: ", "wcwParam"), 3, 4);
            messageFormatPanel.Controls.Add(CreateTextBox("", 3, false, true), 4, 4);
            FinalizeMessagePanel();
        }

        private void PdcButton_Click(object sender, EventArgs e)
        {
            clxMenu.Items.Clear();
            clxMenu.Items.Add(depClxMenu);
            clxMenu.Items.Add(ocnClxMenu);

            ShowMenuAtHotspot(clxMenu, pdcButton);
        }

        private void ReportButton_Click(object sender, EventArgs e)
        {
            fix1 = CreateAutoFillTextBox("", 7, MainForm.reportFixes);
            fix1.TextChanged += PreFill;
            fix2 = CreateTextBox("", 7);
            fix3 = CreateTextBox("", 7);

            fix1.Text = MainForm.nextFix ?? "";

            reportRadioButton.Checked = true;
            messageFormatPanel.Controls.Clear();
            messageFormatPanel.Controls.Add(CreateTemplate("RECIPIENT:"), 1, 0);
            messageFormatPanel.Controls.Add(CreateTextBox(MainForm.CurrentATCUnit, 4, true), 2, 0);
            messageFormatPanel.Controls.Add(CreateTemplate("FIX: "), 1, 1);
            messageFormatPanel.Controls.Add(fix1, 2, 1);
            messageFormatPanel.Controls.Add(CreateTemplate("AT: "), 1, 2);
            messageFormatPanel.Controls.Add(CreateTextBox(DateTime.UtcNow.ToString("HHmm"), 4), 2, 2);
            messageFormatPanel.Controls.Add(CreateTemplate("FL: "), 3, 2);
            messageFormatPanel.Controls.Add(CreateTextBox(MainForm.UseFSUIPC ? (Math.Round(MainForm.fsuipc.altitude.Feet / 1000) * 10).ToString() : userVATSIMData.flight_plan.altitude[..3], 3), 4, 2);
            messageFormatPanel.Controls.Add(CreateTemplate("NEXT: "), 1, 3);
            messageFormatPanel.Controls.Add(fix2, 2, 3);
            messageFormatPanel.Controls.Add(CreateTemplate("AT: "), 3, 3);
            messageFormatPanel.Controls.Add(CreateTextBox("", 4), 4, 3);
            messageFormatPanel.Controls.Add(CreateTemplate("THEN: "), 1, 4);
            messageFormatPanel.Controls.Add(fix3, 2, 4);
            FinalizeMessagePanel();
        }
        private void PreFill(object sender, EventArgs e)
        {
            if (MainForm.reportFixes != null && MainForm.reportFixes.Contains(fix1.Text))
            {
                int refIndex = Array.IndexOf(MainForm.reportFixes, fix1.Text);
                try
                {
                    fix3.Text = MainForm.reportFixes[refIndex + 2];
                }
                catch
                {
                    fix3.Clear();
                }
                try
                {
                    fix2.Text = MainForm.reportFixes[refIndex + 1];
                }
                catch
                {
                    fix2.Clear();
                }
            }
            else
            {
                fix2.Clear();
                fix3.Clear();
            }

        }

        private void LogonButton_Click(object sender, EventArgs e)
        {
            messageFormatPanel.Controls.Clear();
            messageFormatPanel.Controls.Add(CreateTemplate("ATC UNIT:"), 1, 0);
            messageFormatPanel.Controls.Add(CreateTextBox(NeedsLogon ? "" : MainForm.CurrentATCUnit, 4), 2, 0);

            logonRadioButton.Checked = true;
            FinalizeMessagePanel();
        }


        private void RequestButton_Click(object sender, EventArgs e)
        {
            requestRadioButton.Checked = true;
            sendButton.Enabled = false;

            popupMenu.Items.Clear();
            popupMenu.Items.Add(directRequestMenu);
            popupMenu.Items.Add(levelRequestMenu);
            popupMenu.Items.Add(speedRequestMenu);
            popupMenu.Items.Add(whenCanWeRequestMenu);
            ShowMenuAtHotspot(popupMenu, requestButton);
        }

        private UITextBox CreateAutoFillTextBox(string _text, int _maxLength, string[] _source)
        {
            UITextBox _temp = CreateTextBox(_text, _maxLength);
            _temp.AutoCompleteMode = AutoCompleteMode.Append;
            _temp.AutoCompleteSource = AutoCompleteSource.CustomSource;
            var autoComplete = new AutoCompleteStringCollection();
            if (_source != null)
            {
                autoComplete.AddRange(_source);
            }
            _temp.AutoCompleteCustomSource = autoComplete;
            _temp.TextAlign = HorizontalAlignment.Center;

            return _temp;
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
                Padding = new Padding(0, 4, 0, 0),
                Margin = new Padding(0, 0, 0, 0),
                TabStop = true,
                TabIndex = 0
            };

            return _temp;
        }

        private AccessibleLabel CreateBoxTemplate(string _text, AnchorStyles _leftOrRight)
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
                Padding = new Padding(0, 4, 0, 0),
                Margin = new Padding(0, 0, 0, 0),
                TabStop = false,
                Anchor = _leftOrRight
            };

            return _temp;
        }

        private UITextBox CreateTextBox(string _text, int _maxLength, bool _readOnly = false, bool _numsOnly = false)
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
                Height = 24,
                ReadOnly = _readOnly,
                TextAlign = HorizontalAlignment.Left,
                TabIndex = 0,
                Anchor = AnchorStyles.Left
            };

            if (_numsOnly)
            {
                _temp.KeyPress += NumsOnly;
            }

            using (Graphics G = _temp.CreateGraphics())
            {
                _temp.Width = (int)(_temp.MaxLength *
                              G.MeasureString("▯", _temp.Font).Width * 1.5);
            }

            return _temp;
        }

        private void NumsOnly(object sender, KeyPressEventArgs e)
        {
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
            {
                e.Handled = true;
            }
        }

        private UICheckBox CreateCheckBox(string _text, string _group)
        {
            UICheckBox _temp = new(_group)
            {
                BackColor = Color.Transparent,
                ForeColor = controlFrontColor,
                Font = textFont,
                Text = _text,
                Padding = new Padding(3, 4, 10, -30),
                AutoSize = true,
                TabIndex = 0
            };
            _temp.Click += DeselectCheckBox;
            return _temp;
        }

        private void DeselectCheckBox(object sender, EventArgs e)
        {
            UICheckBox _sender = (UICheckBox)sender;

            foreach (UICheckBox box in messageFormatPanel.Controls.OfType<UICheckBox>())
            {
                if (box.Text != _sender.Text && box.group == _sender.group)
                {
                    box.Checked = false;
                }
            }

        }

        private UITextBox CreateMultiLineBox(string _text)
        {
            UITextBox _temp = new(controlFrontColor)
            {
                BackColor = controlBackColor,
                ForeColor = controlFrontColor,
                Font = textFontBold,
                BorderStyle = BorderStyle.None,
                Width = Math.Max(180, messageFormatPanel.ClientSize.Width - 20),
                Multiline = true,
                WordWrap = true,
                ScrollBars = ScrollBars.None,
                AcceptsReturn = true,
                AcceptsTab = false,
                Text = _text,
                MaxLength = 255,
                Height = Math.Max(42, messageFormatPanel.ClientSize.Height - 118),
                TabIndex = 0
            };

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

        private void ClearButton_Click(object sender, EventArgs e)
        {
            messageFormatPanel.Controls.Clear();
            sendButton.Enabled = false;
        }


        private string GetRecipientFromPanel()
        {
            TextBox box = messageFormatPanel.Controls.OfType<TextBox>().FirstOrDefault();
            return box == null ? string.Empty : box.Text.Trim().ToUpperInvariant();
        }

        private void MessageInputChanged(object sender, EventArgs e)
        {
            UpdateSendButtonState();
        }

        private void FinalizeMessagePanel()
        {
            foreach (TextBox box in messageFormatPanel.Controls.OfType<TextBox>())
            {
                box.TextChanged -= MessageInputChanged;
                box.TextChanged += MessageInputChanged;
            }

            foreach (CheckBox box in messageFormatPanel.Controls.OfType<CheckBox>())
            {
                box.CheckedChanged -= MessageInputChanged;
                box.CheckedChanged += MessageInputChanged;
            }

            KeepRequestPanelScrollClean();
            UpdateSendButtonState();
        }

        private void UpdateSendButtonState()
        {
            sendButton.Enabled = IsRequestSendReady();
        }

        private bool IsRequestSendReady()
        {
            RadioButton radioBtn = radioContainer.Controls.OfType<RadioButton>()
                                       .Where(x => x.Checked).FirstOrDefault();

            if (radioBtn == null)
            {
                return false;
            }

            TextBox[] boxes = messageFormatPanel.Controls.OfType<TextBox>().ToArray();

            if (boxes.Length == 0)
            {
                return false;
            }

            if (radioBtn.Name == "logonRadioButton")
            {
                return boxes.All(box => !string.IsNullOrWhiteSpace(box.Text));
            }

            if (radioBtn.Name == "depClxRadioButton" || radioBtn.Name == "ocnClxRadioButton")
            {
                return boxes.Where(box => !box.Multiline).All(box => !string.IsNullOrWhiteSpace(box.Text));
            }

            if (radioBtn.Name == "reportRadioButton")
            {
                return boxes.All(box => !string.IsNullOrWhiteSpace(box.Text));
            }

            if (radioBtn.Name == "requestRadioButton")
            {
                string recipientText = GetRecipientFromPanel();
                if (recipientText.Length < 1)
                {
                    return false;
                }

                bool hasTextInput = boxes.Any(box => !box.ReadOnly && !string.IsNullOrWhiteSpace(box.Text));
                bool hasCheckedOption = messageFormatPanel.Controls.OfType<CheckBox>().Any(box => box.Checked);
                return hasTextInput || hasCheckedOption;
            }

            return false;
        }

        private static async Task<bool?> IsHoppieStationOnlineAsync(string station)
        {
            if (string.IsNullOrWhiteSpace(station))
            {
                return false;
            }

            string cleanStation = station.Trim().ToUpperInvariant();

            try
            {
                string html = await HoppieOnlineClient.GetStringAsync("https://www.hoppie.nl/acars/system/online.html");
                string pattern = "\\b" + Regex.Escape(cleanStation) + "\\b";
                return Regex.IsMatch(html, pattern, RegexOptions.IgnoreCase);
            }
            catch
            {
                return null;
            }
        }

        private async void SendButton_Click(object sender, EventArgs e)
        {
            if (!IsRequestSendReady())
            {
                return;
            }

            RadioButton radioBtn = radioContainer.Controls.OfType<RadioButton>()
                                       .Where(x => x.Checked).FirstOrDefault();

            if (radioBtn == null)
            {
                return;
            }

            string _recipient = GetRecipientFromPanel();
            string _formatMessage = "";
            string _messageType = "";

            switch (radioBtn.Name)
            {
                case "ocnClxRadioButton":
                    foreach (UITextBox _tb in messageFormatPanel.Controls.OfType<UITextBox>().Where(x => x.Multiline == false))
                    {
                        if (_tb.Text.Length < 1)
                        {
                            return;
                        }
                    }

                    _formatMessage = string.Format("OCEANIC CLEARANCE REQUEST CALLSIGN: {0} ENTRY POINT: {1} AT: {2} REQ: M.{3} FL{4} RMKS: {5}",
                        messageFormatPanel.Controls[3].Text,
                        messageFormatPanel.Controls[5].Text,
                        messageFormatPanel.Controls[7].Text,
                        messageFormatPanel.Controls[9].Text,
                        messageFormatPanel.Controls[11].Text,
                        messageFormatPanel.Controls[13].Text
                        );
                    _messageType = "CPDLC";
                    break;

                case "depClxRadioButton":
                    foreach (UITextBox _tb in messageFormatPanel.Controls.OfType<UITextBox>().Where(x => x.Multiline == false))
                    {
                        if (_tb.Text.Length < 1)
                        {
                            return;
                        }
                    }

                    _formatMessage = string.Format("REQUEST PREDEP CLEARANCE {0} {1} TO {2} AT {3} STAND {4} ATIS {5}",
                         messageFormatPanel.Controls[3].Text,
                         messageFormatPanel.Controls[5].Text,
                         messageFormatPanel.Controls[9].Text,
                         messageFormatPanel.Controls[7].Text,
                         messageFormatPanel.Controls[11].Text,
                         messageFormatPanel.Controls[13].Text
                         );
                    _messageType = "TELEX";
                    break;

                case "logonRadioButton":
                    foreach (TextBox box in messageFormatPanel.Controls.OfType<TextBox>())
                    {
                        if (string.IsNullOrWhiteSpace(box.Text))
                        {
                            return;
                        }
                    }

                    if (NeedsLogon)
                    {
                        bool? stationOnline = await IsHoppieStationOnlineAsync(_recipient);
                        if (stationOnline == false)
                        {
                            MainForm.WriteMessage("STATION NOT ONLINE", "SYSTEM", "SYSTEM");
                            return;
                        }

                        _formatMessage = string.Format("/data2/{0}//Y/REQUEST LOGON", MainForm.messageOutCounter);
                        MainForm.pendingLogon = _recipient;
                    }
                    else
                    {
                        _formatMessage = string.Format("/data2/{0}//N/LOGOFF", MainForm.messageOutCounter);
                        MainForm.CurrentATCUnit = null;
                    }

                    _messageType = "CPDLC";
                    break;

                case "requestRadioButton":
                    _formatMessage = string.Format("/data2/{0}//Y/", MainForm.messageOutCounter);
                    string parsedMessage = ParseRequest();

                    if (parsedMessage == "")
                    {
                        MainForm.WriteMessage("ERROR PARSING CPDLC MESSAGE. NO MESSAGE SENT", "SYSTEM", "SYSTEM");
                        return;
                    }

                    _formatMessage += parsedMessage;
                    _messageType = "CPDLC";
                    break;

                case "reportRadioButton":
                    _formatMessage = string.Format("/data2/{0}//N/", MainForm.messageOutCounter);
                    string _messageContent = string.Format("POSITION REPORT PPOS {0} AT {1}Z FL{2} TO {3} AT {4}Z NEXT {5}",
                        fix1.Text,
                        messageFormatPanel.Controls[6].Text,
                        messageFormatPanel.Controls[9].Text,
                        fix2.Text,
                        messageFormatPanel.Controls[13].Text,
                        fix3.Text);
                    _formatMessage += _messageContent;
                    _messageType = "CPDLC";
                    MainForm.nextFix = fix2.Text;
                    break;

                default:
                    return;
            }

            if (string.IsNullOrWhiteSpace(_messageType) || string.IsNullOrWhiteSpace(_formatMessage))
            {
                return;
            }

            if (_messageType == "CPDLC")
            {
                MainForm.messageOutCounter += 1;
            }

            _ = Task.Run(() => MainForm.SendCPDLCMessage(_recipient, _messageType, _formatMessage.Trim()));
            this.Close();
        }

        private string ParseRequest()
        {
            RadioButton radioBtn = requestContainer.Controls.OfType<RadioButton>()
                                      .Where(x => x.Checked).FirstOrDefault();

            UICheckBox dueToBox = messageFormatPanel.Controls.OfType<UICheckBox>()
                                   .Where(x => x.Checked && x.group == "rsnParam").FirstOrDefault();

            UICheckBox unitBox = messageFormatPanel.Controls.OfType<UICheckBox>()
                                   .Where(x => x.Checked && x.group == "unitParam").FirstOrDefault();

            UICheckBox wcwBox = messageFormatPanel.Controls.OfType<UICheckBox>()
                                   .Where(x => x.Checked && x.group == "wcwParam").FirstOrDefault();

            string _request = "";

            switch (radioBtn.Name)
            {
                case "levelRadioButton":

                    if (messageFormatPanel.Controls[3].Text == "")
                    {
                        return string.Empty;
                    }
                    _request = "REQUEST FL";
                    _request += messageFormatPanel.Controls[3].Text;

                    dueToBox = messageFormatPanel.Controls.OfType<UICheckBox>()
                                   .Where(x => x.Checked && x.group == "rsnParam").FirstOrDefault();

                    if (dueToBox != default(UICheckBox))
                    {
                        _request += " " + rsnConversion[dueToBox.Text];
                    }
                    break;

                case "directRadioButton":

                    if (messageFormatPanel.Controls[3].Text == "")
                    {
                        return string.Empty;
                    }

                    _request = "REQUEST DIRECT TO ";
                    _request += messageFormatPanel.Controls[3].Text;

                    dueToBox = messageFormatPanel.Controls.OfType<UICheckBox>()
                                   .Where(x => x.Checked && x.group == "rsnParam").FirstOrDefault();

                    if (dueToBox != default(UICheckBox))
                    {
                        _request += " " + rsnConversion[dueToBox.Text];
                    }
                    break;

                case "speedRadioButton":

                    if (messageFormatPanel.Controls[messageFormatPanel.Controls.IndexOf(unitBox) + 1].Text == "")
                    {
                        return string.Empty;
                    }

                    _request += "REQUEST ";
                    if (unitBox != default(UICheckBox))
                    {
                        if (unitBox.Text == "MACH: M0.")
                        {
                            _request += "M" + messageFormatPanel.Controls[messageFormatPanel.Controls.IndexOf(unitBox) + 1].Text;
                        }
                        else
                        {
                            _request += messageFormatPanel.Controls[messageFormatPanel.Controls.IndexOf(unitBox) + 1].Text + "K";
                        }
                    }
                    else
                    {
                        return string.Empty;
                    }

                    dueToBox = messageFormatPanel.Controls.OfType<UICheckBox>()
                                   .Where(x => x.Checked && x.group == "rsnParam").FirstOrDefault();

                    if (dueToBox != default(UICheckBox))
                    {
                        _request += " " + rsnConversion[dueToBox.Text];
                    }
                    break;

                case "wcwRadioButton":

                    if (wcwBox is null)
                    {
                        return string.Empty;
                    }

                    _request = "WHEN CAN WE EXPECT ";

                    switch (wcwBox.Text)
                    {
                        case "HIGHER LEVEL?":
                            _request += "HIGHER LEVEL";
                            break;

                        case "LOWER LEVEL?":
                            _request += "LOWER LEVEL";
                            break;

                        case "BACK ON ROUTE?":
                            _request += "BACK ON ROUTE";
                            break;

                        case "CLIMB TO: FL":
                            _request += "CLIMB TO FL" + messageFormatPanel.Controls[messageFormatPanel.Controls.IndexOf(wcwBox) + 1];
                            break;

                        case "DESCENT TO: FL":
                            _request += "DESCENT TO FL" + messageFormatPanel.Controls[messageFormatPanel.Controls.IndexOf(wcwBox) + 1];
                            break;

                        case "MACH: M0.":
                            _request += "M" + messageFormatPanel.Controls[messageFormatPanel.Controls.IndexOf(wcwBox) + 1];
                            break;

                        case "SPEED: ":
                            _request += messageFormatPanel.Controls[messageFormatPanel.Controls.IndexOf(wcwBox) + 1] + "K";
                            break;

                        default:
                            break;
                    }

                    break;

                default:
                    break;
            }

            return _request;
        }

        private void WindowDrag(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                _ = SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        }

        private void ExpandMultiLineBox(object sender, EventArgs e)
        {
            // Fixed-height remarks boxes use their own vertical scrollbar.
            if (sender is TextBox textBox)
            {
                textBox.ScrollBars = ScrollBars.Vertical;
                if (textBox.Height != 64)
                {
                    textBox.Height = 64;
                }
            }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= WS_EX_TOOLWINDOW;
                cp.ExStyle &= ~WS_EX_APPWINDOW;
                return cp;
            }
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

        private void RequestForm_Load(object sender, EventArgs e)
        {
            if (Properties.Settings.Default.CPDLCWindowLocation != new Point(0, 0))
            {
                Location = Properties.Settings.Default.CPDLCWindowLocation;
            }

            ApplyWindowLayout();
            DcduWindowHelper.ApplyDeviceWindow(this, requestFrame, 22);
        }

        private void RequestForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            Properties.Settings.Default.CPDLCWindowLocation = Location;
            Properties.Settings.Default.CPDLCWindowSize = Size;
            Properties.Settings.Default.Save();
        }

        private void messageFormatPanel_Paint(object sender, PaintEventArgs e)
        {

        }
    }
}
