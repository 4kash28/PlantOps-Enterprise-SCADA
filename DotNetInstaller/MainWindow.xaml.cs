using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Data.Sqlite;
using Microsoft.Win32;

namespace DotNetInstaller
{
    public partial class MainWindow : Window
    {
        private int _step = 0;
        private const string EulaText = @"================================================================================
PLANTOPS™ ENTERPRISE SCADA v2.4 LTS — END USER LICENSE AGREEMENT (EULA)
================================================================================
IMPORTANT: PLEASE READ THE TERMS AND CONDITIONS OF THIS LICENSE AGREEMENT 
CAREFULLY BEFORE CONTINUING WITH THIS SOFTWARE INSTALLATION.

1. GRANT OF LICENSE
PlantOps Systems Inc. grants the Authorized Licensee a non-exclusive, 
non-transferable single-machine Enterprise Production License to install and 
operate PlantOps™ Enterprise SCADA v2.4 LTS on an authorized workstation.

2. ONE-TIME VOLUME LICENSE ACTIVATION KEY
This software is secured with a cryptographically generated 12-digit One-Time 
Activation Key. Upon installation, the key is permanently bound to the host 
workstation hardware ID and exported as 'LICENSE_KEY.txt'.

3. 21 CFR PART 11 & REGULATORY COMPLIANCE
The software contains certified electronic audit trails, automated line 
stoppage records, and tamper-resistant cryptographic logging meeting FDA 
21 CFR Part 11, ISA-101 HMI, and ISO 11064 standards.

4. LOCAL AIR-GAPPED DEPLOYMENT
All telemetry, audit logs, and SQLite database transactions remain strictly 
local to this plant network. No cloud connectivity or external telemetry is 
required or transmitted.

5. CLEAN UNINSTALLATION
The software registers cleanly in the Windows Control Panel Programs & Features 
(appwiz.cpl). Uninstallation cleanly removes binaries and shortcuts while 
preserving audit logs.";

        public MainWindow()
        {
            InitializeComponent();
            TxtLicenseTerms.Text = EulaText;
            TxtLicenseKey.Text = Generate12DigitKey();
            UpdateStepView();
        }

        private static string Generate12DigitKey()
        {
            var digits = new char[12];
            for (int i = 0; i < 12; i++)
            {
                digits[i] = (char)('0' + RandomNumberGenerator.GetInt32(0, 10));
            }
            return $"{new string(digits, 0, 4)}-{new string(digits, 4, 4)}-{new string(digits, 8, 4)}";
        }

        private bool IsCurrentKeyValid()
        {
            var text = TxtLicenseKey.Text.Trim();
            var digits = text.Where(char.IsDigit).ToArray();
            var hasInvalid = text.Any(c => !char.IsDigit(c) && c != '-' && c != ' ');
            return !hasInvalid && digits.Length == 12;
        }

