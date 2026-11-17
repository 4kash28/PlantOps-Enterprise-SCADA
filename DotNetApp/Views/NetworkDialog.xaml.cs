using System;
using System.Windows;
using DotNetApp.Models;

namespace DotNetApp.Views
{
    public partial class NetworkDialog : Window
    {
        private readonly NetworkProfile _profile;

        public NetworkDialog(NetworkProfile profile)
        {
            InitializeComponent();
            _profile = profile;

            TxtHostIp.Text = _profile.Ip;
            TxtPort.Text = _profile.Port.ToString();
            TxtSubnet.Text = _profile.SubnetCidr;
            TxtGateway.Text = _profile.Gateway;

            UpdatePreview();

            TxtHostIp.TextChanged += (s, e) => UpdatePreview();
            TxtPort.TextChanged += (s, e) => UpdatePreview();
        }

        private void UpdatePreview()
        {
            if (TxtUrlPreview == null || TxtHostIp == null || TxtPort == null) return;
            var ip = string.IsNullOrWhiteSpace(TxtHostIp.Text) ? "127.0.0.1" : TxtHostIp.Text.Trim();
            var port = string.IsNullOrWhiteSpace(TxtPort.Text) ? "5050" : TxtPort.Text.Trim();
            TxtUrlPreview.Text = $"http://{ip}:{port}/api/telemetry";
        }

        private void CopyUrl_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(TxtUrlPreview.Text);
                MessageBox.Show($"Line Node Agent Ingestion URL copied to clipboard:\n\n{TxtUrlPreview.Text}", "URL Copied", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch { }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            _profile.Ip = TxtHostIp.Text.Trim();
            if (int.TryParse(TxtPort.Text.Trim(), out var p)) _profile.Port = p;
            _profile.SubnetCidr = TxtSubnet.Text.Trim();
            _profile.Gateway = TxtGateway.Text.Trim();

            DialogResult = true;
            Close();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
