using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LineNodeAgent.Models
{
    public class AgentFunctionItem : INotifyPropertyChanged
    {
        private bool _isEnabled;
        private string _liveValue = "Reading...";
        private string _status = "ACTIVE";

        public string Key { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Category { get; set; } = "Hardware Core";
        public string Description { get; set; } = string.Empty;

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled != value)
                {
                    _isEnabled = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(StatusDisplay));
                    OnPropertyChanged(nameof(StatusBadgeBg));
                    OnPropertyChanged(nameof(StatusBadgeFg));
                    OnPropertyChanged(nameof(StatusBadgeBorder));
                }
            }
        }

        public string LiveValue
        {
            get => _liveValue;
            set
            {
                if (_liveValue != value)
                {
                    _liveValue = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Status
        {
            get => _status;
            set
            {
                if (_status != value)
                {
                    _status = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(StatusDisplay));
                    OnPropertyChanged(nameof(StatusBadgeBg));
                    OnPropertyChanged(nameof(StatusBadgeFg));
                    OnPropertyChanged(nameof(StatusBadgeBorder));
                }
            }
        }

        public string StatusDisplay => IsEnabled ? "PERMITTED" : "DISABLED";
        public string StatusBadgeBg => IsEnabled ? "#ecfdf5" : "#f1f5f9";
        public string StatusBadgeFg => IsEnabled ? "#059669" : "#94a3b8";
        public string StatusBadgeBorder => IsEnabled ? "#a7f3d0" : "#e2e8f0";

        public string CategoryBadgeBg => Category switch
        {
            "Hardware Core" => "#eff6ff",
            "Network & Comms" => "#f0fdf4",
            "Security & System" => "#faf5ff",
            "Peripherals & Devices" => "#fff7ed",
            _ => "#f1f5f9"
        };

        public string CategoryBadgeFg => Category switch
        {
            "Hardware Core" => "#1d4ed8",
            "Network & Comms" => "#15803d",
            "Security & System" => "#7e22ce",
            "Peripherals & Devices" => "#c2410c",
            _ => "#475569"
        };

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