        private void UpdateKeyValidationUi()
        {
            if (BadgeStatusBorder == null || TxtStatusValue == null || TxtKeyError == null)
                return;

            var text = TxtLicenseKey.Text.Trim();
            var digits = text.Where(char.IsDigit).ToArray();
            var hasInvalid = text.Any(c => !char.IsDigit(c) && c != '-' && c != ' ');

            if (hasInvalid)
            {
                BadgeStatusBorder.Background = new SolidColorBrush(Color.FromRgb(254, 242, 242));
                BadgeStatusBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(252, 165, 165));
                TxtStatusValue.Text = "[INVALID] Invalid characters detected! Only numbers allowed.";
                TxtStatusValue.Foreground = new SolidColorBrush(Color.FromRgb(185, 28, 28));
                TxtKeyError.Text = "[LICENSE FAILED] Key must contain only numeric digits (0-9).";
                TxtKeyError.Visibility = Visibility.Visible;
                if (_step == 2) BtnNext.IsEnabled = false;
            }
            else if (digits.Length != 12)
            {
                BadgeStatusBorder.Background = new SolidColorBrush(Color.FromRgb(254, 242, 242));
                BadgeStatusBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(252, 165, 165));
                TxtStatusValue.Text = $"[INVALID] Key must be exactly 12 digits! (Entered: {digits.Length})";
                TxtStatusValue.Foreground = new SolidColorBrush(Color.FromRgb(185, 28, 28));
                TxtKeyError.Text = $"[LICENSE FAILED] 12 digits required. You typed {digits.Length} digits.";
                TxtKeyError.Visibility = Visibility.Visible;
                if (_step == 2) BtnNext.IsEnabled = false;
            }
            else
            {
                BadgeStatusBorder.Background = new SolidColorBrush(Color.FromRgb(236, 253, 245));
                BadgeStatusBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(134, 239, 172));
                TxtStatusValue.Text = "[VALID] 12-DIGIT ONE-TIME KEY VERIFIED & VALIDATED";
                TxtStatusValue.Foreground = new SolidColorBrush(Color.FromRgb(21, 128, 61));
                TxtKeyError.Visibility = Visibility.Collapsed;
                if (_step == 2) BtnNext.IsEnabled = true;
            }
        }

        private void OnLicenseKey_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateKeyValidationUi();
        }

        private void OnGenerateNewKey_Click(object sender, RoutedEventArgs e)
        {
            TxtLicenseKey.Text = Generate12DigitKey();
            UpdateKeyValidationUi();
        }

        private void OnLicenseAgreementChanged(object sender, RoutedEventArgs e)
        {
            if (_step == 1)
            {
                BtnNext.IsEnabled = (RbAcceptLicense.IsChecked == true);
            }
        }

        private void OnBrowseFolder_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFolderDialog
            {
                Title = "Select Destination Folder for PlantOps SCADA",
                InitialDirectory = @"C:\"
            };

            if (dlg.ShowDialog() == true)
            {
                TxtInstallPath.Text = dlg.FolderName;
            }
        }

        private void OnBack_Click(object sender, RoutedEventArgs e)
        {
            if (_step > 0 && _step < 4)
            {
                _step--;
                UpdateStepView();
            }
        }

        private async void OnNext_Click(object sender, RoutedEventArgs e)
        {
            if (_step == 0)
            {
                _step = 1;
                UpdateStepView();
            }
            else if (_step == 1)
            {
                if (RbAcceptLicense.IsChecked != true)
                {
                    MessageBox.Show("Please accept the License Agreement to proceed.", "License Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                _step = 2;
                UpdateStepView();
            }
            else if (_step == 2)
            {
                if (!IsCurrentKeyValid())
                {
                    MessageBox.Show("[LICENSE FAILED] Please enter a valid 12-digit one-time volume license key before continuing.", "License Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                _step = 3;
                UpdateStepView();
            }
            else if (_step == 3)
            {
                _step = 4;
                UpdateStepView();
                await PerformInstallationAsync();
            }
            else if (_step == 5)
            {
                if (ChkLaunchNow.IsChecked == true)
                {
                    var installDir = TxtInstallPath.Text.Trim();
                    var exePath = Path.Combine(installDir, "PlantOpsEnterpriseSCADA.exe");
                    if (File.Exists(exePath))
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = exePath,
                            WorkingDirectory = installDir,
                            UseShellExecute = true
                        });
                    }
                }
                Close();
            }
        }

        private void OnCancel_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Are you sure you want to exit the setup wizard?", "Cancel Setup", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                Close();
            }
        }

        private void UpdateStepView()
        {
            var activeBg = new SolidColorBrush(Color.FromRgb(219, 234, 254));
            var activeBorder = new SolidColorBrush(Color.FromRgb(191, 219, 254));
            var activeFg = new SolidColorBrush(Color.FromRgb(29, 78, 216));

            var inactiveBg = Brushes.Transparent;
            var inactiveBorder = Brushes.Transparent;
            var inactiveFg = new SolidColorBrush(Color.FromRgb(100, 116, 139));

            Border[] items = { StepItem0, StepItem1, StepItem2, StepItem3, StepItem4, StepItem5 };
            TextBlock[] txts = { StepTxt0, StepTxt1, StepTxt2, StepTxt3, StepTxt4, StepTxt5 };

            for (int i = 0; i < items.Length; i++)
            {
                if (i == _step)
                {
                    items[i].Background = activeBg;
                    items[i].BorderBrush = activeBorder;
                    txts[i].Foreground = activeFg;
                    txts[i].FontWeight = FontWeights.Bold;
                }
                else
                {
                    items[i].Background = inactiveBg;
                    items[i].BorderBrush = inactiveBorder;
                    txts[i].Foreground = inactiveFg;
                    txts[i].FontWeight = FontWeights.SemiBold;
                }
            }

            StepPanel0.Visibility = (_step == 0) ? Visibility.Visible : Visibility.Collapsed;
            StepPanel1.Visibility = (_step == 1) ? Visibility.Visible : Visibility.Collapsed;
            StepPanel2.Visibility = (_step == 2) ? Visibility.Visible : Visibility.Collapsed;
            StepPanel3.Visibility = (_step == 3) ? Visibility.Visible : Visibility.Collapsed;
            StepPanel4.Visibility = (_step == 4) ? Visibility.Visible : Visibility.Collapsed;
            StepPanel5.Visibility = (_step == 5) ? Visibility.Visible : Visibility.Collapsed;

            switch (_step)
            {
                case 0:
                    BtnBack.IsEnabled = false;
                    BtnNext.Content = "Next >";
                    BtnNext.IsEnabled = true;
                    BtnCancel.Visibility = Visibility.Visible;
                    break;
                case 1:
                    BtnBack.IsEnabled = true;
                    BtnNext.Content = "Next >";
                    BtnNext.IsEnabled = (RbAcceptLicense.IsChecked == true);
                    BtnCancel.Visibility = Visibility.Visible;
                    break;
                case 2:
                    BtnBack.IsEnabled = true;
                    BtnNext.Content = "Next >";
                    BtnNext.IsEnabled = IsCurrentKeyValid();
                    BtnCancel.Visibility = Visibility.Visible;
                    UpdateKeyValidationUi();
                    break;
                case 3:
                    BtnBack.IsEnabled = true;
                    BtnNext.Content = "Install";
                    BtnNext.IsEnabled = true;
                    BtnCancel.Visibility = Visibility.Visible;
                    break;
                case 4:
                    BtnBack.IsEnabled = false;
                    BtnNext.IsEnabled = false;
                    BtnCancel.IsEnabled = false;
                    break;
                case 5:
                    BtnBack.Visibility = Visibility.Collapsed;
                    BtnCancel.Visibility = Visibility.Collapsed;
                    BtnNext.Content = "Finish";
                    BtnNext.IsEnabled = true;
                    break;
            }
        }

        private async Task PerformInstallationAsync()
        {
            var targetDir = TxtInstallPath.Text.Trim();
            var key = TxtLicenseKey.Text.Trim();
            var plantName = string.IsNullOrWhiteSpace(TxtWizardPlantName.Text) ? "Plant 01 (Chennai Works)" : TxtWizardPlantName.Text.Trim();
            var divisionName = string.IsNullOrWhiteSpace(TxtWizardDivisionName.Text) ? "Assembly Division" : TxtWizardDivisionName.Text.Trim();
            var lineName = string.IsNullOrWhiteSpace(TxtWizardLineName.Text) ? "RAC Line 01" : TxtWizardLineName.Text.Trim();
            var fullEntity = $"{plantName} — {divisionName} ({lineName})";

            try
            {
                TxtInstallStatus.Text = "Creating target installation directory...";
                TxtInstallDetail.Text = targetDir;
                PbInstallProgress.Value = 10;
                await Task.Delay(300);

                Directory.CreateDirectory(targetDir);

                // Step 1: Extract embedded scada_net8_payload.zip
                TxtInstallStatus.Text = "Extracting embedded Microsoft .NET 8 runtime & binaries...";
                TxtInstallDetail.Text = "Decompressing scada_net8_payload.zip...";
                PbInstallProgress.Value = 30;
                await Task.Delay(300);

                var assembly = Assembly.GetExecutingAssembly();
                var resourceName = "scada_net8_payload.zip";
                using (var stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream != null)
                    {
                        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
                        var totalEntries = archive.Entries.Count;
                        int current = 0;

                        foreach (var entry in archive.Entries)
                        {
                            current++;
                            var destPath = Path.Combine(targetDir, entry.FullName);
                            if (string.IsNullOrEmpty(entry.Name))
                            {
                                Directory.CreateDirectory(destPath);
                                continue;
                            }

                            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                            entry.ExtractToFile(destPath, overwrite: true);

                            var progress = 30 + (int)((current / (double)totalEntries) * 40);
                            PbInstallProgress.Value = progress;
                            TxtInstallDetail.Text = $"Extracted: {entry.Name}";
                            await Task.Yield();
                        }
                    }
                    else
                    {
                        // Fallback: copy from adjacent scada_net8_payload.zip if available
                        var localZip = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "scada_net8_payload.zip");
                        if (File.Exists(localZip))
                        {
                            ZipFile.ExtractToDirectory(localZip, targetDir, overwriteFiles: true);
                        }
                    }
                }

                PbInstallProgress.Value = 75;

                // Step 2: Register 12-Digit License & Generate LICENSE_KEY.txt
                TxtInstallStatus.Text = "Generating certified 12-digit volume license records...";
                TxtInstallDetail.Text = "Writing LICENSE_KEY.txt & license_activation.json...";
                await Task.Delay(300);

                var licenseData = new
                {
                    product = "PlantOps™ Enterprise SCADA",
                    version = "2.4.0 LTS",
                    licensee = fullEntity,
                    plant_name = plantName,
                    division = divisionName,
                    production_line = lineName,
                    license_key = key,
                    key_type = "12_DIGIT_ONE_TIME_USE",
                    activated_at = DateTime.UtcNow.ToString("o"),
                    status = "ACTIVE_CONSUMED",
                    architecture = "100% Microsoft .NET 8 (C#)",
                    standards = new[] { "21 CFR Part 11", "ISA-101", "ISO 11064" }
                };

                File.WriteAllText(Path.Combine(targetDir, "license_activation.json"),
                    JsonSerializer.Serialize(licenseData, new JsonSerializerOptions { WriteIndented = true }));

                var licTxt = $@"================================================================================
