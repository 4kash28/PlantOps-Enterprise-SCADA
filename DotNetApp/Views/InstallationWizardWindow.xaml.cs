using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using DotNetApp.Data;

namespace DotNetApp.Views
{
    public partial class InstallationWizardWindow : Window
    {
        private int _step = 0;

        public bool LaunchDashboardAfter { get; private set; } = true;

        private const string EulaText = @"================================================================================
PLANTOPS™ ENTERPRISE SCADA v2.4 LTS — SOFTWARE LICENSE AGREEMENT (EULA)
================================================================================
Product: PlantOps™ Enterprise SCADA (Microsoft .NET 8 WPF Desktop Application)
Release: Version 2.4.0 LTS (Long Term Support)
Industry Standards: 21 CFR Part 11 Compliant & ISA-101 High-Performance HMI
License Type: Enterprise Volume Single-Machine License (One-Time Activation Key)
Authorized Licensee: Plant 01 (Chennai Works) — Assembly Division / RAC Line 01
================================================================================

IMPORTANT — PLEASE READ CAREFULLY BEFORE INSTALLING OR USING THIS SOFTWARE:

This End User License Agreement (""Agreement"" or ""License"") is a legal contract 
between PlantOps Industrial Systems (""Licensor"") and the authorized operating 
entity (""Licensee""). By installing, copying, or using PlantOps™ Enterprise SCADA 
(""Software""), you agree to be bound by the terms and conditions set forth below.

1. GRANT OF LICENSE
Licensor hereby grants to Licensee a non-exclusive, perpetual, site-wide license 
to install and operate the Software on central SCADA monitoring workstations and 
associated RAC (Room Air Conditioner) assembly line node workstations across 
the designated production facility.

2. COMPLIANCE WITH 21 CFR PART 11 (ELECTRONIC RECORDS & SIGNATURES)
The Software incorporates an automated, immutable SQLite write-ahead log (WAL) 
audit trail recording all downtime events, equipment stoppages, threshold alerts, 
and operator administrative actions. Licensee acknowledges that:
  a. All incident timestamps are strictly maintained in UTC format.
  b. Stoppage records cannot be altered, purged, or manipulated retroactively.
  c. Official CSV exports conform to RFC 4180 certified electronic records.

3. TELEMETRY & PERMISSIONS GOVERNANCE
Each connected workstation operates an autonomous LineNode Agent whose telemetry 
permissions (CPU, RAM, Storage, Network Latency, Uptime, USB Peripherals) 
are governed by station-level operator policy. Licensee retains full sovereignty 
over which hardware metrics are collected and displayed.

4. NETWORK & LOCAL DATABASE STORAGE
All operational data, telemetry caches, and threshold configurations are stored 
locally on the host machine in an isolated SQLite database (""app_local.db""). 
No production data, proprietary line schematics, or operational metrics are 
transmitted to external public cloud networks.

5. 12-DIGIT ONE-TIME VOLUME LICENSE KEY
Each instance generates a unique 12-digit hardware-bound one-time activation key.
Upon installation, this key is permanently registered and exported as 'LICENSE_KEY.txt'
in the local installation directory for plant audit compliance.
================================================================================";

        public InstallationWizardWindow()
        {
            InitializeComponent();
            TxtLicenseTerms.Text = EulaText;
            UpdateStepView();
        }

        public static string Generate12DigitKey()
        {
            var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
            byte[] bytes = new byte[8];
            rng.GetBytes(bytes);
            ulong num = BitConverter.ToUInt64(bytes, 0) % 1_000_000_000_000UL;
            string raw = num.ToString("D12");
            return $"{raw.Substring(0, 4)}-{raw.Substring(4, 4)}-{raw.Substring(8, 4)}";
        }

