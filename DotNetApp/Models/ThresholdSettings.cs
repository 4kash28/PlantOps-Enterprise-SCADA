using System.Text.Json.Serialization;

namespace DotNetApp.Models
{
    public class ThresholdSettings
    {
        [JsonPropertyName("cpu_warning_pct")]
        public double CpuWarningPct { get; set; } = 70.0;

        [JsonPropertyName("cpu_critical_pct")]
        public double CpuCriticalPct { get; set; } = 85.0;

        [JsonPropertyName("ram_warning_pct")]
        public double RamWarningPct { get; set; } = 82.0;

        [JsonPropertyName("ram_critical_pct")]
        public double RamCriticalPct { get; set; } = 88.0;

        [JsonPropertyName("disk_warning_pct")]
        public double DiskWarningPct { get; set; } = 80.0;

        [JsonPropertyName("disk_critical_pct")]
        public double DiskCriticalPct { get; set; } = 90.0;

        [JsonPropertyName("heartbeat_timeout_sec")]
        public int HeartbeatTimeoutSec { get; set; } = 10;

        [JsonPropertyName("temp_files_threshold_count")]
        public int TempFilesThresholdCount { get; set; } = 250;

        [JsonPropertyName("uptime_threshold_hours")]
        public int UptimeThresholdHours { get; set; } = 48;

        [JsonPropertyName("alert_chime_enabled")]
        public bool AlertChimeEnabled { get; set; } = true;

        [JsonPropertyName("dark_theme")]
        public bool DarkTheme { get; set; } = false;
    }
}
