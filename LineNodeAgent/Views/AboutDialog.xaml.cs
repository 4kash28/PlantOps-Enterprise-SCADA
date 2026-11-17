using System;
using System.Diagnostics;
using System.Windows;
using LineNodeAgent.ViewModels;
using MessageBox = System.Windows.MessageBox;
using Clipboard = System.Windows.Clipboard;

namespace LineNodeAgent.Views
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
                TxtStationId.Text = _vm.Settings.LineStationId;
                TxtFriendlyName.Text = _vm.Settings.StationFriendlyName;
                TxtHostname.Text = _vm.Settings.NodeName;
                TxtGatewayUrl.Text = _vm.Settings.ServerUrl;
                TxtStreamStatus.Text = $"{_vm.StreamStatus} ({_vm.Settings.PollingIntervalSeconds}s Interval)";
                TxtPackets.Text = $"{_vm.PacketsCount} (Last Latency: {_vm.LastLatency})";
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
                    "PLANTOPS™ LINE NODE AGENT — WORKSTATION DIAGNOSTICS DUMP\n" +
                    "===========================================================\n" +
                    $"Version: 2.4.0 LTS Build 2026.11\n" +
                    $"Framework: .NET {Environment.Version} ({Environment.OSVersion.VersionString})\n" +
                    $"Station ID: {_vm.Settings.LineStationId}\n" +
                    $"Friendly Name: {_vm.Settings.StationFriendlyName}\n" +
                    $"Workstation Host: {_vm.Settings.NodeName}\n" +
                    $"SCADA Central Gateway: {_vm.Settings.ServerUrl}\n" +
                    $"Stream Status: {_vm.StreamStatus}\n" +
                    $"Packets Count: {_vm.PacketsCount}\n" +
                    $"Last Latency: {_vm.LastLatency}\n" +
                    $"Standards: 21 CFR Part 11, ISA-101.01\n" +
                    $"Timestamp: {DateTime.Now:yyyy-MM-dd hh:mm:ss tt}\n" +
                    "===========================================================";

                Clipboard.SetText(report);
                MessageBox.Show("Diagnostics summary copied to clipboard!", "Workstation Diagnostics", MessageBoxButton.OK, MessageBoxImage.Information);
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
