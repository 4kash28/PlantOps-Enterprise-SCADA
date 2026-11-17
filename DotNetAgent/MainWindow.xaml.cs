using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using DotNetAgent.Models;
using DotNetAgent.Services;

namespace DotNetAgent
{
    public partial class MainWindow : Window
    {
        private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(2.5) };
        private readonly TelemetryCollectorService _collector = new();
        private readonly DispatcherTimer _telemetryTimer = new();

        private AgentConfig _config = new();
        private bool _isStreaming = false;
        private long _packetCount = 0;

        public MainWindow()
        {
            InitializeComponent();
            LoadConfig();
            ApplyConfigToUi();

            TxtFooterHostname.Text = $"HOST: {Environment.MachineName.ToUpperInvariant()}";

            _telemetryTimer.Interval = TimeSpan.FromSeconds(_config.SyncIntervalSec);
            _telemetryTimer.Tick += OnTelemetryTimerTick;
            _telemetryTimer.Start();

            UpdateLiveTelemetryPreview();
            AppendLog("[SYSTEM] Line Node Agent initialized in C# .NET 8 (WPF).");
            AppendLog("[SYSTEM] Data Permission Matrix loaded. Ready to stream.");
        }

        private void LoadConfig()
        {
            try
            {
                var cfgPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
                if (File.Exists(cfgPath))
                {
                    var text = File.ReadAllText(cfgPath);
                    _config = JsonSerializer.Deserialize<AgentConfig>(text, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new AgentConfig();
                    return;
                }
            }
            catch { }

            _config = new AgentConfig();
        }

        private void SaveConfigToFile()
        {
            try
            {
                ReadUiToConfig();
                var cfgPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
                File.WriteAllText(cfgPath, JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex)
            {
                AppendLog($"[ERR] Failed to save config: {ex.Message}");
            }
        }

        private void ApplyConfigToUi()
        {
            TxtServerIp.Text = _config.ServerHost;
            TxtServerPort.Text = _config.ServerPort.ToString();
            TxtStationId.Text = _config.StationId;
            TxtStationName.Text = _config.StationName;

            ChkPermCpu.IsChecked = _config.Permissions.AllowCpu;
            ChkPermRam.IsChecked = _config.Permissions.AllowRam;
            ChkPermDisk.IsChecked = _config.Permissions.AllowDisk;
            ChkPermUptime.IsChecked = _config.Permissions.AllowUptime;
            ChkPermNetwork.IsChecked = _config.Permissions.AllowNetwork;
            ChkPermZebra.IsChecked = _config.Permissions.AllowZebra;

            UpdateHeaderTargetUrl();
            UpdatePermissionBadges();
        }

        private void ReadUiToConfig()
        {
            _config.ServerHost = TxtServerIp.Text.Trim();
            if (int.TryParse(TxtServerPort.Text.Trim(), out var p))
                _config.ServerPort = p;

            _config.StationId = TxtStationId.Text.Trim();
            _config.StationName = TxtStationName.Text.Trim();

            _config.Permissions.AllowCpu = ChkPermCpu.IsChecked == true;
            _config.Permissions.AllowRam = ChkPermRam.IsChecked == true;
            _config.Permissions.AllowDisk = ChkPermDisk.IsChecked == true;
            _config.Permissions.AllowUptime = ChkPermUptime.IsChecked == true;
            _config.Permissions.AllowNetwork = ChkPermNetwork.IsChecked == true;
            _config.Permissions.AllowZebra = ChkPermZebra.IsChecked == true;
        }

        private void UpdateHeaderTargetUrl()
        {
            var host = string.IsNullOrWhiteSpace(TxtServerIp.Text) ? "127.0.0.1" : TxtServerIp.Text.Trim();
            var port = string.IsNullOrWhiteSpace(TxtServerPort.Text) ? "8090" : TxtServerPort.Text.Trim();
            TxtHeaderUrl.Text = $"http://{host}:{port}/api/telemetry";
        }

        private void UpdatePermissionBadges()
        {
            bool cpu = ChkPermCpu.IsChecked == true;
            bool ram = ChkPermRam.IsChecked == true;
            bool disk = ChkPermDisk.IsChecked == true;
            bool zebra = ChkPermZebra.IsChecked == true;

            TxtPermCpuStatus.Text = cpu ? "PERMITTED" : "BLOCKED";
            TxtPermCpuStatus.Foreground = cpu ? new SolidColorBrush(Color.FromRgb(16, 185, 129)) : new SolidColorBrush(Color.FromRgb(239, 68, 68));
            TxtLiveCpu.Opacity = cpu ? 1.0 : 0.35;

            TxtPermRamStatus.Text = ram ? "PERMITTED" : "BLOCKED";
            TxtPermRamStatus.Foreground = ram ? new SolidColorBrush(Color.FromRgb(16, 185, 129)) : new SolidColorBrush(Color.FromRgb(239, 68, 68));
            TxtLiveRam.Opacity = ram ? 1.0 : 0.35;

            TxtPermDiskStatus.Text = disk ? "PERMITTED" : "BLOCKED";
            TxtPermDiskStatus.Foreground = disk ? new SolidColorBrush(Color.FromRgb(16, 185, 129)) : new SolidColorBrush(Color.FromRgb(239, 68, 68));
            TxtLiveDisk.Opacity = disk ? 1.0 : 0.35;

            TxtPermZebraStatus.Text = zebra ? "PERMITTED" : "BLOCKED";
            TxtPermZebraStatus.Foreground = zebra ? new SolidColorBrush(Color.FromRgb(16, 185, 129)) : new SolidColorBrush(Color.FromRgb(239, 68, 68));
            TxtLiveZebra.Opacity = zebra ? 1.0 : 0.35;

            int count = (cpu ? 1 : 0) + (ram ? 1 : 0) + (disk ? 1 : 0) + (ChkPermUptime.IsChecked == true ? 1 : 0) + (ChkPermNetwork.IsChecked == true ? 1 : 0) + (zebra ? 1 : 0);
            TxtPermSummaryBadge.Text = $"{count}/6 PERMITTED";
            TxtPermSummaryBadge.Foreground = count == 6 ? new SolidColorBrush(Color.FromRgb(16, 185, 129))
                                            : count > 0 ? new SolidColorBrush(Color.FromRgb(56, 189, 248))
                                            : new SolidColorBrush(Color.FromRgb(239, 68, 68));
        }

        private void UpdateLiveTelemetryPreview()
        {
            ReadUiToConfig();
            var raw = _collector.ReadRawHardware(_config.ServerHost);

            TxtLiveCpu.Text = $"{raw.rawCpu:F1}%";
            TxtLiveRam.Text = $"{raw.rawRam:F1}%";
            TxtLiveDisk.Text = $"{raw.rawDisk:F1}%";
            TxtLiveZebra.Text = _config.Permissions.AllowZebra ? "READY" : "MUTED";

            var packet = _collector.BuildPayload(_config, 0.5);
            var json = JsonSerializer.Serialize(packet, new JsonSerializerOptions { WriteIndented = true });
            TxtJsonPreview.Text = json;
        }

        private void OnTelemetryTimerTick(object? sender, EventArgs e)
        {
            UpdateLiveTelemetryPreview();

            if (_isStreaming)
            {
                _ = DispatchTelemetryPacketAsync();
            }
        }

        private async Task DispatchTelemetryPacketAsync()
        {
            ReadUiToConfig();
            var url = $"http://{_config.ServerHost}:{_config.ServerPort}/api/telemetry";

            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var packet = _collector.BuildPayload(_config);
                var json = JsonSerializer.Serialize(packet);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");

                var resp = await _http.PostAsync(url, content);
                sw.Stop();

                _packetCount++;
                TxtPacketsCount.Text = $"Packets: {_packetCount}";
                TxtLastPing.Text = $"Latency: {sw.ElapsedMilliseconds} ms";

                if (resp.IsSuccessStatusCode)
                {
                    var respStr = await resp.Content.ReadAsStringAsync();
                    if (respStr.Contains("\"should_stop\":true"))
                    {
                        AppendLog($"[{DateTime.Now:HH:mm:ss}] [ALERT] SCADA operator deregistered this station. Stopping stream.");
                        StopStreaming();
                        return;
                    }

                    AppendLog($"[{DateTime.Now:HH:mm:ss}] [TX OK] Packet #{_packetCount} sent ({sw.ElapsedMilliseconds}ms) -> SCADA 200 OK");
                }
                else
                {
                    AppendLog($"[{DateTime.Now:HH:mm:ss}] [TX WARN] SCADA returned HTTP {(int)resp.StatusCode} {resp.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                AppendLog($"[{DateTime.Now:HH:mm:ss}] [TX ERR] Link failed: {ex.Message}");
                SetStatusError("LINK UNREACHABLE");
            }
        }

        private void OnToggleStream_Click(object sender, RoutedEventArgs e)
        {
            if (!_isStreaming)
            {
                StartStreaming();
            }
            else
            {
                StopStreaming();
            }
        }

        private void StartStreaming()
        {
            _isStreaming = true;
            _packetCount = 0;
            TxtStreamBtnLabel.Text = "Stop Stream";
            BtnToggleStream.Background = new SolidColorBrush(Color.FromRgb(220, 38, 38)); // Red
            PathStreamIcon.Data = Geometry.Parse("M14,19H18V5H14M6,19H10V5H6V19Z"); // Pause/Stop

            TxtStreamStatusBadge.Text = "TRANSMITTING ACTIVE";
            TxtStreamStatusBadge.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
            RectStatusLed.Fill = new SolidColorBrush(Color.FromRgb(16, 185, 129));

            AppendLog($"[{DateTime.Now:HH:mm:ss}] [STREAM STARTED] Broadcasting to {TxtHeaderUrl.Text} every {_config.SyncIntervalSec}s.");
            _ = DispatchTelemetryPacketAsync();
        }

        private void StopStreaming()
        {
            _isStreaming = false;
            TxtStreamBtnLabel.Text = "Start Stream";
            BtnToggleStream.Background = new SolidColorBrush(Color.FromRgb(22, 163, 74)); // Green
            PathStreamIcon.Data = Geometry.Parse("M8,5.14V19.14L19,12.14L8,5.14Z"); // Play

            TxtStreamStatusBadge.Text = "STREAMING IDLE";
            TxtStreamStatusBadge.Foreground = new SolidColorBrush(Color.FromRgb(234, 179, 8));
            RectStatusLed.Fill = new SolidColorBrush(Color.FromRgb(234, 179, 8));

            AppendLog($"[{DateTime.Now:HH:mm:ss}] [STREAM STOPPED] Workstation telemetry paused.");
        }

        private void SetStatusError(string msg)
        {
            TxtStreamStatusBadge.Text = msg;
            TxtStreamStatusBadge.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
            RectStatusLed.Fill = new SolidColorBrush(Color.FromRgb(239, 68, 68));
        }

        private async void OnTestLink_Click(object sender, RoutedEventArgs e)
        {
            ReadUiToConfig();
            var url = $"http://{_config.ServerHost}:{_config.ServerPort}/api/stations";
            AppendLog($"[{DateTime.Now:HH:mm:ss}] [TEST] Pinging SCADA server at {url}...");

            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var resp = await _http.GetAsync(url);
                sw.Stop();

                if (resp.IsSuccessStatusCode)
                {
                    AppendLog($"[{DateTime.Now:HH:mm:ss}] [TEST SUCCESS] SCADA Core is ONLINE & accepting connections ({sw.ElapsedMilliseconds}ms)!");
                    MessageBox.Show($"SCADA Core is Online and Reachable!\n\nTarget: http://{_config.ServerHost}:{_config.ServerPort}/\nLatency: {sw.ElapsedMilliseconds} ms\nStatus: 200 OK",
                        "Link Test Succeeded", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    AppendLog($"[{DateTime.Now:HH:mm:ss}] [TEST FAIL] Server responded with HTTP {(int)resp.StatusCode}");
                    MessageBox.Show($"Server reachable but responded with error: {(int)resp.StatusCode} {resp.StatusCode}",
                        "Link Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                AppendLog($"[{DateTime.Now:HH:mm:ss}] [TEST FAIL] Cannot connect to SCADA Core: {ex.Message}");
                MessageBox.Show($"Cannot reach SCADA Host at http://{_config.ServerHost}:{_config.ServerPort}/\n\nError: {ex.Message}\n\nPlease check that PlantOpsEnterpriseSCADA.exe is running.",
                    "Connection Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnConfigChanged(object sender, TextChangedEventArgs e)
        {
            UpdateHeaderTargetUrl();
            UpdateLiveTelemetryPreview();
        }

        private void OnPermissionChanged(object sender, RoutedEventArgs e)
        {
            UpdatePermissionBadges();
            UpdateLiveTelemetryPreview();
            AppendLog($"[{DateTime.Now:HH:mm:ss}] [PERMISSION] Workstation data sharing rules updated.");
        }

        private void OnStationPresetChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbStationPreset.SelectedIndex == 0)
            {
                TxtStationId.Text = "LINE1_STATION_01";
                TxtStationName.Text = "ST-01: Chassis Press & Base Assembly";
                ChkPermZebra.IsChecked = false;
            }
            else if (CmbStationPreset.SelectedIndex == 1)
            {
                TxtStationId.Text = "LINE1_STATION_02";
                TxtStationName.Text = "ST-02: Evaporator Coil & Copper Brazing";
                ChkPermZebra.IsChecked = false;
            }
            else if (CmbStationPreset.SelectedIndex == 2)
            {
                TxtStationId.Text = "LINE1_STATION_03";
                TxtStationName.Text = "ST-03: Compressor & Motor Mounting";
                ChkPermZebra.IsChecked = false;
            }
            else if (CmbStationPreset.SelectedIndex == 3)
            {
                TxtStationId.Text = "LINE1_STATION_04";
                TxtStationName.Text = "ST-04: Electrical & Inverter PCB Wiring";
                ChkPermZebra.IsChecked = false;
            }
            else if (CmbStationPreset.SelectedIndex == 4)
            {
                TxtStationId.Text = "LINE1_STATION_05";
                TxtStationName.Text = "ST-05: Vacuum Decay & R32 Charge";
                ChkPermZebra.IsChecked = false;
            }
            else if (CmbStationPreset.SelectedIndex == 5)
            {
                TxtStationId.Text = "LINE1_STATION_06";
                TxtStationName.Text = "ST-06: Final QA Run-Test & Zebra ZT411";
                ChkPermZebra.IsChecked = true;
            }

            UpdateLiveTelemetryPreview();
        }

        private void OnIntervalChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbInterval.SelectedIndex == 0) _config.SyncIntervalSec = 1;
            else if (CmbInterval.SelectedIndex == 1) _config.SyncIntervalSec = 2;
            else if (CmbInterval.SelectedIndex == 2) _config.SyncIntervalSec = 3;
            else if (CmbInterval.SelectedIndex == 3) _config.SyncIntervalSec = 5;

            _telemetryTimer.Interval = TimeSpan.FromSeconds(_config.SyncIntervalSec);
            AppendLog($"[{DateTime.Now:HH:mm:ss}] [CONFIG] Broadcast interval set to {_config.SyncIntervalSec}s.");
        }

        private void OnPresetAll_Click(object sender, RoutedEventArgs e)
        {
            ChkPermCpu.IsChecked = true;
            ChkPermRam.IsChecked = true;
            ChkPermDisk.IsChecked = true;
            ChkPermUptime.IsChecked = true;
            ChkPermNetwork.IsChecked = true;
            ChkPermZebra.IsChecked = true;
        }

        private void OnPresetMinimal_Click(object sender, RoutedEventArgs e)
        {
            ChkPermCpu.IsChecked = false;
            ChkPermRam.IsChecked = false;
            ChkPermDisk.IsChecked = false;
            ChkPermUptime.IsChecked = true;
            ChkPermNetwork.IsChecked = true;
            ChkPermZebra.IsChecked = false;
        }

        private void OnPresetMaskHardware_Click(object sender, RoutedEventArgs e)
        {
            ChkPermCpu.IsChecked = false;
            ChkPermRam.IsChecked = false;
            ChkPermDisk.IsChecked = false;
            ChkPermUptime.IsChecked = false;
            ChkPermNetwork.IsChecked = true;
            ChkPermZebra.IsChecked = false;
        }

        private void OnSaveConfig_Click(object sender, RoutedEventArgs e)
        {
            SaveConfigToFile();
            AppendLog($"[{DateTime.Now:HH:mm:ss}] [SAVED] Preferences and permissions saved to config.json.");
            MessageBox.Show("Configuration & Permissions saved successfully to config.json!", "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void AppendLog(string message)
        {
            TxtActivityLog.AppendText(message + Environment.NewLine);
            TxtActivityLog.ScrollToEnd();
        }
    }
}
