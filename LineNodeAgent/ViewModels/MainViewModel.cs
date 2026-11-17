using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using MessageBox = System.Windows.MessageBox;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;
using LineNodeAgent.Data;
using LineNodeAgent.Models;
using LineNodeAgent.Services;

namespace LineNodeAgent.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly AgentDatabaseService _db;
        private readonly WmiTelemetryService _wmi;
        private readonly TelemetryWorker _worker;
        private readonly DispatcherTimer _uiRefreshTimer = new();

        private AgentSettings _settings;
        private string _searchFilter = string.Empty;
        private string _streamStatus = "CONNECTED / TRANSMITTING";
        private string _streamStatusColor = "#10b981"; // Green
        private string _lastLatency = "Latency: -- ms";
        private string _packetsCount = "Packets: 0";
        private string _jsonPreview = "{}";
        private string _logText = "";

        // Theming (Light Mode by default, matching SCADA Dashboard)
        private bool _isDarkTheme = false;

        public Brush CanvasBg { get; private set; } = Brushes.White;
        public Brush SurfaceBg { get; private set; } = Brushes.White;
        public Brush CardBorder { get; private set; } = Brushes.LightGray;
        public Brush TextMain { get; private set; } = Brushes.Black;
        public Brush TextMuted { get; private set; } = Brushes.Gray;
        public Brush HeaderBg { get; private set; } = Brushes.White;
        public Brush HeaderBorder { get; private set; } = Brushes.LightGray;
        public Brush HeaderFg { get; private set; } = Brushes.Black;
        public Brush HeaderSubFg { get; private set; } = Brushes.Gray;
        public Brush HeaderBtnBg { get; private set; } = Brushes.White;
        public Brush HeaderBtnBorder { get; private set; } = Brushes.LightGray;
        public Brush HeaderBtnFg { get; private set; } = Brushes.Black;
        public Brush InputBg { get; private set; } = Brushes.White;
        public Brush InputBorder { get; private set; } = Brushes.LightGray;
        public Brush InputFg { get; private set; } = Brushes.Black;
        public Brush PreviewBg { get; private set; } = Brushes.White;
        public Brush PreviewBorder { get; private set; } = Brushes.LightGray;
        public Brush PreviewFg { get; private set; } = Brushes.DarkGreen;
        public Brush RowBorder { get; private set; } = Brushes.LightGray;
        public Brush BadgeTransmittingBg { get; private set; } = Brushes.LightGreen;
        public Brush BadgeTransmittingFg { get; private set; } = Brushes.DarkGreen;
        public Brush BadgeDisabledBg { get; private set; } = Brushes.LightGray;
        public Brush BadgeDisabledFg { get; private set; } = Brushes.Gray;
        public Brush CategoryBadgeBg { get; private set; } = Brushes.LightBlue;
        public Brush CategoryBadgeFg { get; private set; } = Brushes.DarkBlue;
        public Brush ValuePreviewBg { get; private set; } = Brushes.LightGreen;
        public Brush ValuePreviewBorder { get; private set; } = Brushes.LightGreen;
        public Brush ValuePreviewFg { get; private set; } = Brushes.DarkGreen;

        public MainViewModel()
        {
            _db = new AgentDatabaseService();
            _wmi = new WmiTelemetryService();
            _worker = new TelemetryWorker(_wmi);

            _settings = _db.LoadSettings();
            var functions = _db.LoadFunctions();

            foreach (var f in functions)
            {
                f.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(AgentFunctionItem.IsEnabled))
                    {
                        OnPropertyChanged(nameof(EnabledSummaryText));
                    }
                };
            }

            Functions = new ObservableCollection<AgentFunctionItem>(functions);
            FilteredFunctions = CollectionViewSource.GetDefaultView(Functions);
            FilteredFunctions.Filter = FilterFunctionItem;

            ApplyTheme();

            _worker.TransmissionEvent += OnTransmissionEvent;
            _worker.PayloadGenerated += OnPayloadGenerated;

            // Auto-start worker in background thread (Zero UI thread blocking)
            _worker.Start(_settings, Functions);

            // Resilient Network Reconnect Listener: Detect cable connect/disconnect & Wi-Fi adapter shifts
            try
            {
                NetworkChange.NetworkAvailabilityChanged += (s, e) =>
                {
                    System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
                    {
                        if (e.IsAvailable)
                        {
                            AppendLog("[NETWORK] Network adapter is UP! Telemetry auto-reconnecting to SCADA Central immediately...");
                            if (!_worker.IsRunning)
                            {
                                _worker.Start(_settings, Functions);
                            }
                            else
                            {
                                _worker.WakeUp();
                            }
                        }
                        else
                        {
                            AppendLog("[NETWORK] Network disconnected. Telemetry worker entering auto-retry standby...");
                        }
                    });
                };

                NetworkChange.NetworkAddressChanged += (s, e) =>
                {
                    System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
                    {
                        if (!_worker.IsRunning)
                        {
                            _worker.Start(_settings, Functions);
                        }
                        else
                        {
                            _worker.WakeUp();
                        }
                    });
                };
            }
            catch { }

            // 3-Second Self-Healing Watchdog: Ensures background transmission never halts
            _uiRefreshTimer.Interval = TimeSpan.FromSeconds(3);
            _uiRefreshTimer.Tick += (s, e) =>
            {
                if (!_worker.IsRunning)
                {
                    AppendLog("[WATCHDOG] Worker loop was idle. Automatically resuming background telemetry streaming...");
                    _worker.Start(_settings, Functions);
                }
            };
            _uiRefreshTimer.Start();

            AppendLog($"[INIT] PlantOps Line Node Agent v2.4 (C# .NET 8 / WPF) started.");
            AppendLog($"[INIT] Light Theme applied (matching SCADA central dashboard).");
            AppendLog($"[INIT] Database agent_config.db loaded. {Functions.Count} monitorable functions active.");
            AppendLog($"[INIT] Central SCADA endpoint: {_settings.ServerUrl}");
        }

        public bool IsDarkTheme
        {
            get => _isDarkTheme;
            set
            {
                if (_isDarkTheme != value)
                {
                    _isDarkTheme = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ThemeToggleLabel));
                    ApplyTheme();
                }
            }
        }

        public string ThemeToggleLabel => IsDarkTheme ? "🌙 Dark" : "☀️ Light";

        public void ToggleTheme()
        {
            IsDarkTheme = !IsDarkTheme;
        }

        private void ApplyTheme()
        {
            if (IsDarkTheme)
            {
                CanvasBg = new SolidColorBrush(Color.FromRgb(11, 17, 32));         // #0b1120
                SurfaceBg = new SolidColorBrush(Color.FromRgb(17, 24, 39));        // #111827
                CardBorder = new SolidColorBrush(Color.FromRgb(30, 41, 59));       // #1e293b
                TextMain = new SolidColorBrush(Color.FromRgb(248, 250, 252));      // #f8fafc
                TextMuted = new SolidColorBrush(Color.FromRgb(148, 163, 184));     // #94a3b8
                HeaderBg = new SolidColorBrush(Color.FromRgb(15, 23, 42));         // #0f172a
                HeaderBorder = new SolidColorBrush(Color.FromRgb(30, 41, 59));     // #1e293b
                HeaderFg = Brushes.White;
                HeaderSubFg = new SolidColorBrush(Color.FromRgb(148, 163, 184));   // #94a3b8
                HeaderBtnBg = new SolidColorBrush(Color.FromRgb(30, 41, 59));      // #1e293b
                HeaderBtnBorder = new SolidColorBrush(Color.FromRgb(51, 65, 85));  // #334155
                HeaderBtnFg = new SolidColorBrush(Color.FromRgb(226, 232, 240));   // #e2e8f0
                InputBg = new SolidColorBrush(Color.FromRgb(15, 23, 42));          // #0f172a
                InputBorder = new SolidColorBrush(Color.FromRgb(51, 65, 85));      // #334155
                InputFg = Brushes.White;
                PreviewBg = new SolidColorBrush(Color.FromRgb(15, 23, 42));        // #0f172a
                PreviewBorder = new SolidColorBrush(Color.FromRgb(30, 41, 59));
                PreviewFg = new SolidColorBrush(Color.FromRgb(134, 239, 172));     // #86efac
                RowBorder = new SolidColorBrush(Color.FromRgb(30, 41, 59));
                BadgeTransmittingBg = new SolidColorBrush(Color.FromRgb(20, 83, 45)); // #14532d
                BadgeTransmittingFg = Brushes.White;
                BadgeDisabledBg = new SolidColorBrush(Color.FromRgb(51, 65, 85));  // #334155
                BadgeDisabledFg = new SolidColorBrush(Color.FromRgb(148, 163, 184));
                CategoryBadgeBg = new SolidColorBrush(Color.FromRgb(30, 41, 59));
                CategoryBadgeFg = new SolidColorBrush(Color.FromRgb(56, 189, 248));
                ValuePreviewBg = new SolidColorBrush(Color.FromRgb(17, 24, 39));
                ValuePreviewBorder = new SolidColorBrush(Color.FromRgb(30, 41, 59));
                ValuePreviewFg = new SolidColorBrush(Color.FromRgb(134, 239, 172));
            }
            else
            {
                // Pure Light Mode matching SCADA Dashboard
                CanvasBg = new SolidColorBrush(Color.FromRgb(241, 245, 249));       // #f1f5f9
                SurfaceBg = Brushes.White;                                          // #ffffff
                CardBorder = new SolidColorBrush(Color.FromRgb(226, 232, 240));     // #e2e8f0
                TextMain = new SolidColorBrush(Color.FromRgb(15, 23, 42));          // #0f172a
                TextMuted = new SolidColorBrush(Color.FromRgb(100, 116, 139));      // #64748b
                HeaderBg = Brushes.White;                                           // #ffffff
                HeaderBorder = new SolidColorBrush(Color.FromRgb(226, 232, 240));   // #e2e8f0
                HeaderFg = new SolidColorBrush(Color.FromRgb(15, 23, 42));          // #0f172a
                HeaderSubFg = new SolidColorBrush(Color.FromRgb(100, 116, 139));    // #64748b
                HeaderBtnBg = new SolidColorBrush(Color.FromRgb(241, 245, 249));    // #f1f5f9
                HeaderBtnBorder = new SolidColorBrush(Color.FromRgb(203, 213, 225)); // #cbd5e1
                HeaderBtnFg = new SolidColorBrush(Color.FromRgb(51, 65, 85));       // #334155
                InputBg = new SolidColorBrush(Color.FromRgb(248, 250, 252));        // #f8fafc
                InputBorder = new SolidColorBrush(Color.FromRgb(203, 213, 225));    // #cbd5e1
                InputFg = new SolidColorBrush(Color.FromRgb(15, 23, 42));           // #0f172a
                PreviewBg = new SolidColorBrush(Color.FromRgb(248, 250, 252));      // #f8fafc
                PreviewBorder = new SolidColorBrush(Color.FromRgb(226, 232, 240));  // #e2e8f0
                PreviewFg = new SolidColorBrush(Color.FromRgb(21, 128, 61));        // #15803d
                RowBorder = new SolidColorBrush(Color.FromRgb(241, 245, 249));      // #f1f5f9
                BadgeTransmittingBg = new SolidColorBrush(Color.FromRgb(220, 252, 231)); // #dcfce7
                BadgeTransmittingFg = new SolidColorBrush(Color.FromRgb(21, 128, 61));   // #15803d
                BadgeDisabledBg = new SolidColorBrush(Color.FromRgb(241, 245, 249));     // #f1f5f9
                BadgeDisabledFg = new SolidColorBrush(Color.FromRgb(100, 116, 139));     // #64748b
                CategoryBadgeBg = new SolidColorBrush(Color.FromRgb(219, 234, 254));     // #dbeafe
                CategoryBadgeFg = new SolidColorBrush(Color.FromRgb(29, 78, 216));       // #1d4ed8
                ValuePreviewBg = new SolidColorBrush(Color.FromRgb(240, 253, 244));      // #f0fdf4
                ValuePreviewBorder = new SolidColorBrush(Color.FromRgb(187, 247, 208));  // #bbf7d0
                ValuePreviewFg = new SolidColorBrush(Color.FromRgb(21, 128, 61));        // #15803d
            }

            OnPropertyChanged(nameof(CanvasBg));
            OnPropertyChanged(nameof(SurfaceBg));
            OnPropertyChanged(nameof(CardBorder));
            OnPropertyChanged(nameof(TextMain));
            OnPropertyChanged(nameof(TextMuted));
            OnPropertyChanged(nameof(HeaderBg));
            OnPropertyChanged(nameof(HeaderBorder));
            OnPropertyChanged(nameof(HeaderFg));
            OnPropertyChanged(nameof(HeaderSubFg));
            OnPropertyChanged(nameof(HeaderBtnBg));
            OnPropertyChanged(nameof(HeaderBtnBorder));
            OnPropertyChanged(nameof(HeaderBtnFg));
            OnPropertyChanged(nameof(InputBg));
            OnPropertyChanged(nameof(InputBorder));
            OnPropertyChanged(nameof(InputFg));
            OnPropertyChanged(nameof(PreviewBg));
            OnPropertyChanged(nameof(PreviewBorder));
            OnPropertyChanged(nameof(PreviewFg));
            OnPropertyChanged(nameof(RowBorder));
            OnPropertyChanged(nameof(BadgeTransmittingBg));
            OnPropertyChanged(nameof(BadgeTransmittingFg));
            OnPropertyChanged(nameof(BadgeDisabledBg));
            OnPropertyChanged(nameof(BadgeDisabledFg));
            OnPropertyChanged(nameof(CategoryBadgeBg));
            OnPropertyChanged(nameof(CategoryBadgeFg));
            OnPropertyChanged(nameof(ValuePreviewBg));
            OnPropertyChanged(nameof(ValuePreviewBorder));
            OnPropertyChanged(nameof(ValuePreviewFg));
        }

        public AgentSettings Settings
        {
            get => _settings;
            set { _settings = value; OnPropertyChanged(); }
        }

        public ObservableCollection<AgentFunctionItem> Functions { get; }
        public ICollectionView FilteredFunctions { get; }

        public string SearchFilter
        {
            get => _searchFilter;
            set
            {
                if (_searchFilter != value)
                {
                    _searchFilter = value;
                    OnPropertyChanged();
                    FilteredFunctions.Refresh();
                }
            }
        }

        public string StreamStatus
        {
            get => _streamStatus;
            set { _streamStatus = value; OnPropertyChanged(); }
        }

        public string StreamStatusColor
        {
            get => _streamStatusColor;
            set { _streamStatusColor = value; OnPropertyChanged(); }
        }

        public string LastLatency
        {
            get => _lastLatency;
            set { _lastLatency = value; OnPropertyChanged(); }
        }

        public string PacketsCount
        {
            get => _packetsCount;
            set { _packetsCount = value; OnPropertyChanged(); }
        }

        public string JsonPreview
        {
            get => _jsonPreview;
            set { _jsonPreview = value; OnPropertyChanged(); }
        }

        public string LogText
        {
            get => _logText;
            set { _logText = value; OnPropertyChanged(); }
        }

        public string EnabledSummaryText
        {
            get
            {
                int enabled = Functions.Count(f => f.IsEnabled);
                return $"{enabled} of {Functions.Count} Functions Permitted & Transmitting";
            }
        }

        private bool FilterFunctionItem(object obj)
        {
            if (obj is not AgentFunctionItem item) return false;
            if (string.IsNullOrWhiteSpace(_searchFilter)) return true;

            var q = _searchFilter.Trim();
            return item.DisplayName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                   item.Key.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                   item.Category.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                   item.Description.Contains(q, StringComparison.OrdinalIgnoreCase);
        }

        public void RefreshLiveValues()
        {
            TriggerBackgroundRefresh();
        }

        public void TriggerBackgroundRefresh()
        {
            OnPropertyChanged(nameof(EnabledSummaryText));
            Task.Run(() =>
            {
                var (_, json) = _worker.CollectAndBuildPayload(_settings, Functions);
                System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
                {
                    JsonPreview = json;
                    OnPropertyChanged(nameof(EnabledSummaryText));
                });
            });
        }

        private void OnTransmissionEvent(string message, bool isSuccess)
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                AppendLog(message);
                if (isSuccess)
                {
                    StreamStatus = "CONNECTED / TRANSMITTING";
                    StreamStatusColor = "#10b981"; // Green
                }
                else
                {
                    StreamStatus = "RETRYING / STANDBY";
                    StreamStatusColor = "#f59e0b"; // Amber
                }
                PacketsCount = $"Packets: {_worker.PacketsSent}";
                LastLatency = $"Latency: {_worker.LastLatencyMs} ms";
            });
        }

        private void OnPayloadGenerated(TelemetryPayload payload, string json)
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                JsonPreview = json;
                OnPropertyChanged(nameof(EnabledSummaryText));
            });
        }

        public void SaveAndApply()
        {
            _db.SaveSettings(_settings);
            _db.SaveFunctions(Functions);

            // Register background start on Windows boot
            SetStartupRun(true);

            _worker.Stop();
            _worker.Start(_settings, Functions);

            OnPropertyChanged(nameof(Settings));
            OnPropertyChanged(nameof(EnabledSummaryText));
            AppendLog($"[SAVED] Configuration saved! Transmitting Station '{_settings.LineStationId}' to {_settings.ServerUrl}...");
            MessageBox.Show($"Configuration successfully saved to agent_config.db!\nStation '{_settings.LineStationId}' is now actively streaming enabled telemetry to SCADA Central ({_settings.ServerUrl}).",
                "Configuration Applied & Streaming", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public async Task SendOfflineNotificationAsync(string reason, string message)
        {
            await _worker.SendOfflineStatusAsync(_settings, reason, message);
        }

        public static void SetStartupRun(bool enable)
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
                if (key != null)
                {
                    var exe = Environment.ProcessPath;
                    if (!string.IsNullOrEmpty(exe))
                    {
                        if (enable)
                        {
                            key.SetValue("LineNodeAgent", $"\"{exe}\" --tray");
                        }
                        else
                        {
                            key.DeleteValue("LineNodeAgent", false);
                        }
                    }
                }
            }
            catch { }
        }

        public void EnableAll()
        {
            foreach (var f in Functions) f.IsEnabled = true;
            TriggerBackgroundRefresh();
        }

        public void DisableAll()
        {
            foreach (var f in Functions) f.IsEnabled = false;
            TriggerBackgroundRefresh();
        }

        public void SetHardwareOnly()
        {
            foreach (var f in Functions)
            {
                f.IsEnabled = f.Category.Equals("Hardware Core", StringComparison.OrdinalIgnoreCase);
            }
            TriggerBackgroundRefresh();
        }

        public void SetMinimal()
        {
            foreach (var f in Functions)
            {
                f.IsEnabled = f.Key.Equals("Network_Ping", StringComparison.OrdinalIgnoreCase) ||
                              f.Key.Equals("System_Uptime", StringComparison.OrdinalIgnoreCase);
            }
            TriggerBackgroundRefresh();
        }

        public async Task TestConnectionAsync()
        {
            var target = _settings.ServerUrl.TrimEnd('/') + "/api/stations";
            AppendLog($"[TEST] Testing connectivity to {target}...");

            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var resp = await client.GetAsync(target);
                sw.Stop();

                if (resp.IsSuccessStatusCode)
                {
                    AppendLog($"[TEST OK] SCADA Dashboard responded with HTTP 200 ({sw.ElapsedMilliseconds}ms)");
                    MessageBox.Show($"SCADA Central Dashboard Reachable!\n\nTarget: {target}\nRoundtrip: {sw.ElapsedMilliseconds} ms\nStatus: 200 OK",
                        "Link Test Succeeded", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    AppendLog($"[TEST WARN] Dashboard returned HTTP {(int)resp.StatusCode}");
                    MessageBox.Show($"Server reachable but returned: {(int)resp.StatusCode} {resp.StatusCode}",
                        "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                AppendLog($"[TEST FAILED] Cannot reach {target}: {ex.Message}");
                MessageBox.Show($"Cannot reach SCADA Dashboard at {_settings.ServerUrl}\n\nError: {ex.Message}\n\nPlease verify that DotNetApp (ScadaDashboard) is running.",
                    "Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void ClearLog()
        {
            LogText = string.Empty;
        }

        public void ReloadFromDatabase()
        {
            Settings = _db.LoadSettings();
            _worker.Stop();
            _worker.Start(Settings, Functions);
            OnPropertyChanged(nameof(Settings));
            AppendLog($"[SETUP] Station settings reloaded from Setup Wizard: '{Settings.LineStationId}' streaming to {Settings.ServerUrl}");
        }

        private int _consecutiveRetries = 0;

        public void AppendLog(string msg)
        {
            if (msg.Contains("[TX RETRYING]"))
            {
                _consecutiveRetries++;
                // Throttle repetitive retries so the log remains clean, legible, and unspammed
                if (_consecutiveRetries > 1 && _consecutiveRetries % 10 != 0)
                {
                    return;
                }
                msg = $"[STANDBY] Awaiting SCADA Central at {_settings.ServerUrl} (Attempt #{_consecutiveRetries} • Auto-reconnecting...)";
            }
            else
            {
                _consecutiveRetries = 0;
            }

            var line = $"[{DateTime.Now:HH:mm:ss}] {msg}\n";
            if (LogText.Length > 20000)
            {
                LogText = LogText.Substring(LogText.Length - 10000);
            }
            LogText += line;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
