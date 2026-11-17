using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using DotNetApp.ViewModels;

namespace DotNetApp.Views
{
    public partial class AboutDialog : Window
    {
        private readonly MainViewModel _vm;

        public AboutDialog(MainViewModel vm)
        {
            InitializeComponent();
            _vm = vm;
            DataContext = _vm;
            PopulateDiagnostics();
        }

        private void PopulateDiagnostics()
        {
            try
            {
                TxtTelemetryUrl.Text = $"http://{_vm.HostIpAddress}:{_vm.NetworkProfile.Port}/api/telemetry";
                
                var onlineCount = _vm.Stations.Count(s => s.Model.Status.Equals("NORMAL", StringComparison.OrdinalIgnoreCase) ||
                                                          s.Model.Status.Equals("WARNING", StringComparison.OrdinalIgnoreCase) ||
                                                          s.Model.Status.Equals("CRITICAL", StringComparison.OrdinalIgnoreCase));
                TxtFleetSummary.Text = $"{onlineCount} Online ({_vm.Stations.Count} Configured)";

                TxtFacility.Text = string.IsNullOrWhiteSpace(_vm.PlantName) ? "Plant 01 (Chennai Works)" : _vm.PlantName;
                TxtDivision.Text = $"{_vm.DivisionName} — {_vm.LineName}";

                using var proc = Process.GetCurrentProcess();
                var wsMb = proc.WorkingSet64 / (1024 * 1024);
                TxtMemory.Text = $"{wsMb} MB Physical Commit";

                TxtRuntime.Text = $".NET {Environment.Version} (win-x64, OS {Environment.OSVersion.VersionString})";
            }
            catch { }
        }

        private void OnCopyDiagnostics_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var report = 
                    "===========================================================\n" +
                    "PLANTOPS™ ENTERPRISE SCADA — SYSTEM DIAGNOSTICS DUMP\n" +
                    "===========================================================\n" +
                    $"Version: 2.4.0 LTS Build 2026.11\n" +
                    $"Framework: .NET {Environment.Version} ({Environment.OSVersion.VersionString})\n" +
                    $"Facility: {_vm.PlantName}\n" +
                    $"Division: {_vm.DivisionName} — {_vm.LineName}\n" +
                    $"Server Ingestion Endpoint: http://{_vm.HostIpAddress}:{_vm.NetworkProfile.Port}/api/telemetry\n" +
                    $"Monitored Stations: {_vm.Stations.Count}\n" +
                    $"Database: SQLite WAL (app_local.db)\n" +
                    $"Standards: 21 CFR Part 11, ISA-101.01, ISO 11064\n" +
                    $"Timestamp: {DateTime.Now:yyyy-MM-dd hh:mm:ss tt}\n" +
                    "===========================================================";

                Clipboard.SetText(report);
                MessageBox.Show("Diagnostics summary copied to clipboard!", "System Diagnostics", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not copy to clipboard: {ex.Message}", "Clipboard Notice", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void OnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
