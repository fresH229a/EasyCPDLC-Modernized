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
using NLog;
using Octokit;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Media;
using System.Net.Http;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Security.Principal;
using FSUIPC;

namespace EasyCPDLC
{
    public partial class MainForm : Form
    {

        public const int WM_NCLBUTTONDOWN = 0xA1;
        public const int HT_CAPTION = 0x2;
        private const int cGrip = 16;
        private const int cCaption = 32;
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ReleaseCapture();
        private const int ScrollBarHorizontal = 0;
        private const int ScrollBarBoth = 3;
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ShowScrollBar(IntPtr hWnd, int wBar, bool bShow);

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private sealed class DcduBadgeLabel : System.Windows.Forms.Label
        {
            public DcduBadgeLabel()
            {
                SetStyle(
                    ControlStyles.UserPaint |
                    ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.OptimizedDoubleBuffer |
                    ControlStyles.ResizeRedraw |
                    ControlStyles.Selectable,
                    true);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                Rectangle bounds = new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1));
                Color accent = ForeColor;
                bool focused = Focused;
                bool hovered = ClientRectangle.Contains(PointToClient(Cursor.Position));

                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                using GraphicsPath path = MainForm.RoundedButtonRect(bounds, 3);
                using LinearGradientBrush fill = new LinearGradientBrush(
                    bounds,
                    Color.FromArgb(34, accent),
                    Color.FromArgb(12, accent),
                    LinearGradientMode.Vertical);
                using Pen border = new Pen(Color.FromArgb((focused || hovered) ? 180 : 112, accent), (focused || hovered) ? 1.35f : 1.0f);
                using Pen topLine = new Pen(Color.FromArgb(45, Color.White), 1.0f);

                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(border, path);
                e.Graphics.DrawLine(topLine, bounds.Left + 4, bounds.Top + 1, bounds.Right - 4, bounds.Top + 1);

                string badgeText = Text ?? string.Empty;

                if (badgeText.Contains("●"))
                {
                    string title = badgeText.Replace("●", string.Empty).Trim();
                    Rectangle textRect = new Rectangle(bounds.Left + 5, bounds.Top, Math.Max(1, bounds.Width - 23), bounds.Height);

                    TextRenderer.DrawText(
                        e.Graphics,
                        title,
                        Font,
                        textRect,
                        hovered ? Color.White : accent,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);

                    int ledSize = Math.Min(10, Math.Max(7, bounds.Height - 8));
                    Rectangle ledRect = new Rectangle(
                        bounds.Right - ledSize - 7,
                        bounds.Top + (bounds.Height - ledSize) / 2,
                        ledSize,
                        ledSize);

                    using SolidBrush glowBrush = new SolidBrush(Color.FromArgb((focused || hovered) ? 72 : 42, accent));
                    Rectangle glowRect = Rectangle.Inflate(ledRect, 3, 3);
                    e.Graphics.FillEllipse(glowBrush, glowRect);

                    using LinearGradientBrush ledFill = new LinearGradientBrush(
                        ledRect,
                        Color.FromArgb(245, Color.White),
                        accent,
                        LinearGradientMode.ForwardDiagonal);
                    using Pen ledBorder = new Pen(Color.FromArgb(220, accent), 1.0f);
                    using Pen ledShadow = new Pen(Color.FromArgb(120, Color.Black), 1.0f);

                    e.Graphics.FillEllipse(ledFill, ledRect);
                    e.Graphics.DrawEllipse(ledBorder, ledRect);

                    Rectangle highlight = new Rectangle(ledRect.Left + 2, ledRect.Top + 2, Math.Max(2, ledSize / 3), Math.Max(2, ledSize / 3));
                    using SolidBrush highlightBrush = new SolidBrush(Color.FromArgb(180, Color.White));
                    e.Graphics.FillEllipse(highlightBrush, highlight);

                    Rectangle shadow = new Rectangle(ledRect.Left + 1, ledRect.Top + 1, ledRect.Width - 1, ledRect.Height - 1);
                    e.Graphics.DrawArc(ledShadow, shadow, 35, 110);
                    return;
                }

                TextRenderer.DrawText(
                    e.Graphics,
                    badgeText,
                    Font,
                    Rectangle.Inflate(bounds, -4, -1),
                    hovered ? Color.White : accent,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
            }
        }

        public Pilot userVATSIMData;
        private VATSIMRootobject vatsimData;
        private Navlog simbriefData;
        public string[] reportFixes;
        public string nextFix = null;

        public FSUIPCData fsuipc = new();
        public bool fsConnectionOpen = false;
        public int fsuipcErrorCount = 1;

        private bool isErrorState = false;

        public Random random = new();

        readonly private List<Contract> contracts = new();

        private static readonly HttpClient webclient = new();
        private string logonCode;
        private int cid;
        public string callsign;

