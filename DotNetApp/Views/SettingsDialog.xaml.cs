using System.Globalization;
using System.Windows;
using DotNetApp.Models;

namespace DotNetApp.Views
{
    public partial class SettingsDialog : Window
    {
        private readonly ThresholdSettings _settings;

        public SettingsDialog(ThresholdSettings settings)
        {
            InitializeComponent();
            _settings = settings;

            TxtCpuWarning.Text = _settings.CpuWarningPct.ToString(CultureInfo.InvariantCulture);
            TxtCpuCritical.Text = _settings.CpuCriticalPct.ToString(CultureInfo.InvariantCulture);
            TxtRamWarning.Text = _settings.RamWarningPct.ToString(CultureInfo.InvariantCulture);
            TxtRamCritical.Text = _settings.RamCriticalPct.ToString(CultureInfo.InvariantCulture);
            TxtHeartbeat.Text = _settings.HeartbeatTimeoutSec.ToString(CultureInfo.InvariantCulture);
            TxtTempFilesThreshold.Text = _settings.TempFilesThresholdCount.ToString(CultureInfo.InvariantCulture);
            TxtUptimeThreshold.Text = (_settings.UptimeThresholdHours > 0 ? _settings.UptimeThresholdHours : 48).ToString(CultureInfo.InvariantCulture);
            ChkAlarmSiren.IsChecked = _settings.AlertChimeEnabled;
        }

        private void TestSiren_Click(object sender, RoutedEventArgs e)
        {
            var audio = new DotNetApp.Services.AudioAlertService();
            audio.PlayTestSound();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(TxtCpuWarning.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var cpuW))
                _settings.CpuWarningPct = cpuW;

            if (double.TryParse(TxtCpuCritical.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var cpuC))
                _settings.CpuCriticalPct = cpuC;

            if (double.TryParse(TxtRamWarning.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var ramW))
                _settings.RamWarningPct = ramW;

            if (double.TryParse(TxtRamCritical.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var ramC))
                _settings.RamCriticalPct = ramC;

            if (int.TryParse(TxtHeartbeat.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var hb))
                _settings.HeartbeatTimeoutSec = hb;

            if (int.TryParse(TxtTempFilesThreshold.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var tempCount))
                _settings.TempFilesThresholdCount = tempCount;

            if (int.TryParse(TxtUptimeThreshold.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var uptimeLimit))
                _settings.UptimeThresholdHours = uptimeLimit;

            _settings.AlertChimeEnabled = ChkAlarmSiren.IsChecked ?? true;

            DialogResult = true;
            Close();
        }

        private void EditPlantProfile_Click(object sender, RoutedEventArgs e)
        {
            var db = new DotNetApp.Data.DatabaseService();
            var p = db.GetSetting("plant_name", "Plant 01 (Chennai Works)");
            var d = db.GetSetting("plant_division", "Assembly Division");
            var l = db.GetSetting("plant_line", "RAC Line 01");

            var dlg = new PlantProfileDialogWindow(p, d, l)
            {
                Owner = this
            };

            if (dlg.ShowDialog() == true)
            {
                db.SaveSetting("plant_name", dlg.PlantName);
                db.SaveSetting("plant_division", dlg.DivisionName);
                db.SaveSetting("plant_line", dlg.LineName);
                MessageBox.Show("Plant profile updated successfully!\nRestart or refresh dashboard to see live changes.", "Plant Profile Updated", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
