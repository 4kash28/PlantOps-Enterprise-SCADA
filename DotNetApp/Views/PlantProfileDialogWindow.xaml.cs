using System.Windows;
using System.Windows.Controls;

namespace DotNetApp.Views
{
    public partial class PlantProfileDialogWindow : Window
    {
        public string PlantName { get; private set; } = "Plant 01 (Chennai Works)";
        public string DivisionName { get; private set; } = "Assembly Division";
        public string LineName { get; private set; } = "RAC Line 01";

        public PlantProfileDialogWindow(string currentPlant, string currentDivision, string currentLine)
        {
            InitializeComponent();

            PlantName = string.IsNullOrWhiteSpace(currentPlant) ? "Plant 01 (Chennai Works)" : currentPlant;
            DivisionName = string.IsNullOrWhiteSpace(currentDivision) ? "Assembly Division" : currentDivision;
            LineName = string.IsNullOrWhiteSpace(currentLine) ? "RAC Line 01" : currentLine;

            TxtPlantName.Text = PlantName;
            TxtDivisionName.Text = DivisionName;
            TxtLineName.Text = LineName;

            UpdatePreview();
        }

        private void OnInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdatePreview();
        }

        private void UpdatePreview()
        {
            if (TxtPreview == null || TxtPlantName == null || TxtDivisionName == null || TxtLineName == null)
                return;

            var p = string.IsNullOrWhiteSpace(TxtPlantName.Text) ? "Plant 01" : TxtPlantName.Text.Trim();
            var d = string.IsNullOrWhiteSpace(TxtDivisionName.Text) ? "Assembly Division" : TxtDivisionName.Text.Trim();
            var l = string.IsNullOrWhiteSpace(TxtLineName.Text) ? "RAC Line 01" : TxtLineName.Text.Trim();

            TxtPreview.Text = $"{p} • {d} • {l}";
        }

        private void ResetDefault_Click(object sender, RoutedEventArgs e)
        {
            TxtPlantName.Text = "Plant 01 (Chennai Works)";
            TxtDivisionName.Text = "Assembly Division";
            TxtLineName.Text = "RAC Line 01";
            UpdatePreview();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            PlantName = string.IsNullOrWhiteSpace(TxtPlantName.Text) ? "Plant 01 (Chennai Works)" : TxtPlantName.Text.Trim();
            DivisionName = string.IsNullOrWhiteSpace(TxtDivisionName.Text) ? "Assembly Division" : TxtDivisionName.Text.Trim();
            LineName = string.IsNullOrWhiteSpace(TxtLineName.Text) ? "RAC Line 01" : TxtLineName.Text.Trim();

            DialogResult = true;
            Close();
        }
    }
}
