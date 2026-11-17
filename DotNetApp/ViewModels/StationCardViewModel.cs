using System;
using System.Linq;
using System.Windows.Input;
using System.Windows.Media;
using DotNetApp.Models;
using DotNetApp.Services;

namespace DotNetApp.ViewModels
{
    public class StationCardViewModel : ViewModelBase
    {
        private Station _model;

        public StationCardViewModel(Station model, Action<string>? onDelete = null)
        {
            _model = model;
            DeleteCommand = new RelayCommand(() => onDelete?.Invoke(StationId));
            UpdateBindings();
        }

        public Station Model => _model;

        public string StationId => _model.StationId;
        public string StationName => (_model.StationName ?? string.Empty)
            .Replace(" & Zebra ZT411 Labeling", " & Packaging")
            .Replace(" & Zebra ZT411", "")
            .Replace("Zebra ZT411", "")
            .Trim();
        public string Hostname => _model.Hostname;
        public string IpAddress => _model.IpAddress;
        public string ConnectionType => _model.ConnectionType;
        public string ConnectionIcon => _model.ConnectionType.Equals("WIFI", StringComparison.OrdinalIgnoreCase) ? "WIFI" : "LAN";
        public string ConnectionDetails => _model.ConnectionDetails;
        public double LatencyMs => _model.NetworkLatencyMs;
        public string Status => _model.Status;

        public ICommand DeleteCommand { get; }

        public void Update(Station updated)
        {
            _model = updated;
            UpdateBindings();
            OnPropertyChanged(string.Empty);
        }

        // Status & ISA-101 Visual Bindings
        public Brush StatusBorderBrush { get; private set; } = Brushes.Green;
        public Brush StatusLedBrush { get; private set; } = Brushes.LimeGreen;
        public string StatusText { get; private set; } = "RUNNING - NOMINAL";
        public Brush StatusBadgeBg { get; private set; } = Brushes.Transparent;
        public Brush StatusBadgeFg { get; private set; } = Brushes.Black;

        // Workstation Data Permission Governance Badge
        public string PermBadgeText { get; private set; } = "PERM: ALL";
        public Brush PermBadgeBg { get; private set; } = new SolidColorBrush(Color.FromArgb(40, 16, 185, 129));
        public Brush PermBadgeFg { get; private set; } = new SolidColorBrush(Color.FromRgb(16, 185, 129));

        // Workstation Network Diagnostics Pill (Image 2 - Live Link / Network Drop Indicator)
        public bool IsNetworkFailure { get; private set; }
        public Brush NetworkPillBg { get; private set; } = Brushes.Transparent;
        public Brush NetworkPillBorder { get; private set; } = new SolidColorBrush(Color.FromRgb(226, 232, 240));
        public string NetworkPillConnText { get; private set; } = "WIFI";
        public Brush NetworkPillConnFg { get; private set; } = new SolidColorBrush(Color.FromRgb(37, 99, 235));
        public Brush NetworkPillIpFg { get; private set; } = new SolidColorBrush(Color.FromRgb(22, 163, 74));
        public string NetworkPillLatencyText { get; private set; } = "0.5 ms";
        public Brush NetworkPillLatencyFg { get; private set; } = new SolidColorBrush(Color.FromRgb(217, 119, 6));

        // Troubleshooting & Danger Zone Indicators
        public bool HasTroubleshootingAdvice => !string.IsNullOrEmpty(TroubleshootingAdvice);
        public string TroubleshootingAdvice { get; private set; } = string.Empty;
        public string TroubleSeverity { get; private set; } = "NORMAL";
        public Brush TroubleBg { get; private set; } = Brushes.Transparent;
        public Brush TroubleBorder { get; private set; } = Brushes.Transparent;
        public Brush TroubleFg { get; private set; } = Brushes.Black;
        public Brush TroubleBadgeBg { get; private set; } = Brushes.Transparent;
        public Brush TroubleBadgeFg { get; private set; } = Brushes.White;
        public System.Collections.Generic.List<string> TroubleshootingSteps { get; private set; } = new();

