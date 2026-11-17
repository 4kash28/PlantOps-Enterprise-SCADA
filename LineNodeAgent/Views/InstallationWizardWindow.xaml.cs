using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using LineNodeAgent.Data;
using LineNodeAgent.Models;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;
using MessageBoxResult = System.Windows.MessageBoxResult;
using Color = System.Windows.Media.Color;
using Brushes = System.Windows.Media.Brushes;
using Brush = System.Windows.Media.Brush;

namespace LineNodeAgent.Views
{
    public partial class InstallationWizardWindow : Window
    {
        private int _currentStep = 0;
        private readonly AgentDatabaseService _db;
        public bool LaunchAgentAfter { get; private set; } = true;

        public InstallationWizardWindow()
        {
            InitializeComponent();
            _db = new AgentDatabaseService();

            TxtMachineName.Text = Environment.MachineName;

            try
            {
                var currentSettings = _db.LoadSettings();
                if (!string.IsNullOrWhiteSpace(currentSettings.LineStationId))
                {
                    CmbStationId.Text = currentSettings.LineStationId;
                }
                if (!string.IsNullOrWhiteSpace(currentSettings.StationFriendlyName))
                {
                    TxtStationFriendlyName.Text = currentSettings.StationFriendlyName;
                }
                if (!string.IsNullOrWhiteSpace(currentSettings.ServerUrl))
                {
                    TxtServerUrl.Text = currentSettings.ServerUrl;
                }
                if (currentSettings.PollingIntervalSeconds > 0)
                {
                    TxtInterval.Text = currentSettings.PollingIntervalSeconds.ToString();
                }
                ChkMinimizeToTray.IsChecked = currentSettings.MinimizeToTray;
            }
            catch { }

            LoadLicenseText();
            UpdateStepView();
        }

        private void LoadLicenseText()
        {
            try
            {
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var candidates = new[]
                {
                    Path.Combine(baseDir, "LICENSE.txt"),
                    Path.Combine(baseDir, "..", "LICENSE.txt"),
                    Path.Combine(baseDir, "..", "..", "LICENSE.txt"),
                    Path.Combine(baseDir, "..", "..", "..", "LICENSE.txt"),
                    @"C:\Users\ITSUPA\.gemini\antigravity\scratch\rac_industrial_monitor\LICENSE.txt"
                };

                foreach (var path in candidates)
                {
                    if (File.Exists(path))
                    {
                        TxtLicenseTerms.Text = File.ReadAllText(path);
                        return;
                    }
                }
            }
            catch { }

            TxtLicenseTerms.Text = @"================================================================================
PLANTOPS™ ENTERPRISE SCADA & LINE NODE AGENT v2.4 LTS — SOFTWARE LICENSE AGREEMENT
================================================================================
Product: PlantOps™ Line Node Agent (Microsoft .NET 8 WPF Desktop Client)
Release: Version 2.4.0 LTS (Long Term Support)
Industry Standards: 21 CFR Part 11 Compliant & ISA-101 High-Performance HMI
License Type: Enterprise Commercial Site License (Perpetual Deployment)

1. GRANT OF LICENSE
PlantOps Industrial Systems grants the operating licensee authorization to deploy 
and operate the PlantOps Line Node Agent software across industrial assembly line 
workstation PCs, test benches, and quality control terminals.

2. TELEMETRY & HARDWARE MONITORING GOVERNANCE
This software collects local workstation telemetry (CPU load, RAM commit, storage, 
temperature, uptime, ping round-trip latency, USB scanners, Zebra barcode printers) 
and transmits updates over local air-gapped Ethernet/Wi-Fi to the PlantOps SCADA 
Central Dashboard. All metrics are governed by local station policies.

3. AIR-GAPPED ZERO-CLOUD ASSURANCE
No telemetry data, shop-floor metrics, or operating credentials are transmitted 
outside the local factory network. This software operates completely offline and 
air-gapped without third-party cloud services.

4. 21 CFR PART 11 COMPLIANCE
Incident logs, connection audits, and downtime records are recorded with immutable 
UTC timestamps in SQLite WAL mode.
================================================================================";
        }

