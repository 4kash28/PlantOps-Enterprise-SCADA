using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using DotNetApp.Data;
using DotNetApp.Models;
using DotNetApp.Services;
using Microsoft.Win32;

namespace DotNetApp.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly DatabaseService _db;
        private readonly ScadaHttpServer _server;
        private readonly AudioAlertService _audio;
        private readonly DispatcherTimer _clockTimer;
        private readonly DispatcherTimer _watchdogTimer;

        private ThresholdSettings _thresholds;
        private NetworkProfile _networkProfile;
        private string _activeFilter = "SUMMARY";
        private string _logSearchQuery = "";
        private bool _isDarkTheme = false;
        private bool _isAudioChimeEnabled = true;
        private string _plantName = "Plant 01 (Chennai Works)";
        private string _divisionName = "Assembly Division";
        private string _lineName = "RAC Line 01";
        private readonly DateTime _startupTime = DateTime.UtcNow;

        public MainViewModel()
        {
            _db = new DatabaseService();
            _audio = new AudioAlertService();
            _thresholds = _db.GetThresholdSettings();
            _isDarkTheme = _thresholds.DarkTheme;
            _isAudioChimeEnabled = _thresholds.AlertChimeEnabled;
            _audio.IsEnabled = _isAudioChimeEnabled;

            Stations = new ObservableCollection<StationCardViewModel>();
            DowntimeLogs = new ObservableCollection<DowntimeLog>();
            FilteredDowntimeLogs = new ObservableCollection<DowntimeLog>();
            ActiveFilter = "SUMMARY";

            // Initialize Network Profile
            _networkProfile = DetectHostNetwork();

            // Commands
            ToggleThemeCommand = new RelayCommand(ToggleTheme);
            ToggleAudioCommand = new RelayCommand(ToggleAudio);
            SetFilterCommand = new RelayCommand<string>(SetFilter);
            ExportExcelCommand = new RelayCommand(ExportExcel);
            ExportPdfCommand = new RelayCommand(ExportPdf);
            ExportCsvCommand = new RelayCommand(ExportExcel);
            ClearAuditLogsCommand = new RelayCommand(ClearAuditLogs);
            SimulateTelemetryCommand = new RelayCommand(SimulateRacWorkstations);
            DeleteStationCommand = new RelayCommand<string>(OnDeleteStationRequested);

            // Load saved data
            LoadPersistedState();

            // Setup Embedded HTTP Server for real agent telemetry
            _server = new ScadaHttpServer(_db, _networkProfile.Port);
            _server.TelemetryReceived += OnAgentTelemetryReceived;
            _server.StationDeletedByClient += sid => Application.Current?.Dispatcher.Invoke(() => RemoveStation(sid));
            _server.Start();

            // Timers
            _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _clockTimer.Tick += (s, e) => UpdateClock();
            _clockTimer.Start();
            UpdateClock();

            _watchdogTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _watchdogTimer.Tick += (s, e) => WatchdogEvaluation();
            _watchdogTimer.Start();

            RefreshThemeColors();
        }

        #region Collections & Properties

        public ObservableCollection<StationCardViewModel> Stations { get; }
        public ObservableCollection<DowntimeLog> DowntimeLogs { get; }
        public ObservableCollection<DowntimeLog> FilteredDowntimeLogs { get; }

        public ObservableCollection<ThresholdBreachLog> ThresholdBreachLogs { get; } = new();
        public ObservableCollection<ThresholdBreachLog> FilteredThresholdBreachLogs { get; } = new();
        public ObservableCollection<StationBreachSummary> StationBreachSummaries { get; } = new();
        public ObservableCollection<StationBreachSummary> FilteredStationBreachSummaries { get; } = new();

        private readonly Dictionary<string, long> _activeThresholdBreaches = new();

        public ThresholdSettings Thresholds
        {
            get => _thresholds;
            set
            {
                SetProperty(ref _thresholds, value);
                _db.SaveThresholdSettings(value);
                IsAudioChimeEnabled = value.AlertChimeEnabled;
            }
        }

        public NetworkProfile NetworkProfile
        {
            get => _networkProfile;
            set => SetProperty(ref _networkProfile, value);
        }

        public string PlantName
        {
            get => _plantName;
            set
            {
                if (SetProperty(ref _plantName, value))
                {
                    _db.SaveSetting("plant_name", value);
                    OnPropertyChanged(nameof(PlantHierarchyText));
                }
            }
        }

        public string DivisionName
        {
            get => _divisionName;
            set
            {
                if (SetProperty(ref _divisionName, value))
                {
                    _db.SaveSetting("plant_division", value);
                    OnPropertyChanged(nameof(PlantHierarchyText));
                }
            }
        }

        public string LineName
        {
            get => _lineName;
            set
            {
                if (SetProperty(ref _lineName, value))
                {
                    _db.SaveSetting("plant_line", value);
                    OnPropertyChanged(nameof(PlantHierarchyText));
                }
            }
        }

        public string PlantHierarchyText => $"{PlantName} • {DivisionName} • {LineName}";

        public void UpdatePlantProfile(string plant, string division, string line)
        {
            _plantName = string.IsNullOrWhiteSpace(plant) ? "Plant 01 (Chennai Works)" : plant.Trim();
            _divisionName = string.IsNullOrWhiteSpace(division) ? "Assembly Division" : division.Trim();
            _lineName = string.IsNullOrWhiteSpace(line) ? "RAC Line 01" : line.Trim();

            _db.SaveSetting("plant_name", _plantName);
            _db.SaveSetting("plant_division", _divisionName);
            _db.SaveSetting("plant_line", _lineName);

            OnPropertyChanged(nameof(PlantName));
            OnPropertyChanged(nameof(DivisionName));
            OnPropertyChanged(nameof(LineName));
            OnPropertyChanged(nameof(PlantHierarchyText));
        }

        public string ActiveFilter
        {
            get => _activeFilter;
            set
            {
                if (SetProperty(ref _activeFilter, value))
                {
                    ApplyLogFilter();
                    OnPropertyChanged(nameof(IsFilterSummary));
                    OnPropertyChanged(nameof(IsFilterAll));
                    OnPropertyChanged(nameof(IsFilterActive));
                    OnPropertyChanged(nameof(IsFilterResolved));
                    OnPropertyChanged(nameof(IsViewSummary));
                    OnPropertyChanged(nameof(IsViewEvents));
                }
            }
        }

        public bool IsFilterSummary => ActiveFilter == "SUMMARY";
        public bool IsFilterAll => ActiveFilter == "ALL";
        public bool IsFilterActive => ActiveFilter == "ACTIVE";
        public bool IsFilterResolved => ActiveFilter == "RESOLVED" || ActiveFilter == "NORMALIZED";

        public bool IsViewSummary => ActiveFilter == "SUMMARY";
        public bool IsViewEvents => ActiveFilter != "SUMMARY";

        public bool IsDarkTheme
        {
            get => _isDarkTheme;
            set
            {
                if (SetProperty(ref _isDarkTheme, value))
                {
                    _thresholds.DarkTheme = value;
                    _db.SaveThresholdSettings(_thresholds);
                    RefreshThemeColors();
                }
            }
        }

        public AudioAlertService Audio => _audio;

        public bool IsAudioChimeEnabled
        {
            get => _isAudioChimeEnabled;
            set
            {
                if (SetProperty(ref _isAudioChimeEnabled, value))
                {
                    _thresholds.AlertChimeEnabled = value;
                    _audio.IsEnabled = value;
                    _db.SaveThresholdSettings(_thresholds);
                    OnPropertyChanged(nameof(AudioToggleLabel));
                    OnPropertyChanged(nameof(AudioIconBrush));
                    OnPropertyChanged(nameof(AudioTooltip));
                }
            }
        }

        public string AudioToggleLabel => IsAudioChimeEnabled ? "Chime ON" : "Chime OFF";
        public string ThemeToggleLabel => IsDarkTheme ? "Dark" : "Light";
        public Brush AudioIconBrush => IsAudioChimeEnabled 
            ? new SolidColorBrush(Color.FromRgb(220, 38, 38)) 
            : new SolidColorBrush(Color.FromRgb(148, 163, 184));
        public string AudioTooltip => IsAudioChimeEnabled 
            ? "🔊 Alarm Audio Siren: ACTIVE (Click to Mute)" 
            : "🔇 Alarm Audio Siren: MUTED (Click to Enable)";
        public string ThemeTooltip => IsDarkTheme 
            ? "SCADA Theme: DARK (Click to switch to Light)" 
            : "SCADA Theme: LIGHT (Click to switch to Dark)";

        // Shift Clock & Status Pill
        private string _liveClockText = DateTime.Now.ToString("hh:mm:ss tt");
        public string LiveClockText
        {
            get => _liveClockText;
            set => SetProperty(ref _liveClockText, value);
        }

        public string LiveDateTooltip => DateTime.Now.ToString("dddd, dd MMMM yyyy");

        public string ConnectionTypeBadge => _networkProfile.ConnectionType;
        public string ConnectionIcon => _networkProfile.ConnectionType == "WIFI" ? "WIFI" : "LAN";
        public string ConnectionDetails => _networkProfile.Details;
        public string HostIpAddress => _networkProfile.Ip;

        // KPI Bindings
        private double _lineAvailabilityPct = 100.0;
        public double LineAvailabilityPct
        {
            get => _lineAvailabilityPct;
            set => SetProperty(ref _lineAvailabilityPct, value);
        }

        private int _totalStations = 0;
        public int TotalStations
        {
            get => _totalStations;
            set => SetProperty(ref _totalStations, value);
        }

        private int _onlineStations = 0;
        public int OnlineStations
        {
            get => _onlineStations;
            set => SetProperty(ref _onlineStations, value);
        }

        private int _warningStations = 0;
        public int WarningStations
        {
            get => _warningStations;
            set => SetProperty(ref _warningStations, value);
        }

        private int _criticalStations = 0;
        public int CriticalStations
        {
            get => _criticalStations;
            set => SetProperty(ref _criticalStations, value);
        }

        private int _offlineStations = 0;
        public int OfflineStations
        {
            get => _offlineStations;
            set => SetProperty(ref _offlineStations, value);
        }

        // Live Line Fleet Systems & Network Health (Image 1 Header Requirement)
        public string CompactFleetStatusText
        {
            get
            {
                if (TotalStations == 0) return "No Nodes";
                if (OfflineStations == 0) return $"{TotalStations} Online";
                return $"{OfflineStations} Alert";
            }
        }

        public string FleetNetworkStatusText
        {
            get
            {
                if (TotalStations == 0) return "NO SYSTEMS MONITORED";
                if (OfflineStations == 0)
                {
                    return $"WORKSTATIONS: {TotalStations} / {TotalStations} ONLINE";
                }
                var firstOffline = Stations.FirstOrDefault(s => s.Status == "OFFLINE");
                string stLabel = firstOffline != null ? firstOffline.StationId : "PC";
                return $"⚠️ NETWORK DROP: {OfflineStations} OF {TotalStations} OFFLINE ({stLabel} DROP)";
            }
        }

        public string FleetNetworkTooltip
        {
            get
            {
                if (TotalStations == 0) return "No client workstations registered yet. Start LineNodeAgent on line PCs to auto-register.";
                if (OfflineStations == 0)
                {
                    return $"All {TotalStations} shop-floor workstation PCs are online and communicating with SCADA Central Server.";
                }
                var offlineNames = string.Join(", ", Stations.Where(s => s.Status == "OFFLINE").Select(s => $"{s.StationId} ({s.IpAddress})"));
                return $"CRITICAL NETWORK DROP: {OfflineStations} workstation(s) unreachable: {offlineNames}. Check physical Ethernet cables & ensure LineNodeAgent is running.";
            }
        }

        public Brush FleetNetworkBadgeBg
        {
            get
            {
                if (OfflineStations > 0) return new SolidColorBrush(Color.FromRgb(254, 242, 242)); // #fef2f2 Soft Red
                if (TotalStations == 0) return new SolidColorBrush(Color.FromRgb(241, 245, 249));  // #f1f5f9
                return new SolidColorBrush(Color.FromRgb(240, 253, 244));                         // #f0fdf4 Light Green
            }
        }

        public Brush FleetNetworkBadgeBorder
        {
            get
            {
                if (OfflineStations > 0) return new SolidColorBrush(Color.FromRgb(248, 113, 113)); // #f87171 Red Border
                if (TotalStations == 0) return new SolidColorBrush(Color.FromRgb(203, 213, 225));  // #cbd5e1
                return new SolidColorBrush(Color.FromRgb(187, 247, 208));                         // #bbf7d0 Green Border
            }
        }

        public Brush FleetNetworkBadgeFg
        {
            get
            {
                if (OfflineStations > 0) return new SolidColorBrush(Color.FromRgb(220, 38, 38));   // #dc2626 Red
                if (TotalStations == 0) return new SolidColorBrush(Color.FromRgb(100, 116, 139));  // #64748b
                return new SolidColorBrush(Color.FromRgb(21, 128, 61));                           // #15803d Green
            }
        }

        public Brush FleetNetworkLedColor
        {
            get
            {
                if (OfflineStations > 0) return new SolidColorBrush(Color.FromRgb(239, 68, 68));   // #ef4444 Alert Red
                if (TotalStations == 0) return new SolidColorBrush(Color.FromRgb(148, 163, 184));  // #94a3b8
                return new SolidColorBrush(Color.FromRgb(16, 185, 129));                          // #10b981 Green
            }
        }

        public bool HasCriticalAlert => CriticalStations > 0;
        public string CriticalAlertMessage => $"LINE ALERT: {CriticalStations} WORKSTATION(S) EXPERIENCING STOPPAGE / CRITICAL ERROR!";

        public bool HasTroubleshootingRequired => Stations.Any(s => s.HasTroubleshootingAdvice);
        public bool IsAllSystemsNominal => Stations.Count > 0 && !HasTroubleshootingRequired;
        public IEnumerable<StationCardViewModel> ActionRequiredStations => Stations.Where(s => s.HasTroubleshootingAdvice).ToList();
        public int ActionRequiredCount => Stations.Count(s => s.HasTroubleshootingAdvice);
        public int HighPriorityCount => Stations.Count(s => s.IsHighPriority);
        public int MediumPriorityCount => Stations.Count(s => s.IsMediumPriority);
        public int NominalCount => Stations.Count(s => !s.HasTroubleshootingAdvice);
        public Brush ActionRequiredBadgeBg => ActionRequiredCount > 0 
            ? new SolidColorBrush(Color.FromRgb(220, 38, 38)) 
            : new SolidColorBrush(Color.FromRgb(22, 163, 74));
        public string TroubleshootingHeaderTitle => $"{ActionRequiredStations.Count()} Workstation(s) Needing Priority Action";
        public string TroubleshootingStationsBadgeText => $"{ActionRequiredStations.Count()} ACTION REQUIRED";

        public int AuditLogCount => ThresholdBreachLogs.Count;
        public int ResolvedIncidentsCount => ThresholdBreachLogs.Count(l => l.Status == "NORMALIZED");
        public int ActiveStoppagesLogCount => ThresholdBreachLogs.Count(l => l.Status == "ACTIVE");
        public int BreachedStationsCount => StationBreachSummaries.Count(s => s.TotalBreachCount > 0);

        public string LogSearchQuery
        {
            get => _logSearchQuery;
            set
            {
                if (SetProperty(ref _logSearchQuery, value))
                {
                    ApplyLogFilter();
                }
            }
        }

        #endregion

        #region Theme Brushes

        public Brush CanvasBg { get; private set; } = new SolidColorBrush(Color.FromRgb(241, 245, 249));
        public Brush SurfaceBg { get; private set; } = Brushes.White;
        public Brush CardBorderBrush { get; private set; } = new SolidColorBrush(Color.FromRgb(226, 232, 240));
        public Brush TextMain { get; private set; } = new SolidColorBrush(Color.FromRgb(15, 23, 42));
        public Brush TextMuted { get; private set; } = new SolidColorBrush(Color.FromRgb(100, 116, 139));
        public Brush SubHeaderBg { get; private set; } = Brushes.White;
        public Brush SubHeaderBorder { get; private set; } = new SolidColorBrush(Color.FromRgb(226, 232, 240));

        // Header and Status Bar Dynamic Theming
        public Brush HeaderBg { get; private set; } = Brushes.White;
        public Brush HeaderBorder { get; private set; } = new SolidColorBrush(Color.FromRgb(226, 232, 240));
        public Brush HeaderFg { get; private set; } = new SolidColorBrush(Color.FromRgb(15, 23, 42));
        public Brush HeaderSubFg { get; private set; } = new SolidColorBrush(Color.FromRgb(100, 116, 139));
        public Brush HeaderBtnBg { get; private set; } = new SolidColorBrush(Color.FromRgb(241, 245, 249));
        public Brush HeaderBtnFg { get; private set; } = new SolidColorBrush(Color.FromRgb(51, 65, 85));
        public Brush HeaderBtnBorder { get; private set; } = new SolidColorBrush(Color.FromRgb(203, 213, 225));
        public Brush CenterPillBg { get; private set; } = new SolidColorBrush(Color.FromRgb(248, 250, 252));
        public Brush CenterPillBorder { get; private set; } = new SolidColorBrush(Color.FromRgb(203, 213, 225));
        public Brush HeaderBrandBadgeBg { get; private set; } = new SolidColorBrush(Color.FromRgb(219, 234, 254));
        public Brush HeaderBrandBadgeFg { get; private set; } = new SolidColorBrush(Color.FromRgb(29, 78, 216));
        public Brush HeaderBrandIconBg { get; private set; } = new SolidColorBrush(Color.FromRgb(239, 246, 255));
        public Brush HeaderBrandIconBorder { get; private set; } = new SolidColorBrush(Color.FromRgb(191, 219, 254));

        private void RefreshThemeColors()
        {
            if (IsDarkTheme)
            {
                CanvasBg = new SolidColorBrush(Color.FromRgb(9, 13, 22));
                SurfaceBg = new SolidColorBrush(Color.FromRgb(18, 26, 44));
                CardBorderBrush = new SolidColorBrush(Color.FromRgb(32, 45, 69));
                TextMain = new SolidColorBrush(Color.FromRgb(248, 250, 252));
                TextMuted = new SolidColorBrush(Color.FromRgb(148, 163, 184));
                SubHeaderBg = new SolidColorBrush(Color.FromRgb(11, 16, 28));
                SubHeaderBorder = new SolidColorBrush(Color.FromRgb(26, 37, 58));

                HeaderBg = new SolidColorBrush(Color.FromRgb(15, 23, 42));       // #0f172a
                HeaderBorder = new SolidColorBrush(Color.FromRgb(30, 41, 59));   // #1e293b
                HeaderFg = Brushes.White;
                HeaderSubFg = new SolidColorBrush(Color.FromRgb(148, 163, 184)); // #94a3b8
                HeaderBtnBg = new SolidColorBrush(Color.FromRgb(30, 41, 59));   // #1e293b
                HeaderBtnFg = new SolidColorBrush(Color.FromRgb(226, 232, 240)); // #e2e8f0
                HeaderBtnBorder = new SolidColorBrush(Color.FromRgb(51, 65, 85)); // #334155
                CenterPillBg = new SolidColorBrush(Color.FromRgb(9, 13, 22));     // #090d16
                CenterPillBorder = new SolidColorBrush(Color.FromRgb(51, 65, 85)); // #334155
                HeaderBrandBadgeBg = new SolidColorBrush(Color.FromRgb(30, 58, 138));
                HeaderBrandBadgeFg = new SolidColorBrush(Color.FromRgb(147, 197, 253));
                HeaderBrandIconBg = new SolidColorBrush(Color.FromRgb(30, 58, 138));
                HeaderBrandIconBorder = new SolidColorBrush(Color.FromRgb(59, 130, 246));
            }
            else
            {
                CanvasBg = new SolidColorBrush(Color.FromRgb(241, 245, 249));
                SurfaceBg = Brushes.White;
                CardBorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240));
                TextMain = new SolidColorBrush(Color.FromRgb(15, 23, 42));
                TextMuted = new SolidColorBrush(Color.FromRgb(100, 116, 139));
                SubHeaderBg = Brushes.White;
                SubHeaderBorder = new SolidColorBrush(Color.FromRgb(226, 232, 240));

                HeaderBg = Brushes.White;
                HeaderBorder = new SolidColorBrush(Color.FromRgb(226, 232, 240)); // #e2e8f0
                HeaderFg = new SolidColorBrush(Color.FromRgb(15, 23, 42));        // #0f172a
                HeaderSubFg = new SolidColorBrush(Color.FromRgb(100, 116, 139));  // #64748b
                HeaderBtnBg = new SolidColorBrush(Color.FromRgb(241, 245, 249));  // #f1f5f9
                HeaderBtnFg = new SolidColorBrush(Color.FromRgb(51, 65, 85));     // #334155
                HeaderBtnBorder = new SolidColorBrush(Color.FromRgb(203, 213, 225)); // #cbd5e1
                CenterPillBg = new SolidColorBrush(Color.FromRgb(248, 250, 252)); // #f8fafc
                CenterPillBorder = new SolidColorBrush(Color.FromRgb(203, 213, 225)); // #cbd5e1
                HeaderBrandBadgeBg = new SolidColorBrush(Color.FromRgb(219, 234, 254)); // #dbeafe
                HeaderBrandBadgeFg = new SolidColorBrush(Color.FromRgb(29, 78, 216));   // #1d4ed8
                HeaderBrandIconBg = new SolidColorBrush(Color.FromRgb(239, 246, 255));  // #eff6ff
                HeaderBrandIconBorder = new SolidColorBrush(Color.FromRgb(191, 219, 254)); // #bfdbfe
            }

            OnPropertyChanged(nameof(CanvasBg));
            OnPropertyChanged(nameof(SurfaceBg));
            OnPropertyChanged(nameof(CardBorderBrush));
            OnPropertyChanged(nameof(TextMain));
            OnPropertyChanged(nameof(TextMuted));
            OnPropertyChanged(nameof(SubHeaderBg));
            OnPropertyChanged(nameof(SubHeaderBorder));
            OnPropertyChanged(nameof(HeaderBg));
            OnPropertyChanged(nameof(HeaderBorder));
            OnPropertyChanged(nameof(HeaderFg));
            OnPropertyChanged(nameof(HeaderSubFg));
            OnPropertyChanged(nameof(HeaderBtnBg));
            OnPropertyChanged(nameof(HeaderBtnFg));
            OnPropertyChanged(nameof(HeaderBtnBorder));
            OnPropertyChanged(nameof(CenterPillBg));
            OnPropertyChanged(nameof(CenterPillBorder));
            OnPropertyChanged(nameof(HeaderBrandBadgeBg));
            OnPropertyChanged(nameof(HeaderBrandBadgeFg));
            OnPropertyChanged(nameof(HeaderBrandIconBg));
            OnPropertyChanged(nameof(HeaderBrandIconBorder));
            OnPropertyChanged(nameof(ThemeToggleLabel));
            OnPropertyChanged(nameof(ThemeTooltip));
        }

        #endregion

        #region Commands Implementation

        public ICommand ToggleThemeCommand { get; }
        public ICommand ToggleAudioCommand { get; }
        public ICommand SetFilterCommand { get; }
        public ICommand ExportExcelCommand { get; }
        public ICommand ExportPdfCommand { get; }
        public ICommand ExportCsvCommand { get; }
        public ICommand ClearAuditLogsCommand { get; }
        public ICommand SimulateTelemetryCommand { get; }
        public ICommand DeleteStationCommand { get; }

        public event Action? RequestScrollToAuditLog;

        public void TriggerScrollToAuditLog()
        {
            RequestScrollToAuditLog?.Invoke();
        }

        private void ToggleTheme()
        {
            IsDarkTheme = !IsDarkTheme;
        }

        private void ToggleAudio()
        {
            IsAudioChimeEnabled = !IsAudioChimeEnabled;
            if (IsAudioChimeEnabled)
            {
                _audio.PlayTestSound();
            }
        }

        private void SetFilter(string? filter)
        {
            if (!string.IsNullOrEmpty(filter))
            {
                ActiveFilter = filter.ToUpperInvariant();
                ApplyLogFilter();
            }
        }

        private void ExportExcel()
        {
            try
            {
                var sfd = new SaveFileDialog
                {
                    Filter = "Excel Spreadsheet (*.xls)|*.xls|CSV Comma Separated (*.csv)|*.csv",
                    FilterIndex = 1,
                    DefaultExt = ".xls",
                    FileName = $"PlantOps_Threshold_Breach_Audit_Log_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xls"
                };

                if (sfd.ShowDialog() == true)
                {
                    CsvExportService.SaveThresholdBreachReportToFile(ThresholdBreachLogs, StationBreachSummaries, sfd.FileName);
                    var res = MessageBox.Show(
                        $"Executive Audit Report exported successfully!\n\nSaved to:\n{sfd.FileName}\n\nWould you like to open it in Microsoft Excel now?",
                        "Export Completed",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information);

                    if (res == MessageBoxResult.Yes)
                    {
                        try
                        {
                            Process.Start(new ProcessStartInfo(sfd.FileName) { UseShellExecute = true });
                        }
                        catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export Excel report: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportPdf()
        {
            try
            {
                var sfd = new SaveFileDialog
                {
                    Filter = "Official PDF Document (*.pdf)|*.pdf",
                    FilterIndex = 1,
                    DefaultExt = ".pdf",
                    FileName = $"PlantOps_Audit_Report_{DateTime.UtcNow:yyyyMMdd_HHmmss}.pdf"
                };

                if (sfd.ShowDialog() == true)
                {
                    CsvExportService.GenerateProfessionalPdfReport(ThresholdBreachLogs, StationBreachSummaries, sfd.FileName);
                    var res = MessageBox.Show(
                        $"Certified 21 CFR Part 11 PDF Audit Report generated successfully!\n\nSaved to:\n{sfd.FileName}\n\nWould you like to open the PDF report now?",
                        "PDF Export Completed",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information);

                    if (res == MessageBoxResult.Yes)
                    {
                        try
                        {
                            Process.Start(new ProcessStartInfo(sfd.FileName) { UseShellExecute = true });
                        }
                        catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to generate PDF report: {ex.Message}", "PDF Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportCsv()
        {
            ExportExcel();
        }

        public void ClearAuditLogs()
        {
            var result = MessageBox.Show(
                "Are you sure you want to clear all threshold breach audit logs and reset the counter to 0?\n\nThis will purge all violation records from the database.",
                "Confirm Clear Audit Log",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                _db.ClearAllAuditLogs();
                _activeThresholdBreaches.Clear();
                ReloadDowntimeLogs();
                MessageBox.Show(
                    "Threshold breach audit logs have been successfully cleared and reset to 0.",
                    "Audit Logs Reset",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        public void OnDeleteStationRequested(string? stationId)
        {
            if (string.IsNullOrEmpty(stationId)) return;

            var result = MessageBox.Show(
                $"Are you sure you want to remove workstation node '{stationId}' from the SCADA monitoring system?",
                "Confirm Workstation Removal",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.OK)
            {
                RemoveStation(stationId);
            }
        }

        public void RemoveStation(string stationId)
        {
            var card = Stations.FirstOrDefault(s => s.StationId.Equals(stationId, StringComparison.OrdinalIgnoreCase));
            if (card != null)
            {
                if (card.Model.ActiveIncidentId.HasValue)
                {
                    _db.CloseDowntime(card.Model.ActiveIncidentId.Value);
                }
                Stations.Remove(card);
            }

            _db.DeleteStation(stationId);

            // Clean active threshold breaches dictionary for this station
            var keysToRemove = _activeThresholdBreaches.Keys
                .Where(k => k.StartsWith(stationId + "_", StringComparison.OrdinalIgnoreCase))
                .ToList();
            foreach (var k in keysToRemove)
            {
                _activeThresholdBreaches.Remove(k);
            }

            ReloadDowntimeLogs();
            ComputeKPIs();
        }

        #endregion

        #region Telemetry & Watchdog Engine

        private void OnAgentTelemetryReceived(Station incoming)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                ProcessIncomingStation(incoming);
            });
        }

        public void ProcessIncomingStation(Station incoming)
        {
            var deletedSet = _db.GetDeletedStations();
            if (deletedSet.Contains(incoming.StationId)) return;

            incoming.LastSeen = DateTime.UtcNow;

            // Evaluate Status based on Thresholds (only if not an intentional OFFLINE signal)
            if (!incoming.Status.Equals("OFFLINE", StringComparison.OrdinalIgnoreCase))
            {
                EvaluateStationHealth(incoming);

                // Auto-close active heartbeat breach immediately on packet arrival
                string hbKey = $"{incoming.StationId}_HEARTBEAT";
                if (_activeThresholdBreaches.TryGetValue(hbKey, out var bid))
                {
                    _db.CloseThresholdBreach(bid);
                    _activeThresholdBreaches.Remove(hbKey);
                    ReloadDowntimeLogs();
                }
            }

            var existing = Stations.FirstOrDefault(s => s.StationId.Equals(incoming.StationId, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                existing.Update(incoming);
            }
            else
            {
                var vm = new StationCardViewModel(incoming, OnDeleteStationRequested);
                Stations.Add(vm);
            }

            _db.UpsertStation(incoming);
            ComputeKPIs();
        }

        private void EvaluateStationHealth(Station s)
        {
            if (s.Status.Equals("OFFLINE", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var tele = s.SystemTelemetry ?? new SystemTelemetry();
            var perms = s.Permissions ?? new StationPermissions();

            bool isCritical = false;

            // 1. RAM Physical Commit Threshold Evaluation
            string ramKey = $"{s.StationId}_RAM";
            bool isRamCritical = perms.Ram && tele.RamPercent.HasValue && tele.RamPercent.Value >= _thresholds.RamCriticalPct;
            if (isRamCritical)
            {
                isCritical = true;
                if (!_activeThresholdBreaches.ContainsKey(ramKey))
                {
                    _activeThresholdBreaches[ramKey] = _db.InsertThresholdBreach(
                        s.StationId, s.StationName, "RAM Physical Commit", "CRITICAL",
                        $"{tele.RamPercent.GetValueOrDefault():F1}%", $"{_thresholds.RamCriticalPct:F1}% Max");
                    ReloadDowntimeLogs();
                    if (_isAudioChimeEnabled) _audio.TriggerChime();
                }
            }
            else
            {
                if (_activeThresholdBreaches.TryGetValue(ramKey, out var bid))
                {
                    _db.CloseThresholdBreach(bid);
                    _activeThresholdBreaches.Remove(ramKey);
                    ReloadDowntimeLogs();
                }
            }

            // 2. CPU Workload Threshold Evaluation
            string cpuKey = $"{s.StationId}_CPU";
            bool isCpuCritical = perms.Cpu && tele.CpuPercent.HasValue && tele.CpuPercent.Value >= _thresholds.CpuCriticalPct;
            if (isCpuCritical)
            {
                isCritical = true;
                if (!_activeThresholdBreaches.ContainsKey(cpuKey))
                {
                    _activeThresholdBreaches[cpuKey] = _db.InsertThresholdBreach(
                        s.StationId, s.StationName, "CPU Workload", "CRITICAL",
                        $"{tele.CpuPercent.GetValueOrDefault():F1}%", $"{_thresholds.CpuCriticalPct:F1}% Max");
                    ReloadDowntimeLogs();
                    if (_isAudioChimeEnabled) _audio.TriggerChime();
                }
            }
            else
            {
                if (_activeThresholdBreaches.TryGetValue(cpuKey, out var bid))
                {
                    _db.CloseThresholdBreach(bid);
                    _activeThresholdBreaches.Remove(cpuKey);
                    ReloadDowntimeLogs();
                }
            }

            // 3. Temp & Prefetch Files Threshold Evaluation
            string tempKey = $"{s.StationId}_TEMP";
            int tempLimit = _thresholds.TempFilesThresholdCount > 0 ? _thresholds.TempFilesThresholdCount : 250;
            bool isTempCritical = tele.TempJunkFilesCount.HasValue && tele.TempJunkFilesCount.Value >= tempLimit;
            if (isTempCritical)
            {
                isCritical = true;
                if (!_activeThresholdBreaches.ContainsKey(tempKey))
                {
                    string recordedVal = tele.TempJunkSizeMb.HasValue
                        ? $"{tele.TempJunkFilesCount.GetValueOrDefault()} Files ({tele.TempJunkSizeMb.GetValueOrDefault():F1} MB)"
                        : $"{tele.TempJunkFilesCount.GetValueOrDefault()} Files";
                    _activeThresholdBreaches[tempKey] = _db.InsertThresholdBreach(
                        s.StationId, s.StationName, "Temp & Prefetch Files", "CRITICAL",
                        recordedVal, $"{tempLimit} Files Max");
                    ReloadDowntimeLogs();
                    if (_isAudioChimeEnabled) _audio.TriggerChime();
                }
            }
            else
            {
                if (_activeThresholdBreaches.TryGetValue(tempKey, out var bid))
                {
                    _db.CloseThresholdBreach(bid);
                    _activeThresholdBreaches.Remove(tempKey);
                    ReloadDowntimeLogs();
                }
            }

            // 4. Host Continuous Uptime Threshold Evaluation
            string uptimeKey = $"{s.StationId}_UPTIME";
            int uptimeLimit = _thresholds.UptimeThresholdHours > 0 ? _thresholds.UptimeThresholdHours : 48;
            double actualUptimeHours = tele.GetUptimeHours();
            bool isUptimeWarning = perms.Uptime && actualUptimeHours >= uptimeLimit;
            if (isUptimeWarning)
            {
                if (!_activeThresholdBreaches.ContainsKey(uptimeKey))
                {
                    _activeThresholdBreaches[uptimeKey] = _db.InsertThresholdBreach(
                        s.StationId, s.StationName, "Host Continuous Uptime", "WARNING",
                        $"{actualUptimeHours:F1}h Continuous ({tele.UptimeFormatted})", $"{uptimeLimit}h Max Limit");
                    ReloadDowntimeLogs();
                    if (_isAudioChimeEnabled) _audio.TriggerChime();
                }
            }
            else
            {
                if (_activeThresholdBreaches.TryGetValue(uptimeKey, out var bid))
                {
                    _db.CloseThresholdBreach(bid);
                    _activeThresholdBreaches.Remove(uptimeKey);
                    ReloadDowntimeLogs();
                }
            }

            if (isCritical)
            {
                s.Status = "CRITICAL";
            }
            else
            {
                bool isWarning = (perms.Cpu && tele.CpuPercent.HasValue && tele.CpuPercent.Value >= _thresholds.CpuWarningPct) ||
                                 (perms.Ram && tele.RamPercent.HasValue && tele.RamPercent.Value >= _thresholds.RamWarningPct) ||
                                 isUptimeWarning;

                s.Status = isWarning ? "WARNING" : "NORMAL";
            }
        }

        private void WatchdogEvaluation()
        {
            var now = DateTime.UtcNow;
            if ((now - _startupTime).TotalSeconds < 15)
            {
                return; // Grace period on application startup to allow active line agents to transmit first packet
            }

            foreach (var card in Stations)
            {
                var st = card.Model;
                var secSinceSeen = (now - st.LastSeen).TotalSeconds;
                string hbKey = $"{st.StationId}_HEARTBEAT";

                if (secSinceSeen > _thresholds.HeartbeatTimeoutSec)
                {
                    if (st.Status != "OFFLINE")
                    {
                        st.Status = "OFFLINE";
                        if (!_activeThresholdBreaches.ContainsKey(hbKey))
                        {
                            bool isIntentional = st.OfflineReason is "PC_SHUTDOWN" or "AGENT_STOPPED" ||
                                                 StationPingService.IsHostReachable(st.IpAddress);

                            string breachTitle = st.OfflineReason == "PC_SHUTDOWN" ? "Station PC Shutdown" :
                                                 st.OfflineReason == "AGENT_STOPPED" ? "Line Agent Stopped" : "Station Heartbeat Lost";
                            string severity = isIntentional ? "WARNING" : "CRITICAL";

                            _activeThresholdBreaches[hbKey] = _db.InsertThresholdBreach(
                                st.StationId, st.StationName, breachTitle, severity,
                                $"{secSinceSeen:F0}s Offline", $"{_thresholds.HeartbeatTimeoutSec}s Timeout");
                            ReloadDowntimeLogs();
                            if (_isAudioChimeEnabled && !isIntentional) _audio.TriggerChime();
                        }
                    }
                    card.Update(st);
                }
                else if (_activeThresholdBreaches.ContainsKey(hbKey))
                {
                    if (_activeThresholdBreaches.TryGetValue(hbKey, out var bid))
                    {
                        _db.CloseThresholdBreach(bid);
                        _activeThresholdBreaches.Remove(hbKey);
                        ReloadDowntimeLogs();
                    }
                    card.Update(st);
                }
            }

            ComputeKPIs();
        }

        private void ComputeKPIs()
        {
            TotalStations = Stations.Count;
            OnlineStations = Stations.Count(s => s.Status is "NORMAL" or "WARNING");
            WarningStations = Stations.Count(s => s.Status == "WARNING");
            CriticalStations = Stations.Count(s => s.Status == "CRITICAL");
            OfflineStations = Stations.Count(s => s.Status == "OFFLINE");

            if (TotalStations > 0)
            {
                LineAvailabilityPct = Math.Round(Math.Max(0.0, ((double)(TotalStations - CriticalStations) / TotalStations) * 100.0), 1);
            }
            else
            {
                LineAvailabilityPct = 100.0;
            }

            OnPropertyChanged(nameof(HasCriticalAlert));
            OnPropertyChanged(nameof(CriticalAlertMessage));
            OnPropertyChanged(nameof(AuditLogCount));
            OnPropertyChanged(nameof(HasTroubleshootingRequired));
            OnPropertyChanged(nameof(IsAllSystemsNominal));
            OnPropertyChanged(nameof(ActionRequiredStations));
            OnPropertyChanged(nameof(TroubleshootingHeaderTitle));
            OnPropertyChanged(nameof(TroubleshootingStationsBadgeText));
            OnPropertyChanged(nameof(ActionRequiredCount));
            OnPropertyChanged(nameof(HighPriorityCount));
            OnPropertyChanged(nameof(MediumPriorityCount));
            OnPropertyChanged(nameof(NominalCount));
            OnPropertyChanged(nameof(ActionRequiredBadgeBg));
            OnPropertyChanged(nameof(FleetNetworkStatusText));
            OnPropertyChanged(nameof(CompactFleetStatusText));
            OnPropertyChanged(nameof(FleetNetworkTooltip));
            OnPropertyChanged(nameof(FleetNetworkBadgeBg));
            OnPropertyChanged(nameof(FleetNetworkBadgeBorder));
            OnPropertyChanged(nameof(FleetNetworkBadgeFg));
            OnPropertyChanged(nameof(FleetNetworkLedColor));
        }

        #endregion

        #region Helpers & Data Loading

        private void UpdateClock()
        {
            LiveClockText = DateTime.Now.ToString("hh:mm:ss tt");
        }

        private void LoadPersistedState()
        {
            _plantName = _db.GetSetting("plant_name", "Plant 01 (Chennai Works)");
            _divisionName = _db.GetSetting("plant_division", "Assembly Division");
            _lineName = _db.GetSetting("plant_line", "RAC Line 01");
            OnPropertyChanged(nameof(PlantName));
            OnPropertyChanged(nameof(DivisionName));
            OnPropertyChanged(nameof(LineName));
            OnPropertyChanged(nameof(PlantHierarchyText));

            var saved = _db.GetSavedStations();
            var deleted = _db.GetDeletedStations();

            Stations.Clear();
            foreach (var s in saved)
            {
                if (!deleted.Contains(s.StationId))
                {
                    Stations.Add(new StationCardViewModel(s, OnDeleteStationRequested));
                }
            }

            ReloadDowntimeLogs();
            ComputeKPIs();
        }

        public void ReloadDowntimeLogs()
        {
            var logs = _db.GetThresholdBreachLogs("ALL");
            ThresholdBreachLogs.Clear();
            foreach (var l in logs)
            {
                ThresholdBreachLogs.Add(l);
            }

            var summaries = _db.GetStationBreachSummaries();
            StationBreachSummaries.Clear();
            foreach (var s in summaries)
            {
                StationBreachSummaries.Add(s);
            }

            ApplyLogFilter();
            OnPropertyChanged(nameof(AuditLogCount));
            OnPropertyChanged(nameof(ResolvedIncidentsCount));
            OnPropertyChanged(nameof(ActiveStoppagesLogCount));
            OnPropertyChanged(nameof(BreachedStationsCount));
        }

        private void ApplyLogFilter()
        {
            FilteredThresholdBreachLogs.Clear();
            IEnumerable<ThresholdBreachLog> query = ThresholdBreachLogs;

            if (ActiveFilter == "ACTIVE")
            {
                query = query.Where(l => l.Status == "ACTIVE");
            }
            else if (ActiveFilter == "RESOLVED" || ActiveFilter == "NORMALIZED")
            {
                query = query.Where(l => l.Status == "NORMALIZED");
            }

            if (!string.IsNullOrWhiteSpace(LogSearchQuery))
            {
                var q = LogSearchQuery.Trim();
                query = query.Where(l => (l.StationId != null && l.StationId.Contains(q, StringComparison.OrdinalIgnoreCase))
                                      || (l.StationName != null && l.StationName.Contains(q, StringComparison.OrdinalIgnoreCase))
                                      || (l.ParameterName != null && l.ParameterName.Contains(q, StringComparison.OrdinalIgnoreCase))
                                      || (l.RecordedValue != null && l.RecordedValue.Contains(q, StringComparison.OrdinalIgnoreCase))
                                      || l.Id.ToString().Contains(q));
            }

            foreach (var item in query)
            {
                FilteredThresholdBreachLogs.Add(item);
            }

            FilteredStationBreachSummaries.Clear();
            IEnumerable<StationBreachSummary> sumQuery = StationBreachSummaries;
            if (!string.IsNullOrWhiteSpace(LogSearchQuery))
            {
                var q = LogSearchQuery.Trim();
                sumQuery = sumQuery.Where(s => (s.StationId != null && s.StationId.Contains(q, StringComparison.OrdinalIgnoreCase))
                                            || (s.StationName != null && s.StationName.Contains(q, StringComparison.OrdinalIgnoreCase)));
            }

            foreach (var s in sumQuery)
            {
                FilteredStationBreachSummaries.Add(s);
            }

            OnPropertyChanged(nameof(IsViewSummary));
            OnPropertyChanged(nameof(IsViewEvents));
            OnPropertyChanged(nameof(IsFilterSummary));
            OnPropertyChanged(nameof(IsFilterAll));
            OnPropertyChanged(nameof(IsFilterActive));
            OnPropertyChanged(nameof(IsFilterResolved));
        }

        private static NetworkProfile DetectHostNetwork()
        {
            var profile = new NetworkProfile();
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up ||
                        ni.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                        continue;

                    var ipProps = ni.GetIPProperties();
                    foreach (var addr in ipProps.UnicastAddresses)
                    {
                        if (addr.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            profile.Ip = addr.Address.ToString();
                            profile.AdapterName = ni.Name;
                            profile.ConnectionType = ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ? "WIFI" : "LAN";
                            profile.Details = $"{ni.Name} (Active)";
                            return profile;
                        }
                    }
                }
            }
            catch { }

            return profile;
        }

        public void SimulateRacWorkstations()
        {
            var racStations = new List<Station>
            {
                new Station
                {
                    StationId = "LINE1_STATION_01",
                    StationName = "ST-01: Chassis Base & Heat Exchanger Mounting",
                    IpAddress = "192.168.19.101",
                    Hostname = "RAC-OP-ST01",
                    Status = "NORMAL",
                    ConnectionType = "LAN",
                    ConnectionDetails = "Ethernet Port Gigabit 1",
                    NetworkLatencyMs = 0.42,
                    SystemTelemetry = new SystemTelemetry
                    {
                        CpuPercent = 18.4,
                        RamPercent = 42.1,
                        RamUsedGb = 6.7,
                        RamTotalGb = 16.0,
                        UptimeFormatted = "4d 11:24:02",
                        Disks = new List<DiskInfo> { new DiskInfo { Drive = "C:", UsedGb = 84.2, TotalGb = 256.0, Percent = 32.8 } }
                    }
                },
                new Station
                {
                    StationId = "LINE1_STATION_02",
                    StationName = "ST-02: Rotary Compressor & Copper Piping Braze",
                    IpAddress = "192.168.19.102",
                    Hostname = "RAC-OP-ST02",
                    Status = "NORMAL",
                    ConnectionType = "LAN",
                    ConnectionDetails = "Ethernet Port Gigabit 2",
                    NetworkLatencyMs = 0.55,
                    SystemTelemetry = new SystemTelemetry
                    {
                        CpuPercent = 32.1,
                        RamPercent = 54.8,
                        RamUsedGb = 8.8,
                        RamTotalGb = 16.0,
                        UptimeFormatted = "4d 11:18:14",
                        Disks = new List<DiskInfo> { new DiskInfo { Drive = "C:", UsedGb = 92.0, TotalGb = 256.0, Percent = 35.9 } }
                    }
                },
                new Station
                {
                    StationId = "LINE1_STATION_03",
                    StationName = "ST-03: Evaporator Coil & Blower Sub-Assembly",
                    IpAddress = "192.168.19.103",
                    Hostname = "RAC-OP-ST03",
                    Status = "WARNING",
                    ConnectionType = "LAN",
                    ConnectionDetails = "Ethernet Port Gigabit 3",
                    NetworkLatencyMs = 0.61,
                    SystemTelemetry = new SystemTelemetry
                    {
                        CpuPercent = 74.2,
                        RamPercent = 78.4,
                        RamUsedGb = 12.5,
                        RamTotalGb = 16.0,
                        UptimeFormatted = "3d 08:44:11",
                        Disks = new List<DiskInfo> { new DiskInfo { Drive = "C:", UsedGb = 180.5, TotalGb = 256.0, Percent = 70.5 } }
                    }
                },
                new Station
                {
                    StationId = "LINE1_STATION_04",
                    StationName = "ST-04: Automated Vacuum & R32 Refrigerant Charge",
                    IpAddress = "192.168.19.104",
                    Hostname = "RAC-OP-ST04",
                    Status = "NORMAL",
                    ConnectionType = "LAN",
                    ConnectionDetails = "Ethernet Port Gigabit 4",
                    NetworkLatencyMs = 0.38,
                    SystemTelemetry = new SystemTelemetry
                    {
                        CpuPercent = 28.6,
                        RamPercent = 48.0,
                        RamUsedGb = 7.7,
                        RamTotalGb = 16.0,
                        UptimeFormatted = "4d 10:52:30",
                        Disks = new List<DiskInfo> { new DiskInfo { Drive = "C:", UsedGb = 110.2, TotalGb = 256.0, Percent = 43.0 } }
                    }
                },
                new Station
                {
                    StationId = "LINE1_STATION_05",
                    StationName = "ST-05: Inverter PCB & Electrical Safety Hi-Pot Test",
                    IpAddress = "192.168.19.105",
                    Hostname = "RAC-OP-ST05",
                    Status = "NORMAL",
                    ConnectionType = "WIFI",
                    ConnectionDetails = "RAC-Floor-AP-02 (5GHz)",
                    NetworkLatencyMs = 2.15,
                    SystemTelemetry = new SystemTelemetry
                    {
                        CpuPercent = 21.0,
                        RamPercent = 45.2,
                        RamUsedGb = 7.2,
                        RamTotalGb = 16.0,
                        UptimeFormatted = "2d 04:12:00",
                        Disks = new List<DiskInfo> { new DiskInfo { Drive = "C:", UsedGb = 95.1, TotalGb = 256.0, Percent = 37.1 } }
                    }
                },
                new Station
                {
                    StationId = "LINE1_STATION_06",
                    StationName = "ST-06: Final QA Run-Test & Packaging",
                    IpAddress = "192.168.19.106",
                    Hostname = "RAC-OP-ST06",
                    Status = "NORMAL",
                    ConnectionType = "LAN",
                    ConnectionDetails = "Ethernet Port Gigabit 6",
                    NetworkLatencyMs = 0.49,
                    ZebraMonitored = false,
                    Permissions = new StationPermissions { Zebra = false },
                    SystemTelemetry = new SystemTelemetry
                    {
                        CpuPercent = 38.0,
                        RamPercent = 52.0,
                        RamUsedGb = 8.3,
                        RamTotalGb = 16.0,
                        UptimeFormatted = "1d 19:12:44",
                        Disks = new List<DiskInfo> { new DiskInfo { Drive = "C:", UsedGb = 120.0, TotalGb = 256.0, Percent = 46.9 } }
                    }
                },
                new Station
                {
                    StationId = "LINE1_STATION_07",
                    StationName = "ST-07: Helium Sniffer Mass Spec Leak Detection",
                    IpAddress = "192.168.19.107",
                    Hostname = "RAC-OP-ST07",
                    Status = "NORMAL",
                    ConnectionType = "LAN",
                    ConnectionDetails = "Ethernet Port Gigabit 7",
                    NetworkLatencyMs = 0.44,
                    SystemTelemetry = new SystemTelemetry
                    {
                        CpuPercent = 22.5,
                        RamPercent = 47.0,
                        RamUsedGb = 7.5,
                        RamTotalGb = 16.0,
                        UptimeFormatted = "3d 14:10:05",
                        Disks = new List<DiskInfo> { new DiskInfo { Drive = "C:", UsedGb = 78.0, TotalGb = 256.0, Percent = 30.5 } }
                    }
                },
                new Station
                {
                    StationId = "LINE1_STATION_08",
                    StationName = "ST-08: Vibration FFT & Acoustic Decibel Testing",
                    IpAddress = "192.168.19.108",
                    Hostname = "RAC-OP-ST08",
                    Status = "NORMAL",
                    ConnectionType = "LAN",
                    ConnectionDetails = "Ethernet Port Gigabit 8",
                    NetworkLatencyMs = 0.52,
                    SystemTelemetry = new SystemTelemetry
                    {
                        CpuPercent = 35.8,
                        RamPercent = 51.2,
                        RamUsedGb = 8.2,
                        RamTotalGb = 16.0,
                        UptimeFormatted = "3d 14:05:12",
                        Disks = new List<DiskInfo> { new DiskInfo { Drive = "C:", UsedGb = 88.4, TotalGb = 256.0, Percent = 34.5 } }
                    }
                },
                new Station
                {
                    StationId = "LINE1_STATION_09",
                    StationName = "ST-09: Front Panel, Louver & Aesthetic QA",
                    IpAddress = "192.168.19.109",
                    Hostname = "RAC-OP-ST09",
                    Status = "NORMAL",
                    ConnectionType = "WIFI",
                    ConnectionDetails = "RAC-Floor-AP-03 (5GHz)",
                    NetworkLatencyMs = 1.85,
                    SystemTelemetry = new SystemTelemetry
                    {
                        CpuPercent = 16.2,
                        RamPercent = 39.5,
                        RamUsedGb = 6.3,
                        RamTotalGb = 16.0,
                        UptimeFormatted = "2d 20:30:45",
                        Disks = new List<DiskInfo> { new DiskInfo { Drive = "C:", UsedGb = 65.0, TotalGb = 256.0, Percent = 25.4 } }
                    }
                },
                new Station
                {
                    StationId = "LINE1_STATION_10",
                    StationName = "ST-10: Serial Barcode Binding & Cloud ERP Sync",
                    IpAddress = "192.168.19.110",
                    Hostname = "RAC-OP-ST10",
                    Status = "NORMAL",
                    ConnectionType = "LAN",
                    ConnectionDetails = "Ethernet Port Gigabit 10",
                    NetworkLatencyMs = 0.39,
                    SystemTelemetry = new SystemTelemetry
                    {
                        CpuPercent = 29.4,
                        RamPercent = 48.6,
                        RamUsedGb = 7.8,
                        RamTotalGb = 16.0,
                        UptimeFormatted = "4d 06:15:20",
                        Disks = new List<DiskInfo> { new DiskInfo { Drive = "C:", UsedGb = 99.2, TotalGb = 256.0, Percent = 38.7 } }
                    }
                },
                new Station
                {
                    StationId = "LINE1_STATION_11",
                    StationName = "ST-11: Robotic Packaging & Palletizing Station",
                    IpAddress = "192.168.19.111",
                    Hostname = "RAC-OP-ST11",
                    Status = "NORMAL",
                    ConnectionType = "LAN",
                    ConnectionDetails = "Ethernet Port Gigabit 11",
                    NetworkLatencyMs = 0.41,
                    SystemTelemetry = new SystemTelemetry
                    {
                        CpuPercent = 31.0,
                        RamPercent = 50.1,
                        RamUsedGb = 8.0,
                        RamTotalGb = 16.0,
                        UptimeFormatted = "4d 06:10:00",
                        Disks = new List<DiskInfo> { new DiskInfo { Drive = "C:", UsedGb = 104.5, TotalGb = 256.0, Percent = 40.8 } }
                    }
                }
            };

            foreach (var st in racStations)
            {
                ProcessIncomingStation(st);
            }
        }

        #endregion
    }
}