        // Direct System Priority & Action Properties
        public bool IsHighPriority { get; private set; }
        public bool IsMediumPriority { get; private set; }
        public string PriorityLevelText { get; private set; } = "NORMAL";
        public string ActionRequiredText { get; private set; } = "NO ACTION";
        public Brush PriorityBadgeBg { get; private set; } = Brushes.Transparent;
        public Brush PriorityBadgeFg { get; private set; } = Brushes.White;
        public Brush ActionBadgeBg { get; private set; } = Brushes.Transparent;
        public Brush ActionBadgeFg { get; private set; } = Brushes.White;
        public Brush PriorityCardBorder { get; private set; } = Brushes.Transparent;
        public string ShortIssueText { get; private set; } = string.Empty;

        // Dynamic Function Visibility (Only enabled functions are shown!)
        public bool ShowCpu { get; private set; } = true;
        public bool ShowRam { get; private set; } = true;
        public bool ShowDisk { get; private set; } = true;
        public bool ShowUptime { get; private set; } = true;
        public bool ShowNetwork { get; private set; } = true;
        public bool ShowTemperature { get; private set; } = false;
        public bool ShowActiveUser { get; private set; } = false;
        public bool ShowCriticalProcesses { get; private set; } = false;
        public bool ShowWindowsServices { get; private set; } = false;
        public bool ShowUsbPeripherals { get; private set; } = false;
        public bool ShowTempJunk { get; private set; } = false;

        // Metric Display Texts & Values
        public double CpuPercent { get; set; }
        public string CpuDisplayText { get; private set; } = "0.0%";
        public Brush CpuBarBrush { get; private set; } = Brushes.DodgerBlue;

        public double RamPercent { get; set; }
        public string RamDisplayText { get; private set; } = "0.0%";
        public Brush RamBarBrush { get; private set; } = Brushes.DodgerBlue;

        public double DiskPercent { get; set; }
        public string DiskDisplayText { get; private set; } = "0 / 0 GB (0%)";

        public string UptimeText { get; private set; } = "0d 00:00:00";
        public Brush UptimeBrush { get; private set; } = Brushes.Black;
        public string TemperatureText { get; private set; } = "-- °C";
        public string ActiveUserText { get; private set; } = "--";
        public string CriticalProcessesText { get; private set; } = "--";
        public string WindowsServicesText { get; private set; } = "--";
        public string UsbPeripheralsText { get; private set; } = "--";
        public string TempJunkText { get; private set; } = "--";
        public Brush TempJunkBrush { get; private set; } = new SolidColorBrush(Color.FromRgb(249, 115, 22));

