using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LineNodeAgent.Models;

namespace LineNodeAgent.Services
{
    public class TelemetryWorker
    {
        private static readonly SocketsHttpHandler _handler = new()
        {
            PooledConnectionLifetime = TimeSpan.FromSeconds(10), // Auto-refresh connection to adapt dynamically to network interface disconnect/reconnect
            PooledConnectionIdleTimeout = TimeSpan.FromSeconds(4),
            ConnectTimeout = TimeSpan.FromSeconds(2.0),
            EnableMultipleHttp2Connections = false
        };

        private static readonly HttpClient _http = new(_handler, disposeHandler: false)
        {
            Timeout = Timeout.InfiniteTimeSpan
        };

        private readonly WmiTelemetryService _wmi;
        private CancellationTokenSource? _cts;
        private Task? _workerTask;
        private volatile TaskCompletionSource<bool> _wakeSignal = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public event Action<string, bool>? TransmissionEvent; // (statusMsg, isSuccess)
        public event Action<TelemetryPayload, string>? PayloadGenerated; // (payload, json)

        public bool IsRunning => _workerTask != null && !_workerTask.IsCompleted;
        public long PacketsSent { get; private set; }
        public double LastLatencyMs { get; private set; }

        public TelemetryWorker(WmiTelemetryService wmi)
        {
            _wmi = wmi;
        }

        public void WakeUp()
        {
            try
            {
                _wakeSignal.TrySetResult(true);
            }
            catch { }
        }

        public void Start(AgentSettings settings, IList<AgentFunctionItem> functions)
        {
            if (IsRunning) return;
            _cts = new CancellationTokenSource();
            _workerTask = Task.Run(() => WorkerLoopAsync(settings, functions, _cts.Token));
        }

        public void Stop()
        {
            _cts?.Cancel();
            WakeUp();
            try { _workerTask?.Wait(1000); } catch { }
            _workerTask = null;
        }