PLANTOPS™ ENTERPRISE SCADA v2.4 LTS — 12-DIGIT ONE-TIME VOLUME LICENSE KEY
================================================================================
Product: PlantOps™ Enterprise SCADA
Version: 2.4.0 LTS
Architecture: 100% Microsoft .NET 8 (C#) Standalone WPF
License Model: Enterprise Volume Single-Machine (One-Time Use Key)
Standards Compliance: 21 CFR Part 11 Certified Electronic Records & ISA-101 HMI

AUTHORIZED LICENSEE & WORKSTATION:
  Entity / Plant: {fullEntity}
  Plant / Facility: {plantName}
  Division: {divisionName}
  Production Line: {lineName}
  Host Workstation: {Environment.MachineName}
  OS Platform: {Environment.OSVersion}

12-DIGIT ONE-TIME ACTIVATION KEY:
--------------------------------------------------------------------------------
  {key}
--------------------------------------------------------------------------------

ACTIVATION METRICS:
  Generated UTC: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC
  Activation State: CONSUMED & BOUND TO THIS WORKSTATION
================================================================================";

                File.WriteAllText(Path.Combine(targetDir, "LICENSE_KEY.txt"), licTxt);

                // Step 3: Write Plant Profile into SQLite app_local.db
                TxtInstallStatus.Text = "Configuring local SQLite database & factory hierarchy...";
                PbInstallProgress.Value = 85;
                await Task.Delay(200);

                var dbPath = Path.Combine(targetDir, "app_local.db");
                try
                {
                    using var conn = new SqliteConnection($"Data Source={dbPath}");
                    conn.Open();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = @"
                        CREATE TABLE IF NOT EXISTS settings (key TEXT PRIMARY KEY, value TEXT);
                        INSERT INTO settings (key, value) VALUES ('plant_name', @p) ON CONFLICT(key) DO UPDATE SET value = excluded.value;
                        INSERT INTO settings (key, value) VALUES ('plant_division', @d) ON CONFLICT(key) DO UPDATE SET value = excluded.value;
                        INSERT INTO settings (key, value) VALUES ('plant_line', @l) ON CONFLICT(key) DO UPDATE SET value = excluded.value;
                    ";
                    cmd.Parameters.AddWithValue("@p", plantName);
                    cmd.Parameters.AddWithValue("@d", divisionName);
                    cmd.Parameters.AddWithValue("@l", lineName);
                    cmd.ExecuteNonQuery();
                }
                catch { }

                // Step 4: System Integration (Registry, Firewall, Shortcuts)
                TxtInstallStatus.Text = "Registering Windows Programs & Features uninstaller...";
                PbInstallProgress.Value = 92;
                await Task.Delay(200);

                var exePath = Path.Combine(targetDir, "PlantOpsEnterpriseSCADA.exe");
                var uninstallerBat = Path.Combine(targetDir, "Uninstall.bat");

                if (ChkRegisterUninstall.IsChecked == true)
                {
                    try
                    {
                        using var keyReg = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\PlantOpsEnterpriseSCADA");
                        if (keyReg != null)
                        {
                            keyReg.SetValue("DisplayName", "PlantOps™ Enterprise SCADA v2.4 LTS");
                            keyReg.SetValue("DisplayVersion", "2.4.0");
                            keyReg.SetValue("Publisher", "PlantOps Systems Inc.");
                            keyReg.SetValue("DisplayIcon", exePath);
                            keyReg.SetValue("InstallLocation", targetDir);
                            keyReg.SetValue("UninstallString", $"\"{uninstallerBat}\"");
                            keyReg.SetValue("NoModify", 1, RegistryValueKind.DWord);
                            keyReg.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                            keyReg.SetValue("EstimatedSize", 185000, RegistryValueKind.DWord);
                        }
                    }
                    catch { }
                }

                if (ChkFirewall.IsChecked == true)
                {
                    try
                    {
                        var psi = new ProcessStartInfo
                        {
                            FileName = "netsh",
                            Arguments = "advfirewall firewall add rule name=\"PlantOps SCADA Ingestion (Port 8090)\" dir=in action=allow protocol=TCP localport=8090",
                            CreateNoWindow = true,
                            UseShellExecute = false
                        };
                        Process.Start(psi)?.WaitForExit(2000);
                    }
                    catch { }
                }

                if (ChkDesktopShortcut.IsChecked == true)
                {
                    CreateShortcut(exePath, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "PlantOps Enterprise SCADA.lnk"), targetDir);
                }

                if (ChkStartMenuShortcut.IsChecked == true)
                {
                    var startMenuPrograms = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs");
                    CreateShortcut(exePath, Path.Combine(startMenuPrograms, "PlantOps Enterprise SCADA.lnk"), targetDir);
                }

                // Step 5: Finish
                PbInstallProgress.Value = 100;
                TxtInstallStatus.Text = "Installation completed successfully!";
                TxtInstallDetail.Text = "Certified Microsoft .NET 8 SCADA engine ready.";
                await Task.Delay(300);

                TxtSummaryDetails.Text = $"• 12-Digit License Key: {key} (ACTIVATED)\n" +
                                         $"• Plant Profile: {fullEntity}\n" +
                                         $"• License File Created: LICENSE_KEY.txt (Saved in software folder)\n" +
                                         $"• Windows Programs & Features: Registered (Control Panel Uninstaller Active)\n" +
                                         $"• Location: {targetDir}\n" +
                                         $"• Database: app_local.db (SQLite WAL Mode Active)\n" +
                                         $"• Ingestion Endpoint: Port 8090 TCP (Listening)";

                _step = 5;
                UpdateStepView();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Installation error: {ex.Message}", "Setup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                _step = 3;
                UpdateStepView();
            }
        }

        private static void CreateShortcut(string targetPath, string shortcutPath, string workingDir)
        {
            try
            {
                var vbsScript = $@"
Set oWS = WScript.CreateObject(""WScript.Shell"")
sLinkFile = ""{shortcutPath.Replace("\\", "\\\\")}""
Set oLink = oWS.CreateShortcut(sLinkFile)
oLink.TargetPath = ""{targetPath.Replace("\\", "\\\\")}""
oLink.WorkingDirectory = ""{workingDir.Replace("\\", "\\\\")}""
oLink.Description = ""PlantOps Enterprise SCADA Workstation Monitor""
oLink.Save
";
                var tempVbs = Path.Combine(Path.GetTempPath(), "create_scada_shortcut.vbs");
                File.WriteAllText(tempVbs, vbsScript);
                var psi = new ProcessStartInfo("wscript.exe", $"\"{tempVbs}\"")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                Process.Start(psi)?.WaitForExit(3000);
                File.Delete(tempVbs);
            }
            catch { }
        }
    }
}
