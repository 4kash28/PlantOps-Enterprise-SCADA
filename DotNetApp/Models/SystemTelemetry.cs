using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DotNetApp.Models
{
    public class SystemTelemetry
    {
        [JsonPropertyName("cpu_percent")]
        public double? CpuPercent { get; set; } = 0.0;

        [JsonPropertyName("ram_percent")]
        public double? RamPercent { get; set; } = 0.0;

        [JsonPropertyName("ram_used_gb")]
        public double? RamUsedGb { get; set; } = 0.0;

        [JsonPropertyName("ram_total_gb")]
        public double? RamTotalGb { get; set; } = 16.0;

        [JsonPropertyName("uptime_formatted")]
        public string UptimeFormatted { get; set; } = "0d 00:00:00";

        [JsonPropertyName("uptime_hours")]
        public double? UptimeHours { get; set; }

        public double GetUptimeHours()
        {
            if (UptimeHours.HasValue && UptimeHours.Value > 0) return UptimeHours.Value;
            if (!string.IsNullOrWhiteSpace(UptimeFormatted))
            {
                try
                {
                    var parts = UptimeFormatted.Trim().Split(' ');
                    if (parts.Length >= 2 && parts[0].EndsWith("d", StringComparison.OrdinalIgnoreCase))
                    {
                        if (double.TryParse(parts[0].TrimEnd('d', 'D'), out var days))
                        {
                            var timeParts = parts[1].Split(':');
                            if (timeParts.Length >= 2 && double.TryParse(timeParts[0], out var hrs))
                            {
                                return (days * 24.0) + hrs;
                            }
                        }
                    }
                }
                catch { }
            }
            return 0.0;
        }

        [JsonPropertyName("network_latency_ms")]
        public double NetworkLatencyMs { get; set; } = 0.0;

        [JsonPropertyName("disks")]
        public List<DiskInfo> Disks { get; set; } = new();

        [JsonPropertyName("cpu_temperature_c")]
        public double? CpuTemperatureC { get; set; }

        [JsonPropertyName("active_user")]
        public string? ActiveUser { get; set; }

        [JsonPropertyName("critical_processes_count")]
        public int? CriticalProcessesCount { get; set; }

        [JsonPropertyName("spooler_service_status")]
        public string? SpoolerServiceStatus { get; set; }

        [JsonPropertyName("usb_device_count")]
        public int? UsbDeviceCount { get; set; }

        [JsonPropertyName("temp_junk_files_count")]
        public int? TempJunkFilesCount { get; set; }

        [JsonPropertyName("temp_junk_size_mb")]
        public double? TempJunkSizeMb { get; set; }

        [JsonPropertyName("temp_junk_summary")]
        public string? TempJunkSummary { get; set; }
    }
}
