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
using System.Drawing;
using System.Windows.Forms;

namespace EasyCPDLC
{

    public partial class SettingsForm : Form
    {

        DcduCheckBox stayOnTopBox;
        DcduCheckBox audiblePingBox;
        DcduCheckBox useFSUIPCBox;
        UITextBox simbriefTextBox;
        ComboBox styleSelector;

        private readonly MainForm parent;

        public const int WM_NCLBUTTONDOWN = 0xA1;
        public const int HT_CAPTION = 0x2;
        private const int cGrip = 16;
        private const int cCaption = 32;

        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_APPWINDOW = 0x00040000;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        public SettingsForm(MainForm _parent)
        {
            parent = _parent;
            InitializeComponent();
            this.ShowInTaskbar = false;
            settingsFrame.AssetFileName = DcduStyleManager.AssetFile("SettingsWindowFrame.png");
            ApplyTransparentScreenOverlays();
            ApplyWindowLayout();
            DcduWindowHelper.ApplyDeviceWindow(this, settingsFrame, 10);
            InitialiseHotspots();
            InitialiseSettings();
        }



        private void ApplyWindowLayout()
        {
            bool isBoeing = DcduStyleManager.IsBoeing;
            Size targetSize = isBoeing ? new Size(600, 210) : new Size(670, 235);
            ClientSize = targetSize;
            Size = targetSize;
            MinimumSize = targetSize;
            MaximumSize = targetSize;

            settingsFrame.Location = new Point(0, 0);
            settingsFrame.Size = targetSize;

            if (isBoeing)
            {
                settingsCard.Location = new Point(103, 22);
                settingsCard.Size = new Size(380, 169);
                settingsFormatPanel.Location = new Point(10, 17);
                settingsFormatPanel.Size = new Size(360, 146);
                exitButton.Bounds = new Rectangle(520, 39, 53, 29);
                cancelButton.Bounds = new Rectangle(520, 74, 53, 29);
                okButton.Bounds = new Rectangle(520, 108, 53, 29);
            }
            else
            {
                settingsCard.Location = new Point(90, 32);
                settingsCard.Size = new Size(434, 167);
                settingsFormatPanel.Location = new Point(22, 12);
                settingsFormatPanel.Size = new Size(390, 148);
                exitButton.Bounds = new Rectangle(555, 43, 59, 31);
                cancelButton.Bounds = new Rectangle(555, 83, 59, 31);
                okButton.Bounds = new Rectangle(555, 123, 59, 31);
            }

            settingsFrame.Invalidate();
        }

        private void InitialiseHotspots()
        {
            // The hotspot controls are NOT added to settingsFrame.Controls.
            // They exist only as bounds/event containers so they cannot punch transparent holes through the bitmap.
        }

        private Control GetAssetHotspotAt(Point location)
        {
            Control[] hotspots = { exitButton, cancelButton, okButton };
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
            settingsFrame.HighlightRectangle = Rectangle.Empty;
            settingsFrame.Cursor = hit == null ? Cursors.Default : Cursors.Hand;
        }

        private void AssetFrame_MouseLeave(object sender, EventArgs e)
        {
            settingsFrame.HighlightPressed = false;
            settingsFrame.HighlightRectangle = Rectangle.Empty;
            settingsFrame.Cursor = Cursors.Default;
        }

        private void AssetFrame_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            Control hit = GetAssetHotspotAt(e.Location);
            if (hit != null)
            {
                settingsFrame.HighlightRectangle = Rectangle.Empty;
                settingsFrame.HighlightPressed = false;
                return;
            }
            WindowDrag(sender, e);
        }

        private void AssetFrame_MouseUp(object sender, MouseEventArgs e)
        {
            settingsFrame.HighlightPressed = false;
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
            // Keep the settings content transparent so the bitmap screen remains visible behind it.
            settingsCard.BackColor = Color.Transparent;
            settingsFormatPanel.BackColor = Color.Transparent;
            settingsFormatPanel.AutoScroll = false;
        }

