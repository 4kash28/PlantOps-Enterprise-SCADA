using System.Windows;
using DotNetApp.ViewModels;

namespace DotNetApp.Views
{
    public partial class AuditLogWindow : Window
    {
        private readonly MainViewModel _vm;

        public AuditLogWindow(MainViewModel vm)
        {
            InitializeComponent();
            _vm = vm;
            DataContext = _vm;
        }

        private void OnRefresh_Click(object sender, RoutedEventArgs e)
        {
            _vm.ReloadDowntimeLogs();
        }

        private void OnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
