using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using DotNetApp.Views;

namespace DotNetApp
{
    public partial class App : Application
    {
        private static Mutex? _instanceMutex;
        private static EventWaitHandle? _showEvent;
        private const string MutexName = "Global\\PlantOps_SCADA_SingleInstance_Mutex";
        private const string EventName = "Global\\PlantOps_SCADA_ShowEvent";

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SW_RESTORE = 9;

        protected override void OnStartup(StartupEventArgs e)
        {
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                LogFatal("AppDomain UnhandledException", args.ExceptionObject as Exception);
            };

            DispatcherUnhandledException += (s, args) =>
            {
                LogFatal("DispatcherUnhandledException", args.Exception);
                args.Handled = true;
            };

            // Single Instance Enforcement: Ensure only ONE SCADA server engine runs
            bool createdNew;
            try
            {
                _instanceMutex = new Mutex(true, MutexName, out createdNew);
            }
            catch
            {
                createdNew = false;
            }

            if (!createdNew)
            {
                try
                {
                    using var evt = EventWaitHandle.OpenExisting(EventName);
                    evt.Set();
                }
                catch
                {
                    BringExistingProcessToFront();
                }

                Environment.Exit(0);
                return;
            }

            base.OnStartup(e);

            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            try
            {
                try
                {
                    _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, EventName);
                    Task.Run(() =>
                    {
                        while (_showEvent.WaitOne())
                        {
                            Current?.Dispatcher.Invoke(() =>
                            {
                                if (Current.MainWindow is MainWindow mw)
                                {
                                    mw.Show();
                                    if (mw.WindowState == WindowState.Minimized)
                                    {
                                        mw.WindowState = WindowState.Normal;
                                    }
                                    mw.Activate();
                                    mw.Focus();
                                }
                            });
                        }
                    });
                }
                catch { }

                var processPath = Environment.ProcessPath;
                var appDir = !string.IsNullOrEmpty(processPath)
                    ? Path.GetDirectoryName(processPath) ?? AppDomain.CurrentDomain.BaseDirectory
                    : AppDomain.CurrentDomain.BaseDirectory;

                bool isInstalled = File.Exists(Path.Combine(appDir, "license_activation.json")) ||
                                   File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "license_activation.json")) ||
                                   File.Exists(Path.Combine(AppContext.BaseDirectory, "license_activation.json")) ||
                                   File.Exists("license_activation.json");

                bool forceSetup = Array.Exists(e.Args, a => a.Equals("--setup", StringComparison.OrdinalIgnoreCase) || a.Equals("--install", StringComparison.OrdinalIgnoreCase));

                // If not licensed/installed or if --setup requested, run installation wizard
                if (!isInstalled || forceSetup)
                {
                    var wizard = new InstallationWizardWindow();
                    var result = wizard.ShowDialog();

                    if (result != true || !wizard.LaunchDashboardAfter)
                    {
                        Shutdown();
                        return;
                    }
                }

                // Launch main application window
                var mainWindow = new MainWindow();
                MainWindow = mainWindow;
                ShutdownMode = ShutdownMode.OnExplicitShutdown;

                bool startMinimized = Array.Exists(e.Args, a => a.Equals("--tray", StringComparison.OrdinalIgnoreCase) || 
                                                                a.Equals("--minimized", StringComparison.OrdinalIgnoreCase) ||
                                                                a.Equals("--background", StringComparison.OrdinalIgnoreCase));
                if (!startMinimized)
                {
                    mainWindow.Show();
                }
            }
            catch (Exception ex)
            {
                LogFatal("StartupException", ex);
                MessageBox.Show($"Failed to initialize application: {ex.Message}", "Fatal Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                _instanceMutex?.ReleaseMutex();
                _instanceMutex?.Dispose();
                _showEvent?.Dispose();
            }
            catch { }
            base.OnExit(e);
        }

        private static void BringExistingProcessToFront()
        {
            try
            {
                var current = Process.GetCurrentProcess();
                var others = Process.GetProcessesByName(current.ProcessName);
                foreach (var p in others)
                {
                    if (p.Id != current.Id && p.MainWindowHandle != IntPtr.Zero)
                    {
                        ShowWindow(p.MainWindowHandle, SW_RESTORE);
                        SetForegroundWindow(p.MainWindowHandle);
                        break;
                    }
                }
            }
            catch { }
        }

        private static void LogFatal(string type, Exception? ex)
        {
            try
            {
                var processPath = Environment.ProcessPath;
                var dir = !string.IsNullOrEmpty(processPath)
                    ? Path.GetDirectoryName(processPath) ?? AppDomain.CurrentDomain.BaseDirectory
                    : AppDomain.CurrentDomain.BaseDirectory;
                var logFile = Path.Combine(dir, "app_error.log");
                File.AppendAllText(logFile, $"[{DateTime.UtcNow:o}] [{type}] {ex}\n");
            }
            catch { }
        }
    }
}
