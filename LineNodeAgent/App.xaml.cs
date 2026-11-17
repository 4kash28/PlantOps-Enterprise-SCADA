using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using LineNodeAgent.Views;
using Microsoft.Win32;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;
using MessageBoxResult = System.Windows.MessageBoxResult;

namespace LineNodeAgent
{
    public partial class App : System.Windows.Application
    {
        private static Mutex? _instanceMutex;
        private static EventWaitHandle? _showEvent;
        private const string MutexName = "Global\\PlantOps_LineNodeAgent_SingleInstance_Mutex";
        private const string EventName = "Global\\PlantOps_LineNodeAgent_ShowEvent";

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

            // Uninstallation Routine: Handle before checking single-instance mutex
            bool isUninstall = Array.Exists(e.Args, a => a.Equals("--uninstall", StringComparison.OrdinalIgnoreCase) ||
                                                         a.Equals("/uninstall", StringComparison.OrdinalIgnoreCase) ||
                                                         a.Equals("-uninstall", StringComparison.OrdinalIgnoreCase) ||
                                                         a.Equals("/u", StringComparison.OrdinalIgnoreCase));
            if (isUninstall)
            {
                bool isQuiet = Array.Exists(e.Args, a => a.Equals("--quiet", StringComparison.OrdinalIgnoreCase) ||
                                                         a.Equals("/quiet", StringComparison.OrdinalIgnoreCase) ||
                                                         a.Equals("/s", StringComparison.OrdinalIgnoreCase) ||
                                                         a.Equals("/silent", StringComparison.OrdinalIgnoreCase) ||
                                                         a.Equals("--silent", StringComparison.OrdinalIgnoreCase));
                PerformUninstallation(isQuiet);
                Environment.Exit(0);
                return;
            }

            // Single Instance Enforcement: Ensure only ONE LineNodeAgent runs per workstation
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
                // Another instance of LineNodeAgent is already actively transmitting!
                // Signal the running instance to restore & bring its window to front
                try
                {
                    using var evt = EventWaitHandle.OpenExisting(EventName);
                    evt.Set();
                }
                catch
                {
                    BringExistingProcessToFront();
                }

                // Immediately terminate this duplicate process
                Environment.Exit(0);
                return;
            }

            SessionEnding += (s, args) => HandleHostShutdown();
            try
            {
                Microsoft.Win32.SystemEvents.SessionEnding += (s, args) => HandleHostShutdown();
            }
            catch { }

            try
            {
                Microsoft.Win32.SystemEvents.PowerModeChanged += (s, args) =>
                {
                    if (args.Mode == Microsoft.Win32.PowerModes.Resume)
                    {
                        // PC resumed from Sleep/Modern Standby: auto-resume telemetry worker
                        Current?.Dispatcher.InvokeAsync(() =>
                        {
                            if (Current.MainWindow is MainWindow mw && mw.DataContext is ViewModels.MainViewModel vm)
                            {
                                vm.AppendLog("[POWER] Workstation resumed from sleep. Auto-reconnecting telemetry stream...");
                                vm.RefreshLiveValues();
                            }
                        });
                    }
                };
            }
            catch { }