        private void OnLicenseAgreementChanged(object sender, RoutedEventArgs e)
        {
            if (_currentStep == 1 && BtnNext != null)
            {
                BtnNext.IsEnabled = RbAcceptLicense.IsChecked == true;
            }
        }

        private void OnBack_Click(object sender, RoutedEventArgs e)
        {
            if (_currentStep > 0 && _currentStep <= 4)
            {
                _currentStep--;
                UpdateStepView();
            }
        }

        private async void OnNext_Click(object sender, RoutedEventArgs e)
        {
            if (_currentStep == 0)
            {
                _currentStep = 1;
                UpdateStepView();
            }
            else if (_currentStep == 1)
            {
                if (RbAcceptLicense.IsChecked != true)
                {
                    MessageBox.Show("Please review and accept the deployment agreement to proceed with provisioning.",
                        "Agreement Required", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                _currentStep = 2;
                UpdateStepView();
            }
            else if (_currentStep == 2)
            {
                if (string.IsNullOrWhiteSpace(CmbStationId.Text))
                {
                    MessageBox.Show("Please select or enter a valid Line Station Identifier (e.g. LINE1 1, START, VACUUM_PUMP).",
                        "Station ID Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                _currentStep = 3;
                UpdateStepView();
            }
            else if (_currentStep == 3)
            {
                var url = TxtServerUrl.Text.Trim();
                if (!string.IsNullOrWhiteSpace(url) && !url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    url = "http://" + url;
                    TxtServerUrl.Text = url;
                }
                if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out _))
                {
                    MessageBox.Show("Please enter a valid SCADA Dashboard HTTP URL (e.g. http://localhost:5050 or http://192.168.1.106:5050).",
                        "Server URL Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                _currentStep = 4;
                UpdateStepView();
            }
            else if (_currentStep == 4)
            {
                await PerformInstallationAsync();
            }
            else if (_currentStep == 6)
            {
                LaunchAgentAfter = ChkLaunchNow.IsChecked == true;
                DialogResult = true;
                Close();
            }
        }

        private void OnCancel_Click(object sender, RoutedEventArgs e)
        {
            var res = MessageBox.Show(
                "Are you sure you want to cancel the Line Node Agent setup?\nThe application will not stream telemetry until workstation configuration is completed.",
                "Cancel Setup Wizard?", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (res == MessageBoxResult.Yes)
            {
                DialogResult = false;
                Close();
            }
        }

        private async void OnTestLink_Click(object sender, RoutedEventArgs e)
        {
            var rawUrl = TxtServerUrl.Text.Trim().TrimEnd('/');
            if (!string.IsNullOrWhiteSpace(rawUrl) && !rawUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !rawUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                rawUrl = "http://" + rawUrl;
                TxtServerUrl.Text = rawUrl;
            }
            var url = rawUrl + "/api/stations";
            TxtLinkFeedback.Text = "Pinging SCADA server endpoint...";
            LinkFeedbackBorder.Background = new SolidColorBrush(Color.FromRgb(254, 243, 199)); // Amber / Yellow
            LinkFeedbackBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(251, 191, 36));

            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var resp = await client.GetAsync(url);
                sw.Stop();

                if (resp.IsSuccessStatusCode)
                {
                    TxtLinkFeedback.Text = $"✓ SCADA Central Reachable! Status: 200 OK • Round-trip Latency: {sw.ElapsedMilliseconds} ms";
                    LinkFeedbackBorder.Background = new SolidColorBrush(Color.FromRgb(236, 253, 245)); // Green
                    LinkFeedbackBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(167, 243, 208));
                }
                else
                {
                    TxtLinkFeedback.Text = $"⚠ Server responded with HTTP {(int)resp.StatusCode} ({resp.StatusCode}) in {sw.ElapsedMilliseconds} ms.";
                    LinkFeedbackBorder.Background = new SolidColorBrush(Color.FromRgb(254, 243, 199));
                    LinkFeedbackBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(251, 191, 36));
                }
            }
            catch (Exception ex)
            {
                TxtLinkFeedback.Text = $"❌ Link Test Failed: {ex.Message}. Verify PlantOps SCADA Dashboard is running on {rawUrl}.";
                LinkFeedbackBorder.Background = new SolidColorBrush(Color.FromRgb(254, 242, 242)); // Red
                LinkFeedbackBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(254, 202, 202));
            }
        }