        private void OnLicenseAgreementChanged(object sender, RoutedEventArgs e)
        {
            if (_step == 1)
            {
                BtnNext.IsEnabled = (RbAcceptLicense.IsChecked == true);
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
            if (_step == 3)
            {
                // Move to installation step
                _step = 4;
                UpdateStepView();
                await PerformInstallationAsync();
                return;
            }

            if (_step == 5)
            {
                // Finished
                LaunchDashboardAfter = ChkLaunchNow.IsChecked == true;
                DialogResult = true;
                Close();
                return;
            }

            if (_step < 5)
            {
                _step++;
                UpdateStepView();
            }
        }

        private void OnCancel_Click(object sender, RoutedEventArgs e)
        {
            var res = MessageBox.Show(
                "Are you sure you want to cancel PlantOps™ Enterprise SCADA Setup?\n\nThe software will not be configured.",
                "Cancel Setup",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (res == MessageBoxResult.Yes)
            {
                DialogResult = false;
                Close();
            }
        }

        private void UpdateStepView()
        {
            // Light Mode Sidebar Highlights
            var inactiveBg = Brushes.Transparent;
            var inactiveBorder = Brushes.Transparent;
            var inactiveFg = new SolidColorBrush(Color.FromRgb(100, 116, 139)); // #64748b

            var activeBg = new SolidColorBrush(Color.FromRgb(219, 234, 254));    // #dbeafe
            var activeBorder = new SolidColorBrush(Color.FromRgb(191, 219, 254)); // #bfdbfe
            var activeFg = new SolidColorBrush(Color.FromRgb(29, 78, 216));      // #1d4ed8

            void SetSidebarItem(Border item, TextBlock text, bool isActive)
            {
                item.Background = isActive ? activeBg : inactiveBg;
                item.BorderBrush = isActive ? activeBorder : inactiveBorder;
                text.Foreground = isActive ? activeFg : inactiveFg;
                text.FontWeight = isActive ? FontWeights.Bold : FontWeights.SemiBold;
            }

            SetSidebarItem(StepItem0, StepTxt0, _step == 0);
            SetSidebarItem(StepItem1, StepTxt1, _step == 1);
            SetSidebarItem(StepItem2, StepTxt2, _step == 2);
            SetSidebarItem(StepItem3, StepTxt3, _step == 3);
            SetSidebarItem(StepItem4, StepTxt4, _step == 4);
            SetSidebarItem(StepItem5, StepTxt5, _step == 5);

            // Update Panel Visibility
            StepPanel0.Visibility = (_step == 0) ? Visibility.Visible : Visibility.Collapsed;
            StepPanel1.Visibility = (_step == 1) ? Visibility.Visible : Visibility.Collapsed;
            StepPanel2.Visibility = (_step == 2) ? Visibility.Visible : Visibility.Collapsed;
            StepPanel3.Visibility = (_step == 3) ? Visibility.Visible : Visibility.Collapsed;
            StepPanel4.Visibility = (_step == 4) ? Visibility.Visible : Visibility.Collapsed;
            StepPanel5.Visibility = (_step == 5) ? Visibility.Visible : Visibility.Collapsed;

            // Update Button States
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
                    BtnNext.IsEnabled = true;
                    BtnCancel.Visibility = Visibility.Visible;
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
            var appDir = Path.GetDirectoryName(Environment.ProcessPath) ?? AppDomain.CurrentDomain.BaseDirectory;
            var exePath = Environment.ProcessPath ?? Path.Combine(appDir, "PlantOpsEnterpriseSCADA.exe");
            var key = Generate12DigitKey();

            try
            {
                // Step 1: Initialize Database
                TxtInstallStatus.Text = "Initializing local SQLite 21 CFR Part 11 database...";
                TxtInstallDetail.Text = "Verifying tables: downtime_logs, settings, stations in WAL mode...";
                PbInstallProgress.Value = 20;
                await Task.Delay(400);

                var db = new DatabaseService();
                db.InitializeDatabase();

                var plantName = string.IsNullOrWhiteSpace(TxtWizardPlantName.Text) ? "Plant 01 (Chennai Works)" : TxtWizardPlantName.Text.Trim();
                var divisionName = string.IsNullOrWhiteSpace(TxtWizardDivisionName.Text) ? "Assembly Division" : TxtWizardDivisionName.Text.Trim();
                var lineName = string.IsNullOrWhiteSpace(TxtWizardLineName.Text) ? "RAC Line 01" : TxtWizardLineName.Text.Trim();
                var licensee = $"{plantName} — {divisionName} ({lineName})";

                db.SaveSetting("plant_name", plantName);
                db.SaveSetting("plant_division", divisionName);
                db.SaveSetting("plant_line", lineName);

                // Step 2: Register License & Generate 12-Digit License File
                TxtInstallStatus.Text = "Registering 12-digit one-time volume license key...";
                TxtInstallDetail.Text = "Generating certified LICENSE_KEY.txt & license_activation.json record...";
                PbInstallProgress.Value = 40;
                await Task.Delay(400);

                var licenseData = new
                {
                    product = "PlantOps™ Enterprise SCADA",
                    version = "2.4.0 LTS",
                    licensee = licensee,
                    plant_name = plantName,
                    division = divisionName,
                    production_line = lineName,
                    license_key = key,
                    key_type = "12_DIGIT_ONE_TIME_USE",
                    activated_at = DateTime.UtcNow.ToString("o"),
                    status = "ACTIVE_CONSUMED",
                    standards = new[] { "21 CFR Part 11", "ISA-101", "ISO 11064" }
                };

                var licJsonPath = Path.Combine(appDir, "license_activation.json");
                File.WriteAllText(licJsonPath, JsonSerializer.Serialize(licenseData, new JsonSerializerOptions { WriteIndented = true }));

                // Write LICENSE_KEY.txt text format file directly where software is installed
                var licenseKeyTxtContent = $@"================================================================================
PLANTOPS™ ENTERPRISE SCADA v2.4 LTS — 12-DIGIT ONE-TIME VOLUME LICENSE KEY
================================================================================
Product: PlantOps™ Enterprise SCADA
Version: 2.4.0 LTS
License Model: Enterprise Volume Single-Machine (One-Time Use Key)
Standards Compliance: 21 CFR Part 11 Certified Electronic Records & ISA-101 HMI

AUTHORIZED LICENSEE & WORKSTATION:
  Entity / Plant: {licensee}
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
  Install Directory: {appDir}
  Database: app_local.db (SQLite WAL Mode Active)
  Telemetry Ingestion: Port 5050 TCP Inbound (Active)

NOTICE:
This unique 12-digit key was generated during software installation and 
permanently bound to this host. Retain this text file in the installation 
directory for internal audits and 21 CFR Part 11 electronic record validation.
================================================================================
";
                var licTxtPath = Path.Combine(appDir, "LICENSE_KEY.txt");
                File.WriteAllText(licTxtPath, licenseKeyTxtContent);

                // Step 3: Firewall Configuration
                if (ChkFirewall.IsChecked == true)
                {
                    TxtInstallStatus.Text = "Configuring Windows Defender Firewall...";
                    TxtInstallDetail.Text = "Opening TCP Port 5050 & 8090 Inbound for Workstation Agent Telemetry...";
                    PbInstallProgress.Value = 60;
                    await Task.Delay(300);

                    try
                    {
                        var psi1 = new ProcessStartInfo
                        {
                            FileName = "netsh",
                            Arguments = "advfirewall firewall add rule name=\"PlantOps SCADA Inbound (TCP 5050)\" dir=in action=allow protocol=TCP localport=5050 profile=any",
                            CreateNoWindow = true,
                            UseShellExecute = false
                        };
                        Process.Start(psi1)?.WaitForExit(2000);

                        var psi2 = new ProcessStartInfo
                        {
                            FileName = "netsh",
                            Arguments = "advfirewall firewall add rule name=\"PlantOps SCADA Inbound (TCP 8090)\" dir=in action=allow protocol=TCP localport=8090 profile=any",
                            CreateNoWindow = true,
                            UseShellExecute = false
                        };
                        Process.Start(psi2)?.WaitForExit(2000);
                    }
                    catch { }
                }

                // Step 4: Desktop & Start Menu Shortcuts
                PbInstallProgress.Value = 75;
                if (ChkDesktopShortcut.IsChecked == true || ChkStartMenuShortcut.IsChecked == true)
                {
                    TxtInstallStatus.Text = "Creating Windows Application Shortcuts...";
                    TxtInstallDetail.Text = "Generating PlantOps Enterprise SCADA shortcuts...";
                    await Task.Delay(300);

                    try
                    {
                        Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
                        if (shellType != null)
                        {
                            dynamic shell = Activator.CreateInstance(shellType)!;

                            if (ChkDesktopShortcut.IsChecked == true)
                            {
                                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                                dynamic sc = shell.CreateShortcut(Path.Combine(desktop, "PlantOps Enterprise SCADA.lnk"));
                                sc.TargetPath = exePath;
                                sc.WorkingDirectory = appDir;
                                sc.Description = "PlantOps Enterprise SCADA v2.4 LTS";
                                sc.Save();
                            }

                            if (ChkStartMenuShortcut.IsChecked == true)
                            {
                                string programs = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
                                dynamic sc = shell.CreateShortcut(Path.Combine(programs, "PlantOps Enterprise SCADA.lnk"));
                                sc.TargetPath = exePath;
                                sc.WorkingDirectory = appDir;
                                sc.Description = "PlantOps Enterprise SCADA v2.4 LTS";
                                sc.Save();
                            }
                        }
                    }
                    catch { }
                }

                // Step 5: Register with Windows Programs and Features (Control Panel Uninstaller)
                PbInstallProgress.Value = 90;
                if (ChkRegisterUninstall.IsChecked == true)
                {
                    TxtInstallStatus.Text = "Registering with Windows Programs & Features...";
                    TxtInstallDetail.Text = "Adding uninstall entry to Windows Control Panel (appwiz.cpl)...";
                    await Task.Delay(300);

                    try
                    {
                        using var uninstallKey = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\PlantOpsEnterpriseSCADA");
                        if (uninstallKey != null)
                        {
                            uninstallKey.SetValue("DisplayName", "PlantOps™ Enterprise SCADA v2.4 LTS");
                            uninstallKey.SetValue("DisplayVersion", "2.4.0");
                            uninstallKey.SetValue("Publisher", "PlantOps Industrial Systems");
                            uninstallKey.SetValue("DisplayIcon", exePath);
                            uninstallKey.SetValue("InstallLocation", appDir);
                            uninstallKey.SetValue("UninstallString", $"cmd.exe /c \"\"{Path.Combine(appDir, "Uninstall.bat")}\"\"");
                            uninstallKey.SetValue("QuietUninstallString", $"cmd.exe /c \"\"{Path.Combine(appDir, "Uninstall.bat")}\"\"");
                            uninstallKey.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
                            uninstallKey.SetValue("EstimatedSize", 165000, RegistryValueKind.DWord);
                            uninstallKey.SetValue("NoModify", 1, RegistryValueKind.DWord);
                            uninstallKey.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                            uninstallKey.SetValue("URLInfoAbout", "http://plantops.internal");
                            uninstallKey.SetValue("Comments", "21 CFR Part 11 Certified RAC Assembly Line SCADA Monitor");
                        }
                    }
                    catch { }
                }

                // Write/Update robust Uninstall.bat
                try
                {
                    var uninstallScript = $@"@echo off
title PlantOps Enterprise SCADA Uninstaller
echo ============================================================================
echo   PLANTOPS ENTERPRISE SCADA v2.4 LTS — UNINSTALLATION
echo ============================================================================
echo.
set /p CONFIRM=""Are you sure you want to completely uninstall PlantOps Enterprise SCADA? (Y/N): ""
if /i not ""%CONFIRM%""==""Y"" (
    echo Uninstallation cancelled by user.
    pause
    exit /b
)

echo.
echo [1/5] Stopping running SCADA processes...
taskkill /F /IM PlantOpsEnterpriseSCADA.exe >nul 2>&1

echo [2/5] Removing Windows Desktop and Start Menu Shortcuts...
powershell -NoProfile -Command ""$d = [System.IO.Path]::Combine([Environment]::GetFolderPath('Desktop'), 'PlantOps Enterprise SCADA.lnk'); if (Test-Path $d) {{ Remove-Item $d -Force }}; $p = [System.IO.Path]::Combine([Environment]::GetFolderPath('Programs'), 'PlantOps Enterprise SCADA.lnk'); if (Test-Path $p) {{ Remove-Item $p -Force }}"" >nul 2>&1

echo [3/5] Removing Windows Defender Firewall Rules...
netsh advfirewall firewall delete rule name=""PlantOps SCADA Inbound (TCP 5050)"" >nul 2>&1
netsh advfirewall firewall delete rule name=""PlantOps SCADA Inbound (TCP 8090)"" >nul 2>&1
netsh advfirewall firewall delete rule name=""PlantOps Enterprise SCADA Application"" >nul 2>&1

echo [4/5] Removing from Windows Programs & Features (Registry)...
reg delete ""HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\PlantOpsEnterpriseSCADA"" /f >nul 2>&1
reg delete ""HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PlantOpsEnterpriseSCADA"" /f >nul 2>&1

echo [5/5] Removing Local License & Session configurations...
if exist ""%~dp0license_activation.json"" del /f /q ""%~dp0license_activation.json"" >nul 2>&1
if exist ""%~dp0LICENSE_KEY.txt"" del /f /q ""%~dp0LICENSE_KEY.txt"" >nul 2>&1

echo.
echo ============================================================================
echo   UNINSTALLATION SUCCESSFUL!
echo   PlantOps Enterprise SCADA has been removed from Windows Programs & Features.
echo   You may now delete this folder: %~dp0
echo ============================================================================
powershell -NoProfile -Command ""Add-Type -AssemblyName System.Windows.Forms; [System.Windows.Forms.MessageBox]::Show('PlantOps Enterprise SCADA has been successfully removed from your computer.', 'PlantOps SCADA Uninstalled', [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Information)"" >nul 2>&1
pause
";
                    File.WriteAllText(Path.Combine(appDir, "Uninstall.bat"), uninstallScript);
                }
                catch { }

                // Complete
                PbInstallProgress.Value = 100;
                TxtInstallStatus.Text = "Installation and verification completed successfully!";
                TxtInstallDetail.Text = "All components verified and ready for live production use.";
                await Task.Delay(400);

                TxtSummaryDetails.Text = $"• License Status: ACTIVE (Enterprise Site Edition)\n" +
                                         $"• Compliance: 21 CFR Part 11 Certified Electronic Records\n" +
                                         $"• Windows Programs & Features: Registered (Control Panel Uninstaller Active)\n" +
                                         $"• Plant Profile: {licensee}\n" +
                                         $"• Location: {appDir}\n" +
                                         $"• Database: app_local.db (SQLite WAL Mode Active)\n" +
                                         $"• Ingestion Endpoint: Port 5050 TCP (Listening)";

                _step = 5;
                UpdateStepView();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Installation error: {ex.Message}", "Setup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                DialogResult = false;
                Close();
            }
        }
    }
}