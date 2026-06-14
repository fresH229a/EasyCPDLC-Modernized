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
using System.Windows.Forms;
using System.Drawing;
using System.IO;
using System.Drawing.Drawing2D;

namespace EasyCPDLC
{


    internal static class LoginArtworkBackground
    {
        private static Image cachedArtwork;

        public static void PaintCrop(Control control, PaintEventArgs e)
        {
            try
            {
                Image artwork = GetArtwork();
                if (artwork == null)
                {
                    return;
                }

                Rectangle src = new Rectangle(control.Left, control.Top, control.Width, control.Height);
                Rectangle dst = new Rectangle(0, 0, control.Width, control.Height);
                e.Graphics.DrawImage(artwork, dst, src, GraphicsUnit.Pixel);
            }
            catch
            {
                // Cosmetic only. Do not break the login form if the artwork is unavailable.
            }
        }

        private static Image GetArtwork()
        {
            if (cachedArtwork != null)
            {
                return cachedArtwork;
            }

            cachedArtwork = EmbeddedAssets.LoadImage("Resources", "DCDU_Login_V13.png");
            if (cachedArtwork != null)
            {
                return cachedArtwork;
            }

            // Developer fallback: allow loose resource while running from the IDE/source tree.
            string file = Path.Combine(AppContext.BaseDirectory, "Resources", "DCDU_Login_V13.png");
            if (!File.Exists(file))
            {
                return null;
            }

            byte[] bytes = File.ReadAllBytes(file);
            using MemoryStream stream = new MemoryStream(bytes);
            using Image loaded = Image.FromStream(stream);
            cachedArtwork = new Bitmap(loaded);
            return cachedArtwork;
        }
    }



    internal class LoginFieldPanel : Panel
    {
        public LoginFieldPanel()
        {
            SetStyle(ControlStyles.UserPaint |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            BorderStyle = BorderStyle.None;
            Padding = new Padding(0);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            LoginArtworkBackground.PaintCrop(this, e);
        }
    }

    internal class LoginConnectHotspot : Control
    {
        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public bool Blue { get; set; } = true;

        public LoginConnectHotspot()
        {
            SetStyle(ControlStyles.UserPaint |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            TabStop = false;
        }

        protected override void OnPaintBackground(PaintEventArgs pevent)
        {
            // No visual overlay. The login artwork contains the full CONNECT button.
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            // No hover/press drawing. This is only a transparent hotspot helper.
        }
    }

    internal class LoginBlueCheckBox : Control
    {
        private bool _checked;

        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public bool Checked
        {
            get => _checked;
            set
            {
                if (_checked == value)
                {
                    return;
                }

                _checked = value;
                Invalidate();
                CheckedChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public event EventHandler CheckedChanged;

        public LoginBlueCheckBox()
        {
            SetStyle(ControlStyles.UserPaint |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.Selectable |
                     ControlStyles.SupportsTransparentBackColor, true);

            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            TabStop = false;
            Text = string.Empty;
            Size = new Size(22, 22);
        }

        protected override bool ShowFocusCues => false;

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);

            if (e.Button == MouseButtons.Left && ClientRectangle.Contains(e.Location))
            {
                Checked = !Checked;
            }
        }

        protected override void OnPaintBackground(PaintEventArgs pevent)
        {
            // Always clear the checkbox area. This avoids the old tick mark getting "stuck" visually.
            using SolidBrush clear = new SolidBrush(Color.FromArgb(1, 18, 42));
            pevent.Graphics.FillRectangle(clear, ClientRectangle);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle box = new Rectangle(1, 1, Math.Min(19, Width - 2), Math.Min(19, Height - 2));
            using GraphicsPath path = RoundedRect(box, 4);

            using SolidBrush fill = new SolidBrush(Checked ? Color.FromArgb(35, 145, 255) : Color.FromArgb(1, 18, 42));
            using Pen border = new Pen(Color.FromArgb(105, 205, 255), 1.4f);

            e.Graphics.FillPath(fill, path);
            e.Graphics.DrawPath(border, path);

            if (Checked)
            {
                using Pen check = new Pen(Color.White, 2.2f)
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round
                };

                e.Graphics.DrawLines(check, new[]
                {
                    new Point(box.Left + 4, box.Top + 10),
                    new Point(box.Left + 8, box.Top + 14),
                    new Point(box.Left + 15, box.Top + 5)
                });
            }
        }

