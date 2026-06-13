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
        private const int ScrollBarBoth = 3;
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ShowScrollBar(IntPtr hWnd, int wBar, bool bShow);

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

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
                    }

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
                    }

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
        ToolStripMenuItem deleteAllMenu;

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
            Invalidate(true);
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
                messageHeaderLabel.Location = new Point(12, 58);

                outputTable.Location = new Point(16, 80);
                outputTable.Size = new Size(454, 105);
                outputTable.Padding = new Padding(0, 4, 4, 4);

                messageFormatPanel.Location = new Point(16, 80);
                messageFormatPanel.Size = new Size(454, 105);

                SendingProgress.Location = new Point(16, 190);
                SendingProgress.Size = new Size(454, 10);

                outputScrollBar.Location = new Point(470, 80);
                outputScrollBar.Size = new Size(12, 105);
                outputScrollBar.Target = outputTable;
                outputScrollBar.Invalidate();
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
            messageHeaderLabel.Location = new Point(8, 66);

            outputTable.Location = new Point(8, 94);
            outputTable.Size = new Size(466, 106);
            outputTable.Padding = new Padding(0, 4, 12, 4);

            messageFormatPanel.Location = new Point(8, 94);
            messageFormatPanel.Size = new Size(466, 106);

            SendingProgress.Location = new Point(8, 206);
            SendingProgress.Size = new Size(466, 8);

            outputScrollBar.Location = new Point(476, 94);
            outputScrollBar.Size = new Size(8, 106);
            outputScrollBar.Target = outputTable;
            outputScrollBar.Invalidate();
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
            outputTable.SizeChanged += (sizeSender, sizeArgs) => HideNativeOutputScrollbars();
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
                outputScrollBar.Visible = true;
                outputScrollBar.BringToFront();
                outputScrollBar.Invalidate();
            }
        }

        private static async void CheckNewVersion()
        {
            try
            {
                const string githubOwner = "fresH229a";
                const string githubRepo = "EasyCPDLC";

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

            replyOptionsList = new AccessibleLabel[]
            {
                wilcoLabel, rogerLabel, unableLabel, affirmativeLabel, negativeLabel, standbyLabel, freeTextLabel
            };

            Logger.Info("Login menu initialised");
        }

        private void FreeTextMessage(CPDLCMessage message)
        {
            tForm = message == null
                ? new TelexForm(this)
                : new TelexForm(this, message.recipient);

            tForm.TopMost = StayOnTop;
            tForm.Show();
        }

        public void ClearPreview()
        {
            if (messageFormatPanel != null)
            {
                messageFormatPanel.Controls.Clear();
                messageFormatPanel.Visible = false;
            }

            if (outputTable != null)
            {
                outputTable.Visible = true;
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
        private CPDLCMessage CreateCPDLCMessage(string _contents, string _type, string _recipient, bool _outbound = false, CPDLCResponse _header = null)
        {
            CPDLCMessage _message = new(_type, _recipient, _contents, _outbound, _header)
            {
                AutoSize = true,
                BackColor = Color.Transparent,
                ForeColor = DcduTheme.CyanWhite,
                Font = textFont,
                Text = _type == "SYSTEM" ? "SYSTEM MESSAGE" : _outbound ? String.Format("{1} MESSAGE TO {0}", _recipient, _type.ToUpper()) : String.Format("{1} MESSAGE FROM {0}", _recipient, _type.ToUpper()),
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
            AccessibleLabel _message = new(DcduTheme.CyanWhite)
            {
                Width = 65,
                AutoSize = true,
                BackColor = Color.Transparent,
                ForeColor = DcduTheme.CyanWhite,
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
            SetStyle(ControlStyles.Selectable, true);
            return _message;
        }

        private AccessibleLabel CreateSpecialLabel(string _text, bool _useMaxSize = true)
        {
            Size maxSize = new()
            {
                Width = 65
            };
            AccessibleLabel _message = new(DcduTheme.Cyan)
            {
                Width = 65,
                AutoSize = true,
                BackColor = Color.Transparent,
                ForeColor = DcduTheme.Cyan,
                Font = textFont,
                Text = _text,
                BorderStyle = BorderStyle.None,
                Margin = new Padding(5, 3, 0, 0),
                TabStop = true,
                TabIndex = 0,
            };
            if (_useMaxSize)
            {
                _message.MaximumSize = maxSize;
            }
            return _message;
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
                ForeColor = DcduTheme.CyanWhite,
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
                TableLayoutHelper.RemoveArbitraryRow(outputTable, outputTable.GetPositionFromControl(control).Row);
                ClearPreview();
            }
        }

        private void DeleteAllElement(object sender, EventArgs e)
        {
            outputTable.Controls.Clear();
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

                messageFormatPanel.Size = outputTable.Size;
                messageFormatPanel.Visible = true;
                outputTable.Visible = false;
                messageFormatPanel.Controls.Add(returnLabel);
                messageFormatPanel.SetFlowBreak(returnLabel, true);
                foreach (string line in _sender.message.Split('\n'))
                {
                    messageFormatPanel.Controls.Add(CreateLabel(line, false));
                    messageFormatPanel.SetFlowBreak(messageFormatPanel.Controls[messageFormatPanel.Controls.Count - 1], true);
                }
                foreach (System.Windows.Forms.Label _response in responses)
                {
                    messageFormatPanel.Controls.Add(_response);
                }
                if (_sender.type != "CPDLC")
                {
                    messageFormatPanel.Controls.Add(deleteLabel);
                }
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

        private async Task TelexParser(string response)
        {
            var responses = hoppieParse.Matches(response);

            foreach (Match _response in responses)
            {
                string format_response = "";
                string[] _modify = _response.Groups[1].Value.Replace("}", "").Split('{');
                string sender = _modify[0].Split(' ')[0];
                string type = _modify[0].Split(' ')[1];

                for (int i = 0; i < _modify.Length; i++)
                {
                    if (i > 0 && _modify[i].Length > 2)
                    {
                        if (_modify[1].StartsWith("/DATA2/"))
                        {
                            Logger.Debug("CPDLC Message identified, attempting to parse");
                            await CPDLCParser(_modify[1], sender);
                            break;
                        }

                        if (type == "ADS-C")
                        {
                            if (UseFSUIPC)
                            {
                                Logger.Debug("ADS-C Message identified, attempting to parse");
                                await ADSCParser(_modify[1], sender);
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
                    }
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
            if (_type == "CPDLC" && !_outbound && _header != null && _header.Responses != "NE")
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
                outputTable.ScrollControlIntoView(message);
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
                response = "DISCONNECTED CLIENT";
                vatsimData = new VATSIMRootobject();
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
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://github.com/josh-seagrave/EasyCPDLC/wiki") { UseShellExecute = true });
            MessageBox.Show(
                @"EasyCPDLC
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