        private RequestForm rForm;
        private TelexForm tForm;
        private SettingsForm sForm;

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]

        public bool StayOnTop
        {
            get
            {
                return Properties.Settings.Default.StayOnTop;
            }
            set
            {
                Properties.Settings.Default.StayOnTop = value;
                this.TopMost = value;
            }
        }

        public static bool PlaySound
        {
            get
            {
                return Properties.Settings.Default.PlayAudibleAlert;
            }
            set
            {
                Properties.Settings.Default.PlayAudibleAlert = value;
            }
        }

        public static bool UseFSUIPC
        {
            get
            {
                return Properties.Settings.Default.UseFSUIPC;
            }
            set
            {
                Properties.Settings.Default.UseFSUIPC = value;
            }
        }

        public static int SavedCID
        {
            get
            {
                return Properties.Settings.Default.CID;
            }
            set
            {
                Properties.Settings.Default.CID = value;

            }
        }

        public static string SavedHoppieCode
        {
            get
            {
                return Properties.Settings.Default.HoppieCode;
            }
            set
            {
                Properties.Settings.Default.HoppieCode = value;

            }
        }

        public static string SimbriefID
        {
            get
            {
                return Properties.Settings.Default.SimbriefUsername;
            }
            set
            {
                Properties.Settings.Default.SimbriefUsername = value;
            }
        }

        public int messageOutCounter = 1;
        private bool _connected;

        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public bool Connected
        {
            get
            {
                return _connected;
            }
            set
            {
                _connected = value;

                if (retrieveButton != null)
                {
                    retrieveButton.Text = _connected ? "DISC" : "CONN";
                }

                if (statusValueLabel != null)
                {
                    statusValueLabel.Text = _connected ? "CONNECTED" : "STANDBY";
                    statusValueLabel.ForeColor = _connected ? DcduTheme.Green : DcduTheme.Amber;
                }

                if (atcButton != null)
                {
                    atcButton.Enabled = _connected;
                }

                if (telexButton != null)
                {
                    telexButton.Enabled = _connected;
                }
            }
        }

        public string pendingLogon = null;
        private string _currentATCUnit;

        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public string CurrentATCUnit
        {
            get
            {
                return _currentATCUnit;
            }
            set
            {
                _currentATCUnit = value;

                if (_currentATCUnit is null)
                {
                    if (atcUnitDisplay != null)
                    {
                        atcUnitDisplay.Text = "----";
                        atcUnitDisplay.ForeColor = MainPrimaryTextColor();
                    }

                    UpdateOnlineStatusLabel();

                    if (rForm != null)
                    {
                        rForm.NeedsLogon = true;
                    }
                }
                else
                {
                    if (atcUnitDisplay != null)
                    {
                        atcUnitDisplay.Text = _currentATCUnit;
                        atcUnitDisplay.ForeColor = MainPrimaryTextColor();
                    }

                    UpdateOnlineStatusLabel();

                    if (rForm != null)
                    {
                        rForm.NeedsLogon = false;
                    }
                }
            }
        }

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        private static extern IntPtr AddFontMemResourceEx(IntPtr pbFont, uint cbFont,
            IntPtr pdv, [System.Runtime.InteropServices.In] ref uint pcFonts);


        public byte[][] fontResources = { Properties.Resources.B612Mono_Bold, Properties.Resources.B612Mono_Regular, Properties.Resources.Oxygen_Regular, Properties.Resources.Oxygen_Bold };
        public static PrivateFontCollection fonts = new PrivateFontCollection();

        public Font controlFont;// = new("Oxygen", 10.0f, FontStyle.Regular);
        public Font controlFontBold;// = new("Oxygen", 10.0f, FontStyle.Bold);
        public Font textFont;// = new("B612 Mono", 10.0f, FontStyle.Regular);
        public Font textFontBold;// = new("B612 Mono", 12.5f, FontStyle.Bold);
        public Font dataEntryFont;// = new("B612 Mono", 11.0f, FontStyle.Regular);
        public Color controlBackColor = DcduTheme.Screen;
        public Color controlFrontColor = DcduTheme.CyanWhite;

        private readonly ContextMenuStrip popupMenu = new();
        private readonly ContextMenuStrip filterMenu = new();
        ToolStripMenuItem deleteAllMenu;
        ToolStripMenuItem exportLogMenu;
        ToolStripMenuItem weatherCacheMenu;

        private System.Windows.Forms.Label messageFilterLabel;
        private System.Windows.Forms.Label onlineStatusLabel;
        private System.Windows.Forms.Label clearanceStatusLabel;
        private System.Windows.Forms.Label datalinkStatusLabel;
        private System.Windows.Forms.Label atisStatusLabel;
        private string activeMessageFilter = "ALL";
        private string clearanceStatusText = "CLR --";
        private string datalinkStatusText = "PDC --";
        private string atisStatusText = "ATIS --";
        private readonly string[] messageFilterOrder = { "ALL", "NEW", "ATIS", "METAR", "CPDLC", "TELEX", "SYSTEM" };

        private readonly System.Windows.Forms.Timer vatsimOnlineTimer = new();
        private DateTime lastVatsimOnlineRefreshUtc = DateTime.MinValue;
        private readonly HashSet<string> hoppieOnlineStations = new(StringComparer.OrdinalIgnoreCase);
        private bool hoppieOnlineStationsLoaded = false;

        private sealed class WeatherCacheItem
        {
            public string Type { get; set; }
            public string Target { get; set; }
            public string Summary { get; set; }
            public string Contents { get; set; }
            public DateTime TimestampUtc { get; set; }
        }

        private readonly Dictionary<string, WeatherCacheItem> weatherCache = new();

        private AccessibleLabel wilcoLabel;
        private AccessibleLabel rogerLabel;
        private AccessibleLabel affirmativeLabel;
        private AccessibleLabel negativeLabel;
        private AccessibleLabel standbyLabel;
        private AccessibleLabel unableLabel;
        private AccessibleLabel deleteLabel;
        private AccessibleLabel freeTextLabel;
        private AccessibleLabel returnLabel;

        private AccessibleLabel[] replyOptionsList;

        private CPDLCMessage previewMessage;

        private readonly SoundPlayer startupPlayer = new();
        private readonly SoundPlayer messagePlayer = new();

        private readonly System.Windows.Forms.Timer unreadReminderTimer = new();
        private readonly List<CPDLCMessage> unreadMessages = new();
        private readonly Queue<string> pendingAtisRequestTargets = new();
        private readonly object pendingAtisRequestLock = new();
        private readonly Queue<string> pendingMetarRequestTargets = new();
        private readonly object pendingMetarRequestLock = new();

        private readonly TimeSpan freeTextCooldown = TimeSpan.FromMinutes(5);
        private readonly object freeTextCooldownLock = new();
        private DateTime lastFreeTextSentUtc = DateTime.MinValue;

        private static readonly Regex hoppieParse = new(@"{(.*?)}");
        private static readonly Regex cpdlcHeaderParse = new(@"(\/\s*)\w*");
        private static readonly Regex cpdlcUnitParse = new(@"_@([\w]*)@_");

        private static readonly TimeSpan updateTimer = TimeSpan.FromSeconds(20);
        private CancellationTokenSource requestCancellationTokenSource;
        private CancellationToken requestCancellationToken;
        private const string HoppieConnectUrl = "https://www.hoppie.nl/acars/system/connect.html";

        public MainForm()
        {
            var config = new NLog.Config.LoggingConfiguration();
            var logFile = new NLog.Targets.FileTarget("logfile") { FileName = "EasyCPDLCLog.txt" };
            config.AddRule(LogLevel.Debug, LogLevel.Fatal, logFile);
            LogManager.Configuration = config;
            Logger.Info("Logging initialised, beginning setup");

            textFont = new Font("Consolas", 10.5f, FontStyle.Regular);
            textFontBold = new Font("Consolas", 11.5f, FontStyle.Bold);
            controlFont = new Font("Segoe UI", 10.0f, FontStyle.Regular);
            controlFontBold = new Font("Segoe UI", 10.0f, FontStyle.Bold);
            dataEntryFont = new Font("Consolas", 13.0f, FontStyle.Bold);

            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;

            ConfigureSoundPlayers();
            ConfigureUnreadMessageReminder();
            ConfigureVatsimOnlineRefresh();
            if (!string.IsNullOrWhiteSpace(startupPlayer.SoundLocation) || startupPlayer.Stream != null)
            {
                startupPlayer.Play();
            }
            InitializeComponent();
            ApplyDisplayStyle();
            this.TopMost = StayOnTop;
            this.FormBorderStyle = FormBorderStyle.None;
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.ResizeRedraw, true);
            ApplyMainWindowBounds(DcduStyleManager.IsBoeing);
            CurrentATCUnit = null;

        }



        private Color MainAccentColor()
        {
            return DcduStyleManager.IsBoeing
                ? Color.FromArgb(86, 255, 103)
                : DcduTheme.Cyan;
        }

        private Color MainPrimaryTextColor()
        {
            return DcduStyleManager.IsBoeing
                ? Color.FromArgb(178, 255, 188)
                : DcduTheme.CyanWhite;
        }

        private void ApplyMainThemeColors()
        {
            controlFrontColor = MainPrimaryTextColor();

            if (titleLabel != null)
            {
                titleLabel.ForeColor = MainAccentColor();
            }

            if (messageHeaderLabel != null)
            {
                messageHeaderLabel.ForeColor = MainAccentColor();
            }

            if (clockLabel != null)
            {
                clockLabel.ForeColor = MainPrimaryTextColor();
            }

            if (statusCaptionLabel != null)
            {
                statusCaptionLabel.ForeColor = MainPrimaryTextColor();
            }

            if (atcUnitLabel != null)
            {
                atcUnitLabel.ForeColor = MainPrimaryTextColor();
            }

            if (atcUnitDisplay != null && CurrentATCUnit != null)
            {
                atcUnitDisplay.ForeColor = MainPrimaryTextColor();
            }

            if (popupMenu != null)
            {
                popupMenu.BackColor = controlBackColor;
                popupMenu.ForeColor = controlFrontColor;
                foreach (ToolStripItem item in popupMenu.Items)
                {
                    item.ForeColor = controlFrontColor;
                    item.BackColor = controlBackColor;
                }
            }

            UpdateSmartStatusLabelColors();
            RefreshVisibleMessageColors();
        }

        private void UpdateSmartStatusLabelColors()
        {
            if (messageFilterLabel != null)
            {
                ApplyMainBadgeStyle(messageFilterLabel, activeMessageFilter == "ALL" ? MainPrimaryTextColor() : DcduTheme.Amber, true);
            }

            if (onlineStatusLabel != null)
            {
                onlineStatusLabel.Visible = false;
            }

            if (clearanceStatusLabel != null)
            {
                ApplyMainBadgeStyle(clearanceStatusLabel, ClearanceStatusColor(), true);
            }

            if (datalinkStatusLabel != null)
            {
                ApplyMainBadgeStyle(datalinkStatusLabel, DatalinkStatusColor(), false);
            }

            if (atisStatusLabel != null)
            {
                ApplyMainBadgeStyle(atisStatusLabel, AtisStatusColor(), false);
            }
        }

        private Color ClearanceStatusColor()
        {
            string upper = (clearanceStatusText ?? string.Empty).ToUpperInvariant();

            if (upper.Contains("ACCEPTED") || upper.Contains("ACC"))
            {
                return DcduTheme.Green;
            }

            if (upper.Contains("REJECTED") || upper.Contains("REJ"))
            {
                return Color.FromArgb(255, 86, 74);
            }

            if (upper.Contains("REQUESTED") || upper.Contains("REQ") || upper.Contains("STANDBY") || upper.Contains("STBY"))
            {
                return DcduTheme.Amber;
            }

            return MainPrimaryTextColor();
        }

        private Color DatalinkStatusColor()
        {
            string upper = (datalinkStatusText ?? string.Empty).ToUpperInvariant();

            // PDC availability is based on the Hoppie callsign list:
            // white = unknown/not checked yet, red = no CPDLC/PDC station found, green = station available.
            if (upper.Contains("AVAIL"))
            {
                return DcduTheme.Green;
            }

            if (upper.Contains("NONE"))
            {
                return Color.FromArgb(255, 86, 74);
            }

            if (upper.Contains("?") || upper.Contains("--"))
            {
                return MainPrimaryTextColor();
            }

            return MainPrimaryTextColor();
        }

        private Color AtisStatusColor()
        {
            string upper = (atisStatusText ?? string.Empty).ToUpperInvariant();

            if (upper.Contains("A") || upper.Contains("D") || upper.Contains("N"))
            {
                return MainPrimaryTextColor();
            }

            if (upper.Contains("-") || upper.Contains("?"))
            {
                return DcduTheme.Amber;
            }

            return MainPrimaryTextColor();
        }

        private void ApplyMainBadgeStyle(System.Windows.Forms.Label label, Color color, bool bold)
        {
            if (label == null)
            {
                return;
            }

            label.ForeColor = color;
            label.BackColor = Color.Transparent;
            label.BorderStyle = BorderStyle.None;
            label.Font = new Font(
                bold ? textFontBold.FontFamily : textFont.FontFamily,
                Math.Max(7.0f, (bold ? textFontBold.Size : textFont.Size) - 2.7f),
                bold ? FontStyle.Bold : FontStyle.Regular);
            label.Invalidate();
        }

        private void MainStatusBadge_Paint(object sender, PaintEventArgs e)
        {
            if (sender is not System.Windows.Forms.Label label)
            {
                return;
            }

            Rectangle bounds = new Rectangle(0, 0, Math.Max(1, label.Width - 1), Math.Max(1, label.Height - 1));
            Color accent = label.ForeColor;
            bool focused = label.Focused;
            bool hovered = label.ClientRectangle.Contains(label.PointToClient(Cursor.Position));

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

            using GraphicsPath path = RoundedButtonRect(bounds, 3);
            using LinearGradientBrush fill = new LinearGradientBrush(
                bounds,
                Color.FromArgb(34, accent),
                Color.FromArgb(12, accent),
                LinearGradientMode.Vertical);
            using Pen border = new Pen(Color.FromArgb((focused || hovered) ? 180 : 112, accent), (focused || hovered) ? 1.35f : 1.0f);
            using Pen topLine = new Pen(Color.FromArgb(45, Color.White), 1.0f);

            e.Graphics.FillPath(fill, path);
            e.Graphics.DrawPath(border, path);
            e.Graphics.DrawLine(topLine, bounds.Left + 4, bounds.Top + 1, bounds.Right - 4, bounds.Top + 1);

            TextRenderer.DrawText(
                e.Graphics,
                label.Text,
                label.Font,
                Rectangle.Inflate(bounds, -4, -1),
                hovered ? Color.White : accent,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
        }

        private void RefreshVisibleMessageColors()
        {
            if (outputTable == null || outputTable.IsDisposed)
            {
                return;
            }

            foreach (Control control in outputTable.Controls)
            {
                if (control is CPDLCMessage message)
                {
                    bool isUnread = unreadMessages.Contains(message);

                    if (isUnread)
                    {
                        message.Font = textFontBold;
                        message.ForeColor = DcduTheme.Amber;
                    }
                    else if (message.acknowledged)
                    {
                        message.Font = ShouldEmphasizeAtisLetter(message) ? textFontBold : textFont;
                        message.ForeColor = SystemColors.ControlDark;
                    }
                    else
                    {
                        message.Font = ShouldEmphasizeAtisLetter(message) ? textFontBold : textFont;
                        message.ForeColor = MainPrimaryTextColor();
                    }
                }
                else if (control is TimerLabel timerLabel)
                {
                    if (timerLabel.Text == "NEW")
                    {
                        timerLabel.ForeColor = DcduTheme.Amber;
                    }
                    else
                    {
                        timerLabel.ForeColor = MainPrimaryTextColor();
                    }
                }
                else if (control is AccessibleLabel label)
                {
                    label.ForeColor = MainPrimaryTextColor();
                }
            }
        }

        public void ApplyDisplayStyle()
        {
            bool isBoeing = DcduStyleManager.IsBoeing;
            ApplyMainWindowBounds(isBoeing);
            if (dcduFrame != null)
            {
                dcduFrame.AssetFileName = DcduStyleManager.AssetFile("DCDU_Main_V15.png");
                dcduFrame.HighlightRectangle = Rectangle.Empty;
                dcduFrame.HighlightPressed = false;
                dcduFrame.Invalidate();
            }

            ApplyMainScreenLayout(isBoeing);
            ApplyMainButtonLayout(isBoeing);
            ConfigureInboundMessageSound();
            ApplyMainThemeColors();
            Invalidate(true);
        }

        private void ConfigureSmartWidgets()
        {
            if (screenPanel == null)
            {
                return;
            }

            if (messageFilterLabel == null)
            {
                messageFilterLabel = new DcduBadgeLabel
                {
                    AutoSize = false,
                    BackColor = Color.Transparent,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Cursor = Cursors.Hand,
                    Text = "ALL ▾"
                };
                messageFilterLabel.MouseEnter += (_, __) =>
                {
                    messageFilterLabel.Invalidate();
                };
                messageFilterLabel.MouseLeave += (_, __) =>
                {
                    UpdateSmartStatusLabelColors();
                    messageFilterLabel.Invalidate();
                };
                messageFilterLabel.Click += MessageFilterLabel_Click;
                screenPanel.Controls.Add(messageFilterLabel);
            }

            if (onlineStatusLabel == null)
            {
                onlineStatusLabel = new System.Windows.Forms.Label
                {
                    AutoSize = false,
                    BackColor = Color.Transparent,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Cursor = Cursors.Hand,
                    Text = string.Empty,
                    Visible = false
                };
                screenPanel.Controls.Add(onlineStatusLabel);
            }

            if (clearanceStatusLabel == null)
            {
                clearanceStatusLabel = CreateMainStatusBadge(BuildDotBadgeText("CLR"), Cursors.Default);
                screenPanel.Controls.Add(clearanceStatusLabel);
            }

            if (datalinkStatusLabel == null)
            {
                datalinkStatusLabel = CreateMainStatusBadge(BuildDotBadgeText("PDC"), Cursors.Default);
                screenPanel.Controls.Add(datalinkStatusLabel);
            }

            if (atisStatusLabel == null)
            {
                atisStatusLabel = CreateMainStatusBadge(atisStatusText, Cursors.Default);
                screenPanel.Controls.Add(atisStatusLabel);
            }

            clearanceStatusLabel.BringToFront();
            datalinkStatusLabel.BringToFront();
            atisStatusLabel.BringToFront();
            messageFilterLabel.BringToFront();
            ConfigureFilterDropdownMenu();
            UpdateSmartStatusLabelColors();
            UpdateMessageFilterLabel();
            UpdateOnlineStatusLabel();
            UpdateClearanceStatusLabel();
        }

        private System.Windows.Forms.Label CreateMainStatusBadge(string text, Cursor cursor)
        {
            System.Windows.Forms.Label label = new DcduBadgeLabel
            {
                AutoSize = false,
                BackColor = Color.Transparent,
                BorderStyle = BorderStyle.None,
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = cursor,
                Text = text ?? string.Empty,
                TabStop = cursor == Cursors.Hand
            };

            label.MouseEnter += (_, __) =>
            {
                label.Invalidate();
            };
            label.MouseLeave += (_, __) =>
            {
                UpdateSmartStatusLabelColors();
                label.Invalidate();
            };
            label.GotFocus += (_, __) => label.Invalidate();
            label.LostFocus += (_, __) => label.Invalidate();

            return label;
        }

        private void ApplySmartWidgetLayout(bool isBoeing)
        {
            ConfigureSmartWidgets();

            if (messageFilterLabel == null || clearanceStatusLabel == null || datalinkStatusLabel == null || atisStatusLabel == null)
            {
                return;
            }

            if (isBoeing)
            {
                // Filter belongs to the MESSAGES / DATA area on the left.
                messageFilterLabel.Location = new Point(146, 69);
                messageFilterLabel.Size = new Size(46, 19);

                // CLR / PDC / ATIS belong under the CURRENT ATS UNIT block on the right.
                clearanceStatusLabel.Location = new Point(246, 57);
                clearanceStatusLabel.Size = new Size(58, 19);
                datalinkStatusLabel.Location = new Point(320, 57);
                datalinkStatusLabel.Size = new Size(58, 19);
                atisStatusLabel.Location = new Point(386, 57);
                atisStatusLabel.Size = new Size(72, 19);
            }
            else
            {
                // Filter belongs to the MESSAGES / DATA area on the left.
                messageFilterLabel.Location = new Point(148, 77);
                messageFilterLabel.Size = new Size(46, 19);

                // CLR / PDC / ATIS belong under the CURRENT ATS UNIT block on the right.
                clearanceStatusLabel.Location = new Point(248, 65);
                clearanceStatusLabel.Size = new Size(58, 19);
                datalinkStatusLabel.Location = new Point(322, 65);
                datalinkStatusLabel.Size = new Size(58, 19);
                atisStatusLabel.Location = new Point(388, 65);
                atisStatusLabel.Size = new Size(72, 19);
            }

            if (onlineStatusLabel != null)
            {
                onlineStatusLabel.Visible = false;
                onlineStatusLabel.Size = new Size(1, 1);
            }

            clearanceStatusLabel.BringToFront();
            datalinkStatusLabel.BringToFront();
            atisStatusLabel.BringToFront();
            messageFilterLabel.BringToFront();
        }

        private static string BuildDotBadgeText(string title)
        {
            return string.IsNullOrWhiteSpace(title)
                ? "●"
                : title.Trim().ToUpperInvariant() + " ●";
        }

        private void UpdateClearanceStatusLabel()
        {
            if (clearanceStatusLabel != null)
            {
                clearanceStatusLabel.Text = BuildDotBadgeText("CLR");
                clearanceStatusLabel.ForeColor = ClearanceStatusColor();
                clearanceStatusLabel.Invalidate();
            }

            if (datalinkStatusLabel != null)
            {
                datalinkStatusLabel.Text = BuildDotBadgeText("PDC");
                datalinkStatusLabel.ForeColor = DatalinkStatusColor();
                datalinkStatusLabel.Invalidate();
            }

            if (atisStatusLabel != null)
            {
                atisStatusLabel.Text = atisStatusText;
                atisStatusLabel.ForeColor = AtisStatusColor();
                atisStatusLabel.Invalidate();
            }

            UpdateSmartStatusLabelColors();
        }

        private void SetClearanceStatus(string status)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                return;
            }

            clearanceStatusText = status.Trim().ToUpperInvariant();

            SafeUi(() =>
            {
                UpdateClearanceStatusLabel();
                UpdateSmartStatusLabelColors();
            });
        }

        private void TrackClearanceStatus(string contents, string type, bool outbound)
        {
            if (string.IsNullOrWhiteSpace(contents))
            {
                return;
            }

            string normalizedType = (type ?? string.Empty).Trim().ToUpperInvariant();

            if (normalizedType != "CPDLC" && normalizedType != "TELEX")
            {
                return;
            }

            string upper = contents.ToUpperInvariant();

            if (!IsClearanceStatusText(upper))
            {
                return;
            }

            if (outbound)
            {
                SetClearanceStatus("CLR REQ");
                return;
            }

            if (upper.Contains("REJECT") || upper.Contains("UNABLE") || upper.Contains("DENIED"))
            {
                SetClearanceStatus("CLR REJ");
                return;
            }

            SetClearanceStatus("CLR RX");
        }

        private static bool IsClearanceStatusText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            string upper = text.ToUpperInvariant();

            return upper.Contains("CLEARANCE") ||
                   upper.Contains("CLR TO") ||
                   upper.Contains("CLRD TO") ||
                   upper.Contains("CLEARED TO") ||
                   upper.Contains("PREDEP") ||
                   upper.Contains("PDC");
        }

        private void TrackClearanceReply(CPDLCMessage message, string reply)
        {
            if (message == null || !IsClearanceStatusText(message.message))
            {
                return;
            }

            string upperReply = (reply ?? string.Empty).ToUpperInvariant();

            if (upperReply == "STANDBY")
            {
                SetClearanceStatus("CLR STBY");
            }
            else if (upperReply == "UNABLE" || upperReply == "NEGATIVE" || upperReply == "REJECT")
            {
                SetClearanceStatus("CLR REJ");
            }
            else if (upperReply == "WILCO" || upperReply == "AFFIRMATIVE" || upperReply == "ROGER" || upperReply == "ACCEPT")
            {
                SetClearanceStatus("CLR ACC");
            }
        }

        private void MessageFilterLabel_Click(object sender, EventArgs e)
        {
            if (messageFilterLabel == null)
            {
                return;
            }

            ConfigureFilterDropdownMenu();
            filterMenu.Show(messageFilterLabel, new Point(0, messageFilterLabel.Height + 2));
        }

        private void ConfigureFilterDropdownMenu()
        {
            filterMenu.Items.Clear();
            filterMenu.BackColor = Color.FromArgb(4, 9, 13);
            filterMenu.ForeColor = MainPrimaryTextColor();
            filterMenu.Font = textFontBold;
            filterMenu.ShowImageMargin = false;
            filterMenu.ShowCheckMargin = false;
            filterMenu.RenderMode = ToolStripRenderMode.Professional;
            filterMenu.Padding = new Padding(2);

            foreach (string filter in messageFilterOrder)
            {
                ToolStripMenuItem item = CreateFilterMenuItem(filter);
                filterMenu.Items.Add(item);
            }
        }

        private ToolStripMenuItem CreateFilterMenuItem(string filter)
        {
            bool selected = string.Equals(filter, activeMessageFilter, StringComparison.OrdinalIgnoreCase);
            int count = CountVisibleMessagesForFilter(filter);

            ToolStripMenuItem item = new ToolStripMenuItem(filter + "  " + count)
            {
                AutoSize = false,
                Size = new Size(118, 28),
                BackColor = selected ? Color.FromArgb(20, MainAccentColor()) : Color.FromArgb(4, 9, 13),
                ForeColor = selected ? DcduTheme.Amber : MainPrimaryTextColor(),
                Font = selected ? textFontBold : textFont,
                Tag = filter,
                Padding = new Padding(8, 0, 8, 0),
                TextAlign = ContentAlignment.MiddleLeft
            };

            item.Click += FilterMenuItem_Click;
            return item;
        }

        private void FilterMenuItem_Click(object sender, EventArgs e)
        {
            if (sender is ToolStripMenuItem item && item.Tag is string filter)
            {
                activeMessageFilter = filter;
                ApplyMessageFilter();
            }
        }

        private void UpdateMessageFilterLabel()
        {
            if (messageFilterLabel == null)
            {
                return;
            }

            messageFilterLabel.Text = activeMessageFilter == "ALL" ? "ALL ▾" : activeMessageFilter + " ▾";
            UpdateSmartStatusLabelColors();
        }

        private int CountVisibleMessagesForFilter(string filter)
        {
            if (outputTable == null || outputTable.IsDisposed)
            {
                return 0;
            }

            int count = 0;

            foreach (Control control in outputTable.Controls)
            {
                if (control is CPDLCMessage message && MessageMatchesFilter(message, filter))
                {
                    count++;
                }
            }

            return count;
        }

        private bool MessageMatchesFilter(CPDLCMessage message, string filter)
        {
            if (message == null)
            {
                return false;
            }

            string normalizedFilter = string.IsNullOrWhiteSpace(filter) ? "ALL" : filter.ToUpperInvariant();
            string type = (message.type ?? string.Empty).ToUpperInvariant();
            string recipient = (message.recipient ?? string.Empty).ToUpperInvariant();
            string contents = message.message ?? string.Empty;
            string listText = (message.Text ?? string.Empty).ToUpperInvariant();

            bool looksAtis = string.Equals(type, "ATIS", StringComparison.OrdinalIgnoreCase) ||
                ShouldUseAtisListText(contents, type, recipient);

            bool looksMetar = string.Equals(type, "METAR", StringComparison.OrdinalIgnoreCase) ||
                ShouldUseMetarListText(contents, type, recipient) ||
                listText.Contains(" METAR ") ||
                listText.StartsWith("METAR ");

            return normalizedFilter switch
            {
                "ALL" => true,
                "NEW" => unreadMessages.Contains(message),
                "ATIS" => looksAtis,
                "METAR" => looksMetar,
                "CPDLC" => string.Equals(type, "CPDLC", StringComparison.OrdinalIgnoreCase),
                "TELEX" => string.Equals(type, "TELEX", StringComparison.OrdinalIgnoreCase),
                "SYSTEM" => string.Equals(type, "SYSTEM", StringComparison.OrdinalIgnoreCase),
                _ => true
            };
        }

        private void ApplyMessageFilter()
        {
            if (outputTable == null || outputTable.IsDisposed)
            {
                return;
            }

            outputTable.SuspendLayout();

            try
            {
                for (int row = 0; row < outputTable.RowCount; row++)
                {
                    CPDLCMessage rowMessage = null;

                    for (int col = 0; col < outputTable.ColumnCount; col++)
                    {
                        if (outputTable.GetControlFromPosition(col, row) is CPDLCMessage message)
                        {
                            rowMessage = message;
                            break;
                        }
                    }

                    bool visible = rowMessage == null || MessageMatchesFilter(rowMessage, activeMessageFilter);

                    for (int col = 0; col < outputTable.ColumnCount; col++)
                    {
                        Control control = outputTable.GetControlFromPosition(col, row);
                        if (control != null)
                        {
                            control.Visible = visible;
                        }
                    }

                    if (row < outputTable.RowStyles.Count)
                    {
                        outputTable.RowStyles[row].SizeType = visible ? SizeType.AutoSize : SizeType.Absolute;
                        outputTable.RowStyles[row].Height = 0F;
                    }
                }
            }
            finally
            {
                outputTable.ResumeLayout(true);
            }

            UpdateMessageFilterLabel();
            HideNativeOutputScrollbars();
        }

        private void ConfigureVatsimOnlineRefresh()
        {
            // Kept intentionally as a no-op.
            // Online status is refreshed once on connect/start and manually by clicking the label.
        }

        private async Task RefreshVatsimOnlineStatusAsync(bool force)
        {
            if (!force && DateTime.UtcNow - lastVatsimOnlineRefreshUtc < TimeSpan.FromMinutes(10))
            {
                UpdateOnlineStatusLabel();
                return;
            }

            try
            {
                await RefreshHoppieOnlineStationsAsync();

                string data = await webclient.GetStringAsync("https://data.vatsim.net/v3/vatsim-data.json");
                VATSIMRootobject refreshed = JsonConvert.DeserializeObject<VATSIMRootobject>(data);

                if (refreshed != null)
                {
                    vatsimData = refreshed;
                }

                lastVatsimOnlineRefreshUtc = DateTime.UtcNow;
                UpdateOnlineStatusLabel();
            }
            catch (Exception ex)
            {
                Logger.Debug("Online status refresh failed: " + ex.Message);
                if (onlineStatusLabel != null)
                {
                    datalinkStatusText = "PDC ?";
                    atisStatusText = "ATIS ?";
                    UpdateClearanceStatusLabel();
                }
            }
        }

        private async Task RefreshHoppieOnlineStationsAsync()
        {
            var connectionValues = new Dictionary<string, string>
            {
                {"logon", logonCode ?? String.Empty},
                {"from", string.IsNullOrWhiteSpace(callsign) ? "EASYCPDLC" : callsign},
                {"to", "SERVER"},
                {"type", "ping"},
                {"packet", "ALL-CALLSIGNS"}
            };

            var content = new FormUrlEncodedContent(connectionValues);
            var response = await webclient.PostAsync(HoppieConnectUrl, content);
            string responseString = await response.Content.ReadAsStringAsync();

            hoppieOnlineStations.Clear();

            foreach (Match match in Regex.Matches(responseString.ToUpperInvariant(), @"\b[A-Z0-9]{3,10}(?:_[A-Z0-9]{1,10})?\b"))
            {
                string token = match.Value.Trim();

                if (IsUsefulHoppieStationToken(token))
                {
                    hoppieOnlineStations.Add(token);
                }
            }

            hoppieOnlineStationsLoaded = true;
        }

        private static bool IsUsefulHoppieStationToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            string upper = token.Trim().ToUpperInvariant();

            string[] reserved =
            {
                "OK", "ERROR", "SERVER", "SYSTEM", "PING", "POLL",
                "ALL", "CALLSIGNS", "ALL-CALLSIGNS", "ACARS",
                "TYPE", "PACKET", "LOGON", "FROM", "TO"
            };

            return !reserved.Contains(upper);
        }

        private void UpdateOnlineStatusLabel()
        {
            string station = GetPrimaryOnlineStatusStation();

            if (string.IsNullOrWhiteSpace(station))
            {
                datalinkStatusText = "PDC --";
                atisStatusText = "ATIS --";
                UpdateClearanceStatusLabel();
                return;
            }

            station = station.ToUpperInvariant();

            bool hasDatalink = IsControllerOnlineForStation(station);
            bool atisGeneric = IsAtisOnline(station);
            bool atisArrival = IsAtisOnline(station + "_A");
            bool atisDeparture = IsAtisOnline(station + "_D");

            List<string> atisParts = new();
            if (atisGeneric) atisParts.Add("N");
            if (atisArrival) atisParts.Add("A");
            if (atisDeparture) atisParts.Add("D");

            string atisText = atisParts.Count == 0 ? "-" : string.Join("/", atisParts);
            string datalinkText = hoppieOnlineStationsLoaded ? (hasDatalink ? "AVAIL" : "NONE") : "?";

            datalinkStatusText = "PDC " + datalinkText;
            atisStatusText = "ATIS " + atisText;
            UpdateClearanceStatusLabel();
        }

        private string GetPrimaryOnlineStatusStation()
        {
            if (!string.IsNullOrWhiteSpace(CurrentATCUnit) && CurrentATCUnit.Length >= 4)
            {
                return CurrentATCUnit.Substring(0, 4);
            }

            if (userVATSIMData?.flight_plan != null)
            {
                if (!string.IsNullOrWhiteSpace(userVATSIMData.flight_plan.departure))
                {
                    return userVATSIMData.flight_plan.departure;
                }

                if (!string.IsNullOrWhiteSpace(userVATSIMData.flight_plan.arrival))
                {
                    return userVATSIMData.flight_plan.arrival;
                }
            }

            return string.Empty;
        }

        private bool IsControllerOnlineForStation(string station)
        {
            if (!hoppieOnlineStationsLoaded || string.IsNullOrWhiteSpace(station))
            {
                return false;
            }

            string upper = station.ToUpperInvariant();

            return hoppieOnlineStations.Any(hoppieStation =>
            {
                string candidate = (hoppieStation ?? string.Empty).ToUpperInvariant();
                return candidate == upper ||
                       candidate.StartsWith(upper + "_", StringComparison.Ordinal) ||
                       candidate.StartsWith(upper, StringComparison.Ordinal);
            });
        }

        private bool IsAtisOnline(string target)
        {
            if (vatsimData?.atis == null || string.IsNullOrWhiteSpace(target))
            {
                return false;
            }

            string clean = target.ToUpperInvariant();
            string withAtis = clean.EndsWith("_ATIS", StringComparison.OrdinalIgnoreCase)
                ? clean
                : clean + "_ATIS";

            return vatsimData.atis.Any(atis =>
                !string.IsNullOrWhiteSpace(atis.callsign) &&
                string.Equals(atis.callsign.ToUpperInvariant(), withAtis, StringComparison.Ordinal));
        }

        private void ApplyMainWindowBounds(bool isBoeing)
        {
            Size targetSize = isBoeing ? new Size(654, 385) : new Size(700, 311);

            if (ClientSize != targetSize)
            {
                ClientSize = targetSize;
            }

            Size = targetSize;
            MinimumSize = targetSize;
            MaximumSize = targetSize;

            if (dcduFrame != null)
            {
                dcduFrame.Location = new Point(0, 0);
                dcduFrame.Size = targetSize;
                dcduFrame.Invalidate();
            }

            DcduWindowHelper.ApplyDeviceWindow(this, dcduFrame, 22);
        }

        private void ApplyMainScreenLayout(bool isBoeing)
        {
            if (isBoeing)
            {
                // Boeing main layout scaled for the new 654x385 main window asset.
                if (outputTable.ColumnStyles.Count >= 3)
                {
                    outputTable.ColumnStyles[0].SizeType = SizeType.Absolute;
                    outputTable.ColumnStyles[0].Width = 74F;
                    outputTable.ColumnStyles[1].SizeType = SizeType.Percent;
                    outputTable.ColumnStyles[1].Width = 100F;
                    outputTable.ColumnStyles[2].SizeType = SizeType.Absolute;
                    outputTable.ColumnStyles[2].Width = 44F;
                }
                screenPanel.Location = new Point(76, 22);
                screenPanel.Size = new Size(490, 216);
                screenPanel.BackColor = Color.Transparent;
                screenPanel.DrawScreenBackground = false;

                titleLabel.Location = new Point(12, 10);
                clockLabel.Location = new Point(396, 6);
                statusCaptionLabel.Location = new Point(12, 34);
                statusValueLabel.Location = new Point(82, 34);
                atcUnitLabel.Location = new Point(270, 34);
                atcUnitDisplay.Location = new Point(412, 34);
                messageHeaderLabel.Location = new Point(12, 70);
                messageHeaderLabel.Text = "MESSAGES / DATA";

                outputTable.Location = new Point(16, 92);
                outputTable.Size = new Size(454, 105);
                outputTable.Padding = new Padding(0, 4, 4, 4);

                messageFormatPanel.Location = new Point(16, 92);
                messageFormatPanel.Size = new Size(454, 105);

                SendingProgress.Location = new Point(16, 202);
                SendingProgress.Size = new Size(454, 10);

                outputScrollBar.Location = new Point(470, 92);
                outputScrollBar.Size = new Size(12, 105);
                outputScrollBar.Target = outputTable;
                outputScrollBar.Invalidate();
                ApplySmartWidgetLayout(isBoeing);
                return;
            }

            // Airbus layout retuned to the new 700x311 frame artwork.
            if (outputTable.ColumnStyles.Count >= 3)
            {
                outputTable.ColumnStyles[0].SizeType = SizeType.Absolute;
                outputTable.ColumnStyles[0].Width = 68F;
                outputTable.ColumnStyles[1].SizeType = SizeType.Percent;
                outputTable.ColumnStyles[1].Width = 100F;
                outputTable.ColumnStyles[2].SizeType = SizeType.Absolute;
                outputTable.ColumnStyles[2].Width = 40F;
            }
            screenPanel.Location = new Point(103, 34);
            screenPanel.Size = new Size(493, 232);
            screenPanel.BackColor = Color.Transparent;
            screenPanel.DrawScreenBackground = false;

            titleLabel.Location = new Point(8, 10);
            clockLabel.Location = new Point(386, 8);
            statusCaptionLabel.Location = new Point(8, 38);
            statusValueLabel.Location = new Point(84, 38);
            atcUnitLabel.Location = new Point(238, 38);
            atcUnitDisplay.Location = new Point(397, 38);
            messageHeaderLabel.Location = new Point(8, 78);
            messageHeaderLabel.Text = "MESSAGES / DATA";

            outputTable.Location = new Point(8, 106);
            outputTable.Size = new Size(466, 106);
            outputTable.Padding = new Padding(0, 4, 12, 4);

            messageFormatPanel.Location = new Point(8, 106);
            messageFormatPanel.Size = new Size(466, 106);

            SendingProgress.Location = new Point(8, 218);
            SendingProgress.Size = new Size(466, 8);

            outputScrollBar.Location = new Point(476, 106);
            outputScrollBar.Size = new Size(8, 106);
            outputScrollBar.Target = outputTable;
            outputScrollBar.Invalidate();
            ApplySmartWidgetLayout(isBoeing);
        }

        private void ApplyMainButtonLayout(bool isBoeing)
        {
            if (isBoeing)
            {
                // Hitboxes tuned for the compact 654x385 Boeing main asset.
                retrieveButton.Location = new Point(24, 266);
                retrieveButton.Size = new Size(40, 28);

                telexButton.Location = new Point(67, 266);
                telexButton.Size = new Size(40, 28);

                atcButton.Location = new Point(110, 266);
                atcButton.Size = new Size(40, 28);

                settingsButton.Location = new Point(153, 266);
                settingsButton.Size = new Size(45, 28);

                helpButton.Location = new Point(541, 266);
                helpButton.Size = new Size(43, 28);

                exitButton.Location = new Point(587, 266);
                exitButton.Size = new Size(46, 28);
                return;
            }

            // Airbus hitboxes aligned to the provided 700x311 DCDU_Main_V15.png.
            retrieveButton.Location = new Point(26, 57);
            retrieveButton.Size = new Size(47, 33);

            telexButton.Location = new Point(26, 101);
            telexButton.Size = new Size(48, 31);

            atcButton.Location = new Point(25, 143);
            atcButton.Size = new Size(48, 32);

            settingsButton.Location = new Point(26, 185);
            settingsButton.Size = new Size(47, 32);

            helpButton.Location = new Point(623, 74);
            helpButton.Size = new Size(47, 31);

            exitButton.Location = new Point(623, 116);
            exitButton.Size = new Size(47, 31);
        }

        private void ConfigureSoundPlayers()
        {
            // Sounds are embedded into the EXE for single-file publishing.
            // Notification.wav remains app-start only.
            // Notification2.wav is the Airbus inbound message sound.
            // Notification3.wav is the Boeing inbound message sound.
            ConfigureSoundPlayer(startupPlayer, "Notification.wav");   // app start only
            ConfigureInboundMessageSound();
        }

        private void ConfigureInboundMessageSound()
        {
            ConfigureSoundPlayer(messagePlayer, DcduStyleManager.IsBoeing ? "Notification3.wav" : "Notification2.wav");
        }

        private void PlayInboundMessageSound()
        {
            ConfigureInboundMessageSound();

            if (!string.IsNullOrWhiteSpace(messagePlayer.SoundLocation) || messagePlayer.Stream != null)
            {
                messagePlayer.Play();
            }
        }

        private static bool ShouldPlayInboundMessageSound(string messageType, string recipient)
        {
            if (string.Equals(recipient, "SYSTEM", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(messageType))
            {
                return false;
            }

            string normalizedType = messageType.Trim().ToUpperInvariant();
            return normalizedType == "CPDLC" ||
                   normalizedType == "TELEX" ||
                   normalizedType == "INFO" ||
                   normalizedType == "INFOREQ" ||
                   normalizedType == "METAR" ||
                   normalizedType == "ATIS";
        }


        private void ConfigureUnreadMessageReminder()
        {
            unreadReminderTimer.Interval = 30000;
            unreadReminderTimer.Tick += UnreadReminderTimer_Tick;
        }

        private void UnreadReminderTimer_Tick(object sender, EventArgs e)
        {
            unreadMessages.RemoveAll(message => message == null || message.IsDisposed || message.outbound);

            if (unreadMessages.Count == 0 || !PlaySound)
            {
                unreadReminderTimer.Stop();
                return;
            }

            PlayInboundMessageSound();
        }

        private bool ShouldTrackUnreadMessage(CPDLCMessage message)
        {
            return message != null &&
                   !message.outbound &&
                   ShouldPlayInboundMessageSound(message.type, message.recipient);
        }

        private bool ShouldFlashForReply(CPDLCMessage message)
        {
            return message != null &&
                   string.Equals(message.type, "CPDLC", StringComparison.OrdinalIgnoreCase) &&
                   !message.outbound &&
                   !message.acknowledged &&
                   message.header != null &&
                   message.header.Responses != "NE";
        }

        private void MarkMessageUnread(CPDLCMessage message, TimerLabel menuLabel)
        {
            if (!ShouldTrackUnreadMessage(message))
            {
                return;
            }

            if (!unreadMessages.Contains(message))
            {
                unreadMessages.Add(message);
            }

            message.Font = textFontBold;
            message.ForeColor = DcduTheme.Amber;

            if (menuLabel != null)
            {
                menuLabel.Text = "NEW";
                menuLabel.CanFlash = true;
                menuLabel.ForeColor = DcduTheme.Amber;
            }

            if (PlaySound && !unreadReminderTimer.Enabled)
            {
                unreadReminderTimer.Start();
            }
        }

        private void MarkMessageRead(CPDLCMessage message)
        {
            if (message == null)
            {
                return;
            }

            bool wasUnread = unreadMessages.Remove(message);

            if (!wasUnread)
            {
                return;
            }

            message.Font = textFont;
            message.ForeColor = message.acknowledged ? SystemColors.ControlDark : MainPrimaryTextColor();

            try
            {
                if (outputTable != null && !outputTable.IsDisposed)
                {
                    int index = outputTable.Controls.GetChildIndex(message);
                    if (index >= 0 && index + 1 < outputTable.Controls.Count &&
                        outputTable.Controls[index + 1] is TimerLabel menuLabel)
                    {
                        menuLabel.Text = ">>";
                        menuLabel.CanFlash = ShouldFlashForReply(message);
                        if (!menuLabel.CanFlash)
                        {
                            menuLabel.ForeColor = MainPrimaryTextColor();
                        }
                    }
                }
            }
            catch
            {
                // Visual read marker only. Never block opening a message.
            }

            if (unreadMessages.Count == 0)
            {
                unreadReminderTimer.Stop();
            }

            ApplyMessageFilter();
        }

        private void ClearUnreadMessages()
        {
            unreadMessages.Clear();
            unreadReminderTimer.Stop();
            ApplyMessageFilter();
        }

        private static void ConfigureSoundPlayer(SoundPlayer soundPlayer, string fileName)
        {
            // Prefer embedded sounds for single-file publishing.
            if (EmbeddedAssets.ConfigureSoundPlayer(soundPlayer, fileName))
            {
                Logger.Info($"Configured embedded sound {fileName}");
                return;
            }

            // Developer fallback: allow loose sound files while running from the IDE/source tree.
            string soundFile = Path.Combine(AppContext.BaseDirectory, "Sounds", fileName);
            if (File.Exists(soundFile))
            {
                soundPlayer.Stream = null;
                soundPlayer.SoundLocation = soundFile;
                Logger.Info($"Configured sound {fileName}: {soundFile}");
            }
            else
            {
                Logger.Warn($"Sound file not found as embedded resource or loose file: {fileName}");
            }
        }

        private static void SyncSoundFileToOutput(string fileName)
        {
            try
            {
                string sourceFile = GetProjectSoundFile(fileName);
                if (string.IsNullOrWhiteSpace(sourceFile) || !File.Exists(sourceFile))
                {
                    return;
                }

                string outputSoundsDir = Path.Combine(AppContext.BaseDirectory, "Sounds");
                Directory.CreateDirectory(outputSoundsDir);
                string targetFile = Path.Combine(outputSoundsDir, fileName);
                File.Copy(sourceFile, targetFile, true);
                Logger.Info($"Synchronized sound {fileName}: {sourceFile} -> {targetFile}");
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, $"Could not synchronize sound file {fileName}");
            }
        }

        private static string GetProjectSoundFile(string fileName)
        {
            string baseDir = AppContext.BaseDirectory;
            DirectoryInfo dir = new(baseDir);
            for (int i = 0; i < 10 && dir != null; i++, dir = dir.Parent)
            {
                string projectFile = Path.Combine(dir.FullName, "EasyCPDLC.csproj");
                if (File.Exists(projectFile))
                {
                    string projectSound = Path.Combine(dir.FullName, "Sounds", fileName);
                    if (File.Exists(projectSound))
                    {
                        return projectSound;
                    }
                    break;
                }
            }

            string startupSound = Path.Combine(System.Windows.Forms.Application.StartupPath, "Sounds", fileName);
            if (File.Exists(startupSound))
            {
                return startupSound;
            }

            string currentSound = Path.Combine(Environment.CurrentDirectory, "Sounds", fileName);
            return File.Exists(currentSound) ? currentSound : null;
        }

        private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            if (new AssemblyName(args.Name).Name == "System.Runtime.CompilerServices.Unsafe")
            {
                string unsafeDllPath = Path.Combine(System.Windows.Forms.Application.StartupPath, "System.Runtime.CompilerServices.Unsafe.dll");
                if (File.Exists(unsafeDllPath))
                {
                    return Assembly.LoadFrom(unsafeDllPath);
                }
            }

            return null;
        }
        private void DcduFrame_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            if (HitDcduButton(retrieveButton, e.Location))
            {
                RetrieveButton_Click(retrieveButton, EventArgs.Empty);
                return;
            }

            if (HitDcduButton(telexButton, e.Location))
            {
                TelexButton_Click(telexButton, EventArgs.Empty);
                return;
            }

            if (HitDcduButton(atcButton, e.Location))
            {
                RequestButton_Click(atcButton, EventArgs.Empty);
                return;
            }

            if (HitDcduButton(settingsButton, e.Location))
            {
                SettingsButton_Click(settingsButton, EventArgs.Empty);
                return;
            }

            if (HitDcduButton(helpButton, e.Location))
            {
                HelpButton_Click(helpButton, EventArgs.Empty);
                return;
            }

            if (HitDcduButton(exitButton, e.Location))
            {
                ExitButton_Click(exitButton, EventArgs.Empty);
            }
        }

        private Control GetDcduButtonAt(Point location)
        {
            Control[] candidates =
            {
                retrieveButton,
                telexButton,
                atcButton,
                settingsButton,
                helpButton,
                exitButton
            };

            foreach (Control button in candidates)
            {
                if (HitDcduButton(button, location))
                {
                    return button;
                }
            }

            return null;
        }

        private static bool HitDcduButton(Control button, Point location)
        {
            return button != null && button.Enabled && button.Bounds.Contains(location);
        }

        private void DcduFrame_MouseMove(object sender, MouseEventArgs e)
        {
            Control hit = GetDcduButtonAt(e.Location);
            dcduFrame.HighlightRectangle = Rectangle.Empty;
            dcduFrame.HighlightPressed = false;
            dcduFrame.Cursor = hit == null ? Cursors.Default : Cursors.Hand;
        }

        private void DcduFrame_MouseLeave(object sender, EventArgs e)
        {
            dcduFrame.HighlightRectangle = Rectangle.Empty;
            dcduFrame.HighlightPressed = false;
            dcduFrame.Cursor = Cursors.Default;
        }

        private void DcduFrame_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            if (GetDcduButtonAt(e.Location) != null)
            {
                dcduFrame.HighlightRectangle = Rectangle.Empty;
                dcduFrame.HighlightPressed = false;
                return;
            }

            MoveWindow(sender, e);
        }

        private void DcduFrame_MouseUp(object sender, MouseEventArgs e)
        {
            dcduFrame.HighlightRectangle = Rectangle.Empty;
            dcduFrame.HighlightPressed = false;
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            outputTable.BorderStyle = BorderStyle.None;
            outputTable.BackColor = Color.Transparent;
            messageFormatPanel.BackColor = Color.Transparent;
            outputTable.HorizontalScroll.Maximum = 0;
            outputTable.AutoScroll = false;
            outputTable.VerticalScroll.Visible = false;
            outputTable.AutoScroll = true;
            outputTable.Scroll += (scrollSender, scrollArgs) => HideNativeOutputScrollbars();
            outputTable.ControlAdded += (controlSender, controlArgs) => HideNativeOutputScrollbars();
            outputTable.ControlRemoved += (controlSender, controlArgs) => HideNativeOutputScrollbars();
            outputTable.SizeChanged += (sizeSender, sizeArgs) => HideNativeOutputScrollbars();
            outputTable.Layout += (layoutSender, layoutArgs) => HideNativeOutputScrollbars();
            HideNativeOutputScrollbars();

            CheckNewVersion();
            //CheckAdministrator();
            InitialisePopupMenu();
            ShowSetupForm();
            Setup();

            if (Properties.Settings.Default.MainWindowLocation != new Point(0, 0))
            {
                Location = Properties.Settings.Default.MainWindowLocation;
            }

            ApplyMainWindowBounds(DcduStyleManager.IsBoeing);
            ApplyMainScreenLayout(DcduStyleManager.IsBoeing);
            ApplyMainButtonLayout(DcduStyleManager.IsBoeing);
            ConfigureSmartWidgets();
            ApplyMessageFilter();

            UpdateOnlineStatusLabel();

            Logger.Info("Setup completed successfully");
        }


        private void HideNativeOutputScrollbars()
        {
            if (outputTable == null || outputTable.IsDisposed)
            {
                return;
            }

            try
            {
                if (outputTable.IsHandleCreated)
                {
                    ShowScrollBar(outputTable.Handle, ScrollBarBoth, false);
                }
            }
            catch
            {
                // Cosmetic only: keep message handling safe if Win32 rejects the call.
            }

            if (outputScrollBar != null && !outputScrollBar.IsDisposed)
            {
                bool hasScrollableContent = outputTable.DisplayRectangle.Height > outputTable.ClientSize.Height ||
                    outputTable.Controls.Cast<Control>().Any(control => control.Visible && control.Bottom > outputTable.ClientSize.Height);

                outputScrollBar.Visible = hasScrollableContent;
                outputScrollBar.BringToFront();
                outputScrollBar.Invalidate();
            }
        }

        private static async void CheckNewVersion()
        {
            try
            {
                const string githubOwner = "fresH229a";
                const string githubRepo = "EasyCPDLC-Modernized";

                var client = new GitHubClient(new ProductHeaderValue("EasyCPDLC"));
                var releases = await client.Repository.Release.GetAll(githubOwner, githubRepo);

                var latest = releases
                    .Where(release => !release.Prerelease && !release.Draft)
                    .OrderByDescending(release => release.PublishedAt ?? release.CreatedAt)
                    .FirstOrDefault();

                if (latest == null)
                {
                    return;
                }

                Version latestVersion = ParseReleaseVersion(latest.TagName);
                Version currentVersion = ParseReleaseVersion(System.Windows.Forms.Application.ProductVersion);

                if (latestVersion == null || currentVersion == null)
                {
                    return;
                }

                if (latestVersion > currentVersion)
                {
                    DialogResult updateBox = MessageBox.Show(
                        string.Format(
                            "New Version {0} available on GitHub. You are currently running {1}. Would you like to open the latest release page?",
                            latest.TagName,
                            System.Windows.Forms.Application.ProductVersion),
                        "New Version Available",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Information);

                    if (updateBox == DialogResult.Yes)
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(latest.HtmlUrl) { UseShellExecute = true });
                    }
                }
            }
            catch
            {
                // Update checks must never block the application startup.
            }
        }

        private static Version ParseReleaseVersion(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            string cleaned = value.Trim();

            if (cleaned.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            {
                cleaned = cleaned.Substring(1);
            }

            if (cleaned.StartsWith("cpdlc", StringComparison.OrdinalIgnoreCase))
            {
                cleaned = cleaned.Substring("cpdlc".Length);
            }

            cleaned = cleaned.Trim('-', '_', ' ', '.');

            Match match = Regex.Match(cleaned, @"\d+(?:\.\d+){0,3}");
            if (!match.Success)
            {
                return null;
            }

            string versionText = match.Value;
            int partCount = versionText.Split('.').Length;

            while (partCount < 4)
            {
                versionText += ".0";
                partCount++;
            }

            return Version.TryParse(versionText, out Version version) ? version : null;
        }

        public static void CheckAdministrator()
        {
            if (!new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))
            {
                MessageBox.Show("EasyCPDLC does not appear to be running in Administrator mode. This will limit certain functionalities of the program. Please restart EasyCPDLC in admin mode. The program will now exit.", "Error");
                System.Windows.Forms.Application.Exit();
            }
        }

        private ToolStripMenuItem CreateMenuItem(string name)
        {
            ToolStripMenuItem _temp = new(name)
            {
                BackColor = controlBackColor,
                ForeColor = controlFrontColor,
                Font = controlFont,
                TextAlign = ContentAlignment.TopLeft
            };

            return _temp;
        }
        private void InitialisePopupMenu()
        {
            popupMenu.BackColor = controlBackColor;
            popupMenu.ForeColor = controlFrontColor;
            popupMenu.Font = controlFont;
            popupMenu.ShowImageMargin = false;

            rogerLabel = CreateSpecialLabel("> ROGER", false);
            rogerLabel.Click += (_sender, e) => ReplyMessage(e, previewMessage, "ROGER");
            rogerLabel.KeyDown += (_sender, e) => ReplyMessage(e, previewMessage, "ROGER");

            wilcoLabel = CreateSpecialLabel("> WILCO", false);
            wilcoLabel.Click += (_sender, e) => ReplyMessage(e, previewMessage, "WILCO");
            wilcoLabel.KeyDown += (_sender, e) => ReplyMessage(e, previewMessage, "WILCO");

            standbyLabel = CreateSpecialLabel("> STANDBY", false);
            standbyLabel.Click += (_sender, e) => ReplyMessage(e, previewMessage, "STANDBY");
            standbyLabel.KeyDown += (_sender, e) => ReplyMessage(e, previewMessage, "STANDBY");

            unableLabel = CreateSpecialLabel("> UNABLE", false);
            unableLabel.Click += (_sender, e) => ReplyMessage(e, previewMessage, "UNABLE");
            unableLabel.KeyDown += (_sender, e) => ReplyMessage(e, previewMessage, "UNABLE");

            affirmativeLabel = CreateSpecialLabel("> AFFIRMATIVE", false);
            affirmativeLabel.Click += (_sender, e) => ReplyMessage(e, previewMessage, "AFFIRMATIVE");
            affirmativeLabel.KeyDown += (_sender, e) => ReplyMessage(e, previewMessage, "AFFIRMATIVE");

            negativeLabel = CreateSpecialLabel("> NEGATIVE", false);
            negativeLabel.Click += (_sender, e) => ReplyMessage(e, previewMessage, "NEGATIVE");
            negativeLabel.KeyDown += (_sender, e) => ReplyMessage(e, previewMessage, "NEGATIVE");

            freeTextLabel = CreateSpecialLabel("> FREE TEXT", false);
            freeTextLabel.Click += (_sender, e) => FreeTextMessage(previewMessage);
            freeTextLabel.KeyDown += (_sender, e) => FreeTextMessage(previewMessage);

            deleteLabel = CreateSpecialLabel("> DELETE", false);
            deleteLabel.Click += (_sender, e) => DeleteElement(e, previewMessage);
            deleteLabel.KeyDown += (_sender, e) => DeleteElement(e, previewMessage);

            returnLabel = CreateSpecialLabel("< RETURN", false);
            returnLabel.Click += ReturnMessage;
            returnLabel.KeyDown += ReturnMessage;

            deleteAllMenu = CreateMenuItem("DELETE ALL");
            deleteAllMenu.Click += DeleteAllElement;

            exportLogMenu = CreateMenuItem("EXPORT LOG");
            exportLogMenu.Click += ExportLogElement;

            weatherCacheMenu = CreateMenuItem("WX CACHE");
            weatherCacheMenu.Click += WeatherCacheElement;

            replyOptionsList = new AccessibleLabel[]
            {
                wilcoLabel, rogerLabel, unableLabel, affirmativeLabel, negativeLabel, standbyLabel, freeTextLabel
            };

            Logger.Info("Login menu initialised");
        }

        private TimeSpan FreeTextCooldownRemainingNoLock()
        {
            if (lastFreeTextSentUtc == DateTime.MinValue)
            {
                return TimeSpan.Zero;
            }

            TimeSpan remaining = freeTextCooldown - (DateTime.UtcNow - lastFreeTextSentUtc);
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }

        public TimeSpan GetFreeTextCooldownRemaining()
        {
            lock (freeTextCooldownLock)
            {
                return FreeTextCooldownRemainingNoLock();
            }
        }

        public bool TryReserveFreeTextSlot(out TimeSpan remaining)
        {
            lock (freeTextCooldownLock)
            {
                remaining = FreeTextCooldownRemainingNoLock();

                if (remaining > TimeSpan.Zero)
                {
                    return false;
                }

                lastFreeTextSentUtc = DateTime.UtcNow;
                remaining = TimeSpan.Zero;
                return true;
            }
        }

        public string FormatFreeTextCooldown(TimeSpan remaining)
        {
            int totalSeconds = Math.Max(0, (int)Math.Ceiling(remaining.TotalSeconds));
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;

            return minutes.ToString("00") + ":" + seconds.ToString("00");
        }

        public void NotifyFreeTextCooldown(TimeSpan remaining)
        {
            WriteMessage("FREE TEXT AVAILABLE IN " + FormatFreeTextCooldown(remaining), "SYSTEM", "SYSTEM");
        }

        private void FreeTextMessage(CPDLCMessage message)
        {
            tForm = message == null
                ? new TelexForm(this)
                : new TelexForm(this, message.recipient);

            tForm.TopMost = StayOnTop;
            tForm.Show();
        }

        private void SetSmartWidgetsVisible(bool visible)
        {
            if (messageFilterLabel != null)
            {
                messageFilterLabel.Visible = visible;
            }

            if (onlineStatusLabel != null)
            {
                onlineStatusLabel.Visible = false;
            }

            if (clearanceStatusLabel != null)
            {
                clearanceStatusLabel.Visible = visible;
            }

            if (datalinkStatusLabel != null)
            {
                datalinkStatusLabel.Visible = visible;
            }

            if (atisStatusLabel != null)
            {
                atisStatusLabel.Visible = visible;
            }
        }

        public void ClearPreview()
        {
            if (messageFormatPanel != null)
            {
                messageFormatPanel.Controls.Clear();
                HidePreviewHorizontalScrollbar();
                messageFormatPanel.Visible = false;
            }

            if (outputTable != null)
            {
                outputTable.Visible = true;
            }

            SetSmartWidgetsVisible(true);
            UpdateSmartStatusLabelColors();
            UpdateClearanceStatusLabel();
        }

        private void HidePreviewHorizontalScrollbar()
        {
            if (messageFormatPanel == null || messageFormatPanel.IsDisposed)
            {
                return;
            }

            try
            {
                messageFormatPanel.HorizontalScroll.Enabled = false;
                messageFormatPanel.HorizontalScroll.Visible = false;
                messageFormatPanel.AutoScrollMinSize = new Size(0, 0);

                if (messageFormatPanel.IsHandleCreated)
                {
                    ShowScrollBar(messageFormatPanel.Handle, ScrollBarHorizontal, false);
                }
            }
            catch
            {
                // Cosmetic only.
            }
        }

        private void ReturnMessage(object sender, EventArgs e)
        {
            try
            {
                KeyEventArgs kE = (KeyEventArgs)e;
                if (kE.KeyCode == Keys.Enter || kE.KeyCode == Keys.Space)
                {
                    throw new Exception();
                }
                else
                {
                    return;
                }
            }
            catch
            {
                ClearPreview();
            }
        }
        private void ReplyMessage(EventArgs e, CPDLCMessage message, string reply)
        {
            try
            {
                KeyEventArgs kE = (KeyEventArgs)e;
                if (kE.KeyCode == Keys.Enter || kE.KeyCode == Keys.Space)
                {
                    throw new Exception();
                }
                else
                {
                    return;
                }
            }
            catch
            {
                if (message == null || message.header == null)
                {
                    return;
                }

                foreach (AccessibleLabel _label in replyOptionsList ?? Array.Empty<AccessibleLabel>())
                {
                    if (_label != null)
                    {
                        _label.Enabled = false;
                    }
                }

                message.header.ResponseID = messageOutCounter;

                if (reply != "STANDBY")
                {
                    message.acknowledged = true;
                    int index = outputTable.Controls.GetChildIndex(message);
                    ((TimerLabel)outputTable.Controls[index + 1]).CanFlash = false;
                    outputTable.Controls[index + 1].ForeColor = controlFrontColor;
                    message.ForeColor = SystemColors.ControlDark;
                }

                TrackClearanceReply(message, reply);
                _ = Task.Run(() => SendCPDLCMessage(message.recipient, message.type, String.Format("/data2/{0}/{1}/N/{2}", message.header.ResponseID, message.header.MessageID, reply)));
                messageOutCounter += 1;
                ClearPreview();

                foreach (AccessibleLabel _label in replyOptionsList ?? Array.Empty<AccessibleLabel>())
                {
                    if (_label != null)
                    {
                        _label.Enabled = true;
                    }
                }
            }
        }
        private void ShowSetupForm()
        {

            Logger.Info("Login Form Displayed");

            DataEntry dataEntry = new(SavedHoppieCode == String.Empty ? null : SavedHoppieCode, SavedCID == new int() ? null : global::EasyCPDLC.MainForm.SavedCID);

            if (dataEntry.ShowDialog(this) == DialogResult.OK)
            {
                logonCode = dataEntry.HoppieLogonCode;
                cid = dataEntry.VatsimCID;
                if (dataEntry.Remember)
                {
                    Logger.Info("REMEMBER ME: TRUE. REGISTRY SET.");
                    SavedHoppieCode = logonCode;
                    SavedCID = cid;
                }
                else
                {
                    SavedCID = new int();
                    SavedHoppieCode = String.Empty;
                }
            }
            else
            {
                Logger.Info("Goodbye");
                LogManager.Shutdown();
                FSUIPCData.CloseConnection();
                System.Windows.Forms.Application.Exit();
            }
        }
        private void Setup()
        {
            retrieveButton.Enabled = true;
            Logger.Info("Setup Complete.");
        }
        private async Task PeriodicCheckMessage(TimeSpan interval, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                Logger.Debug("Attempting to poll Hoppie for new messages");

                await SendCPDLCMessage("NONE", "poll", "");

                try
                {
                    await Task.Delay(interval, cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }

                if (UseFSUIPC && fsConnectionOpen)
                {
                    try
                    {
                        await fsuipc.RefreshData();
                        fsuipcErrorCount = 1;
                    }
                    catch (FSUIPCException)
                    {
                        if (fsuipcErrorCount <= 3)
                        {
                            try
                            {
                                fsConnectionOpen = FSUIPCData.OpenConnection();
                            }
                            catch { }
                            WriteMessage(String.Format("UNABLE TO CHECK FLIGHT SIM DATA. RETRYING (ATTEMPT {0} OF 3)", fsuipcErrorCount), "SYSTEM", "SYSTEM");
                            fsuipcErrorCount += 1;
                        }
                        else
                        {
                            WriteMessage("FLIGHT SIM DATA RETRIEVAL FAILED 3 TIMES CONSECUTIVELY. DISCONNECTING FROM FLIGHT SIM", "SYSTEM", "SYSTEM");
                            fsConnectionOpen = FSUIPCData.CloseConnection();
                            fsuipcErrorCount = 1;
                        }

                    }
                }


            }
        }
        private void SafeUi(Action action)
        {
            if (action == null || IsDisposed)
            {
                return;
            }

            try
            {
                if (IsHandleCreated && InvokeRequired)
                {
                    BeginInvoke(action);
                }
                else
                {
                    action();
                }
            }
            catch (ObjectDisposedException)
            {
                // The form is closing.
            }
            catch (InvalidOperationException)
            {
                // The form is closing or its handle is no longer available.
            }
        }

        private void UpdateSendingProgress(Action action)
        {
            SafeUi(() =>
            {
                if (SendingProgress != null && !SendingProgress.IsDisposed)
                {
                    action();
                }
            });
        }

        public async Task SendCPDLCMessage(string recipient, string messageType, string packetData, bool _write = true)
        {

            var connectionValues = new Dictionary<string, string> {
                {"logon", logonCode ?? String.Empty},
                {"from", callsign ?? String.Empty},
                {"to", recipient ?? String.Empty},
                {"type", messageType ?? String.Empty},
                {"packet", packetData ?? String.Empty}
            };

            var content = new FormUrlEncodedContent(connectionValues);
            try
            {

                if (_write && messageType != "poll")
                {
                    UpdateSendingProgress(() => SendingProgress.Visible = true);
                    UpdateSendingProgress(() => SendingProgress.Value = 0);
                    UpdateSendingProgress(() => SendingProgress.PerformStep());
                }

                var response = await webclient.PostAsync(HoppieConnectUrl, content);

                UpdateSendingProgress(() => SendingProgress.PerformStep());

                Logger.Debug(String.Format("PACKET SENT: {0} | {1} | {2} | {3} | {4}", recipient, messageType, packetData, true, _write));
                var responseString = await response.Content.ReadAsStringAsync();
                string printString = responseString.ToString().ToUpper().Trim();
                Logger.Debug("RECEIVED: " + responseString);

                if (printString.Contains("ERROR"))
                {
                    throw new HttpRequestException();
                }
                else
                {
                    if (isErrorState)
                    {
                        WriteMessage("HOPPIE CONNECTIVITY RESTORED.", "SYSTEM", "SYSTEM");
                        isErrorState = false;
                    }

                    UpdateSendingProgress(() => SendingProgress.PerformStep());

                    if (_write && messageType != "poll")
                    {
                        WriteMessage(messageType == "CPDLC" ? packetData.Split('/').Last() : packetData, messageType, recipient, true);
                    }
                }

                if (printString != "OK")
                {
                    await TelexParser(printString);
                }

                UpdateSendingProgress(() => SendingProgress.Visible = false);
            }

            catch (Exception e)
            {
                if (!isErrorState)
                {
                    Logger.Error(String.Format("{0}: {1}", e.GetType().FullName, e.Message));
                    WriteMessage("ERROR CHECKING FOR NEW MESSAGES. THIS IS LIKELY AN ERROR WITH THE HOPPIE NETWORK. THE SYSTEM WILL CONTINUE ATTEMPTING TO CONTACT THE SERVER AND LET YOU KNOW WHEN CONNECTION IS RE-ESTABLISHED.", "SYSTEM", "SYSTEM");
                    isErrorState = true;
                }
                UpdateSendingProgress(() => SendingProgress.Visible = false);
            }

            return;

        }
        private string CreateMessageListText(string contents, string type, string recipient, bool outbound)
        {
            string normalizedType = (type ?? string.Empty).Trim().ToUpperInvariant();

            if (normalizedType == "SYSTEM")
            {
                return "SYSTEM MESSAGE";
            }

            if (ShouldUseAtisListText(contents, normalizedType, recipient))
            {
                string target = GetAtisListTarget(contents, recipient, !outbound);
                string upperContents = (contents ?? string.Empty).ToUpperInvariant();

                if (outbound)
                {
                    return "REQUESTING ATIS FOR " + target;
                }

                if (upperContents.Contains("NOT AVAILABLE"))
                {
                    return BuildAtisListSummary(target, "NOT AVAILABLE");
                }

                string atisLetter = GetAtisOverviewLetter(contents, target);
                string atisStatus = string.IsNullOrWhiteSpace(atisLetter)
                    ? "? RECEIVED"
                    : atisLetter + " RECEIVED";
                string atisSummary = atisStatus + BuildWeatherOverviewSuffix(contents, 26, false);

                CacheWeatherMessage("ATIS", target, contents, atisSummary);
                return BuildAtisListSummary(target, atisSummary);
            }

            if (ShouldUseMetarListText(contents, normalizedType, recipient))
            {
                string target = GetMetarListTarget(contents, recipient, !outbound);
                string upperContents = (contents ?? string.Empty).ToUpperInvariant();

                if (outbound)
                {
                    return "REQUESTING METAR FOR " + target;
                }

                if (upperContents.Contains("NOT AVAILABLE") ||
                    upperContents.Contains("NO METAR") ||
                    upperContents.Contains("METAR NOT"))
                {
                    return BuildMetarListSummary(target, "NOT AVAILABLE");
                }

                string metarSummary = "RECEIVED" + BuildWeatherOverviewSuffix(contents, 30, true);
                CacheWeatherMessage("METAR", target, contents, metarSummary);
                return BuildMetarListSummary(target, metarSummary);
            }

            return outbound
                ? string.Format("{1} MESSAGE TO {0}", recipient, normalizedType)
                : string.Format("{1} MESSAGE FROM {0}", recipient, normalizedType);
        }

        private bool ShouldUseAtisListText(string contents, string normalizedType, string recipient)
        {
            if (normalizedType == "ATIS")
            {
                return true;
            }

            string upperContents = (contents ?? string.Empty).ToUpperInvariant();
            string upperRecipient = (recipient ?? string.Empty).ToUpperInvariant();

            if (upperRecipient == "VATATIS")
            {
                return true;
            }

            if (upperContents.Contains("VATATIS") ||
                upperContents.Contains("_ATIS") ||
                Regex.IsMatch(upperContents, @"\b[A-Z0-9]{4}_[AD]\b"))
            {
                return true;
            }

            // Generic VATATIS replies can arrive as INFO/ACARS and may be very generic.
            // If an ATIS request is pending and no METAR request is pending, treat the next
            // ACARS/INFO response as ATIS. This fixes "INFO MESSAGE FROM ACARS" after ATIS requests.
            if (upperRecipient == "ACARS" && HasPendingAtisRequest())
            {
                if (!HasPendingMetarRequest())
                {
                    return true;
                }

                return LooksLikeGenericAtisInformation(upperContents) || upperContents.Contains("ATIS");
            }

            // A CPDLC clearance can also contain "ATIS C", so only map generic text
            // to ATIS when an ATIS request is actually pending.
            return LooksLikeGenericAtisInformation(upperContents) && HasPendingAtisRequest();
        }

        private bool ShouldUseMetarListText(string contents, string normalizedType, string recipient)
        {
            if (normalizedType == "METAR")
            {
                return true;
            }

            string upperContents = (contents ?? string.Empty).ToUpperInvariant();
            string upperRecipient = (recipient ?? string.Empty).ToUpperInvariant();

            if (upperContents.Contains("METAR ") ||
                upperContents.StartsWith("METAR") ||
                upperContents.StartsWith("SPECI") ||
                Regex.IsMatch(upperContents, @"\b[A-Z][A-Z0-9]{3}\s+\d{6}Z\b"))
            {
                return true;
            }

            // METAR replies may also arrive as generic "INFO FROM ACARS".
            // If a METAR request is pending, map the next ACARS info reply to it.
            return upperRecipient == "ACARS" && HasPendingMetarRequest();
        }

        private void RememberMetarRequestTarget(string target)
        {
            string formatted = FormatAtisTargetForList(target);
            if (formatted == "ATIS")
            {
                return;
            }

            lock (pendingMetarRequestLock)
            {
                pendingMetarRequestTargets.Enqueue(formatted);

                while (pendingMetarRequestTargets.Count > 8)
                {
                    pendingMetarRequestTargets.Dequeue();
                }
            }
        }

        private bool HasPendingMetarRequest()
        {
            lock (pendingMetarRequestLock)
            {
                return pendingMetarRequestTargets.Count > 0;
            }
        }

        private string GetPendingMetarRequestTarget(bool consume)
        {
            lock (pendingMetarRequestLock)
            {
                if (pendingMetarRequestTargets.Count == 0)
                {
                    return "METAR";
                }

                return consume
                    ? pendingMetarRequestTargets.Dequeue()
                    : pendingMetarRequestTargets.Peek();
            }
        }

        private string GetMetarListTarget(string contents, string recipient, bool consumePending)
        {
            string combined = ((contents ?? string.Empty) + "\n" + (recipient ?? string.Empty)).ToUpperInvariant();

            Match explicitMetar = Regex.Match(combined, @"\b(?:METAR|SPECI)\s+([A-Z][A-Z0-9]{3})\b");
            if (explicitMetar.Success && IsAirportCodeToken(explicitMetar.Groups[1].Value))
            {
                return explicitMetar.Groups[1].Value.ToUpperInvariant();
            }

            Match timedMetar = Regex.Match(combined, @"\b([A-Z][A-Z0-9]{3})\s+\d{6}Z\b");
            if (timedMetar.Success && IsAirportCodeToken(timedMetar.Groups[1].Value))
            {
                return timedMetar.Groups[1].Value.ToUpperInvariant();
            }

            MatchCollection matches = Regex.Matches(combined, @"\b[A-Z][A-Z0-9]{3}\b");
            foreach (Match match in matches)
            {
                string candidate = match.Value.Trim().ToUpperInvariant();

                if (IsAirportCodeToken(candidate))
                {
                    return candidate;
                }
            }

            return GetPendingMetarRequestTarget(consumePending);
        }

        private static bool IsAirportCodeToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            string upper = token.Trim().ToUpperInvariant();

            string[] reserved =
            {
                "METAR", "SPECI", "AUTO", "CORR", "CAVOK", "NOSIG",
                "TEMPO", "BECMG", "PROB", "WIND", "RWYS", "RWY",
                "QNH", "INFO", "FROM", "ACARS", "DATA", "LINK",
                "ATIS", "THIS", "WITH", "TEXT", "NIL"
            };

            if (reserved.Contains(upper))
            {
                return false;
            }

            return Regex.IsMatch(upper, @"^[A-Z][A-Z0-9]{3}$");
        }

        private static string BuildMetarListSummary(string target, string status)
        {
            string cleanTarget = FormatAtisTargetForList(target);
            string cleanStatus = (status ?? string.Empty).Trim().ToUpperInvariant();

            return cleanTarget == "ATIS" || cleanTarget == "METAR"
                ? "METAR " + cleanStatus
                : cleanTarget + " METAR " + cleanStatus;
        }

        private void CacheWeatherMessage(string type, string target, string contents, string summary)
        {
            string cleanType = string.IsNullOrWhiteSpace(type) ? "WX" : type.ToUpperInvariant();
            string cleanTarget = FormatAtisTargetForList(target);

            if (cleanTarget == "ATIS" || cleanTarget == "METAR")
            {
                return;
            }

            string key = cleanType + ":" + cleanTarget;
            weatherCache[key] = new WeatherCacheItem
            {
                Type = cleanType,
                Target = cleanTarget,
                Summary = string.IsNullOrWhiteSpace(summary) ? "RECEIVED" : summary.Trim().ToUpperInvariant(),
                Contents = contents ?? string.Empty,
                TimestampUtc = DateTime.UtcNow
            };
        }

        private static string BuildWeatherOverviewSuffix(string contents, int maxChars, bool includeWind)
        {
            string summary = BuildWeatherOverviewSummary(contents, includeWind);

            if (string.IsNullOrWhiteSpace(summary))
            {
                return string.Empty;
            }

            summary = summary.Trim().ToUpperInvariant();

            if (summary.Length > maxChars)
            {
                summary = summary.Substring(0, maxChars).TrimEnd();
            }

            return " " + summary;
        }

        private static string BuildWeatherOverviewSummary(string contents, bool includeWind)
        {
            if (string.IsNullOrWhiteSpace(contents))
            {
                return string.Empty;
            }

            string upper = contents
                .Replace("\r\n", " ")
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Replace("\t", " ")
                .ToUpperInvariant();

            upper = Regex.Replace(upper, @"\s+", " ").Trim();

            List<string> parts = new();

            Match qnh = Regex.Match(upper, @"\bQNH\s*([0-9]{4})\b");
            if (qnh.Success)
            {
                parts.Add("QNH " + qnh.Groups[1].Value);
            }
            else
            {
                Match altimeter = Regex.Match(upper, @"\bA([0-9]{4})\b");
                if (altimeter.Success)
                {
                    parts.Add("A" + altimeter.Groups[1].Value);
                }
            }

            if (includeWind)
            {
                Match wind = Regex.Match(upper, @"\b((?:[0-3][0-9]{2}|VRB)[0-9]{2,3}(?:G[0-9]{2,3})?KT)\b");
                if (wind.Success)
                {
                    parts.Add("WIND " + NormalizeWindForOverview(wind.Groups[1].Value));
                }
                else
                {
                    Match spokenWind = Regex.Match(upper, @"\bWIND\s+((?:[0-3][0-9]{2}|VRB)[0-9]{2,3}(?:G[0-9]{2,3})?)\b");
                    if (spokenWind.Success)
                    {
                        parts.Add("WIND " + NormalizeWindForOverview(spokenWind.Groups[1].Value));
                    }
                }
            }

            Match rwy = Regex.Match(upper, @"\b(?:DEP\s+RWYS?|ARR\s+RWYS?|RWYS?|RWY)\s+([0-9]{2}[LCR]?(?:\s*(?:AND|/|,)\s*[0-9]{2}[LCR]?)?)\b");
            if (rwy.Success)
            {
                parts.Add("RWY " + Regex.Replace(rwy.Groups[1].Value, @"\s+", " "));
            }

            if (parts.Count == 0)
            {
                return string.Empty;
            }

            return string.Join(" ", parts.Take(3));
        }

        private static string NormalizeWindForOverview(string wind)
        {
            if (string.IsNullOrWhiteSpace(wind))
            {
                return string.Empty;
            }

            string clean = wind.Trim().ToUpperInvariant();

            if (clean.EndsWith("KT", StringComparison.OrdinalIgnoreCase))
            {
                clean = clean.Substring(0, clean.Length - 2);
            }

            Match match = Regex.Match(clean, @"^((?:[0-3][0-9]{2}|VRB))([0-9]{2,3})(G[0-9]{2,3})?$");
            if (!match.Success)
            {
                return clean;
            }

            string direction = match.Groups[1].Value;
            string speed = match.Groups[2].Value.TrimStart('0');
            if (speed.Length == 0)
            {
                speed = "0";
            }

            string gust = match.Groups[3].Success ? match.Groups[3].Value : string.Empty;
            return direction + "/" + speed + gust;
        }

        private void RememberAtisRequestTarget(string target)
        {
            string formatted = FormatAtisTargetForList(target);
            if (formatted == "ATIS")
            {
                return;
            }

            lock (pendingAtisRequestLock)
            {
                // VATATIS/ACARS replies often do not include the ICAO.
                // Keep the request order so the next generic reply can be matched
                // to the next pending ATIS request.
                pendingAtisRequestTargets.Enqueue(formatted);

                while (pendingAtisRequestTargets.Count > 8)
                {
                    pendingAtisRequestTargets.Dequeue();
                }
            }
        }

        private bool HasPendingAtisRequest()
        {
            lock (pendingAtisRequestLock)
            {
                return pendingAtisRequestTargets.Count > 0;
            }
        }

        private string GetPendingAtisRequestTarget(bool consume)
        {
            lock (pendingAtisRequestLock)
            {
                if (pendingAtisRequestTargets.Count == 0)
                {
                    return "ATIS";
                }

                return consume
                    ? pendingAtisRequestTargets.Dequeue()
                    : pendingAtisRequestTargets.Peek();
            }
        }

        private string GetAtisListTarget(string contents, string recipient, bool consumePending)
        {
            string contentsUpper = (contents ?? string.Empty).ToUpperInvariant();
            string recipientUpper = (recipient ?? string.Empty).ToUpperInvariant();
            string combined = (contentsUpper + "\n" + recipientUpper).ToUpperInvariant();

            string target = ExtractAtisTarget(combined);
            if (target != "ATIS")
            {
                return FormatAtisTargetForList(target);
            }

            // Generic VATATIS replies can look like:
            // "THIS IS GENEVA INFORMATION ECHO ..."
            // In that case words like ECHO/DELTA must NOT be treated as the airport.
            // Use the last ATIS request target first.
            if (LooksLikeGenericAtisInformation(contentsUpper))
            {
                string pendingTarget = GetPendingAtisRequestTarget(consumePending);
                if (pendingTarget != "ATIS")
                {
                    return pendingTarget;
                }
            }

            MatchCollection matches = Regex.Matches(combined, @"\b[A-Z0-9]{4}(?:_[AD])?(?:_ATIS)?\b");
            foreach (Match match in matches)
            {
                string candidate = match.Value.Trim('_', ' ', '\r', '\n', '\t');

                if (IsAtisTargetToken(candidate))
                {
                    return FormatAtisTargetForList(candidate);
                }
            }

            return GetPendingAtisRequestTarget(consumePending);
        }

        private static bool LooksLikeGenericAtisInformation(string upperContents)
        {
            if (string.IsNullOrWhiteSpace(upperContents))
            {
                return false;
            }

            return upperContents.Contains("THIS IS ") &&
                   upperContents.Contains(" INFORMATION ");
        }

        private static bool IsAtisTargetToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            string upper = token.Trim().ToUpperInvariant();

            string[] reserved =
            {
                "ATIS", "THIS", "FROM", "INFO", "WIND", "QNH", "CAVOK", "RWYS",
                "RWY", "DUE", "FOR", "NOT", "WITH", "TEXT", "DATA", "LINK",
                "ILS", "VOR", "NDB", "DME", "GPS", "RNP", "RNAV",
                "ALPHA", "BRAVO", "CHARLIE", "DELTA", "ECHO", "FOXTROT",
                "GOLF", "HOTEL", "INDIA", "JULIET", "KILO", "LIMA", "MIKE",
                "NOVEMBER", "OSCAR", "PAPA", "QUEBEC", "ROMEO", "SIERRA",
                "TANGO", "UNIFORM", "VICTOR", "WHISKEY", "XRAY", "X-RAY",
                "YANKEE", "ZULU"
            };

            if (reserved.Contains(upper))
            {
                return false;
            }

            if (!Regex.IsMatch(upper, @"^[A-Z][A-Z0-9]{3}(?:_[AD])?(?:_ATIS)?$"))
            {
                return false;
            }

            return upper.Take(4).Any(char.IsLetter);
        }

        private static string FormatAtisTargetForList(string target)
        {
            if (string.IsNullOrWhiteSpace(target))
            {
                return "ATIS";
            }

            string clean = target.Trim().Trim('_').ToUpperInvariant();

            if (clean.EndsWith("_ATIS", StringComparison.OrdinalIgnoreCase))
            {
                clean = clean.Substring(0, clean.Length - "_ATIS".Length);
            }

            // For the message overview, show only the airport ICAO.
            // Requests may internally use XXXX_A / XXXX_D for split ATIS,
            // but the list should stay clean: LOWW ATIS..., not LOWW_A ATIS...
            if (clean.EndsWith("_A", StringComparison.OrdinalIgnoreCase) ||
                clean.EndsWith("_D", StringComparison.OrdinalIgnoreCase))
            {
                clean = clean.Substring(0, clean.Length - 2);
            }

            return string.IsNullOrWhiteSpace(clean) ? "ATIS" : clean;
        }

        private static string BuildAtisListSummary(string target, string status)
        {
            string cleanTarget = FormatAtisTargetForList(target);
            string cleanStatus = (status ?? string.Empty).Trim().ToUpperInvariant();

            return cleanTarget == "ATIS"
                ? "ATIS " + cleanStatus
                : cleanTarget + " ATIS " + cleanStatus;
        }

        private string GetAtisOverviewLetter(string contents, string target)
        {
            string letter = ExtractAtisInformationLetter(contents);

            if (!string.IsNullOrWhiteSpace(letter))
            {
                return letter;
            }

            return ExtractAtisLetterFromVatsimData(target);
        }

        private string ExtractAtisLetterFromVatsimData(string target)
        {
            if (vatsimData?.atis == null || string.IsNullOrWhiteSpace(target))
            {
                return string.Empty;
            }

            string cleanTarget = FormatAtisTargetForList(target);
            if (cleanTarget == "ATIS" || cleanTarget == "METAR")
            {
                return string.Empty;
            }

            string exactGeneric = cleanTarget + "_ATIS";

            Atis[] candidates = vatsimData.atis
                .Where(atis => !string.IsNullOrWhiteSpace(atis.callsign))
                .OrderBy(atis => string.Equals(atis.callsign, exactGeneric, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(atis => atis.callsign)
                .ToArray();

            foreach (Atis atis in candidates)
            {
                string callsign = atis.callsign.ToUpperInvariant();

                if (!string.Equals(callsign, exactGeneric, StringComparison.Ordinal) &&
                    !callsign.StartsWith(cleanTarget + "_", StringComparison.Ordinal))
                {
                    continue;
                }

                string code = (atis.atis_code ?? string.Empty).Trim().ToUpperInvariant();
                if (code.Length > 0 && char.IsLetter(code[0]))
                {
                    return code[0].ToString();
                }

                if (atis.text_atis != null && atis.text_atis.Length > 0)
                {
                    string fromText = ExtractAtisInformationLetter(string.Join(" ", atis.text_atis));
                    if (!string.IsNullOrWhiteSpace(fromText))
                    {
                        return fromText;
                    }
                }
            }

            return string.Empty;
        }

        private static string ExtractAtisInformationLetter(string contents)
        {
            if (string.IsNullOrWhiteSpace(contents))
            {
                return string.Empty;
            }

            string upper = contents.ToUpperInvariant();

            const string natoOrLetter = "ALPHA|BRAVO|CHARLIE|DELTA|ECHO|FOXTROT|GOLF|HOTEL|INDIA|JULIET|KILO|LIMA|MIKE|NOVEMBER|OSCAR|PAPA|QUEBEC|ROMEO|SIERRA|TANGO|UNIFORM|VICTOR|WHISKEY|XRAY|X-RAY|YANKEE|ZULU|[A-Z]";

            Match match = Regex.Match(
                upper,
                @"\b(?:INFORMATION|INFO)\s*[:\-]?\s*(" + natoOrLetter + @")\b");

            if (!match.Success)
            {
                match = Regex.Match(upper, @"\bATIS\s+(" + natoOrLetter + @")\s+(?:RECEIVED|AT|VALID|QNH|RWY|RUNWAY)\b");
            }

            if (!match.Success)
            {
                return string.Empty;
            }

            string value = match.Groups[1].Value.Replace("-", string.Empty);

            return value.Length == 1 ? value : value[0].ToString();
        }

        private bool ShouldEmphasizeAtisLetter(CPDLCMessage message)
        {
            if (message == null || message.outbound)
            {
                return false;
            }

            return string.Equals(message.type, "ATIS", StringComparison.OrdinalIgnoreCase) &&
                Regex.IsMatch(message.Text ?? string.Empty, @"\bATIS\s+[A-Z?]\s+RECEIVED\b");
        }

        private CPDLCMessage CreateCPDLCMessage(string _contents, string _type, string _recipient, bool _outbound = false, CPDLCResponse _header = null)
        {
            string previewType = (_type ?? string.Empty).Trim().ToUpperInvariant();
            bool emphasizeAtisLetter = !_outbound &&
                (previewType == "ATIS" || string.Equals((_recipient ?? string.Empty).Trim(), "VATATIS", StringComparison.OrdinalIgnoreCase)) &&
                Regex.IsMatch(CreateMessageListText(_contents, _type, _recipient, _outbound), @"\bATIS\s+[A-Z?]\s+RECEIVED\b");

            CPDLCMessage _message = new(_type, _recipient, _contents, _outbound, _header)
            {
                AutoSize = true,
                BackColor = Color.Transparent,
                ForeColor = MainPrimaryTextColor(),
                Font = emphasizeAtisLetter ? textFontBold : textFont,
                Text = CreateMessageListText(_contents, _type, _recipient, _outbound),
                BorderStyle = BorderStyle.None,
                TabStop = true,
                TabIndex = 0,
                Margin = new Padding(0, 3, 0, 0)
            };

            return _message;
        }

        private AccessibleLabel CreateLabel(string _text, bool _useMaxSize = true)
        {
            Size maxSize = new()
            {
                Width = 65
            };

            AccessibleLabel _message = new(MainPrimaryTextColor())
            {
                Width = 65,
                AutoSize = true,
                BackColor = Color.Transparent,
                ForeColor = MainPrimaryTextColor(),
                Font = textFont,
                Text = _text,
                BorderStyle = BorderStyle.None,
                Margin = new Padding(5, 3, 0, 0),
                TabStop = true,
                TabIndex = 0
            };

            if (_useMaxSize)
            {
                _message.MaximumSize = maxSize;
            }
            else
            {
                int previewWidth = Math.Max(240, (messageFormatPanel?.ClientSize.Width ?? 430) - 54);
                _message.Width = previewWidth;
                _message.MaximumSize = new Size(previewWidth, 0);
            }

            SetStyle(ControlStyles.Selectable, true);
            return _message;
        }

        private AccessibleLabel CreateSpecialLabel(string _text, bool _useMaxSize = true)
        {
            string displayText = NormalizeActionButtonText(_text);
            Color buttonColor = ActionButtonColor(displayText);
            Size buttonSize = ActionButtonSize(displayText);

            AccessibleLabel _message = new(buttonColor)
            {
                AutoSize = false,
                BackColor = Color.Transparent,
                ForeColor = buttonColor,
                Font = new Font(textFontBold.FontFamily, Math.Max(8.2f, textFontBold.Size - 1.7f), FontStyle.Bold),
                Text = displayText,
                BorderStyle = BorderStyle.None,
                TextAlign = ContentAlignment.MiddleCenter,
                Padding = new Padding(7, 0, 7, 1),
                Margin = new Padding(4, 5, 3, 2),
                TabStop = true,
                TabIndex = 0,
                Cursor = Cursors.Hand,
                Size = buttonSize,
                MinimumSize = buttonSize
            };

            _message.Paint += ActionButton_Paint;
            _message.MouseEnter += (_, __) =>
            {
                _message.ForeColor = Color.White;
                _message.Invalidate();
            };
            _message.MouseLeave += (_, __) =>
            {
                _message.ForeColor = buttonColor;
                _message.Invalidate();
            };
            _message.MouseDown += (_, __) =>
            {
                _message.Tag = "PRESSED";
                _message.Invalidate();
            };
            _message.MouseUp += (_, __) =>
            {
                _message.Tag = null;
                _message.Invalidate();
            };

            return _message;
        }

        private string NormalizeActionButtonText(string text)
        {
            string cleaned = (text ?? string.Empty)
                .Replace(">", string.Empty)
                .Replace("<", string.Empty)
                .Trim()
                .ToUpperInvariant();

            return cleaned switch
            {
                "FREE TEXT" => "FREE TXT",
                "AFFIRMATIVE" => "AFFIRM",
                "NEGATIVE" => "NEG",
                _ => cleaned
            };
        }

        private Size ActionButtonSize(string text)
        {
            string displayText = string.IsNullOrWhiteSpace(text) ? "ACTION" : text.Trim().ToUpperInvariant();
            Size measured = TextRenderer.MeasureText(displayText, textFontBold);
            int width = Math.Max(58, measured.Width + 22);

            if (displayText.Contains("STANDBY"))
            {
                width = Math.Max(width, 78);
            }
            else if (displayText.Contains("FREE"))
            {
                width = Math.Max(width, 78);
            }
            else if (displayText.Contains("AFFIRM"))
            {
                width = Math.Max(width, 74);
            }

            return new Size(width, 22);
        }

        private Color ActionButtonColor(string text)
        {
            string upper = (text ?? string.Empty).ToUpperInvariant();

            if (upper.Contains("ACCEPT") ||
                upper.Contains("WILCO") ||
                upper.Contains("ROGER") ||
                upper.Contains("AFFIRM"))
            {
                return DcduTheme.Green;
            }

            if (upper.Contains("REJECT") ||
                upper.Contains("UNABLE") ||
                upper == "NEG" ||
                upper.Contains("DELETE"))
            {
                return DcduTheme.Amber;
            }

            if (upper.Contains("STANDBY"))
            {
                return DcduTheme.Amber;
            }

            return MainAccentColor();
        }

        private void ActionButton_Paint(object sender, PaintEventArgs e)
        {
            if (sender is not System.Windows.Forms.Label label)
            {
                return;
            }

            Rectangle bounds = new Rectangle(0, 0, Math.Max(1, label.Width - 1), Math.Max(1, label.Height - 1));
            Color accent = ActionButtonColor(label.Text);
            bool focused = label.Focused;
            bool pressed = string.Equals(label.Tag as string, "PRESSED", StringComparison.OrdinalIgnoreCase);

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

            Color fillTop = pressed
                ? Color.FromArgb(70, accent)
                : Color.FromArgb(focused ? 52 : 30, accent);
            Color fillBottom = pressed
                ? Color.FromArgb(40, accent)
                : Color.FromArgb(focused ? 24 : 14, accent);

            using GraphicsPath path = RoundedButtonRect(bounds, 4);
            using LinearGradientBrush fill = new LinearGradientBrush(bounds, fillTop, fillBottom, LinearGradientMode.Vertical);
            using Pen glow = new Pen(Color.FromArgb(focused ? 190 : 118, accent), focused ? 1.4f : 1.0f);
            using Pen inner = new Pen(Color.FromArgb(pressed ? 35 : 60, Color.White), 1.0f);

            e.Graphics.FillPath(fill, path);
            e.Graphics.DrawPath(glow, path);
            e.Graphics.DrawLine(inner, bounds.Left + 5, bounds.Top + 1, bounds.Right - 5, bounds.Top + 1);

            Rectangle textRect = Rectangle.Inflate(bounds, -4, -2);
            TextRenderer.DrawText(
                e.Graphics,
                label.Text,
                label.Font,
                pressed ? new Rectangle(textRect.X + 1, textRect.Y + 1, textRect.Width, textRect.Height) : textRect,
                label.Focused ? Color.White : label.ForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        private static GraphicsPath RoundedButtonRect(Rectangle bounds, int radius)
        {
            int diameter = Math.Max(2, radius * 2);
            Rectangle arc = new Rectangle(bounds.Location, new Size(diameter, diameter));
            GraphicsPath path = new();

            path.AddArc(arc, 180, 90);
            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = bounds.Left;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();

            return path;
        }

        private TimerLabel CreateTimerLabel(string _text, bool _useMaxSize = true)
        {
            Size maxSize = new()
            {
                Width = 65
            };
            TimerLabel _message = new()
            {
                Width = 65,
                AutoSize = true,
                BackColor = Color.Transparent,
                ForeColor = MainPrimaryTextColor(),
                Font = textFontBold,
                Text = _text,
                BorderStyle = BorderStyle.None,
                Margin = new Padding(0, 0, 2, 0),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                TextAlign = ContentAlignment.TopRight,
            };
            if (_useMaxSize)
            {
                _message.MaximumSize = maxSize;
            }
            return _message;
        }
        private void DeleteElement(EventArgs e, CPDLCMessage control)
        {
            try
            {
                KeyEventArgs kE = (KeyEventArgs)e;
                if (kE.KeyCode == Keys.Enter || kE.KeyCode == Keys.Space)
                {
                    throw new Exception();
                }
                else
                {
                    return;
                }
            }
            catch
            {
                MarkMessageRead(control);
                TableLayoutHelper.RemoveArbitraryRow(outputTable, outputTable.GetPositionFromControl(control).Row);
                ClearPreview();
                ApplyMessageFilter();
            }
        }

        private void DeleteAllElement(object sender, EventArgs e)
        {
            ClearUnreadMessages();
            outputTable.Controls.Clear();
            UpdateMessageFilterLabel();
        }

        private void ExportLogElement(object sender, EventArgs e)
        {
            try
            {
                string path = ExportMessageLog();
                WriteMessage("MESSAGE LOG EXPORTED\n" + path, "SYSTEM", "SYSTEM");
            }
            catch (Exception ex)
            {
                Logger.Error("Unable to export message log: " + ex.Message);
                WriteMessage("UNABLE TO EXPORT MESSAGE LOG", "SYSTEM", "SYSTEM");
            }
        }

        private string ExportMessageLog()
        {
            string filename = "EasyCPDLC_MessageLog_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt";
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, filename);

            List<CPDLCMessage> messages = outputTable == null || outputTable.IsDisposed
                ? new List<CPDLCMessage>()
                : outputTable.Controls.OfType<CPDLCMessage>().ToList();

            messages = messages
                .OrderBy(message => outputTable.GetPositionFromControl(message).Row)
                .ToList();

            using StreamWriter writer = new StreamWriter(path, false);

            writer.WriteLine("EasyCPDLC Message Export");
            writer.WriteLine("Created: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            writer.WriteLine();

            foreach (CPDLCMessage message in messages)
            {
                int row = outputTable.GetPositionFromControl(message).Row;
                string time = string.Empty;

                if (outputTable.GetControlFromPosition(0, row) is System.Windows.Forms.Label timeLabel)
                {
                    time = timeLabel.Text;
                }

                writer.WriteLine("[" + time + "] " + message.Text);
                writer.WriteLine("TYPE: " + message.type + "  " + (message.outbound ? "OUTBOUND" : "INBOUND") + "  STATION: " + message.recipient);
                writer.WriteLine(message.message ?? string.Empty);
                writer.WriteLine(new string('-', 72));
            }

            return path;
        }

        private void WeatherCacheElement(object sender, EventArgs e)
        {
            WriteMessage(BuildWeatherCacheMessage(), "SYSTEM", "SYSTEM");
        }

        private string BuildWeatherCacheMessage()
        {
            if (weatherCache.Count == 0)
            {
                return "WX CACHE EMPTY";
            }

            List<string> lines = new()
            {
                "WX CACHE"
            };

            foreach (WeatherCacheItem item in weatherCache.Values.OrderByDescending(item => item.TimestampUtc).Take(8))
            {
                lines.Add(item.TimestampUtc.ToLocalTime().ToString("HH:mm") + " " +
                    item.Target + " " + item.Type + " " + item.Summary);
            }

            return string.Join("\n", lines);
        }

        private string GetPreviewMessageText(CPDLCMessage message)
        {
            if (message == null)
            {
                return string.Empty;
            }

            string text = message.message ?? string.Empty;

            if (string.Equals(message.type, "ATIS", StringComparison.OrdinalIgnoreCase) ||
                text.IndexOf(" ATIS ", StringComparison.OrdinalIgnoreCase) >= 0 ||
                Regex.IsMatch(text, @"\b[A-Z0-9]{4}_[AD](?:_ATIS)?\b", RegexOptions.IgnoreCase))
            {
                return FormatAtisPreviewText(text);
            }

            return WrapPreviewText(text, GetPreviewWrapLength());
        }

        private int GetPreviewWrapLength()
        {
            return DcduStyleManager.IsBoeing ? 48 : 46;
        }

        private string FormatAtisPreviewText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            string normalized = text
                .Replace("\r\n", "\n")
                .Replace("\r", "\n")
                .Replace("\t", " ");

            normalized = Regex.Replace(normalized, @"[ ]+", " ");
            normalized = Regex.Replace(normalized, @" *\n *", "\n").Trim();

            // Split the common ATIS header away from the body.
            normalized = Regex.Replace(
                normalized,
                @"^(.+?\bATIS\b\s+[A-Z]\s+\d{4}Z)\s+",
                "$1\n",
                RegexOptions.IgnoreCase);

            // Insert helpful avionics-style line breaks before common ATIS sections.
            string[] sectionPatterns =
            {
                @"\bDEP RWYS?\b",
                @"\bARR RWYS?\b",
                @"\bARRIVALS?\b",
                @"\bDEPARTURES?\b",
                @"\bRWYS?\b",
                @"\bWIND\b",
                @"\bVIS\b",
                @"\bCAVOK\b",
                @"\bFEW\d",
                @"\bSCT\d",
                @"\bBKN\d",
                @"\bOVC\d",
                @"\bTRL\b",
                @"\bTL\b",
                @"\bTA\b",
                @"\bTRANS(?:ITION)?\b",
                @"\bT\s+[-+]?\d+",
                @"\bQNH\b",
                @"\bRECEIVE\b",
                @"\bDATALINK\b",
                @"\bENR\b",
                @"\bEND OF\b"
            };

            foreach (string pattern in sectionPatterns)
            {
                normalized = Regex.Replace(
                    normalized,
                    @"\s+(" + pattern + @")",
                    "\n$1",
                    RegexOptions.IgnoreCase);
            }

            normalized = Regex.Replace(normalized, @"\n{2,}", "\n");

            return WrapPreviewText(normalized, GetPreviewWrapLength());
        }

        private static string WrapPreviewText(string text, int maxChars)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            maxChars = Math.Max(24, maxChars);

            List<string> wrappedLines = new();

            foreach (string rawLine in text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n'))
            {
                string line = Regex.Replace(rawLine.Trim(), @"[ ]+", " ");

                if (line.Length == 0)
                {
                    continue;
                }

                while (line.Length > maxChars)
                {
                    int breakAt = line.LastIndexOf(' ', Math.Min(maxChars, line.Length - 1));

                    if (breakAt < maxChars / 2)
                    {
                        breakAt = Math.Min(maxChars, line.Length);
                    }

                    wrappedLines.Add(line.Substring(0, breakAt).Trim());
                    line = line.Substring(Math.Min(breakAt + 1, line.Length)).Trim();
                }

                if (line.Length > 0)
                {
                    wrappedLines.Add(line);
                }
            }

            return string.Join("\n", wrappedLines);
        }

        private void MessageClicked(object sender, EventArgs e)
        {
            if (sender is not System.Windows.Forms.Label clickedLabel || outputTable == null)
            {
                return;
            }

            int messageIndex = outputTable.Controls.GetChildIndex(clickedLabel) - 1;
            try
            {
                KeyEventArgs kE = (KeyEventArgs)e;
                if (kE.KeyCode == Keys.Enter || kE.KeyCode == Keys.Space)
                {
                    messageIndex++;
                    throw new Exception();
                }
                else
                {
                    return;
                }
            }
            catch
            {
                if (messageIndex < 0 || messageIndex >= outputTable.Controls.Count)
                {
                    return;
                }

                if (outputTable.Controls[messageIndex] is not CPDLCMessage _sender)
                {
                    return;
                }
                previewMessage = _sender;
                MarkMessageRead(_sender);
                System.Windows.Forms.Label _timeStamp = (System.Windows.Forms.Label)outputTable.Controls[messageIndex - 1];
                List<System.Windows.Forms.Label> responses = new();


                if (_sender.type == "CPDLC" && !_sender.outbound && !_sender.acknowledged)
                {
                    if (_sender.message.Contains("CLR TO") || _sender.message.Contains("CLRD TO") || _sender.message.Contains("CLEARED TO"))
                    {
                        AccessibleLabel acceptLabel = CreateSpecialLabel("> ACCEPT", false);
                        AccessibleLabel rejectLabel = CreateSpecialLabel("> REJECT", false);
                        switch (_sender.header.Responses)
                        {
                            case "WU":
                                acceptLabel.Click += (lSender, le) => ReplyMessage(e, previewMessage, "WILCO");
                                acceptLabel.KeyDown += (lSender, le) => ReplyMessage(e, previewMessage, "WILCO");
                                rejectLabel.Click += (lSender, le) => ReplyMessage(e, previewMessage, "UNABLE");
                                rejectLabel.KeyDown += (lSender, le) => ReplyMessage(e, previewMessage, "UNABLE");
                                break;

                            case "AN":
                                acceptLabel.Click += (lSender, le) => ReplyMessage(e, previewMessage, "AFFIRMATIVE");
                                acceptLabel.KeyDown += (lSender, le) => ReplyMessage(e, previewMessage, "AFFIRMATIVE");
                                rejectLabel.Click += (lSender, le) => ReplyMessage(e, previewMessage, "NEGATIVE");
                                rejectLabel.KeyDown += (lSender, le) => ReplyMessage(e, previewMessage, "NEGATIVE");
                                break;

                            case "R":
                                acceptLabel.Click += (lSender, le) => ReplyMessage(e, previewMessage, "ROGER");
                                acceptLabel.KeyDown += (lSender, le) => ReplyMessage(e, previewMessage, "ROGER");
                                rejectLabel.Click += (lSender, le) => ReplyMessage(e, previewMessage, "UNABLE");
                                rejectLabel.KeyDown += (lSender, le) => ReplyMessage(e, previewMessage, "UNABLE");
                                break;


                        }

                        responses.Add(acceptLabel);
                        responses.Add(rejectLabel);
                        responses.Add(standbyLabel);
                    }

                    else
                    {
                        switch (_sender.header.Responses)
                        {
                            case "WU":
                                responses.Add(wilcoLabel);
                                responses.Add(unableLabel);
                                responses.Add(standbyLabel);
                                break;

                            case "AN":
                                responses.Add(affirmativeLabel);
                                responses.Add(negativeLabel);
                                responses.Add(standbyLabel);
                                break;

                            case "R":
                                responses.Add(rogerLabel);
                                responses.Add(standbyLabel);
                                break;
                        }
                    }

                    responses.Add(freeTextLabel);
                }
                else if (_sender.type == "TELEX" && !_sender.outbound)
                {
                    responses.Add(freeTextLabel);
                }

                SetSmartWidgetsVisible(false);
                messageFormatPanel.Size = outputTable.Size;
                messageFormatPanel.Visible = true;
                outputTable.Visible = false;
                messageFormatPanel.Controls.Add(returnLabel);
                messageFormatPanel.SetFlowBreak(returnLabel, true);
                foreach (string line in GetPreviewMessageText(_sender).Split('\n'))
                {
                    messageFormatPanel.Controls.Add(CreateLabel(line, false));
                    messageFormatPanel.SetFlowBreak(messageFormatPanel.Controls[messageFormatPanel.Controls.Count - 1], true);
                }
                bool firstActionButton = true;
                foreach (System.Windows.Forms.Label _response in responses)
                {
                    if (firstActionButton)
                    {
                        _response.Margin = new Padding(_response.Margin.Left, 7, _response.Margin.Right, _response.Margin.Bottom);
                        firstActionButton = false;
                    }

                    messageFormatPanel.Controls.Add(_response);
                }
                if (_sender.type != "CPDLC")
                {
                    messageFormatPanel.Controls.Add(deleteLabel);
                }
                messageFormatPanel.PerformLayout();
                HidePreviewHorizontalScrollbar();
                messageFormatPanel.Controls[1].Focus();
            }
        }
        private Task ADSCParser(string _response, string _sender)
        {
            string[] responseElements = _response.Split(' ');
            try
            {
                Convert.ToInt32(responseElements[2]);
            }
            catch
            {
                return Task.CompletedTask;
            }
            Contract _contract;
            switch (responseElements[1])
            {
                case "PERIODIC":
                    _contract = new Contract(this, _sender, responseElements[2]);
                    contracts.Add(_contract);
                    _contract.StartContract();

                    break;

                case "CANCEL":
                    _contract = contracts.Where(x => x.sender == _sender && x.contractLength == responseElements[2]).FirstOrDefault();
                    if (_contract != null)
                    {
                        _contract.StopContract();
                        contracts.Remove(_contract);
                    }

                    break;
            }

            return Task.CompletedTask;
        }


        private string ExtractAtisTarget(string upperText)
        {
            if (string.IsNullOrWhiteSpace(upperText))
            {
                return "ATIS";
            }

            Match afterVatat = Regex.Match(upperText, @"\bVATATIS\s+([A-Z0-9]{4}(?:_[AD])?(?:_ATIS)?)\b");
            if (afterVatat.Success)
            {
                return afterVatat.Groups[1].Value;
            }

            Match atisTarget = Regex.Match(upperText, @"\b[A-Z0-9]{4}(?:_[AD])?_ATIS\b|\b[A-Z0-9]{4}_[AD]\b");
            return atisTarget.Success ? atisTarget.Value : "ATIS";
        }

        private bool TryWriteAtisUnavailableResponse(string response)
        {
            if (string.IsNullOrWhiteSpace(response))
            {
                return false;
            }

            string normalized = response
                .Replace("\r\n", "\n")
                .Replace("\r", "\n")
                .Trim();

            string upper = normalized.ToUpperInvariant();

            if (!upper.Contains("THIS ATIS IS NOT") && !upper.Contains("ATIS IS NOT AVAILABLE"))
            {
                return false;
            }

            string explicitTarget = ExtractAtisTarget(upper);
            string target = explicitTarget != "ATIS"
                ? FormatAtisTargetForList(explicitTarget)
                : GetAtisListTarget(normalized, "VATATIS", true);

            string displayMessage = target + "\nTHIS ATIS IS NOT AVAILABLE";

            WriteMessage(displayMessage, "ATIS", "VATATIS");
            FlashWindow.Flash(this);
            Logger.Debug("Displayed Hoppie ATIS unavailable response: " + displayMessage.Replace("\n", " "));
            return true;
        }

        private bool TryWriteFlatInfoResponse(string sender, string type, string payload)
        {
            if (string.IsNullOrWhiteSpace(payload))
            {
                return false;
            }

            string normalizedPayload = payload
                .Replace("\r\n", "\n")
                .Replace("\r", "\n")
                .Trim();

            if (TryWriteAtisUnavailableResponse(normalizedPayload))
            {
                return true;
            }

            string upperPayload = normalizedPayload.ToUpperInvariant();

            if (!upperPayload.Contains("_ATIS") && !upperPayload.Contains("VATATIS") &&
                !Regex.IsMatch(upperPayload, @"\b[A-Z0-9]{4}_[AD]\b"))
            {
                return false;
            }

            string target = ExtractAtisTarget(upperPayload);
            if (target == "ATIS")
            {
                return false;
            }

            int targetIndex = upperPayload.IndexOf(target, StringComparison.Ordinal);
            string displayMessage = targetIndex >= 0
                ? normalizedPayload.Substring(targetIndex).Trim()
                : normalizedPayload.Trim();
            if (string.IsNullOrWhiteSpace(displayMessage))
            {
                displayMessage = target;
            }

            WriteMessage(displayMessage, "ATIS", string.IsNullOrWhiteSpace(sender) ? "VATATIS" : sender);
            FlashWindow.Flash(this);
            Logger.Debug("Displayed flat Hoppie ATIS/info response: " + displayMessage.Replace("\n", " "));
            return true;
        }

        private async Task TelexParser(string response)
        {
            var responses = hoppieParse.Matches(response);

            if (responses.Count == 0)
            {
                TryWriteFlatInfoResponse("VATATIS", "ATIS", response);
                return;
            }

            foreach (Match _response in responses)
            {
                string format_response = "";
                string[] _modify = _response.Groups[1].Value.Replace("}", "").Split('{');

                string[] headerParts = _modify[0]
                    .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                string sender = headerParts.Length > 0 ? headerParts[0] : "UNKNOWN";
                string type = headerParts.Length > 1 ? headerParts[1] : "INFO";

                bool handled = false;

                for (int i = 0; i < _modify.Length; i++)
                {
                    if (i > 0 && _modify[i].Length > 2)
                    {
                        if (_modify[1].StartsWith("/DATA2/"))
                        {
                            Logger.Debug("CPDLC Message identified, attempting to parse");
                            await CPDLCParser(_modify[1], sender);
                            handled = true;
                            break;
                        }

                        if (type == "ADS-C")
                        {
                            if (UseFSUIPC)
                            {
                                Logger.Debug("ADS-C Message identified, attempting to parse");
                                await ADSCParser(_modify[1], sender);
                                handled = true;
                                break;
                            }
                            else
                            {
                                Logger.Debug("ADS-C Message identified, but no simulator connection was recognised. Ignoring.");
                            }
                        }

                        format_response += _modify[1];
                        WriteMessage(format_response, type, sender);
                        FlashWindow.Flash(this);
                        handled = true;
                    }
                }

                if (!handled)
                {
                    string flatPayload = _modify.Length > 0 ? _modify[0] : _response.Groups[1].Value;
                    TryWriteFlatInfoResponse(sender, type, flatPayload);
                }
            }
            return;
        }

        private async Task CPDLCParser(string _response, string _sender)
        {
            bool _showUser = true;
            string messageString;

            var unit = cpdlcUnitParse.Match(_response);
            if (unit.Success)
            {
                CurrentATCUnit = unit.Value.Trim('_', '@');
            }

            var responses = cpdlcHeaderParse.Matches(_response);
            CPDLCResponse header = new()
            {
                DataType = responses[0].Value.Trim('/'),
                MessageID = Convert.ToInt32(responses[1].Value.Trim('/')),
                ResponseID = responses[2].Value.Trim('/').Length < 1 ? 0 : Convert.ToInt32(responses[2].Value.Trim('/')),
                Responses = responses[3].Value.Trim('/')
            };

            string[] messageContent = _response.Split(new string[] { header.Responses + "/" }, StringSplitOptions.None);
            if (messageContent[1].Contains(callsign))
            {
                messageString = messageContent[1].Split(new string[] { callsign }, StringSplitOptions.None).Last();
            }
            else
            {
                messageString = messageContent[1];
            }
            if (messageString.StartsWith("HANDOVER"))
            {
                string nextATCUnit = messageString.Split(' ').Last().Trim('@').Trim();
                CurrentATCUnit = null;
                await SendCPDLCMessage(nextATCUnit, "CPDLC", String.Format("/data2/{0}//Y/REQUEST LOGON", messageOutCounter), false);
                pendingLogon = nextATCUnit;
                messageOutCounter += 1;
                _showUser = false;
            }
            else if (messageString.StartsWith("LOGON ACCEPTED"))
            {
                CurrentATCUnit = pendingLogon;
                WriteMessage("CURRENT ATS UNIT: " + pendingLogon, "CPDLC", _sender, false, header);
                _showUser = false;
            }
            else if (messageString.StartsWith("CURRENT ATC UNIT") || messageString.StartsWith("CURRENT ATS UNIT"))
            {
                _showUser = false;
            }

            string message = callsign + " " + messageString.Replace("@@", "N/A").Replace("@", Environment.NewLine).Replace("_", "");
            message = Regex.Replace(message, @"\s+", " ");

            Logger.Debug(message);

            if (message.Contains("LOGOFF"))
            {
                CurrentATCUnit = null;
                pendingLogon = null;
            }

            if (_showUser)
            {
                WriteMessage(message, "CPDLC", _sender, false, header);

                FlashWindow.Flash(this);
            }

            return;
        }

        public CPDLCMessage WriteMessage(string _response, string _type, string _recipient, bool _outbound = false, CPDLCResponse _header = null)
        {
            if (_outbound && string.Equals(_type, "ATIS", StringComparison.OrdinalIgnoreCase))
            {
                RememberAtisRequestTarget(_recipient);
            }

            if (_outbound && string.Equals(_type, "METAR", StringComparison.OrdinalIgnoreCase))
            {
                RememberMetarRequestTarget(_recipient);
            }

            TrackClearanceStatus(_response, _type, _outbound);

            CPDLCMessage message;
            if (_outbound)
            {
                message = CreateCPDLCMessage(_response, _type, _recipient, _outbound, _header);
            }
            else
            {
                message = CreateCPDLCMessage(_response, _type, _recipient, _outbound, _header);
                if (PlaySound && ShouldPlayInboundMessageSound(_type, _recipient))
                {
                    PlayInboundMessageSound();
                }
            }

            Logger.Debug("Writing message: " + _response);

            TimerLabel menuLabel = CreateTimerLabel(">>", true);
            if (ShouldFlashForReply(message))
            {
                menuLabel.CanFlash = true;
            }
            menuLabel.Click += MessageClicked;
            message.KeyDown += MessageClicked;

            SafeUi(() =>
            {
                if (outputTable == null || outputTable.IsDisposed)
                {
                    return;
                }

                outputTable.Controls.Add(CreateLabel(DateTime.Now.ToString("HH:mm")), 0, outputTable.RowCount - 1);
                outputTable.Controls.Add(message, 1, outputTable.RowCount - 1);
                outputTable.Controls.Add(menuLabel, 2, outputTable.RowCount - 1);
                outputTable.RowCount += 1;
                outputTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                MarkMessageUnread(message, menuLabel);
                ApplyMessageFilter();
                if (message.Visible)
                {
                    outputTable.ScrollControlIntoView(message);
                }
                HideNativeOutputScrollbars();
            });

            return message;
        }

        public async void ArtificialDelay(string _message, string _type, string _sender, int _minDelay = 5, int _maxDelay = 15)
        {
            await Task.Delay(random.Next(_minDelay, _maxDelay) * 1000);
            await SendCPDLCMessage(_sender, _type, _message, false);
            return;
        }
        private void ExitButton_Click(object sender, EventArgs e)
        {
            try
            {
                unreadReminderTimer?.Stop();
                vatsimOnlineTimer?.Stop();
                requestCancellationTokenSource?.Cancel();
            }
            catch (NullReferenceException) { }
            this.Close();

            LogManager.Shutdown();
            FSUIPCData.CloseConnection();
            System.Windows.Forms.Application.Exit();
        }
        private void MoveWindow(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                _ = SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        }
        private async void RetrieveButton_Click(object sender, EventArgs e)
        {
            string response = "";

            if (!Connected)
            {

                try
                {
                    using (HttpClient wc = new())
                    {
                        vatsimData = JsonConvert.DeserializeObject<VATSIMRootobject>(wc.GetStringAsync("https://data.vatsim.net/v3/vatsim-data.json").Result);
                        lastVatsimOnlineRefreshUtc = DateTime.UtcNow;
                        UpdateOnlineStatusLabel();
                        Logger.Debug("VATSIM Data Retrieved and Parsed");

                    }

                    userVATSIMData = vatsimData.pilots.Where(i => i.cid == cid).FirstOrDefault();
                    if (userVATSIMData is null)
                    {
                        response += "VATSIM: PILOT NOT FOUND. WAIT 60 SECONDS AND RETRY.\n";
                        atcButton.Enabled = false;
                        telexButton.Enabled = false;
                        Connected = false;
                        WriteMessage(response, "SYSTEM", "SYSTEM");
                        return;
                    }

                    callsign = userVATSIMData.callsign;
                    _ = RefreshVatsimOnlineStatusAsync(true);

                    if (userVATSIMData.flight_plan is null)
                    {
                        response += "VATSIM: NO FLIGHT PLAN FILED. FILE A FLIGHT PLAN AND RETRY.\n";
                        atcButton.Enabled = false;
                        telexButton.Enabled = false;
                        Connected = false;
                        WriteMessage(response, "SYSTEM", "SYSTEM");
                        return;
                    }

                    Connected = true;

                    requestCancellationTokenSource = new CancellationTokenSource();
                    requestCancellationToken = requestCancellationTokenSource.Token;
                    _ = PeriodicCheckMessage(updateTimer, requestCancellationToken);

                }
                catch (Exception ex) when (ex is IndexOutOfRangeException || ex is NullReferenceException)
                {
                    response += "VATSIM: ERROR. WAIT 60 SECONDS AND RETRY.\n";
                    atcButton.Enabled = false;
                    telexButton.Enabled = false;
                    Connected = false;
                    WriteMessage(response, "SYSTEM", "SYSTEM");
                    return;
                }

                response += "LOGON SUCCESSFUL.";

                try
                {

                    using HttpClient wc = new();
                    var simbriefjson = wc.GetStringAsync(String.Format("https://www.simbrief.com/api/xml.fetcher.php?userid={0}&json=1", SimbriefID)).Result;
                    var simbriefNavlog = JObject.Parse(simbriefjson)["navlog"].ToString();
                    simbriefData = JsonConvert.DeserializeObject<Navlog>(simbriefNavlog);

                    Logger.Debug("Simbrief Data Retrieved and Parsed");

                    reportFixes = simbriefData.fix.Where(x => x.is_sid_star == "0" && !new string[] { "apt" }.Contains(x.type)).Select(x => x.ident).ToArray();
                    response += " SIMBRIEF OK,";
                }

                catch
                {
                    response += "SIMBRIEF ERROR,";
                }

                if (UseFSUIPC)
                {
                    try
                    {
                        fsConnectionOpen = FSUIPCData.OpenConnection();
                        if (fsConnectionOpen)
                        {
                            await fsuipc.RefreshData();
                            response += "SIMULATOR OK.";
                        }
                        else
                        {
                            string fsuipcError = string.IsNullOrWhiteSpace(FSUIPCData.LastError) ? "CONNECTION FAILED" : FSUIPCData.LastError;
                            response += "SIMULATOR ERROR: " + fsuipcError;
                        }
                    }
                    catch (Exception ex)
                    {
                        string fsuipcError = !string.IsNullOrWhiteSpace(FSUIPCData.LastError) ? FSUIPCData.LastError : ex.Message;
                        response += "SIMULATOR ERROR: " + fsuipcError;
                    }
                }
                WriteMessage(response, "SYSTEM", "SYSTEM");

            }
            else
            {
                if (CurrentATCUnit is not null)
                {
                    await SendCPDLCMessage(CurrentATCUnit, "CPDLC", String.Format("/data2/{0}//N/LOGOFF", messageOutCounter), false);
                }
                foreach (Contract _contract in contracts)
                {
                    await SendCPDLCMessage(_contract.sender, "ADS-C", "REJECT " + _contract.contractLength, false);
                }
                requestCancellationTokenSource?.Cancel();
                callsign = "";
                datalinkStatusText = "PDC --";
                atisStatusText = "ATIS --";
                SetClearanceStatus("CLR --");
                response = "DISCONNECTED CLIENT";
                vatsimData = new VATSIMRootobject();
                hoppieOnlineStations.Clear();
                hoppieOnlineStationsLoaded = false;
                userVATSIMData = new Pilot();
                simbriefData = new Navlog();
                fsConnectionOpen = FSUIPCData.CloseConnection();

                atcButton.Enabled = false;
                telexButton.Enabled = false;
                Connected = false;

                WriteMessage(response, "SYSTEM", "SYSTEM");

            }
        }
        private void TelexButton_Click(object sender, EventArgs e)
        {
            tForm = new TelexForm(this);
            tForm.Show();
        }
        private void RequestButton_Click(object sender, EventArgs e)
        {
            rForm = new RequestForm(this)
            {
                TopMost = StayOnTop
            };
            rForm.Show();
        }

        private async void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            Properties.Settings.Default.MainWindowLocation = Location;
            Properties.Settings.Default.MainWindowSize = Size;
            Properties.Settings.Default.Save();

            if (CurrentATCUnit is not null)
            {
                await SendCPDLCMessage(CurrentATCUnit, "CPDLC", String.Format("/data2/{0}//N/LOGOFF", messageOutCounter), false);
                requestCancellationTokenSource?.Cancel();
            }
        }

        private void OutputTable_Click(object sender, EventArgs e)
        {
            MouseEventArgs me = (MouseEventArgs)e;

            TableLayoutPanel _sender = (TableLayoutPanel)sender;

            if (me.Button == MouseButtons.Right)
            {
                popupMenu.Items.Clear();
                popupMenu.Items.Add(deleteAllMenu);
                popupMenu.Items.Add(exportLogMenu);
                popupMenu.Items.Add(weatherCacheMenu);

                popupMenu.Show(_sender, _sender.PointToClient(Cursor.Position));
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

        private void SettingsButton_Click(object sender, EventArgs e)
        {
            sForm = new SettingsForm(this)
            {
                TopMost = StayOnTop
            };
            sForm.Show();
        }

        private void HelpButton_Click(object sender, EventArgs e)
        {
            //worst bodge I've ever had to pull, thanks to this: github.com/dotnet/runtime/issues/17938
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://github.com/fresH229a/EasyCPDLC-Modernized/wiki") { UseShellExecute = true });
            MessageBox.Show(
                @"EasyCPDLC - original from Joshua Seagrave
Copyright(C) 2022 Joshua Seagrave

This program is free software: you can redistribute it and / or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.If not, see <https://www.gnu.org/licenses/>.", String.Format("EasyCPDLC v{0} Licensing & Copyright Notice", System.Windows.Forms.Application.ProductVersion), MessageBoxButtons.OK);
        }

        private void messageFormatPanel_Paint(object sender, PaintEventArgs e)
        {

        }
    }
    internal class NoHighlightRenderer : ToolStripProfessionalRenderer
    {
        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (e.Item.OwnerItem == null)
            {
                base.OnRenderMenuItemBackground(e);
            }
        }
    }
    public static class TableLayoutHelper
    {
        public static void RemoveArbitraryRow(TableLayoutPanel panel, int rowIndex)
        {
            if (rowIndex >= panel.RowCount)
            {
                return;
            }

            // delete all controls of row that we want to delete
            for (int i = 0; i < panel.ColumnCount; i++)
            {
                var control = panel.GetControlFromPosition(i, rowIndex);
                if (control != null)
                {
                    panel.Controls.Remove(control);
                }
            }

            // move up row controls that comes after row we want to remove
            for (int i = rowIndex + 1; i < panel.RowCount; i++)
            {
                for (int j = 0; j < panel.ColumnCount; j++)
                {
                    var control = panel.GetControlFromPosition(j, i);
                    if (control != null)
                    {
                        panel.SetRow(control, i - 1);
                    }
                }
            }

            var removeStyle = panel.RowCount - 1;

            if (panel.RowStyles.Count > removeStyle)
                panel.RowStyles.RemoveAt(removeStyle);

            panel.RowCount--;

            panel.AutoScroll = false;
            panel.AutoScroll = true;
        }
    }
}