        private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    public partial class DataEntry : Form
    {
        public const int WM_NCLBUTTONDOWN = 0xA1;
        public const int HT_CAPTION = 0x2;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        private const int EM_SETCUEBANNER = 0x1501;
        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

        private static readonly Rectangle CloseHotspot = new Rectangle(257, 12, 28, 30);
        private static readonly Rectangle HoppieFieldHotspot = new Rectangle(35, 256, 232, 38);
        private static readonly Rectangle CidFieldHotspot = new Rectangle(35, 329, 232, 38);
        private static readonly Rectangle ConnectHotspot = new Rectangle(30, 406, 240, 60);

        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public string HoppieLogonCode { get; set; }

        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public int VatsimCID { get; set; }

        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public bool Remember { get; set; }
        public DataEntry(object _hoppieLogonCode = null, object _vatsimCID = null)
        {
            InitializeComponent();
            this.ShowInTaskbar = false;
            Size = new Size(300, 533);
            MinimumSize = new Size(300, 533);
            MaximumSize = new Size(300, 533);
            DcduWindowHelper.ApplyDeviceWindow(this, loginFrame, 18);
            UpdateRememberText();

            try
            {

                if (_hoppieLogonCode is not null)
                {
                    hoppieCodeTextBox.Text = _hoppieLogonCode.ToString();
                }
                else
                {
                    throw new Exception();
                }
                if (_vatsimCID is not null)
                {
                    vatsimCIDTextBox.Text = _vatsimCID.ToString();
                }
                else
                {
                    throw new Exception();
                }
                rememberCheckBox.Checked = true;
            }

            catch (Exception)
            {
                hoppieCodeTextBox.Text = "";
                vatsimCIDTextBox.Text = "";
                rememberCheckBox.Checked = false;
            }

            UpdateRememberText();
            ApplyCueBanners();
            loginFrame.MouseMove += LoginFrame_MouseMove;
            loginScreen.MouseMove += LoginScreen_MouseMove;
            connectButton.MouseUp += ConnectButton_MouseUp;
            connectButton.BringToFront();
            rememberCheckBox.BringToFront();
            rememberCheckBox.TabStop = false;
            if (rememberLabel != null)
            {
                rememberLabel.Visible = false;
                rememberLabel.Text = string.Empty;
            }
        }

        private void ApplyCueBanners()
        {
            if (hoppieCodeTextBox != null)
            {
                _ = SendMessage(hoppieCodeTextBox.Handle, EM_SETCUEBANNER, IntPtr.Zero, "Enter Hoppie Logon Code");
            }

            if (vatsimCIDTextBox != null)
            {
                _ = SendMessage(vatsimCIDTextBox.Handle, EM_SETCUEBANNER, IntPtr.Zero, "Enter VATSIM CID");
            }
        }

        private void HandleOverlayMouseClick(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            if (CloseHotspot.Contains(e.Location))
            {
                ExitButton_Click(exitButton, EventArgs.Empty);
                return;
            }

            if (HoppieFieldHotspot.Contains(e.Location))
            {
                hoppieCodeTextBox?.Focus();
                return;
            }

            if (CidFieldHotspot.Contains(e.Location))
            {
                vatsimCIDTextBox?.Focus();
                return;
            }

            if (ConnectHotspot.Contains(e.Location))
            {
                ConnectButton_Click(connectButton, EventArgs.Empty);
            }
        }


        private void LoginFrame_MouseDown(object sender, MouseEventArgs e)
        {
            if (IsLoginHotspot(e.Location))
            {
                HandleOverlayMouseClick(e);
                return;
            }

            DataEntry_MouseDown(sender, e);
        }

        private void LoginScreen_MouseDown(object sender, MouseEventArgs e)
        {
            if (IsLoginHotspot(e.Location))
            {
                HandleOverlayMouseClick(e);
                return;
            }

            DataEntry_MouseDown(sender, e);
        }

        private static bool IsLoginHotspot(Point location)
        {
            return CloseHotspot.Contains(location) ||
                   HoppieFieldHotspot.Contains(location) ||
                   CidFieldHotspot.Contains(location) ||
                   ConnectHotspot.Contains(location);
        }

        private void ExitButton_Click(object sender, EventArgs e)
        {
            this.Close();
        }
        private void LoginFrame_MouseClick(object sender, MouseEventArgs e)
        {
            HandleOverlayMouseClick(e);
        }

        private void LoginScreen_MouseClick(object sender, MouseEventArgs e)
        {
            HandleOverlayMouseClick(e);
        }

        private void LoginFrame_MouseMove(object sender, MouseEventArgs e)
        {
            UpdateHotCursor(e.Location);
        }

        private void LoginScreen_MouseMove(object sender, MouseEventArgs e)
        {
            UpdateHotCursor(e.Location);
        }

        private void UpdateHotCursor(Point location)
        {
            Cursor desired = (CloseHotspot.Contains(location) || HoppieFieldHotspot.Contains(location) || CidFieldHotspot.Contains(location) || ConnectHotspot.Contains(location))
                ? Cursors.Hand
                : Cursors.Default;

            if (loginFrame != null) loginFrame.Cursor = desired;
            if (loginScreen != null) loginScreen.Cursor = desired;
        }



        private void ConnectButton_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ConnectButton_Click(connectButton, EventArgs.Empty);
            }
        }