        public async Task SendOfflineStatusAsync(AgentSettings settings, string reason, string message)
        {
            try
            {
                var netInfo = _wmi.GetLocalNetworkInfo();
                var payload = new TelemetryPayload
                {
                    NodeId = settings.NodeName,
                    StationId = settings.LineStationId,
                    StationName = (settings.StationFriendlyName ?? string.Empty).Replace(" & Zebra ZT411 Labeling", " & Packaging").Replace(" & Zebra ZT411", "").Replace("Zebra ZT411", "").Trim(),
                    Hostname = Environment.MachineName,
                    Timestamp = DateTime.UtcNow,
                    Status = "OFFLINE",
                    OfflineReason = reason,
                    OfflineMessage = message,
                    IpAddress = netInfo.ip,
                    ConnectionType = netInfo.connType,
                    NetworkLatencyMs = 0.0
                };

                var json = JsonSerializer.Serialize(payload);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                using var msg = new HttpRequestMessage(HttpMethod.Post, settings.ServerUrl.TrimEnd('/') + "/api/telemetry") { Content = content };
                msg.Headers.Add("X-Line-Agent", "true");
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1.5));
                await _http.SendAsync(msg, cts.Token);
            }
            catch { }
        }

        public void SendOfflineStatusSync(AgentSettings settings, string reason, string message)
        {
            try
            {
                SendOfflineStatusAsync(settings, reason, message).Wait(1500);
            }
            catch { }
        }

        private async Task WorkerLoopAsync(AgentSettings settings, IList<AgentFunctionItem> functions, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var (payload, json) = CollectAndBuildPayload(settings, functions);
                    PayloadGenerated?.Invoke(payload, json);

                    var targetUrl = settings.ServerUrl.TrimEnd('/') + "/api/telemetry";
                    var sw = System.Diagnostics.Stopwatch.StartNew();

                    using var content = new StringContent(json, Encoding.UTF8, "application/json");
                    using var msg = new HttpRequestMessage(HttpMethod.Post, targetUrl) { Content = content };
                    msg.Headers.Add("X-Line-Agent", "true");

                    // Per-request timeout token (2.5s) linked to parent shutdown token
                    using var reqCts = CancellationTokenSource.CreateLinkedTokenSource(token);
                    reqCts.CancelAfter(TimeSpan.FromSeconds(2.5));

                    var resp = await _http.SendAsync(msg, reqCts.Token);
                    sw.Stop();

                    LastLatencyMs = sw.ElapsedMilliseconds;

                    if (resp.IsSuccessStatusCode)
                    {
                        var respBody = await resp.Content.ReadAsStringAsync(token);
                        if (respBody.Contains("\"deregistered\""))
                        {
                            TransmissionEvent?.Invoke($"[TX WARN] Station ID '{settings.LineStationId}' was removed in SCADA. Auto-restoring & re-registering...", false);
                            try
                            {
                                var restoreUrl = settings.ServerUrl.TrimEnd('/') + $"/api/stations/{Uri.EscapeDataString(settings.LineStationId)}/restore";
                                using var restoreMsg = new HttpRequestMessage(HttpMethod.Post, restoreUrl);
                                using var restoreCts = new CancellationTokenSource(TimeSpan.FromSeconds(2.0));
                                await _http.SendAsync(restoreMsg, restoreCts.Token);
                            }
                            catch { }
                        }
                        else
                        {
                            PacketsSent++;
                            TransmissionEvent?.Invoke($"[TX 200 OK] Packet #{PacketsSent} -> SCADA Central ({LastLatencyMs}ms)", true);
                        }
                    }
                    else
                    {
                        TransmissionEvent?.Invoke($"[TX WARN] Central Server returned HTTP {(int)resp.StatusCode}", false);
                    }
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    // Clean loop exit when agent is intentionally stopped or closed
                    break;
                }
                catch (OperationCanceledException)
                {
                    // 2.5s Request timeout due to disconnected network or server standby
                    // CRITICAL: DO NOT BREAK THE LOOP! Keep retrying seamlessly!
                    TransmissionEvent?.Invoke($"[TX RETRYING] Connection to {settings.ServerUrl} timed out. Auto-reconnecting...", false);
                }
                catch (Exception ex)
                {
                    // SocketException / Unreachable host / Network interface down
                    // CRITICAL: DO NOT BREAK THE LOOP! Keep retrying seamlessly!
                    TransmissionEvent?.Invoke($"[TX RETRYING] Connection to {settings.ServerUrl} failed: {ex.Message}", false);
                }

                try
                {
                    // Real-time polling delay with instant wake-up support on network reconnect
                    int delay = Math.Clamp(settings.PollingIntervalSeconds, 1, 2);
                    await Task.WhenAny(Task.Delay(TimeSpan.FromSeconds(delay), token), _wakeSignal.Task);
                    _wakeSignal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    break;
                }
                catch { }
            }
        }

        public (TelemetryPayload payload, string json) CollectAndBuildPayload(AgentSettings settings, IList<AgentFunctionItem> functions)
        {
            var funcMap = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            foreach (var f in functions)
            {
                funcMap[f.Key] = f.IsEnabled;
            }

            bool IsEnabled(string k) => funcMap.TryGetValue(k, out var b) && b;

            // Collect all raw hardware metrics
            double cpu = _wmi.GetCpuUsage();
            var ram = _wmi.GetRamUsage();
            var disk = _wmi.GetDiskSpace();
            double tempC = _wmi.GetCpuTemperature(cpu);
            var uptime = _wmi.GetSystemUptime();
            var ping = _wmi.GetNetworkPing(settings.ServerUrl);
            var user = _wmi.GetActiveUserSession();
            var procs = _wmi.GetCriticalProcesses();
            var services = _wmi.GetWindowsServices();
            var usb = _wmi.GetUsbPeripherals();
            var tempJunk = _wmi.GetTempAndPrefetchMetrics();
            var netInfo = _wmi.GetLocalNetworkInfo();

            // Update live previews in the function items
            foreach (var f in functions)
            {
                switch (f.Key)
                {
                    case "CPU_Usage": f.LiveValue = $"{cpu:F1}%"; break;
                    case "RAM_Usage": f.LiveValue = $"{ram.percent:F1}% ({ram.usedGb:F1}/{ram.totalGb:F1} GB)"; break;
                    case "Disk_Space": f.LiveValue = $"{disk.usedGb:F1}/{disk.totalGb:F1} GB ({disk.percent:F1}%)"; break;
                    case "CPU_Temperature": f.LiveValue = $"{tempC:F1} °C"; break;
                    case "System_Uptime": f.LiveValue = uptime.formatted; break;
                    case "Network_Ping": f.LiveValue = $"{ping.latencyMs:F1} ms ({ping.status})"; break;
                    case "Active_User_Session": f.LiveValue = $"{user.user} [{user.idleTime}]"; break;
                    case "Critical_Processes": f.LiveValue = procs.summary; break;
                    case "Windows_Services": f.LiveValue = services.summary; break;
                    case "USB_Peripherals": f.LiveValue = usb.summary; break;
                    case "Temp_Prefetch_Junk": f.LiveValue = tempJunk.summary; break;
                }
            }

            var payload = new TelemetryPayload
            {
                NodeId = settings.NodeName,
                StationId = settings.LineStationId,
                StationName = (settings.StationFriendlyName ?? string.Empty).Replace(" & Zebra ZT411 Labeling", " & Packaging").Replace(" & Zebra ZT411", "").Replace("Zebra ZT411", "").Trim(),
                Hostname = Environment.MachineName,
                Timestamp = DateTime.UtcNow,
                Status = (IsEnabled("CPU_Usage") && cpu > 90) ? "CRITICAL" : (IsEnabled("CPU_Usage") && cpu > 80) ? "WARNING" : "NORMAL",
                IpAddress = IsEnabled("Network_Ping") ? netInfo.ip : "127.0.0.1 (Masked)",
                ConnectionType = netInfo.connType,
                NetworkLatencyMs = IsEnabled("Network_Ping") ? ping.latencyMs : 0.0
            };

            // Build dictionary of ONLY enabled metrics
            var enabledMetrics = new Dictionary<string, object?>();
            var perms = new Dictionary<string, bool>();

            foreach (var f in functions)
            {
                perms[f.Key] = f.IsEnabled;
                if (f.IsEnabled)
                {
                    enabledMetrics[f.Key] = f.LiveValue;
                }
            }
            payload.EnabledMetrics = enabledMetrics;
            payload.Permissions = perms;

            // System Telemetry structure for SCADA Central Dashboard
            var sys = new SystemTelemetryDto();

            if (IsEnabled("CPU_Usage")) sys.CpuPercent = cpu;
            if (IsEnabled("RAM_Usage"))
            {
                sys.RamPercent = ram.percent;
                sys.RamUsedGb = ram.usedGb;
                sys.RamTotalGb = ram.totalGb;
            }
            if (IsEnabled("Disk_Space"))
            {
                sys.Disks.Add(new DiskInfoDto
                {
                    Drive = "C:",
                    Percent = disk.percent,
                    UsedGb = disk.usedGb,
                    TotalGb = disk.totalGb
                });
            }
            if (IsEnabled("CPU_Temperature")) sys.CpuTemperatureC = tempC;
            if (IsEnabled("System_Uptime"))
            {
                sys.UptimeFormatted = uptime.formatted;
                sys.UptimeHours = uptime.hours;
            }
            if (IsEnabled("Network_Ping")) sys.NetworkLatencyMs = ping.latencyMs;
            if (IsEnabled("Active_User_Session")) sys.ActiveUser = user.user;
            if (IsEnabled("Critical_Processes")) sys.CriticalProcessesCount = procs.count;
            if (IsEnabled("Windows_Services")) sys.SpoolerServiceStatus = services.spoolerStatus;
            if (IsEnabled("USB_Peripherals")) sys.UsbDeviceCount = usb.deviceCount;
            if (IsEnabled("Temp_Prefetch_Junk"))
            {
                sys.TempJunkFilesCount = tempJunk.totalFiles;
                sys.TempJunkSizeMb = tempJunk.totalSizeMb;
                sys.TempJunkSummary = tempJunk.summary;
            }

            payload.SystemTelemetry = sys;

            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
            return (payload, json);
        }
    }
}