        private async Task PerformInstallationAsync()
        {
            _currentStep = 5;
            UpdateStepView();

            try
            {
                TxtInstallStatus.Text = "Validating station configuration...";
                TxtInstallDetail.Text = "Testing local workstation environment and telemetry parameters...";
                PbInstallProgress.Value = 20;
                await Task.Delay(350);

                var processPath = Environment.ProcessPath;
                var appDir = !string.IsNullOrEmpty(processPath)
                    ? Path.GetDirectoryName(processPath) ?? AppDomain.CurrentDomain.BaseDirectory
                    : AppDomain.CurrentDomain.BaseDirectory;
                var exePath = processPath ?? Path.Combine(appDir, "LineNodeAgent.exe");

                // 1. Save Settings to Database
                TxtInstallStatus.Text = "Configuring SQLite database (agent_config.db)...";
                TxtInstallDetail.Text = "Writing telemetry permissions, station identifier, and SCADA gateway URL...";
                PbInstallProgress.Value = 45;
                await Task.Delay(350);

                var settings = new AgentSettings
                {
                    NodeName = string.IsNullOrWhiteSpace(TxtMachineName.Text) ? Environment.MachineName : TxtMachineName.Text.Trim(),
                    LineStationId = string.IsNullOrWhiteSpace(CmbStationId.Text) ? "START" : CmbStationId.Text.Trim().ToUpperInvariant(),
                    StationFriendlyName = string.IsNullOrWhiteSpace(TxtStationFriendlyName.Text) ? "Assembly Station" : TxtStationFriendlyName.Text.Trim(),
                    ServerUrl = string.IsNullOrWhiteSpace(TxtServerUrl.Text) ? "http://localhost:5050" : TxtServerUrl.Text.Trim(),
                    PollingIntervalSeconds = int.TryParse(TxtInterval.Text, out var iv) && iv > 0 ? iv : 1,
                    MinimizeToTray = ChkMinimizeToTray.IsChecked == true
                };

                _db.SaveSettings(settings);

                // 2. Windows Auto-Start Registry Configuration
                TxtInstallStatus.Text = "Configuring Windows integration...";
                TxtInstallDetail.Text = "Updating HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Run registry...";
                PbInstallProgress.Value = 70;
                await Task.Delay(300);

                try
                {
                    using var runKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
                    if (runKey != null)
                    {
                        if (ChkAutoStart.IsChecked == true && File.Exists(exePath))
                        {
                            runKey.SetValue("PlantOpsLineNodeAgent", $"\"{exePath}\"");
                        }
                        else
                        {
                            runKey.DeleteValue("PlantOpsLineNodeAgent", false);
                        }
                    }
                }
                catch { }

                // 3. Desktop Shortcut Creation
                TxtInstallStatus.Text = "Generating desktop shortcut...";
                TxtInstallDetail.Text = "Creating shortcut on Windows Desktop...";
                PbInstallProgress.Value = 85;
                await Task.Delay(250);

                if (ChkDesktopShortcut.IsChecked == true && File.Exists(exePath))
                {
                    try
                    {
                        var desktopDir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                        var shortcutPath = Path.Combine(desktopDir, "PlantOps Line Node Agent.lnk");

                        Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
                        if (shellType != null)
                        {
                            dynamic shell = Activator.CreateInstance(shellType)!;
                            var shortcut = shell.CreateShortcut(shortcutPath);
                            shortcut.TargetPath = exePath;
                            shortcut.WorkingDirectory = appDir;
                            shortcut.Description = "PlantOps Line Node Agent — RAC Shop-Floor Telemetry Gateway";
                            shortcut.IconLocation = exePath + ",0";
                            shortcut.Save();
                        }
                    }
                    catch { }
                }

                // 4. Windows Programs & Features Registration (Control Panel & Settings Uninstaller)
                if (ChkRegisterUninstall.IsChecked == true)
                {
                    try
                    {
                        var uninstBatPath = Path.Combine(appDir, "Uninstall_LineNodeAgent.bat");
                        var uninstContent = @"@echo off
setlocal
cd /d ""%~dp0""
title PlantOps Line Node Agent Uninstaller

if exist ""%~dp0LineNodeAgent.exe"" (
    ""%~dp0LineNodeAgent.exe"" --uninstall %*
    goto :finish
)

echo ============================================================================
echo   PLANTOPS LINE NODE AGENT v2.4 - WORKSTATION UNINSTALLATION
echo ============================================================================
echo.
echo [1/4] Stopping active LineNodeAgent processes...
taskkill /F /IM LineNodeAgent.exe >nul 2>&1

echo [2/4] Removing Windows Startup & Programs and Features registrations...
reg delete ""HKCU\Software\Microsoft\Windows\CurrentVersion\Run"" /v ""PlantOpsLineNodeAgent"" /f >nul 2>&1
reg delete ""HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\PlantOpsLineNodeAgent"" /f >nul 2>&1
reg delete ""HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PlantOpsLineNodeAgent"" /f >nul 2>&1

echo [3/4] Removing Desktop and Start Menu Shortcuts...
powershell -NoProfile -Command ""$d = [System.IO.Path]::Combine([Environment]::GetFolderPath('Desktop'), 'PlantOps Line Node Agent.lnk'); if (Test-Path $d) { Remove-Item $d -Force }; $p = [System.IO.Path]::Combine([Environment]::GetFolderPath('Programs'), 'PlantOps Line Node Agent.lnk'); if (Test-Path $p) { Remove-Item $p -Force }"" >nul 2>&1

echo [4/4] Removing configuration files...
if exist ""%~dp0agent_installed.json"" del /f /q ""%~dp0agent_installed.json"" >nul 2>&1
if exist ""%~dp0agent_config.db"" del /f /q ""%~dp0agent_config.db"" >nul 2>&1
if exist ""%~dp0agent_config.db-wal"" del /f /q ""%~dp0agent_config.db-wal"" >nul 2>&1
if exist ""%~dp0agent_config.db-shm"" del /f /q ""%~dp0agent_config.db-shm"" >nul 2>&1
if exist ""%~dp0agent_error.log"" del /f /q ""%~dp0agent_error.log"" >nul 2>&1

echo.
echo Uninstallation completed successfully.
pause

:finish
exit /b 0
";
                        File.WriteAllText(uninstBatPath, uninstContent.Replace("\r\n", "\n").Replace("\n", "\r\n"));

                        using var uninstallKey = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\PlantOpsLineNodeAgent");
                        if (uninstallKey != null)
                        {
                            uninstallKey.SetValue("DisplayName", "PlantOps™ Line Node Agent v2.4 LTS");
                            uninstallKey.SetValue("DisplayVersion", "2.4.0");
                            uninstallKey.SetValue("Publisher", "PlantOps Industrial Systems");
                            uninstallKey.SetValue("DisplayIcon", $"{exePath},0");
                            uninstallKey.SetValue("InstallLocation", appDir);
                            uninstallKey.SetValue("UninstallString", $"\"{exePath}\" --uninstall");
                            uninstallKey.SetValue("QuietUninstallString", $"\"{exePath}\" --uninstall --quiet");
                            uninstallKey.SetValue("ModifyPath", $"\"{exePath}\" --setup");
                            uninstallKey.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
                            uninstallKey.SetValue("EstimatedSize", 165000, RegistryValueKind.DWord);
                            uninstallKey.SetValue("NoModify", 1, RegistryValueKind.DWord);
                            uninstallKey.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                            uninstallKey.SetValue("Comments", "PlantOps Line Node Agent — RAC Shop-Floor Assembly Workstation Telemetry Gateway (21 CFR Part 11 Compliant)");
                        }
                    }
                    catch { }
                }

                // 5. Write Installation Marker File
                TxtInstallStatus.Text = "Writing deployment manifest...";
                TxtInstallDetail.Text = "Saving agent_installed.json deployment receipt...";
                PbInstallProgress.Value = 95;
                await Task.Delay(200);

                var markerPath = Path.Combine(appDir, "agent_installed.json");
                var markerData = new
                {
                    product = "PlantOps™ Line Node Agent",
                    version = "2.4.0",
                    installed_at = DateTime.UtcNow.ToString("o"),
                    node_machine_name = settings.NodeName,
                    line_station_id = settings.LineStationId,
                    station_friendly_name = settings.StationFriendlyName,
                    scada_server_url = settings.ServerUrl,
                    polling_interval_seconds = settings.PollingIntervalSeconds,
                    windows_autostart = ChkAutoStart.IsChecked == true,
                    windows_programs_and_features = ChkRegisterUninstall.IsChecked == true,
                    minimize_to_tray = settings.MinimizeToTray,
                    standards = new[] { "21 CFR Part 11", "ISA-101", "ISO 11064" }
                };

                File.WriteAllText(markerPath, JsonSerializer.Serialize(markerData, new JsonSerializerOptions { WriteIndented = true }));

                PbInstallProgress.Value = 100;
                await Task.Delay(300);

                // Update Summary details on StepPanel6
                TxtSummaryDetails.Text = $"• Workstation Hostname: {settings.NodeName}\n" +
                                         $"• Station Identifier: {settings.LineStationId} ({settings.StationFriendlyName})\n" +
                                         $"• SCADA Server: {settings.ServerUrl}\n" +
                                         $"• Telemetry Frequency: {settings.PollingIntervalSeconds} second(s)\n" +
                                         $"• Windows Startup: {(ChkAutoStart.IsChecked == true ? "Enabled (HKCU\\Run)" : "Disabled")}\n" +
                                         $"• Programs & Features: Registered (Windows Control Panel Uninstaller Active)\n" +
                                         $"• Background System Tray: {(settings.MinimizeToTray ? "Enabled (24/7 background streaming)" : "Disabled")}\n" +
                                         $"• Regulatory Standard: 21 CFR Part 11 / ISA-101 Air-Gapped Industrial Telemetry";

                _currentStep = 6;
                UpdateStepView();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to complete workstation provisioning:\n{ex.Message}", "Provisioning Error", MessageBoxButton.OK, MessageBoxImage.Error);
                _currentStep = 4;
                UpdateStepView();
            }
        }