        private void ConnectButton_Click(object sender, EventArgs e)
        {
            try
            {
                HoppieLogonCode = hoppieCodeTextBox.Text;
                VatsimCID = Convert.ToInt32(vatsimCIDTextBox.Text);
                Remember = rememberCheckBox.Checked;

                this.DialogResult = DialogResult.OK;
            }
            catch (FormatException)
            {
                MessageBox.Show("Invalid CID/Code, please check and try again.", "Error!", MessageBoxButtons.OK);
            }

        }

        private void HoppieCodeTextBox_TextChanged(object sender, EventArgs e)
        {
            try
            {
                if (vatsimCIDTextBox.Text.Length < 1 || hoppieCodeTextBox.Text.Length < 1)
                {
                    throw new FormatException();
                }

                Convert.ToInt32(vatsimCIDTextBox.Text);
                connectButton.Enabled = true;

            }
            catch (FormatException)
            {
                connectButton.Enabled = false;
            }
        }

        private void VatsimCIDTextBox_TextChanged(object sender, EventArgs e)
        {
            try
            {
                if (vatsimCIDTextBox.Text.Length < 1 || hoppieCodeTextBox.Text.Length < 1)
                {
                    throw new FormatException();
                }

                Convert.ToInt32(vatsimCIDTextBox.Text);
                connectButton.Enabled = true;

            }
            catch (FormatException)
            {
                connectButton.Enabled = false;
            }
        }

        private void RememberCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            UpdateRememberText();
        }

        private void RememberCheckBox_Click(object sender, EventArgs e)
        {
            // Not used. LoginBlueCheckBox handles its own toggle exactly once.
        }


        private void RememberLabel_Click(object sender, EventArgs e)
        {
            // Not used. The Remember Me text is part of the artwork.
        }

        private void UpdateRememberText()
        {
            if (rememberCheckBox != null)
            {
                rememberCheckBox.Text = string.Empty;
                rememberCheckBox.Invalidate();
            }
        }

        private void DataEntry_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                _ = SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        }

        private void NumsOnly(object sender, KeyPressEventArgs e)
        {
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
            {
                e.Handled = true;
            }
        }
    }
}