        private void InitialiseSettings()
        {
            settingsFormatPanel.Controls.Clear();
            stayOnTopBox = CreateCheckBox("Keep Window On Top", "0");
            stayOnTopBox.Checked = parent.StayOnTop;
            audiblePingBox = CreateCheckBox("Play Sound on Message Receive", "1");
            audiblePingBox.Checked = MainForm.PlaySound;
            useFSUIPCBox = CreateCheckBox("Use Simulator Connection (req. FSUIPC/XPUIPC)", "2");
            useFSUIPCBox.Checked = MainForm.UseFSUIPC;
            simbriefTextBox = CreateTextBox(MainForm.SimbriefID, 7, false, true);

            settingsFormatPanel.Controls.Add(stayOnTopBox);
            settingsFormatPanel.SetFlowBreak(stayOnTopBox, true);
            settingsFormatPanel.Controls.Add(audiblePingBox);
            settingsFormatPanel.SetFlowBreak(audiblePingBox, true);
            settingsFormatPanel.Controls.Add(useFSUIPCBox);
            settingsFormatPanel.SetFlowBreak(useFSUIPCBox, true);

            FlowLayoutPanel styleRow = CreateStyleSelectorRow();
            settingsFormatPanel.Controls.Add(styleRow);
            settingsFormatPanel.SetFlowBreak(styleRow, true);

            bool isBoeing = DcduStyleManager.IsBoeing;
            FlowLayoutPanel simbriefRow = new()
            {
                BackColor = Color.Transparent,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = false,
                Size = isBoeing ? new Size(350, 24) : new Size(390, 25),
                Margin = isBoeing ? new Padding(0, 3, 0, 0) : new Padding(0, 3, 0, 0),
                Padding = new Padding(0, 0, 0, 0)
            };
            Label simbriefLabel = CreateTemplate("SIMBRIEF PILOT ID:");
            simbriefLabel.Width = isBoeing ? 148 : 156;
            simbriefLabel.AutoSize = false;
            simbriefLabel.Padding = new Padding(0, 2, 0, 0);
            simbriefLabel.Margin = new Padding(0, 0, 6, 0);
            simbriefRow.Controls.Add(simbriefLabel);
            simbriefRow.Controls.Add(simbriefTextBox);
            settingsFormatPanel.Controls.Add(simbriefRow);
        }


        private FlowLayoutPanel CreateStyleSelectorRow()
        {
            bool isBoeing = DcduStyleManager.IsBoeing;
            FlowLayoutPanel styleRow = new()
            {
                BackColor = Color.Transparent,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = false,
                Size = isBoeing ? new Size(350, 24) : new Size(390, 25),
                Margin = isBoeing ? new Padding(0, 3, 0, 0) : new Padding(0, 3, 0, 0),
                Padding = new Padding(0, 0, 0, 0)
            };

            Label styleLabel = CreateTemplate("DCDU STYLE:");
            styleLabel.Width = isBoeing ? 148 : 156;
            styleLabel.AutoSize = false;
            styleLabel.Padding = new Padding(0, 2, 0, 0);
            styleLabel.Margin = new Padding(0, 0, 6, 0);

            styleSelector = new ComboBox()
            {
                BackColor = DcduTheme.ScreenAlt,
                ForeColor = DcduTheme.CyanWhite,
                Font = new Font("Consolas", isBoeing ? 8.0f : 8.2f, FontStyle.Bold),
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Popup,
                Width = isBoeing ? 104 : 120,
                Height = isBoeing ? 20 : 21,
                Margin = isBoeing ? new Padding(0, 0, 0, 0) : new Padding(0, 1, 0, 0)
            };

            styleSelector.Items.Add(DcduStyleManager.Airbus);
            styleSelector.Items.Add(DcduStyleManager.Boeing);
            styleSelector.SelectedItem = DcduStyleManager.CurrentStyle;
            styleSelector.SelectedIndexChanged += StyleSelector_SelectedIndexChanged;

            styleRow.Controls.Add(styleLabel);
            styleRow.Controls.Add(styleSelector);
            return styleRow;
        }