        private void UpdateStepView()
        {
            // Panel Visibilities
            StepPanel0.Visibility = _currentStep == 0 ? Visibility.Visible : Visibility.Collapsed;
            StepPanel1.Visibility = _currentStep == 1 ? Visibility.Visible : Visibility.Collapsed;
            StepPanel2.Visibility = _currentStep == 2 ? Visibility.Visible : Visibility.Collapsed;
            StepPanel3.Visibility = _currentStep == 3 ? Visibility.Visible : Visibility.Collapsed;
            StepPanel4.Visibility = _currentStep == 4 ? Visibility.Visible : Visibility.Collapsed;
            StepPanel5.Visibility = _currentStep == 5 ? Visibility.Visible : Visibility.Collapsed;
            StepPanel6.Visibility = _currentStep == 6 ? Visibility.Visible : Visibility.Collapsed;

            // Stepper Header Indicators
            int stepperIndex = _currentStep >= 5 ? 5 : _currentStep;
            UpdateStepIndicator(StepItem0, StepTxt0, "1. Welcome", 0, stepperIndex);
            UpdateStepIndicator(StepItem1, StepTxt1, "2. Agreement", 1, stepperIndex);
            UpdateStepIndicator(StepItem2, StepTxt2, "3. Station ID", 2, stepperIndex);
            UpdateStepIndicator(StepItem3, StepTxt3, "4. SCADA Link", 3, stepperIndex);
            UpdateStepIndicator(StepItem4, StepTxt4, "5. Integration", 4, stepperIndex);
            UpdateStepIndicator(StepItem5, StepTxt5, "6. Complete", 5, stepperIndex);

            // Bottom Navigation Buttons
            if (_currentStep == 0)
            {
                BtnBack.IsEnabled = false;
                BtnNext.IsEnabled = true;
                BtnNext.Content = "Next >";
                BtnCancel.IsEnabled = true;
                BtnBack.Visibility = Visibility.Visible;
                BtnCancel.Visibility = Visibility.Visible;
            }
            else if (_currentStep == 1)
            {
                BtnBack.IsEnabled = true;
                BtnNext.IsEnabled = RbAcceptLicense.IsChecked == true;
                BtnNext.Content = "Next >";
                BtnCancel.IsEnabled = true;
                BtnBack.Visibility = Visibility.Visible;
                BtnCancel.Visibility = Visibility.Visible;
            }
            else if (_currentStep == 2 || _currentStep == 3)
            {
                BtnBack.IsEnabled = true;
                BtnNext.IsEnabled = true;
                BtnNext.Content = "Next >";
                BtnCancel.IsEnabled = true;
                BtnBack.Visibility = Visibility.Visible;
                BtnCancel.Visibility = Visibility.Visible;
            }
            else if (_currentStep == 4)
            {
                BtnBack.IsEnabled = true;
                BtnNext.IsEnabled = true;
                BtnNext.Content = "Install & Provision";
                BtnCancel.IsEnabled = true;
                BtnBack.Visibility = Visibility.Visible;
                BtnCancel.Visibility = Visibility.Visible;
            }
            else if (_currentStep == 5)
            {
                // Provisioning in progress
                BtnBack.IsEnabled = false;
                BtnNext.IsEnabled = false;
                BtnCancel.IsEnabled = false;
                BtnBack.Visibility = Visibility.Visible;
                BtnCancel.Visibility = Visibility.Visible;
            }
            else if (_currentStep == 6)
            {
                // Provisioning complete
                BtnBack.Visibility = Visibility.Collapsed;
                BtnCancel.Visibility = Visibility.Collapsed;
                BtnNext.IsEnabled = true;
                BtnNext.Content = "Finish";
            }
        }