            base.OnStartup(e);

            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            try
            {
                // Listen for activation requests from secondary process launches
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

                bool forceSetup = Array.Exists(e.Args, a => a.Equals("--setup", StringComparison.OrdinalIgnoreCase) || 
                                                            a.Equals("--install", StringComparison.OrdinalIgnoreCase) ||
                                                            a.Equals("/setup", StringComparison.OrdinalIgnoreCase) ||
                                                            a.Equals("/install", StringComparison.OrdinalIgnoreCase));
                var installedMarker = Path.Combine(appDir, "agent_installed.json");
                bool isInstalled = File.Exists(installedMarker);

                if (forceSetup || !isInstalled)
                {
                    var wizard = new InstallationWizardWindow();
                    var wizardResult = wizard.ShowDialog();
                    if (wizardResult != true)
                    {
                        // User cancelled setup on fresh install
                        Shutdown();
                        return;
                    }
                    if (!wizard.LaunchAgentAfter)
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
                MessageBox.Show($"Failed to initialize Line Node Agent: {ex.Message}", "Fatal Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
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

        private static void HandleHostShutdown()
        {
            try
            {
                var db = new Data.AgentDatabaseService();
                var settings = db.LoadSettings();
                var wmi = new Services.WmiTelemetryService();
                var worker = new Services.TelemetryWorker(wmi);
                worker.SendOfflineStatusSync(settings, "PC_SHUTDOWN", "Workstation PC Shutdown / Restart");
            }
            catch { }
        }

        private static void PerformUninstallation(bool isQuiet)
        {
            try
            {
                if (!isQuiet)
                {
                    var confirm = MessageBox.Show(
                        "Are you sure you want to completely uninstall PlantOps™ Line Node Agent v2.4 from this workstation?\n\n" +
                        "This will:\n" +
                        "• Stop active background telemetry transmission\n" +
                        "• Deregister this station from Central SCADA Dashboard\n" +
                        "• Remove Windows Startup and Programs & Features registrations\n" +
                        "• Remove Desktop and Start Menu shortcuts\n" +
                        "• Clean configuration and local database markers",
                        "PlantOps™ Line Node Agent — Confirm Uninstallation",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (confirm != MessageBoxResult.Yes)
                    {
                        return;
                    }
                }

                // 1. Terminate all other running instances of LineNodeAgent
                try
                {
                    int currentId = Process.GetCurrentProcess().Id;
                    foreach (var p in Process.GetProcessesByName("LineNodeAgent"))
                    {
                        if (p.Id != currentId)
                        {
                            try
                            {
                                p.Kill();
                                p.WaitForExit(2000);
                            }
                            catch { }
                        }
                    }
                }
                catch { }

                var processPath = Environment.ProcessPath;
                var appDir = !string.IsNullOrEmpty(processPath)
                    ? Path.GetDirectoryName(processPath) ?? AppDomain.CurrentDomain.BaseDirectory
                    : AppDomain.CurrentDomain.BaseDirectory;

                // 2. Deregister station from Central SCADA Dashboard
                string? stationId = null;
                string? serverUrl = null;
                try
                {
                    var markerPath = Path.Combine(appDir, "agent_installed.json");
                    if (File.Exists(markerPath))
                    {
                        try
                        {
                            using var doc = JsonDocument.Parse(File.ReadAllText(markerPath));
                            if (doc.RootElement.TryGetProperty("line_station_id", out var sid))
                                stationId = sid.GetString();
                            if (doc.RootElement.TryGetProperty("scada_server_url", out var surl))
                                serverUrl = surl.GetString();
                        }
                        catch { }
                    }

                    if (string.IsNullOrEmpty(stationId))
                    {
                        try
                        {
                            var db = new Data.AgentDatabaseService();
                            var st = db.LoadSettings();
                            stationId = st.LineStationId;
                            serverUrl = st.ServerUrl;
                        }
                        catch { }
                    }

                    if (!string.IsNullOrEmpty(serverUrl) && !string.IsNullOrEmpty(stationId))
                    {
                        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
                        var ep = serverUrl.TrimEnd('/') + $"/api/stations/{Uri.EscapeDataString(stationId)}";
                        http.DeleteAsync(ep).Wait(1500);
                    }
                }
                catch { }

                // 3. Clean local SCADA database records (if SCADA is hosted on same PC)
                try
                {
                    var appLocalDb = Path.Combine(appDir, "app_local.db");
                    if (File.Exists(appLocalDb) && !string.IsNullOrEmpty(stationId))
                    {
                        using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={appLocalDb}");
                        conn.Open();
                        using var cmd = conn.CreateCommand();
                        cmd.CommandText = "DELETE FROM stations WHERE station_id = $id; DELETE FROM threshold_breach_logs WHERE station_id = $id; DELETE FROM downtime_logs WHERE station_id = $id;";
                        cmd.Parameters.AddWithValue("$id", stationId);
                        cmd.ExecuteNonQuery();
                    }
                }
                catch { }

                // 4. Remove Windows Startup Key (HKCU\Software\Microsoft\Windows\CurrentVersion\Run)
                try
                {
                    using var runKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
                    runKey?.DeleteValue("PlantOpsLineNodeAgent", false);
                }
                catch { }

                // 5. Remove Windows Programs & Features Uninstall Keys (HKCU and HKLM)
                try
                {
                    using var uninstHkcu = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall", true);
                    uninstHkcu?.DeleteSubKeyTree("PlantOpsLineNodeAgent", false);
                }
                catch { }

                try
                {
                    using var uninstHklm = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall", true);
                    uninstHklm?.DeleteSubKeyTree("PlantOpsLineNodeAgent", false);
                }
                catch { }

                // 6. Remove Desktop and Start Menu Shortcuts
                try
                {
                    var desktopLnk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "PlantOps Line Node Agent.lnk");
                    if (File.Exists(desktopLnk)) File.Delete(desktopLnk);

                    var commonDesktopLnk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory), "PlantOps Line Node Agent.lnk");
                    if (File.Exists(commonDesktopLnk)) File.Delete(commonDesktopLnk);

                    var startLnk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "PlantOps Line Node Agent.lnk");
                    if (File.Exists(startLnk)) File.Delete(startLnk);
                }
                catch { }

                // 7. Delete installation receipts and local configuration caches
                try
                {
                    string[] filesToClean =
                    {
                        "agent_installed.json",
                        "agent_config.db",
                        "agent_config.db-wal",
                        "agent_config.db-shm",
                        "agent_error.log",
                        "agent_out.txt",
                        "agent_err.txt"
                    };

                    foreach (var fn in filesToClean)
                    {
                        var fp = Path.Combine(appDir, fn);
                        if (File.Exists(fp))
                        {
                            try { File.Delete(fp); } catch { }
                        }
                    }
                }
                catch { }

                // 8. Inform user upon successful completion
                if (!isQuiet)
                {
                    MessageBox.Show(
                        "PlantOps™ Line Node Agent has been successfully uninstalled from this workstation.\n\n" +
                        "• All background telemetry processes stopped\n" +
                        "• Deregistered from Central SCADA Dashboard\n" +
                        "• Removed from Windows Startup and Programs & Features\n" +
                        "• Desktop shortcuts removed",
                        "Line Node Agent Uninstalled",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                if (!isQuiet)
                {
                    MessageBox.Show($"Uninstallation encountered an issue:\n{ex.Message}", "Uninstall Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private static void LogFatal(string type, Exception? ex)
        {
            try
            {
                var processPath = Environment.ProcessPath;
                var dir = !string.IsNullOrEmpty(processPath)
                    ? Path.GetDirectoryName(processPath) ?? AppDomain.CurrentDomain.BaseDirectory
                    : AppDomain.CurrentDomain.BaseDirectory;
                var logFile = Path.Combine(dir, "agent_error.log");
                File.AppendAllText(logFile, $"[{DateTime.UtcNow:o}] [{type}] {ex}\n");
            }
            catch { }
        }
    }
}
