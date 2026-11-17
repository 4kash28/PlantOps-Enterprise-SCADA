using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LineNodeAgent.Models
{
    public class AgentSettings : INotifyPropertyChanged
    {
        private string _nodeName = Environment.MachineName;
        private string _lineStationId = "LINE1_STATION_06";
        private string _stationFriendlyName = "ST-06: Final QA Run-Test";
        private string _serverUrl = "http://localhost:5050";
        private int _pollingIntervalSeconds = 1;
        private bool _minimizeToTray = true;

        public string NodeName
        {
            get => _nodeName;
            set { _nodeName = value; OnPropertyChanged(); }
        }

        public string LineStationId
        {
            get => _lineStationId;
            set { _lineStationId = value?.Trim() ?? string.Empty; OnPropertyChanged(); }
        }

        public string StationFriendlyName
        {
            get => (_stationFriendlyName ?? string.Empty)
                .Replace(" & Zebra ZT411 Labeling", " & Packaging")
                .Replace(" & Zebra ZT411", "")
                .Replace("Zebra ZT411", "")
                .Trim();
            set { _stationFriendlyName = value?.Trim() ?? string.Empty; OnPropertyChanged(); }
        }

        public string ServerUrl
        {
            get => _serverUrl;
            set
            {
                var val = value?.Trim() ?? string.Empty;
                if (!string.IsNullOrEmpty(val) &&
                    !val.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                    !val.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    val = "http://" + val;
                }
                _serverUrl = val;
                OnPropertyChanged();
            }
        }

        public int PollingIntervalSeconds
        {
            get => _pollingIntervalSeconds;
            set { _pollingIntervalSeconds = value; OnPropertyChanged(); }
        }

        public bool MinimizeToTray
        {
            get => _minimizeToTray;
            set { _minimizeToTray = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }
    }
}