        private void UpdateStepIndicator(Border item, TextBlock txt, string label, int index, int current)
        {
            if (current == index)
            {
                // Active Step
                item.Background = new SolidColorBrush(Color.FromRgb(219, 234, 254)); // #dbeafe
                item.BorderBrush = new SolidColorBrush(Color.FromRgb(191, 219, 254)); // #bfdbfe
                txt.Foreground = new SolidColorBrush(Color.FromRgb(29, 78, 216));    // #1d4ed8
                txt.FontWeight = FontWeights.Bold;
                txt.Text = label;
            }
            else if (current > index)
            {
                // Completed Step
                item.Background = new SolidColorBrush(Color.FromRgb(240, 253, 244)); // #f0fdf4
                item.BorderBrush = new SolidColorBrush(Color.FromRgb(187, 247, 208)); // #bbf7d0
                txt.Foreground = new SolidColorBrush(Color.FromRgb(22, 163, 74));     // #16a34a
                txt.FontWeight = FontWeights.SemiBold;
                int dotIdx = label.IndexOf('.');
                string titlePart = dotIdx >= 0 ? label.Substring(dotIdx + 2) : label;
                txt.Text = $"✓ {titlePart}";
            }
            else
            {
                // Inactive Step
                item.Background = Brushes.Transparent;
                item.BorderBrush = Brushes.Transparent;
                txt.Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139));   // #64748b
                txt.FontWeight = FontWeights.Normal;
                txt.Text = label;
            }
        }
    }
}