        private DcduCheckBox CreateCheckBox(string _text, string _group)
        {
            bool isBoeing = DcduStyleManager.IsBoeing;
            DcduCheckBox _temp = new()
            {
                BackColor = Color.Transparent,
                ForeColor = DcduTheme.CyanWhite,
                Font = new Font("Consolas", isBoeing ? 7.8f : 8.1f, FontStyle.Bold),
                Text = _text,
                Margin = isBoeing ? new Padding(0, 0, 0, 4) : new Padding(0, 0, 0, 4),
                Size = isBoeing ? new Size(350, 23) : new Size(390, 25)
            };
            return _temp;
        }

        private Label CreateTemplate(string _text)
        {
            bool isBoeing = DcduStyleManager.IsBoeing;
            Label _temp = new()
            {
                BackColor = Color.Transparent,
                ForeColor = DcduTheme.CyanWhite,
                Font = new Font("Consolas", isBoeing ? 7.8f : 8.1f, FontStyle.Bold),
                AutoSize = true,
                Text = _text,
                Top = 10,
                Height = 16,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(0, 2, 0, 0),
                Margin = isBoeing ? new Padding(0, 3, 0, 2) : new Padding(0, 3, 0, 2)
            };

            return _temp;
        }

        private UITextBox CreateTextBox(string _text, int _maxLength, bool _readOnly = false, bool _numsOnly = false)
        {
            bool isBoeing = DcduStyleManager.IsBoeing;
            UITextBox _temp = new(parent.controlFrontColor)
            {
                BackColor = DcduTheme.ScreenAlt,
                ForeColor = DcduTheme.CyanWhite,
                Font = new Font("Consolas", DcduStyleManager.IsBoeing ? 8.0f : 8.2f, FontStyle.Bold),
                MaxLength = _maxLength,
                BorderStyle = BorderStyle.FixedSingle,
                Text = _text,
                CharacterCasing = CharacterCasing.Upper,
                Top = 10,
                Margin = isBoeing ? new Padding(0, 1, 0, 0) : new Padding(0, 1, 0, 0),
                Padding = new Padding(3, 1, 3, 1),
                Height = isBoeing ? 20 : 21,
                Width = isBoeing ? 104 : 120,
                ReadOnly = _readOnly,
                TextAlign = HorizontalAlignment.Center
            };

            if (_numsOnly)
            {
                _temp.KeyPress += NumsOnly;
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

        private void CancelButton_Click(object sender, EventArgs e)
        {
            this.Close();
        }


        private void StyleSelector_SelectedIndexChanged(object sender, EventArgs e)
        {
            bool stayOnTop = stayOnTopBox?.Checked ?? parent.StayOnTop;
            bool playSound = audiblePingBox?.Checked ?? MainForm.PlaySound;
            bool useFsuipc = useFSUIPCBox?.Checked ?? MainForm.UseFSUIPC;
            string simbriefId = simbriefTextBox?.Text ?? MainForm.SimbriefID;

            string selectedStyle = styleSelector?.SelectedItem?.ToString() ?? DcduStyleManager.Airbus;
            DcduStyleManager.CurrentStyle = selectedStyle;

            // Update the currently open Settings window immediately.
            if (settingsFrame != null)
            {
                settingsFrame.AssetFileName = DcduStyleManager.AssetFile("SettingsWindowFrame.png");
            }
            ApplyWindowLayout();

            // Rebuild the Settings content with style-specific spacing.
            InitialiseSettings();
            stayOnTopBox.Checked = stayOnTop;
            audiblePingBox.Checked = playSound;
            useFSUIPCBox.Checked = useFsuipc;
            simbriefTextBox.Text = simbriefId;

            // Update the already open Main window immediately.
            parent?.ApplyDisplayStyle();
        }

        private void OkButton_Click(object sender, EventArgs e)
        {
            parent.StayOnTop = stayOnTopBox.Checked;
            MainForm.PlaySound = audiblePingBox.Checked;
            MainForm.UseFSUIPC = useFSUIPCBox.Checked;
            MainForm.SimbriefID = simbriefTextBox.Text;
            DcduStyleManager.CurrentStyle = styleSelector?.SelectedItem?.ToString() ?? DcduStyleManager.Airbus;
            parent.ApplyDisplayStyle();

            Properties.Settings.Default.Save();
            this.Close();
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
    }
}
