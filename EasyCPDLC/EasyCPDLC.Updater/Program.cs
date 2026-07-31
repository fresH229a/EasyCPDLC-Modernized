using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EasyCPDLC.Updater
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (args == null || args.Length == 0)
            {
                HandleManualLaunch();
                return;
            }

            UpdateArguments parsed;
            string error;
            if (!UpdateArguments.TryParse(args, out parsed, out error))
            {
                MessageBox.Show(
                    error,
                    "EasyCPDLC Updater",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            Application.Run(new UpdaterForm(parsed));
        }

        private static void HandleManualLaunch()
        {
            string mainExecutable = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "EasyCPDLC.exe");

            if (!File.Exists(mainExecutable))
            {
                MessageBox.Show(
                    "EasyCPDLC Updater is an integrated component and is started automatically by EasyCPDLC." +
                    Environment.NewLine + Environment.NewLine +
                    "EasyCPDLC.exe was not found beside the updater.",
                    "EasyCPDLC Updater",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            DialogResult result = MessageBox.Show(
                "EasyCPDLC Updater is an integrated component and is started automatically when an update is installed." +
                Environment.NewLine + Environment.NewLine +
                "To check for updates, start EasyCPDLC. Open EasyCPDLC now?",
                "EasyCPDLC Updater",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information);

            if (result != DialogResult.Yes)
            {
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = mainExecutable,
                    WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "EasyCPDLC could not be started:" + Environment.NewLine + ex.Message,
                    "EasyCPDLC Updater",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }
    }

    internal sealed class UpdateArguments
    {
        private const string MainExecutableName = "EasyCPDLC.exe";
        private const string UpdaterExecutableName = "EasyCPDLC.Updater.exe";
        private const string UpdateDirectoryPrefix = "EasyCPDLC_Update_";

        public int WaitProcessId { get; private set; }
        public string ZipPath { get; private set; }
        public string ExpectedSha256 { get; private set; }
        public string ExpectedCurrentExecutableSha256 { get; private set; }
        public string ReadyFilePath { get; private set; }
        public string MainExecutablePath { get; private set; }
        public string AppDirectory { get; private set; }
        public string UpdateRootDirectory { get; private set; }
        public string ConfigSourcePath { get; private set; }
        public string CurrentUserConfigPath { get; private set; }
        public string TargetReleaseVersion { get; private set; }

        private UpdateArguments()
        {
            ZipPath = string.Empty;
            ExpectedSha256 = string.Empty;
            ExpectedCurrentExecutableSha256 = string.Empty;
            ReadyFilePath = string.Empty;
            MainExecutablePath = string.Empty;
            AppDirectory = string.Empty;
            UpdateRootDirectory = string.Empty;
            ConfigSourcePath = string.Empty;
            CurrentUserConfigPath = string.Empty;
            TargetReleaseVersion = string.Empty;
        }

        public static bool TryParse(string[] args, out UpdateArguments parsed, out string error)
        {
            parsed = null;
            error = string.Empty;

            try
            {
                HashSet<string> allowedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "--wait-process-id",
                    "--zip-path",
                    "--expected-sha256",
                    "--expected-current-exe-sha256",
                    "--ready-file",
                    "--config-source-path",
                    "--current-user-config-path",
                    "--target-release-version"
                };

                Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < (args == null ? 0 : args.Length); i++)
                {
                    string key = args[i] ?? string.Empty;
                    if (!key.StartsWith("--", StringComparison.Ordinal) || i + 1 >= args.Length)
                    {
                        error = "The updater received an incomplete command line.";
                        return false;
                    }

                    if (!allowedKeys.Contains(key))
                    {
                        error = "The updater refused an unknown command-line option: " + key;
                        return false;
                    }

                    if (values.ContainsKey(key))
                    {
                        error = "The updater refused a duplicated command-line option: " + key;
                        return false;
                    }

                    values.Add(key, args[++i] ?? string.Empty);
                }

                string pidText;
                int pid;
                if (!values.TryGetValue("--wait-process-id", out pidText) ||
                    !int.TryParse(pidText, NumberStyles.Integer, CultureInfo.InvariantCulture, out pid) ||
                    pid <= 0)
                {
                    error = "The updater did not receive a valid EasyCPDLC process id.";
                    return false;
                }

                string zipPath;
                if (!values.TryGetValue("--zip-path", out zipPath) || string.IsNullOrWhiteSpace(zipPath))
                {
                    error = "The updater did not receive an update ZIP path.";
                    return false;
                }

                string expectedSha256;
                if (!values.TryGetValue("--expected-sha256", out expectedSha256) || !IsSha256(expectedSha256))
                {
                    error = "The updater did not receive a valid update-package SHA-256 checksum.";
                    return false;
                }

                string expectedCurrentExecutableSha256;
                if (!values.TryGetValue("--expected-current-exe-sha256", out expectedCurrentExecutableSha256) ||
                    !IsSha256(expectedCurrentExecutableSha256))
                {
                    error = "The updater did not receive a valid current EasyCPDLC SHA-256 checksum.";
                    return false;
                }

                string fullZipPath = Path.GetFullPath(zipPath);
                if (!File.Exists(fullZipPath))
                {
                    error = "The downloaded update package no longer exists.";
                    return false;
                }

                string updateRootDirectory = Path.GetDirectoryName(fullZipPath) ?? string.Empty;
                if (!IsApprovedUpdateRoot(updateRootDirectory))
                {
                    error = "The updater refused an update package outside its private EasyCPDLC temporary directory.";
                    return false;
                }

                string currentUpdaterPath = Path.GetFullPath(Application.ExecutablePath);
                if (!string.Equals(Path.GetFileName(currentUpdaterPath), UpdaterExecutableName, StringComparison.OrdinalIgnoreCase) ||
                    !IsInsideDirectory(currentUpdaterPath, updateRootDirectory))
                {
                    error = "The updater was not started from the verified EasyCPDLC update workspace.";
                    return false;
                }

                string readyFilePath = FullPathOrEmpty(Get(values, "--ready-file"));
                if (string.IsNullOrWhiteSpace(readyFilePath) ||
                    !string.Equals(Path.GetFileName(readyFilePath), "updater.ready", StringComparison.OrdinalIgnoreCase) ||
                    !IsInsideDirectory(readyFilePath, updateRootDirectory))
                {
                    error = "The updater did not receive a valid startup-handshake path.";
                    return false;
                }

                string mainExecutablePath;
                if (!TryResolveMainProcessPath(pid, out mainExecutablePath, out error))
                {
                    return false;
                }

                if (!string.Equals(Path.GetFileName(mainExecutablePath), MainExecutableName, StringComparison.OrdinalIgnoreCase))
                {
                    error = "The supplied process is not EasyCPDLC.exe.";
                    return false;
                }

                string appDirectory = Path.GetDirectoryName(mainExecutablePath) ?? string.Empty;
                if (!Directory.Exists(appDirectory) || IsDriveRoot(appDirectory))
                {
                    error = "The EasyCPDLC installation directory is invalid.";
                    return false;
                }

                string configSourcePath = FullPathOrEmpty(Get(values, "--config-source-path"));
                string currentUserConfigPath = FullPathOrEmpty(Get(values, "--current-user-config-path"));

                if (!ValidateConfigSourcePath(configSourcePath, updateRootDirectory) ||
                    !ValidateCurrentUserConfigPath(currentUserConfigPath))
                {
                    error = "The updater refused an unsafe user.config path.";
                    return false;
                }

                string targetVersion = Get(values, "--target-release-version").Trim();
                if (targetVersion.Length > 0 && ParseVersion(targetVersion) == null)
                {
                    error = "The updater received an invalid target release version.";
                    return false;
                }

                parsed = new UpdateArguments
                {
                    WaitProcessId = pid,
                    ZipPath = fullZipPath,
                    ExpectedSha256 = expectedSha256.Trim().ToLowerInvariant(),
                    ExpectedCurrentExecutableSha256 = expectedCurrentExecutableSha256.Trim().ToLowerInvariant(),
                    ReadyFilePath = readyFilePath,
                    MainExecutablePath = mainExecutablePath,
                    AppDirectory = appDirectory,
                    UpdateRootDirectory = updateRootDirectory,
                    ConfigSourcePath = configSourcePath,
                    CurrentUserConfigPath = currentUserConfigPath,
                    TargetReleaseVersion = targetVersion
                };

                return true;
            }
            catch (Exception ex)
            {
                error = "The updater command line could not be validated:" + Environment.NewLine + ex.Message;
                return false;
            }
        }

        private static bool TryResolveMainProcessPath(int processId, out string path, out string error)
        {
            path = string.Empty;
            error = string.Empty;

            try
            {
                using (Process process = Process.GetProcessById(processId))
                {
                    if (process.HasExited)
                    {
                        error = "EasyCPDLC closed before the updater could validate its identity.";
                        return false;
                    }

                    ProcessModule mainModule = process.MainModule;
                    if (mainModule == null || string.IsNullOrWhiteSpace(mainModule.FileName))
                    {
                        error = "The updater could not determine the EasyCPDLC executable path.";
                        return false;
                    }

                    path = Path.GetFullPath(mainModule.FileName);
                    return File.Exists(path);
                }
            }
            catch (ArgumentException)
            {
                error = "The EasyCPDLC process no longer exists.";
                return false;
            }
            catch (Win32Exception ex)
            {
                error = "Windows prevented the updater from validating the EasyCPDLC process: " + ex.Message;
                return false;
            }
        }

        private static bool IsApprovedUpdateRoot(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            {
                return false;
            }

            string fullDirectory = Path.GetFullPath(directory);
            string tempRoot = Path.GetFullPath(Path.GetTempPath());
            string name = new DirectoryInfo(fullDirectory).Name;

            return IsInsideDirectory(fullDirectory, tempRoot) &&
                   name.StartsWith(UpdateDirectoryPrefix, StringComparison.OrdinalIgnoreCase) &&
                   name.Length > UpdateDirectoryPrefix.Length;
        }

        private static bool IsDriveRoot(string directory)
        {
            string full = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string root = (Path.GetPathRoot(full) ?? string.Empty).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return string.Equals(full, root, StringComparison.OrdinalIgnoreCase);
        }

        private static string Get(Dictionary<string, string> values, string key)
        {
            string value;
            return values.TryGetValue(key, out value) ? value ?? string.Empty : string.Empty;
        }

        private static string FullPathOrEmpty(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : Path.GetFullPath(value);
        }

        private static bool ValidateConfigSourcePath(string path, string updateRootDirectory)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return true;
            }

            if (!File.Exists(path) || !string.Equals(Path.GetExtension(path), ".config", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string localAppData = Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
            return IsInsideDirectory(path, localAppData) || IsInsideDirectory(path, updateRootDirectory);
        }

        private static bool ValidateCurrentUserConfigPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return true;
            }

            if (!string.Equals(Path.GetExtension(path), ".config", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string localAppData = Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
            return IsInsideDirectory(path, localAppData);
        }

        internal static bool IsInsideDirectory(string path, string directory)
        {
            string root = Path.GetFullPath(directory)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string fullPath = Path.GetFullPath(path);
            return fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSha256(string value)
        {
            string cleaned = (value ?? string.Empty).Trim();
            return cleaned.Length == 64 && cleaned.All(Uri.IsHexDigit);
        }

        internal static Version ParseVersion(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            string text = value.Trim();
            if (text.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            {
                text = text.Substring(1);
            }

            int start = -1;
            int end = -1;
            for (int i = 0; i < text.Length; i++)
            {
                if (char.IsDigit(text[i]))
                {
                    if (start < 0)
                    {
                        start = i;
                    }
                    end = i;
                }
                else if (start >= 0 && text[i] != '.')
                {
                    break;
                }
            }

            if (start < 0 || end < start)
            {
                return null;
            }

            string versionText = text.Substring(start, end - start + 1).Trim('.');
            List<string> parts = versionText
                .Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries)
                .Take(4)
                .ToList();

            while (parts.Count < 4)
            {
                parts.Add("0");
            }

            Version parsed;
            return Version.TryParse(string.Join(".", parts.ToArray()), out parsed) ? parsed : null;
        }
    }

    internal sealed class UpdaterForm : Form
    {
        private const long MaximumPackageBytes = 500L * 1024L * 1024L;
        private const long MaximumMainExecutableBytes = 220L * 1024L * 1024L;
        private const long MaximumUpdaterExecutableBytes = 220L * 1024L * 1024L;
        private const long MinimumExecutableBytes = 16L * 1024L;
        private const string UpdaterExecutableName = "EasyCPDLC.Updater.exe";
        private const string MainExecutableName = "EasyCPDLC.exe";
        private const string MainConfigName = "EasyCPDLC.dll.config";

        private readonly UpdateArguments arguments;
        private readonly Label titleLabel;
        private readonly Label detailLabel;
        private readonly ProgressBar progressBar;
        private readonly string logPath;
        private bool updateRunning;

        public UpdaterForm(UpdateArguments arguments)
        {
            if (arguments == null)
            {
                throw new ArgumentNullException("arguments");
            }

            this.arguments = arguments;
            logPath = ResolveLogPath();

            Text = "EasyCPDLC Updater";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = true;
            TopMost = true;
            ClientSize = new Size(460, 148);
            BackColor = Color.FromArgb(12, 18, 22);
            ForeColor = Color.White;
            AutoScaleMode = AutoScaleMode.None;
            Font = new Font("Segoe UI", 9.0f, FontStyle.Regular, GraphicsUnit.Point);

            titleLabel = new Label
            {
                AutoSize = false,
                Left = 20,
                Top = 17,
                Width = 420,
                Height = 28,
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 10.5f, FontStyle.Regular, GraphicsUnit.Point),
                Text = "Preparing update"
            };

            detailLabel = new Label
            {
                AutoSize = false,
                Left = 20,
                Top = 48,
                Width = 420,
                Height = 30,
                ForeColor = Color.FromArgb(190, 210, 220),
                Text = "Starting EasyCPDLC Updater..."
            };

            progressBar = new ProgressBar
            {
                Left = 20,
                Top = 92,
                Width = 420,
                Height = 24,
                Minimum = 0,
                Maximum = 100,
                Style = ProgressBarStyle.Marquee,
                MarqueeAnimationSpeed = 28
            };

            Controls.Add(titleLabel);
            Controls.Add(detailLabel);
            Controls.Add(progressBar);

            FormClosing += UpdaterForm_FormClosing;
            Shown += UpdaterForm_Shown;
        }

        private void UpdaterForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (updateRunning)
            {
                e.Cancel = true;
            }
        }

        private async void UpdaterForm_Shown(object sender, EventArgs e)
        {
            await RunUpdateAsync();
        }

        private async Task RunUpdateAsync()
        {
            updateRunning = true;
            bool completed = false;
            List<FileReplacement> installedReplacements = null;
            string workRoot = Path.Combine(arguments.UpdateRootDirectory, "work_" + Guid.NewGuid().ToString("N"));

            try
            {
                Directory.CreateDirectory(workRoot);
                WriteLog("Updater started. ZIP=" + arguments.ZipPath + " APP=" + arguments.AppDirectory);

                SetStep("Verifying update", "Validating the running EasyCPDLC instance...", 5);
                await Task.Run((Action)ValidateCurrentInstallationIdentity);

                // Tell the launcher that process identity and the current EXE hash were
                // validated while EasyCPDLC was still alive. The launcher only exits
                // after this file appears, preventing a startup race.
                await Task.Run((Action)SignalLauncherReady);

                SetStep("Installing update", "Waiting for EasyCPDLC to close...", 12);
                await WaitForMainProcessAsync(arguments.WaitProcessId, TimeSpan.FromSeconds(90));
                TryDeleteFile(arguments.ReadyFilePath);

                SetStep("Verifying update", "Checking the current installation again...", 18);
                await Task.Run((Action)ValidateCurrentExecutableHash);

                SetStep("Verifying update", "Checking the downloaded package again...", 25);
                await Task.Run((Action)delegate { VerifySha256(arguments.ZipPath, arguments.ExpectedSha256); });

                string packageDirectory = Path.Combine(workRoot, "package");
                SetStep("Installing update", "Validating the restricted update package...", 38);
                UpdatePackage package = await Task.Run(() => ExtractRestrictedPackage(arguments.ZipPath, packageDirectory));

                SetStep("Installing update", "Atomically replacing EasyCPDLC files...", 68);
                installedReplacements = await Task.Run(() => InstallPackage(package));

                SetStep("Installing update", "Preserving user settings...", 88);
                await Task.Run((Action)PreserveUserSettingsBestEffort);

                SetStep("Restarting EasyCPDLC", "Opening the updated client...", 100);
                Process started = Process.Start(new ProcessStartInfo
                {
                    FileName = arguments.MainExecutablePath,
                    WorkingDirectory = arguments.AppDirectory,
                    UseShellExecute = true
                });

                if (started == null)
                {
                    throw new InvalidOperationException("Windows could not restart EasyCPDLC after the files were installed.");
                }

                completed = true;
                WriteLog("Update completed successfully. Restarted " + arguments.MainExecutablePath);
                await Task.Delay(1000);
                DeleteBackupsBestEffort(installedReplacements);
            }
            catch (Exception ex)
            {
                if (!completed && installedReplacements != null)
                {
                    RollBackFiles(installedReplacements);
                }

                WriteLog("Update failed: " + ex);
                SetStep("Update failed", ex.Message, -1);
                MessageBox.Show(
                    "EasyCPDLC update failed:" + Environment.NewLine + ex.Message + Environment.NewLine +
                    Environment.NewLine + "Log: " + logPath,
                    "EasyCPDLC Update Failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                if (completed)
                {
                    TryDeleteFile(arguments.ZipPath);
                    TryDeleteDirectory(workRoot);
                    TryDeleteDirectory(Path.Combine(arguments.UpdateRootDirectory, "launcher"));
                }

                TryDeleteFile(arguments.ReadyFilePath);
                updateRunning = false;
                Close();
            }
        }

        private void ValidateCurrentInstallationIdentity()
        {
            string currentPath;
            try
            {
                using (Process process = Process.GetProcessById(arguments.WaitProcessId))
                {
                    if (process.HasExited || process.MainModule == null)
                    {
                        throw new InvalidOperationException("The EasyCPDLC process closed before identity validation completed.");
                    }

                    currentPath = Path.GetFullPath(process.MainModule.FileName);
                }
            }
            catch (ArgumentException)
            {
                throw new InvalidOperationException("The EasyCPDLC process no longer exists.");
            }
            catch (Win32Exception ex)
            {
                throw new InvalidOperationException("Windows prevented validation of the EasyCPDLC process.", ex);
            }

            if (!string.Equals(currentPath, arguments.MainExecutablePath, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The EasyCPDLC process path changed before installation.");
            }

            ValidateCurrentExecutableHash();
            WriteLog("Current EasyCPDLC identity verified: " + currentPath);
        }

        private void ValidateCurrentExecutableHash()
        {
            if (!File.Exists(arguments.MainExecutablePath))
            {
                throw new FileNotFoundException("The installed EasyCPDLC.exe no longer exists.", arguments.MainExecutablePath);
            }

            string actual = ComputeSha256(arguments.MainExecutablePath);
            if (!string.Equals(actual, arguments.ExpectedCurrentExecutableSha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The installed EasyCPDLC.exe changed after the update was approved. Installation was aborted.");
            }
        }

        private Task WaitForMainProcessAsync(int processId, TimeSpan timeout)
        {
            return Task.Run((Action)delegate
            {
                try
                {
                    using (Process process = Process.GetProcessById(processId))
                    {
                        Stopwatch stopwatch = Stopwatch.StartNew();
                        while (!process.HasExited)
                        {
                            if (stopwatch.Elapsed > timeout)
                            {
                                throw new TimeoutException("EasyCPDLC did not close within " + (int)timeout.TotalSeconds + " seconds.");
                            }

                            Thread.Sleep(200);
                            process.Refresh();
                        }
                    }
                }
                catch (ArgumentException)
                {
                    // The process already exited before polling began.
                }
            });
        }

        private void SignalLauncherReady()
        {
            string directory = Path.GetDirectoryName(arguments.ReadyFilePath) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(directory) ||
                !UpdateArguments.IsInsideDirectory(arguments.ReadyFilePath, arguments.UpdateRootDirectory))
            {
                throw new InvalidOperationException("The updater startup-handshake path is invalid.");
            }

            Directory.CreateDirectory(directory);

            using (FileStream stream = new FileStream(
                arguments.ReadyFilePath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.Read))
            using (StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                writer.WriteLine("READY");
                writer.WriteLine(Process.GetCurrentProcess().Id.ToString(CultureInfo.InvariantCulture));
                writer.Flush();
                stream.Flush(true);
            }

            WriteLog("Updater startup handshake completed.");
        }

        private void VerifySha256(string filePath, string expectedSha256)
        {
            string actual = ComputeSha256(filePath);
            if (!string.Equals(actual, expectedSha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The update package SHA-256 checksum changed after download. Installation was aborted.");
            }

            WriteLog("Package SHA-256 verified: " + actual);
        }

        private static string ComputeSha256(string filePath)
        {
            using (FileStream stream = File.OpenRead(filePath))
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(stream);
                StringBuilder builder = new StringBuilder(hash.Length * 2);
                foreach (byte value in hash)
                {
                    builder.Append(value.ToString("x2", CultureInfo.InvariantCulture));
                }
                return builder.ToString();
            }
        }

        private UpdatePackage ExtractRestrictedPackage(string zipPath, string destinationDirectory)
        {
            Directory.CreateDirectory(destinationDirectory);

            Dictionary<string, ZipArchiveEntry> accepted = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
            long packageBytes = 0;

            using (ZipArchive archive = ZipFile.OpenRead(zipPath))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.FullName))
                    {
                        continue;
                    }

                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        throw new InvalidOperationException("The update ZIP may not contain directories.");
                    }

                    if (!string.Equals(entry.FullName, entry.Name, StringComparison.Ordinal) ||
                        entry.FullName.IndexOf('/') >= 0 ||
                        entry.FullName.IndexOf('\\') >= 0 ||
                        Path.IsPathRooted(entry.FullName) ||
                        entry.FullName.IndexOf(':') >= 0)
                    {
                        throw new InvalidOperationException("The update ZIP contains a nested or unsafe path: " + entry.FullName);
                    }

                    if (!IsAllowedPackageFile(entry.Name))
                    {
                        throw new InvalidOperationException("The update ZIP contains an unexpected file: " + entry.Name);
                    }

                    if (accepted.ContainsKey(entry.Name))
                    {
                        throw new InvalidOperationException("The update ZIP contains a duplicated file: " + entry.Name);
                    }

                    ValidateEntrySize(entry);
                    packageBytes += entry.Length;
                    if (packageBytes > MaximumPackageBytes)
                    {
                        throw new InvalidOperationException("The update package is unexpectedly large.");
                    }

                    accepted.Add(entry.Name, entry);
                }

                if (!accepted.ContainsKey(MainExecutableName))
                {
                    throw new InvalidOperationException("The update ZIP does not contain " + MainExecutableName + ".");
                }

                string main = ExtractAcceptedEntry(accepted[MainExecutableName], destinationDirectory);
                string config = accepted.ContainsKey(MainConfigName)
                    ? ExtractAcceptedEntry(accepted[MainConfigName], destinationDirectory)
                    : null;
                string updater = accepted.ContainsKey(UpdaterExecutableName)
                    ? ExtractAcceptedEntry(accepted[UpdaterExecutableName], destinationDirectory)
                    : null;

                ValidatePortableExecutable(main, MainExecutableName);
                if (!string.IsNullOrWhiteSpace(updater))
                {
                    ValidatePortableExecutable(updater, UpdaterExecutableName);
                }

                Version targetVersion = UpdateArguments.ParseVersion(arguments.TargetReleaseVersion);
                Version executableVersion = ReadExecutableVersion(main);
                if (targetVersion != null && executableVersion != null && executableVersion != targetVersion)
                {
                    WriteLog("Version metadata warning: package executable=" + executableVersion + " release=" + targetVersion + ". SHA-256 remains authoritative.");
                }

                WriteLog("Restricted package validated. Files=" + accepted.Count + " bytes=" + packageBytes);
                return new UpdatePackage(main, config, updater);
            }
        }

        private static bool IsAllowedPackageFile(string name)
        {
            return string.Equals(name, MainExecutableName, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, UpdaterExecutableName, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, MainConfigName, StringComparison.OrdinalIgnoreCase);
        }

        private static void ValidateEntrySize(ZipArchiveEntry entry)
        {
            if (entry.Length < 0)
            {
                throw new InvalidOperationException("The update ZIP contains an invalid file length.");
            }

            if (string.Equals(entry.Name, MainExecutableName, StringComparison.OrdinalIgnoreCase))
            {
                if (entry.Length < MinimumExecutableBytes || entry.Length > MaximumMainExecutableBytes)
                {
                    throw new InvalidOperationException("The update ZIP contains an invalid EasyCPDLC executable size.");
                }
                return;
            }

            if (string.Equals(entry.Name, UpdaterExecutableName, StringComparison.OrdinalIgnoreCase))
            {
                // The updater may be a small framework-dependent executable or a much larger
                // self-contained .NET executable. Do not reject a valid updater solely because
                // of its packaging model; the extracted file is additionally validated as PE.
                if (entry.Length <= 0 || entry.Length > MaximumUpdaterExecutableBytes)
                {
                    throw new InvalidOperationException(
                        "The update ZIP contains an invalid updater executable size: " +
                        FormatByteCount(entry.Length) + " (maximum " +
                        FormatByteCount(MaximumUpdaterExecutableBytes) + ").");
                }
                return;
            }

            if (entry.Length > 1024L * 1024L)
            {
                throw new InvalidOperationException("The update configuration file is unexpectedly large.");
            }
        }

        private static string FormatByteCount(long bytes)
        {
            string[] units = { "B", "KB", "MB", "GB" };
            double value = Math.Max(0L, bytes);
            int unit = 0;

            while (value >= 1024.0 && unit < units.Length - 1)
            {
                value /= 1024.0;
                unit++;
            }

            return value.ToString(unit == 0 ? "0" : "0.0", CultureInfo.InvariantCulture) + " " + units[unit];
        }

        private static string ExtractAcceptedEntry(ZipArchiveEntry entry, string destinationDirectory)
        {
            string targetPath = Path.GetFullPath(Path.Combine(destinationDirectory, entry.Name));
            if (!UpdateArguments.IsInsideDirectory(targetPath, destinationDirectory))
            {
                throw new InvalidOperationException("The updater refused an unsafe package path.");
            }

            using (Stream source = entry.Open())
            using (FileStream destination = new FileStream(targetPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                source.CopyTo(destination);
                destination.Flush(true);
            }

            if (new FileInfo(targetPath).Length != entry.Length)
            {
                throw new IOException("The extracted file length did not match the update package: " + entry.Name);
            }

            return targetPath;
        }

        private static void ValidatePortableExecutable(string path, string displayName)
        {
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                if (stream.Length < 256 || reader.ReadUInt16() != 0x5A4D)
                {
                    throw new InvalidOperationException(displayName + " is not a valid Windows executable.");
                }

                stream.Position = 0x3C;
                int peOffset = reader.ReadInt32();
                if (peOffset <= 0 || peOffset > stream.Length - 4)
                {
                    throw new InvalidOperationException(displayName + " has an invalid PE header offset.");
                }

                stream.Position = peOffset;
                if (reader.ReadUInt32() != 0x00004550)
                {
                    throw new InvalidOperationException(displayName + " does not contain a valid PE signature.");
                }
            }
        }

        private List<FileReplacement> InstallPackage(UpdatePackage package)
        {
            List<FileReplacement> replacements = new List<FileReplacement>();

            try
            {
                ReplaceFileAtomically(package.MainExecutable, SafeTargetPath(MainExecutableName), replacements);

                if (!string.IsNullOrWhiteSpace(package.ConfigFile))
                {
                    ReplaceFileAtomically(package.ConfigFile, SafeTargetPath(MainConfigName), replacements);
                }

                if (!string.IsNullOrWhiteSpace(package.UpdaterExecutable))
                {
                    ReplaceFileAtomically(package.UpdaterExecutable, SafeTargetPath(UpdaterExecutableName), replacements);
                }

                return replacements;
            }
            catch
            {
                RollBackFiles(replacements);
                throw;
            }
        }

        private void ReplaceFileAtomically(string sourcePath, string targetPath, List<FileReplacement> replacements)
        {
            EnsureInsideAppDirectory(targetPath);

            string token = Guid.NewGuid().ToString("N");
            string stagePath = targetPath + ".easycpdlc-new-" + token;
            string backupPath = targetPath + ".easycpdlc-backup-" + token;
            bool existed = File.Exists(targetPath);

            try
            {
                File.Copy(sourcePath, stagePath, false);
                string sourceHash = ComputeSha256(sourcePath);
                string stageHash = ComputeSha256(stagePath);
                if (!string.Equals(sourceHash, stageHash, StringComparison.OrdinalIgnoreCase))
                {
                    throw new IOException("The staged file did not match its package source: " + Path.GetFileName(targetPath));
                }

                if (existed)
                {
                    File.Replace(stagePath, targetPath, backupPath, true);
                }
                else
                {
                    File.Move(stagePath, targetPath);
                }

                string targetHash = ComputeSha256(targetPath);
                if (!string.Equals(sourceHash, targetHash, StringComparison.OrdinalIgnoreCase))
                {
                    throw new IOException("The installed file did not match its package source: " + Path.GetFileName(targetPath));
                }

                replacements.Add(new FileReplacement(targetPath, backupPath, stagePath, existed));
                WriteLog("Atomically installed " + targetPath);
            }
            catch
            {
                TryDeleteFile(stagePath);
                if (File.Exists(backupPath))
                {
                    try
                    {
                        if (File.Exists(targetPath))
                        {
                            File.Replace(backupPath, targetPath, null, true);
                        }
                        else
                        {
                            File.Move(backupPath, targetPath);
                        }
                    }
                    catch
                    {
                        // The outer rollback/logging path will report any persistent problem.
                    }
                }
                else if (!existed)
                {
                    TryDeleteFile(targetPath);
                }
                throw;
            }
        }

        private void RollBackFiles(List<FileReplacement> replacements)
        {
            WriteLog("Installation failed. Rolling back " + replacements.Count + " file operation(s).");

            foreach (FileReplacement replacement in replacements.AsEnumerable().Reverse())
            {
                try
                {
                    TryDeleteFile(replacement.StagePath);

                    if (replacement.ExistedBefore && File.Exists(replacement.BackupPath))
                    {
                        if (File.Exists(replacement.TargetPath))
                        {
                            File.Replace(replacement.BackupPath, replacement.TargetPath, null, true);
                        }
                        else
                        {
                            File.Move(replacement.BackupPath, replacement.TargetPath);
                        }
                    }
                    else if (!replacement.ExistedBefore && File.Exists(replacement.TargetPath))
                    {
                        File.Delete(replacement.TargetPath);
                    }
                }
                catch (Exception ex)
                {
                    WriteLog("Rollback warning for " + replacement.TargetPath + ": " + ex.Message);
                }
            }
        }

        private static void DeleteBackupsBestEffort(List<FileReplacement> replacements)
        {
            if (replacements == null)
            {
                return;
            }

            foreach (FileReplacement replacement in replacements)
            {
                TryDeleteFile(replacement.BackupPath);
                TryDeleteFile(replacement.StagePath);
            }
        }

        private string SafeTargetPath(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName) ||
                !string.Equals(fileName, Path.GetFileName(fileName), StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Unsafe target file name: " + fileName);
            }

            string target = Path.GetFullPath(Path.Combine(arguments.AppDirectory, fileName));
            EnsureInsideAppDirectory(target);
            return target;
        }

        private void EnsureInsideAppDirectory(string path)
        {
            if (!UpdateArguments.IsInsideDirectory(path, arguments.AppDirectory))
            {
                throw new InvalidOperationException("The updater refused to write outside the EasyCPDLC directory.");
            }
        }

        private void PreserveUserSettingsBestEffort()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(arguments.ConfigSourcePath) || !File.Exists(arguments.ConfigSourcePath))
                {
                    WriteLog("No user.config source was supplied. Fixed EasyCPDLC settings remain untouched.");
                    return;
                }

                HashSet<string> destinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (!string.IsNullOrWhiteSpace(arguments.CurrentUserConfigPath))
                {
                    destinations.Add(arguments.CurrentUserConfigPath);

                    string targetVersionedPath = BuildTargetVersionedConfigPath(
                        arguments.CurrentUserConfigPath,
                        arguments.TargetReleaseVersion);
                    if (!string.IsNullOrWhiteSpace(targetVersionedPath))
                    {
                        destinations.Add(targetVersionedPath);
                    }
                }

                foreach (string destination in destinations)
                {
                    try
                    {
                        if (string.Equals(
                            Path.GetFullPath(destination),
                            Path.GetFullPath(arguments.ConfigSourcePath),
                            StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        string directory = Path.GetDirectoryName(destination);
                        if (!string.IsNullOrWhiteSpace(directory))
                        {
                            Directory.CreateDirectory(directory);
                        }

                        File.Copy(arguments.ConfigSourcePath, destination, true);
                        WriteLog("Preserved user config at " + destination);
                    }
                    catch (Exception ex)
                    {
                        WriteLog("Could not preserve user config at " + destination + ": " + ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                WriteLog("User config preservation warning: " + ex.Message);
            }
        }

        private static string BuildTargetVersionedConfigPath(string currentConfigPath, string targetVersionText)
        {
            try
            {
                Version targetVersion = UpdateArguments.ParseVersion(targetVersionText);
                if (targetVersion == null || string.IsNullOrWhiteSpace(currentConfigPath))
                {
                    return string.Empty;
                }

                FileInfo current = new FileInfo(currentConfigPath);
                DirectoryInfo currentVersionDirectory = current.Directory;
                if (currentVersionDirectory == null ||
                    currentVersionDirectory.Parent == null ||
                    UpdateArguments.ParseVersion(currentVersionDirectory.Name) == null)
                {
                    return string.Empty;
                }

                return Path.Combine(currentVersionDirectory.Parent.FullName, targetVersion.ToString(), "user.config");
            }
            catch
            {
                return string.Empty;
            }
        }

        private static Version ReadExecutableVersion(string executablePath)
        {
            try
            {
                FileVersionInfo info = FileVersionInfo.GetVersionInfo(executablePath);
                return UpdateArguments.ParseVersion(info.ProductVersion) ?? UpdateArguments.ParseVersion(info.FileVersion);
            }
            catch
            {
                return null;
            }
        }

        private void SetStep(string title, string detail, int percent)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(delegate { SetStep(title, detail, percent); }));
                return;
            }

            titleLabel.Text = title ?? string.Empty;
            detailLabel.Text = detail ?? string.Empty;

            if (percent >= 0)
            {
                progressBar.Style = ProgressBarStyle.Continuous;
                progressBar.MarqueeAnimationSpeed = 0;
                progressBar.Value = Math.Max(0, Math.Min(100, percent));
            }
            else
            {
                progressBar.Style = ProgressBarStyle.Marquee;
                progressBar.MarqueeAnimationSpeed = 28;
            }

            titleLabel.Refresh();
            detailLabel.Refresh();
            progressBar.Refresh();
        }

        private void WriteLog(string text)
        {
            try
            {
                string directory = Path.GetDirectoryName(logPath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.AppendAllText(
                    logPath,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + " " + text + Environment.NewLine);
            }
            catch
            {
                // Logging must never interrupt installation or rollback.
            }
        }

        private static string ResolveLogPath()
        {
            string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(root, "EasyCPDLCModernized", "Logs", "EasyCPDLC_Updater.log");
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
            }
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
            }
            catch
            {
            }
        }

        private sealed class UpdatePackage
        {
            public UpdatePackage(string mainExecutable, string configFile, string updaterExecutable)
            {
                MainExecutable = mainExecutable;
                ConfigFile = configFile;
                UpdaterExecutable = updaterExecutable;
            }

            public string MainExecutable { get; private set; }
            public string ConfigFile { get; private set; }
            public string UpdaterExecutable { get; private set; }
        }

        private sealed class FileReplacement
        {
            public FileReplacement(string targetPath, string backupPath, string stagePath, bool existedBefore)
            {
                TargetPath = targetPath;
                BackupPath = backupPath;
                StagePath = stagePath;
                ExistedBefore = existedBefore;
            }

            public string TargetPath { get; private set; }
            public string BackupPath { get; private set; }
            public string StagePath { get; private set; }
            public bool ExistedBefore { get; private set; }
        }
    }
}
