using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using LineNodeAgent.ViewModels;

namespace LineNodeAgent.Views
{
    public partial class MainWindow : Window
    {
        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        private static extern uint RegisterWindowMessage(string lpString);
        private uint _taskbarCreatedMsg;

        private readonly MainViewModel _vm;
        private System.Windows.Forms.NotifyIcon? _notifyIcon;

        public MainWindow()
        {
            InitializeComponent();
            _vm = (MainViewModel)DataContext;
            InitializeSystemTray();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            try
            {
                var source = PresentationSource.FromVisual(this) as System.Windows.Interop.HwndSource;
                if (source != null)
                {
                    _taskbarCreatedMsg = RegisterWindowMessage("TaskbarCreated");
                    source.AddHook(WndProc);
                }
            }
            catch { }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (_taskbarCreatedMsg != 0 && (uint)msg == _taskbarCreatedMsg)
            {
                // Windows Explorer restarted: Re-register system tray icon so it never vanishes
                if (_notifyIcon != null)
                {
                    _notifyIcon.Visible = false;
                    _notifyIcon.Visible = true;
                }
            }
            return IntPtr.Zero;
        }

        private void InitializeSystemTray()
        {
            try
            {
                var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "linenode_agent.ico");
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
                    Text = "PlantOps™ Line Node Agent",
                    Visible = true
                };

                _notifyIcon.DoubleClick += (s, e) =>
                {
                    Show();
                    WindowState = WindowState.Normal;
                    Activate();
                };

                var contextMenu = new System.Windows.Forms.ContextMenuStrip();
                contextMenu.Items.Add("Open Line Agent Dashboard", null, (s, e) =>
                {
                    Show();
                    WindowState = WindowState.Normal;
                    Activate();
                });
                contextMenu.Items.Add("📡 Test SCADA Link", null, async (s, e) =>
                {
                    await _vm.TestConnectionAsync();
                });
                contextMenu.Items.Add("ℹ️ About Line Node Agent...", null, (s, e) =>
                {
                    OpenAboutDialog();
                });
                contextMenu.Items.Add("-");
                contextMenu.Items.Add("Minimize to System Tray", null, (s, e) => Hide());
                contextMenu.Items.Add("-");
                contextMenu.Items.Add("Exit Agent", null, async (s, e) =>
                {
                    var res = System.Windows.MessageBox.Show(
                        "Stopping Line Node Agent will stop hardware telemetry streaming to SCADA Central.\n\nAre you sure you want to exit?",
                        "Exit Line Node Agent",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (res == MessageBoxResult.Yes)
                    {
                        try
                        {
                            await _vm.SendOfflineNotificationAsync("AGENT_STOPPED", "Line Agent Exited by Operator");
                        }
                        catch { }

                        _notifyIcon.Visible = false;
                        _notifyIcon.Dispose();
                        System.Windows.Application.Current.Shutdown();
                    }
                });

                _notifyIcon.ContextMenuStrip = contextMenu;
            }
            catch { }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            // Background Running: ALWAYS keep agent running in background when 'X' is clicked
            e.Cancel = true;
            Hide();
            _notifyIcon?.ShowBalloonTip(1500, "PlantOps™ Line Node Agent", "Line Agent is running in the background.\nTelemetry streaming remains active.", System.Windows.Forms.ToolTipIcon.Info);
        }

        private void OnMinimizeTray_Click(object sender, RoutedEventArgs e)
        {
            Hide();
            _notifyIcon?.ShowBalloonTip(1500, "PlantOps™ Line Node Agent", "Minimized to Windows system tray.", System.Windows.Forms.ToolTipIcon.Info);
        }

        private void OnSaveAndApply_Click(object sender, RoutedEventArgs e)
        {
            _vm.SaveAndApply();
        }

        private void OnEnableAll_Click(object sender, RoutedEventArgs e)
        {
            _vm.EnableAll();
        }

        private void OnDisableAll_Click(object sender, RoutedEventArgs e)
        {
            _vm.DisableAll();
        }

        private void OnHardwareOnly_Click(object sender, RoutedEventArgs e)
        {
            _vm.SetHardwareOnly();
        }

        private void OnMinimal_Click(object sender, RoutedEventArgs e)
        {
            _vm.SetMinimal();
        }

        private async void OnTestLink_Click(object sender, RoutedEventArgs e)
        {
            await _vm.TestConnectionAsync();
        }

        private void OnToggleTheme_Click(object sender, RoutedEventArgs e)
        {
            _vm.ToggleTheme();
        }

        private void OnClearLog_Click(object sender, RoutedEventArgs e)
        {
            _vm.ClearLog();
        }

        private void OnClearSearch_Click(object sender, RoutedEventArgs e)
        {
            _vm.SearchFilter = string.Empty;
        }

        private void OnLogTextChanged(object sender, TextChangedEventArgs e)
        {
            TxtLog.ScrollToEnd();
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
