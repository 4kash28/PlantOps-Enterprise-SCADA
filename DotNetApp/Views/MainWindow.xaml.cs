using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using DotNetApp.ViewModels;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;
using MessageBoxResult = System.Windows.MessageBoxResult;

namespace DotNetApp.Views
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _vm;
        private AuditLogWindow? _auditLogWindow;
        private System.Windows.Forms.NotifyIcon? _notifyIcon;
        private bool _isExplicitExit = false;

        public MainWindow()
        {
            InitializeComponent();
            _vm = new MainViewModel();
            DataContext = _vm;

            _vm.RequestScrollToAuditLog += OpenAuditLogWindow;
            InitializeSystemTray();
        }

        private void InitializeSystemTray()
        {
            try
            {
                var appDir = AppDomain.CurrentDomain.BaseDirectory;
                var iconPath = Path.Combine(appDir, "plantops_monitor.ico");
                System.Drawing.Icon? appIcon = null;
                if (File.Exists(iconPath))
                {
                    appIcon = new System.Drawing.Icon(iconPath);
                }
                else
                {
                    appIcon = System.Drawing.SystemIcons.Application;
                }

                _notifyIcon = new System.Windows.Forms.NotifyIcon
                {
                    Icon = appIcon,
                    Text = "PlantOps™ Enterprise SCADA Server (Port 5050 Active)",
                    Visible = true
                };

                _notifyIcon.DoubleClick += (s, e) => RestoreMainWindow();

                var contextMenu = new System.Windows.Forms.ContextMenuStrip();

                var headerItem = new System.Windows.Forms.ToolStripMenuItem("PlantOps™ Enterprise SCADA v2.4")
                {
                    Enabled = false,
                    Font = new System.Drawing.Font("Segoe UI", 9, System.Drawing.FontStyle.Bold)
                };
                contextMenu.Items.Add(headerItem);

                var statusItem = new System.Windows.Forms.ToolStripMenuItem("⚡ Telemetry Server: Port 5050 Active")
                {
                    Enabled = false
                };
                contextMenu.Items.Add(statusItem);

                contextMenu.Items.Add("-");

                contextMenu.Items.Add("Open SCADA Dashboard", null, (s, e) => RestoreMainWindow());
                contextMenu.Items.Add("Open Audit Log Window", null, (s, e) => OpenAuditLogWindow());
                contextMenu.Items.Add("🔊 Test Alarm Siren", null, (s, e) => _vm.Audio.PlayTestSound());
                contextMenu.Items.Add("ℹ️ About & System Diagnostics...", null, (s, e) => OpenAboutDialog());
                contextMenu.Items.Add("-");
                contextMenu.Items.Add("Minimize to System Tray", null, (s, e) => Hide());

                contextMenu.Items.Add("-");

                contextMenu.Items.Add("Exit SCADA Server", null, (s, e) =>
                {
                    var res = MessageBox.Show(
                        "Stopping PlantOps™ Enterprise SCADA will stop telemetry ingestion from line workstations.\n\nAre you sure you want to stop the SCADA server?",
                        "Exit SCADA Server",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (res == MessageBoxResult.Yes)
                    {
                        _isExplicitExit = true;
                        _notifyIcon.Visible = false;
                        _notifyIcon.Dispose();
                        Application.Current.Shutdown();
                    }
                });

                _notifyIcon.ContextMenuStrip = contextMenu;
            }
            catch { }
        }

        public void RestoreMainWindow()
        {
            Show();
            if (WindowState == WindowState.Minimized)
            {
                WindowState = WindowState.Normal;
            }
            Activate();
            Focus();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (!_isExplicitExit)
            {
                // Background Service Mode: Do NOT stop the SCADA server when window is closed!
                e.Cancel = true;
                Hide();
                _notifyIcon?.ShowBalloonTip(
                    2000,
                    "PlantOps™ Enterprise SCADA",
                    "SCADA server is running in the background.\nTelemetry ingestion on Port 5050 remains active.",
                    System.Windows.Forms.ToolTipIcon.Info);
            }
            else
            {
                _notifyIcon?.Dispose();
                base.OnClosing(e);
            }
        }

        private void OnAuditLogHeaderBtn_Click(object sender, RoutedEventArgs e)
        {
            OpenAuditLogWindow();
        }

        private void OpenAuditLogWindow()
        {
            if (_auditLogWindow == null || !_auditLogWindow.IsLoaded)
            {
                _auditLogWindow = new AuditLogWindow(_vm);
                _auditLogWindow.Closed += (s, args) => _auditLogWindow = null;
                _auditLogWindow.Show();
            }
            else
            {
                if (_auditLogWindow.WindowState == WindowState.Minimized)
                    _auditLogWindow.WindowState = WindowState.Normal;
                _auditLogWindow.Activate();
            }
        }

        private void OnEditPlantProfile_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new PlantProfileDialogWindow(_vm.PlantName, _vm.DivisionName, _vm.LineName)
            {
                Owner = this
            };

            if (dlg.ShowDialog() == true)
            {
                _vm.UpdatePlantProfile(dlg.PlantName, dlg.DivisionName, dlg.LineName);
            }
        }

        private void OnSettingsBtn_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SettingsDialog(_vm.Thresholds)
            {
                Owner = this
            };

            if (dlg.ShowDialog() == true)
            {
                _vm.Thresholds = _vm.Thresholds; // Triggers save & re-evaluate
            }
        }

        private void OnTroubleshootingBtn_Click(object sender, RoutedEventArgs e)
        {
            var win = new TroubleshootingWindow(_vm)
            {
                Owner = this
            };
            win.Show();
        }

        private void OnNetworkBtn_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new NetworkDialog(_vm.NetworkProfile)
            {
                Owner = this
            };

            if (dlg.ShowDialog() == true)
            {
                _vm.NetworkProfile = _vm.NetworkProfile;
            }
        }

        private void OnAboutBtn_Click(object sender, RoutedEventArgs e)
        {
            OpenAboutDialog();
        }

        public void OpenAboutDialog()
        {
            var dlg = new AboutDialog(_vm)
            {
                Owner = this
            };
            dlg.ShowDialog();
        }
    }
}