        private void UpdateBindings()
        {
            var tele = _model.SystemTelemetry ?? new SystemTelemetry();
            var perms = _model.Permissions ?? new StationPermissions();

            // Evaluate Enabled/Disabled Functions Dynamically
            ShowCpu = perms.IsFunctionEnabled("CPU_Usage") || perms.Cpu;
            ShowRam = perms.IsFunctionEnabled("RAM_Usage") || perms.Ram;
            ShowDisk = perms.IsFunctionEnabled("Disk_Space") || perms.Disk;
            ShowUptime = perms.IsFunctionEnabled("System_Uptime") || perms.Uptime;
            ShowNetwork = perms.IsFunctionEnabled("Network_Ping") || perms.Network;
            ShowTemperature = perms.IsFunctionEnabled("CPU_Temperature") && tele.CpuTemperatureC.HasValue;
            ShowActiveUser = perms.IsFunctionEnabled("Active_User_Session") && !string.IsNullOrWhiteSpace(tele.ActiveUser);
            ShowCriticalProcesses = perms.IsFunctionEnabled("Critical_Processes") && tele.CriticalProcessesCount.HasValue;
            ShowWindowsServices = perms.IsFunctionEnabled("Windows_Services") && !string.IsNullOrWhiteSpace(tele.SpoolerServiceStatus);
            ShowUsbPeripherals = perms.IsFunctionEnabled("USB_Peripherals") && tele.UsbDeviceCount.HasValue;
            ShowTempJunk = perms.IsFunctionEnabled("Temp_Prefetch_Junk") && tele.TempJunkFilesCount.HasValue;

            // Heartbeat & Connection Health Evaluation
            var elapsedSeconds = (DateTime.UtcNow - _model.LastSeen).TotalSeconds;
            bool isTimedOut = elapsedSeconds > 15.0;
            bool isExplicitOffline = _model.Status.Equals("OFFLINE", StringComparison.OrdinalIgnoreCase);

            // Differentiate Between Operator Shutdown / Agent Stop vs Network Failure
            if (isExplicitOffline || isTimedOut)
            {
                // Check if this was a clean shutdown or stopped agent
                if (isExplicitOffline && _model.OfflineReason.Equals("PC_SHUTDOWN", StringComparison.OrdinalIgnoreCase))
                {
                    IsNetworkFailure = false;
                    StatusBorderBrush = new SolidColorBrush(Color.FromRgb(148, 163, 184)); // Slate Gray #94a3b8
                    StatusLedBrush = new SolidColorBrush(Color.FromRgb(148, 163, 184));
                    StatusText = "STATION OFFLINE (PC SHUTDOWN)";
                    StatusBadgeBg = new SolidColorBrush(Color.FromArgb(40, 148, 163, 184));
                    StatusBadgeFg = new SolidColorBrush(Color.FromRgb(71, 85, 105));

                    NetworkPillBg = Brushes.Transparent;
                    NetworkPillBorder = new SolidColorBrush(Color.FromRgb(226, 232, 240));
                    NetworkPillConnText = $"{ConnectionType} • POWERED OFF";
                    NetworkPillConnFg = new SolidColorBrush(Color.FromRgb(100, 116, 139));
                    NetworkPillIpFg = new SolidColorBrush(Color.FromRgb(100, 116, 139));
                    NetworkPillLatencyText = "STANDBY";
                    NetworkPillLatencyFg = new SolidColorBrush(Color.FromRgb(100, 116, 139));
                }
                else if (isExplicitOffline && _model.OfflineReason.Equals("AGENT_STOPPED", StringComparison.OrdinalIgnoreCase))
                {
                    IsNetworkFailure = false;
                    StatusBorderBrush = new SolidColorBrush(Color.FromRgb(148, 163, 184)); // Slate Gray #94a3b8
                    StatusLedBrush = new SolidColorBrush(Color.FromRgb(148, 163, 184));
                    StatusText = "STATION OFFLINE (AGENT STOPPED)";
                    StatusBadgeBg = new SolidColorBrush(Color.FromArgb(40, 148, 163, 184));
                    StatusBadgeFg = new SolidColorBrush(Color.FromRgb(71, 85, 105));

                    NetworkPillBg = Brushes.Transparent;
                    NetworkPillBorder = new SolidColorBrush(Color.FromRgb(226, 232, 240));
                    NetworkPillConnText = $"{ConnectionType} • AGENT OFFLINE";
                    NetworkPillConnFg = new SolidColorBrush(Color.FromRgb(100, 116, 139));
                    NetworkPillIpFg = new SolidColorBrush(Color.FromRgb(100, 116, 139));
                    NetworkPillLatencyText = "STANDBY";
                    NetworkPillLatencyFg = new SolidColorBrush(Color.FromRgb(100, 116, 139));
                }
                else
                {
                    // Check if localhost or local workstation machine
                    bool isLocal = _model.IpAddress == "127.0.0.1" ||
                                   _model.IpAddress.StartsWith("127.") ||
                                   _model.IpAddress.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                                   _model.Hostname.Equals(Environment.MachineName, StringComparison.OrdinalIgnoreCase);

                    // For remote workstations, check if network ICMP ping is alive
                    bool isPingAlive = isLocal || StationPingService.IsHostReachable(_model.IpAddress);

                    if (isPingAlive)
                    {
                        // Network is alive! Switch & network cable are intact. Only the Line Node Agent is closed.
                        IsNetworkFailure = false;
                        StatusBorderBrush = new SolidColorBrush(Color.FromRgb(148, 163, 184)); // Slate Gray #94a3b8
                        StatusLedBrush = new SolidColorBrush(Color.FromRgb(148, 163, 184));
                        StatusText = "STATION OFFLINE (AGENT INACTIVE)";
                        StatusBadgeBg = new SolidColorBrush(Color.FromArgb(40, 148, 163, 184));
                        StatusBadgeFg = new SolidColorBrush(Color.FromRgb(71, 85, 105));

                        NetworkPillBg = Brushes.Transparent;
                        NetworkPillBorder = new SolidColorBrush(Color.FromRgb(226, 232, 240));
                        NetworkPillConnText = $"{ConnectionType} • PING OK";
                        NetworkPillConnFg = new SolidColorBrush(Color.FromRgb(37, 99, 235));
                        NetworkPillIpFg = new SolidColorBrush(Color.FromRgb(22, 163, 74));
                        double cachedMs = StationPingService.GetCachedLatency(_model.IpAddress);
                        NetworkPillLatencyText = $"{cachedMs:F1} ms";
                        NetworkPillLatencyFg = new SolidColorBrush(Color.FromRgb(217, 119, 6));
                    }
                    else
                    {
                        // True Network Failure / Cable Disconnected / Unreachable Workstation
                        IsNetworkFailure = true;
                        StatusBorderBrush = new SolidColorBrush(Color.FromRgb(220, 38, 38)); // Vibrant Alert Red
                        StatusLedBrush = new SolidColorBrush(Color.FromRgb(239, 68, 68));     // Alert Red LED
                        StatusText = $"DISCONNECTED • NETWORK FAILURE ({(int)elapsedSeconds}s)";
                        StatusBadgeBg = new SolidColorBrush(Color.FromArgb(40, 220, 38, 38));
                        StatusBadgeFg = new SolidColorBrush(Color.FromRgb(220, 38, 38));

                        NetworkPillBg = new SolidColorBrush(Color.FromRgb(254, 242, 242));       // #fef2f2 Soft Red
                        NetworkPillBorder = new SolidColorBrush(Color.FromRgb(248, 113, 113));   // #f87171 Red Border
                        NetworkPillConnText = $"{ConnectionType} • DROPPED";
                        NetworkPillConnFg = new SolidColorBrush(Color.FromRgb(220, 38, 38));     // #dc2626
                        NetworkPillIpFg = new SolidColorBrush(Color.FromRgb(153, 27, 27));       // #991b1b
                        NetworkPillLatencyText = $"PING FAIL (TIMEOUT {(int)elapsedSeconds}s)";
                        NetworkPillLatencyFg = new SolidColorBrush(Color.FromRgb(220, 38, 38));   // #dc2626
                    }
                }
            }
            else
            {
                IsNetworkFailure = false;
                NetworkPillBg = Brushes.Transparent;
                NetworkPillBorder = new SolidColorBrush(Color.FromRgb(226, 232, 240));   // #e2e8f0
                NetworkPillConnText = ConnectionType;
                NetworkPillConnFg = new SolidColorBrush(Color.FromRgb(37, 99, 235));      // #2563eb Blue
                NetworkPillIpFg = new SolidColorBrush(Color.FromRgb(22, 163, 74));       // #16a34a Green
                NetworkPillLatencyText = $"{LatencyMs:F1} ms";
                NetworkPillLatencyFg = new SolidColorBrush(Color.FromRgb(217, 119, 6));    // #d97706 Orange

                switch (_model.Status.ToUpperInvariant())
                {
                    case "CRITICAL":
                        StatusBorderBrush = new SolidColorBrush(Color.FromRgb(220, 38, 38));
                        StatusLedBrush = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                        StatusText = "STOPPAGE ALERT";
                        StatusBadgeBg = new SolidColorBrush(Color.FromArgb(50, 239, 68, 68));
                        StatusBadgeFg = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                        break;
                    case "WARNING":
                        StatusBorderBrush = new SolidColorBrush(Color.FromRgb(217, 119, 6));
                        StatusLedBrush = new SolidColorBrush(Color.FromRgb(245, 158, 11));
                        StatusText = "DEGRADED / HIGH LOAD";
                        StatusBadgeBg = new SolidColorBrush(Color.FromArgb(50, 245, 158, 11));
                        StatusBadgeFg = new SolidColorBrush(Color.FromRgb(245, 158, 11));
                        break;
                    default: // NORMAL
                        StatusBorderBrush = new SolidColorBrush(Color.FromRgb(22, 163, 74));
                        StatusLedBrush = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                        StatusText = "RUNNING - NOMINAL";
                        StatusBadgeBg = new SolidColorBrush(Color.FromArgb(40, 16, 185, 129));
                        StatusBadgeFg = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                        break;
                }
            }

            // Permission Governance Summary Badge
            int enabledCount = (ShowCpu ? 1 : 0) + (ShowRam ? 1 : 0) + (ShowDisk ? 1 : 0) +
                               (ShowUptime ? 1 : 0) + (ShowNetwork ? 1 : 0) +
                               (ShowTemperature ? 1 : 0) + (ShowActiveUser ? 1 : 0) +
                               (ShowCriticalProcesses ? 1 : 0) + (ShowWindowsServices ? 1 : 0) +
                               (ShowUsbPeripherals ? 1 : 0) + (ShowTempJunk ? 1 : 0);

            if (enabledCount >= 6)
            {
                PermBadgeText = $"PERM: {enabledCount} ACTIVE";
                PermBadgeBg = new SolidColorBrush(Color.FromArgb(40, 16, 185, 129));
                PermBadgeFg = new SolidColorBrush(Color.FromRgb(16, 185, 129));
            }
            else
            {
                PermBadgeText = $"PERM: {enabledCount} RESTRICTED";
                PermBadgeBg = new SolidColorBrush(Color.FromArgb(40, 245, 158, 11));
                PermBadgeFg = new SolidColorBrush(Color.FromRgb(245, 158, 11));
            }

            // CPU
            if (ShowCpu && tele.CpuPercent.HasValue)
            {
                CpuPercent = Math.Clamp(tele.CpuPercent.Value, 0, 100);
                CpuDisplayText = $"{CpuPercent:F1}%";
                CpuBarBrush = CpuPercent > 85 ? new SolidColorBrush(Color.FromRgb(220, 38, 38))
                            : CpuPercent > 70 ? new SolidColorBrush(Color.FromRgb(217, 119, 6))
                            : new SolidColorBrush(Color.FromRgb(37, 99, 235));
            }

            // RAM
            if (ShowRam && tele.RamPercent.HasValue)
            {
                RamPercent = Math.Clamp(tele.RamPercent.Value, 0, 100);
                RamDisplayText = $"{RamPercent:F1}% ({(tele.RamUsedGb ?? 0):F1}/{(tele.RamTotalGb ?? 16):F1} GB)";
                RamBarBrush = RamPercent > 88 ? new SolidColorBrush(Color.FromRgb(220, 38, 38))
                            : RamPercent > 75 ? new SolidColorBrush(Color.FromRgb(217, 119, 6))
                            : new SolidColorBrush(Color.FromRgb(37, 99, 235));
            }

            // Disk
            if (ShowDisk && tele.Disks != null && tele.Disks.Count > 0)
            {
                var mainDisk = tele.Disks[0];
                DiskPercent = Math.Clamp(mainDisk.Percent, 0, 100);
                DiskDisplayText = $"{mainDisk.UsedGb:F1} / {mainDisk.TotalGb:F1} GB ({DiskPercent:F1}%)";
            }

            // Uptime
            if (ShowUptime)
            {
                UptimeText = tele.UptimeFormatted ?? "0d 00:00:00";
            }

            // Temperature
            if (ShowTemperature && tele.CpuTemperatureC.HasValue)
            {
                TemperatureText = $"{tele.CpuTemperatureC.Value:F1} °C";
            }

            // Active User
            if (ShowActiveUser)
            {
                ActiveUserText = tele.ActiveUser ?? "--";
            }

            // Critical Processes
            if (ShowCriticalProcesses && tele.CriticalProcessesCount.HasValue)
            {
                CriticalProcessesText = $"{tele.CriticalProcessesCount.Value} Tasks Running";
            }

            // Windows Services
            if (ShowWindowsServices)
            {
                WindowsServicesText = $"Spooler: {tele.SpoolerServiceStatus ?? "OK"}";
            }

            // USB Peripherals
            if (ShowUsbPeripherals && tele.UsbDeviceCount.HasValue)
            {
                UsbPeripheralsText = $"{tele.UsbDeviceCount.Value} Devices Attached";
            }

            // Temp & Prefetch Junk Storage
            if (ShowTempJunk && tele.TempJunkFilesCount.HasValue)
            {
                int fc = tele.TempJunkFilesCount.Value;
                double mb = tele.TempJunkSizeMb ?? 0.0;
                string sz = mb >= 1024 ? $"{(mb / 1024.0):F2} GB" : $"{mb:F1} MB";
                TempJunkText = $"{fc:N0} Files ({sz})";

                TempJunkBrush = fc > 5000 || mb > 5000
                    ? new SolidColorBrush(Color.FromRgb(220, 38, 38))  // Red warning
                    : fc > 2000 || mb > 2000
                    ? new SolidColorBrush(Color.FromRgb(217, 119, 6))  // Amber warning
                    : new SolidColorBrush(Color.FromRgb(234, 88, 12));  // Crisp industrial orange
            }

            // ==============================================================
            // ACTIONABLE PRIORITY & ISSUE EVALUATION ENGINE
            // ==============================================================
            var issues = new System.Collections.Generic.List<string>();
            var steps = new System.Collections.Generic.List<string>();

            if (isTimedOut)
            {
                issues.Add($"Critical Network Failure: Workstation unreachable ({(int)elapsedSeconds}s)");
                steps.Add($"Check physical LAN Ethernet cable / Wi-Fi connection on {Hostname}");
                steps.Add($"Ensure LineNodeAgent.exe process is running and transmitting");
                steps.Add($"Test network ping from SCADA host to {IpAddress}");

                IsHighPriority = true;
                IsMediumPriority = false;
                PriorityLevelText = "HIGH PRIORITY";
                ActionRequiredText = "ACTION: RECONNECT PC";
                PriorityBadgeBg = new SolidColorBrush(Color.FromRgb(220, 38, 38));
                PriorityBadgeFg = Brushes.White;
                ActionBadgeBg = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                ActionBadgeFg = Brushes.White;
                PriorityCardBorder = new SolidColorBrush(Color.FromRgb(252, 165, 165));

                TroubleSeverity = "NETWORK FAILURE";
                TroubleBg = new SolidColorBrush(Color.FromRgb(254, 242, 242));       // #fef2f2 Soft Red
                TroubleBorder = new SolidColorBrush(Color.FromRgb(248, 113, 113));   // #f87171 Red Border
                TroubleFg = new SolidColorBrush(Color.FromRgb(153, 27, 27));         // #991b1b
                TroubleBadgeBg = new SolidColorBrush(Color.FromRgb(220, 38, 38));    // #dc2626
                TroubleBadgeFg = Brushes.White;
                TroubleshootingAdvice = $"⚠️ CRITICAL NETWORK FAILURE: Workstation connection dropped ({(int)elapsedSeconds}s). Check LAN cable & verify LineNodeAgent on {IpAddress}.";
            }
            else
            {
                // Check Temp & Prefetch Files
                int tempLimit = 250;
                bool hasTempOverload = ShowTempJunk && tele.TempJunkFilesCount.HasValue && tele.TempJunkFilesCount.Value >= tempLimit;
                if (hasTempOverload)
                {
                    issues.Add($"Temp Junk Overload ({tele.TempJunkFilesCount!.Value:N0} files > {tempLimit} limit)");
                    steps.Add("Delete temporary files (%temp%) or run Disk Cleanup");
                }

                // Check RAM Load
                bool hasRamCrit = ShowRam && tele.RamPercent.HasValue && tele.RamPercent.Value >= 88.0;
                bool hasRamWarn = ShowRam && tele.RamPercent.HasValue && tele.RamPercent.Value >= 75.0 && !hasRamCrit;
                if (hasRamCrit)
                {
                    issues.Add($"RAM Critical ({tele.RamPercent!.Value:F1}% load)");
                    steps.Add("Close heavy background processes in Task Manager");
                }
                else if (hasRamWarn)
                {
                    issues.Add($"RAM Warning ({tele.RamPercent!.Value:F1}% load)");
                    steps.Add("Monitor workstation memory usage");
                }

                // Check CPU Load
                bool hasCpuCrit = ShowCpu && tele.CpuPercent.HasValue && tele.CpuPercent.Value >= 85.0;
                bool hasCpuWarn = ShowCpu && tele.CpuPercent.HasValue && tele.CpuPercent.Value >= 70.0 && !hasCpuCrit;
                if (hasCpuCrit)
                {
                    issues.Add($"CPU Critical ({tele.CpuPercent!.Value:F1}% load)");
                    steps.Add("Check for runaway background processes");
                }
                else if (hasCpuWarn)
                {
                    issues.Add($"CPU Warning ({tele.CpuPercent!.Value:F1}% load)");
                    steps.Add("Monitor workstation CPU load");
                }

                // Check Host Uptime
                int uptimeLimit = 48;
                double upHours = tele.GetUptimeHours();
                bool hasUptimeOverload = ShowUptime && upHours >= uptimeLimit;
                if (hasUptimeOverload)
                {
                    issues.Add($"Host Continuous Uptime ({upHours:F0}h > {uptimeLimit}h limit)");
                    steps.Add("Schedule workstation reboot to clear accumulated system memory");
                    UptimeBrush = new SolidColorBrush(Color.FromRgb(217, 119, 6)); // Amber
                }
                else
                {
                    UptimeBrush = Brushes.Black;
                }

                if (issues.Count > 0)
                {
                    bool isHigh = hasRamCrit || hasCpuCrit || _model.Status.Equals("CRITICAL", StringComparison.OrdinalIgnoreCase);
                    bool isMed = !isHigh && (hasTempOverload || hasRamWarn || hasCpuWarn || hasUptimeOverload);

                    if (isHigh)
                    {
                        IsHighPriority = true;
                        IsMediumPriority = false;
                        PriorityLevelText = "HIGH PRIORITY";
                        ActionRequiredText = "ACTION: IMMEDIATE ATTENTION";
                        PriorityBadgeBg = new SolidColorBrush(Color.FromRgb(220, 38, 38));
                        PriorityBadgeFg = Brushes.White;
                        ActionBadgeBg = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                        ActionBadgeFg = Brushes.White;
                        PriorityCardBorder = new SolidColorBrush(Color.FromRgb(252, 165, 165));

                        TroubleSeverity = "DANGER ZONE";
                        TroubleBg = new SolidColorBrush(Color.FromRgb(254, 242, 242));
                        TroubleBorder = new SolidColorBrush(Color.FromRgb(252, 165, 165));
                        TroubleFg = new SolidColorBrush(Color.FromRgb(185, 28, 28));
                        TroubleBadgeBg = new SolidColorBrush(Color.FromRgb(220, 38, 38));
                        TroubleBadgeFg = Brushes.White;
                    }
                    else
                    {
                        IsHighPriority = false;
                        IsMediumPriority = true;
                        PriorityLevelText = "MEDIUM PRIORITY";
                        ActionRequiredText = "ACTION: ATTENTION NEEDED";
                        PriorityBadgeBg = new SolidColorBrush(Color.FromRgb(217, 119, 6));
                        PriorityBadgeFg = Brushes.White;
                        ActionBadgeBg = new SolidColorBrush(Color.FromRgb(245, 158, 11));
                        ActionBadgeFg = Brushes.White;
                        PriorityCardBorder = new SolidColorBrush(Color.FromRgb(253, 230, 138));

                        TroubleSeverity = "WARNING ZONE";
                        TroubleBg = new SolidColorBrush(Color.FromRgb(255, 251, 235));
                        TroubleBorder = new SolidColorBrush(Color.FromRgb(253, 230, 138));
                        TroubleFg = new SolidColorBrush(Color.FromRgb(180, 83, 9));
                        TroubleBadgeBg = new SolidColorBrush(Color.FromRgb(217, 119, 6));
                        TroubleBadgeFg = Brushes.White;
                    }
                }
                else
                {
                    IsHighPriority = false;
                    IsMediumPriority = false;
                    PriorityLevelText = "NORMAL";
                    ActionRequiredText = "NO ACTION";
                    PriorityBadgeBg = Brushes.Transparent;
                    PriorityBadgeFg = Brushes.Black;
                    ActionBadgeBg = Brushes.Transparent;
                    ActionBadgeFg = Brushes.Black;
                    PriorityCardBorder = Brushes.Transparent;

                    TroubleSeverity = "NORMAL";
                    TroubleBg = Brushes.Transparent;
                    TroubleBorder = Brushes.Transparent;
                    TroubleFg = Brushes.Black;
                    TroubleBadgeBg = Brushes.Transparent;
                    TroubleBadgeFg = Brushes.White;
                }
            }

            if (issues.Count > 0)
            {
                ShortIssueText = string.Join("  •  ", issues);
                TroubleshootingAdvice = ShortIssueText;
            }
            else
            {
                ShortIssueText = string.Empty;
                TroubleshootingAdvice = string.Empty;
            }
            TroubleshootingSteps = steps;
        }
    }
}
