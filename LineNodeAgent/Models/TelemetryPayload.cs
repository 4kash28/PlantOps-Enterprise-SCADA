using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace LineNodeAgent.Models
{
    public class TelemetryPayload
    {
        [JsonPropertyName("node_id")]
        public string NodeId { get; set; } = string.Empty;

        [JsonPropertyName("station_id")]
        public string StationId { get; set; } = string.Empty;

        [JsonPropertyName("station_name")]
        public string StationName { get; set; } = string.Empty;

        [JsonPropertyName("hostname")]
        public string Hostname { get; set; } = string.Empty;

        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("status")]
        public string Status { get; set; } = "NORMAL";

        [JsonPropertyName("ip_address")]
        public string IpAddress { get; set; } = "127.0.0.1";

        [JsonPropertyName("connection_type")]
        public string ConnectionType { get; set; } = "LAN";

        [JsonPropertyName("network_latency_ms")]
        public double NetworkLatencyMs { get; set; } = 0.5;

        [JsonPropertyName("offline_reason")]
        public string OfflineReason { get; set; } = string.Empty;

        [JsonPropertyName("offline_message")]
        public string OfflineMessage { get; set; } = string.Empty;

        // Dictionary of enabled metrics sent by agent
        [JsonPropertyName("enabled_metrics")]
        public Dictionary<string, object?> EnabledMetrics { get; set; } = new();

        // Standard System Telemetry structure for SCADA Central Dashboard
        [JsonPropertyName("system_telemetry")]
        public SystemTelemetryDto SystemTelemetry { get; set; } = new();

        [JsonPropertyName("permissions")]
        public Dictionary<string, bool> Permissions { get; set; } = new();
    }

    public class SystemTelemetryDto
    {
        [JsonPropertyName("cpu_percent")]
        public double? CpuPercent { get; set; }

        [JsonPropertyName("ram_percent")]
        public double? RamPercent { get; set; }

        [JsonPropertyName("ram_used_gb")]
        public double? RamUsedGb { get; set; }

        [JsonPropertyName("ram_total_gb")]
        public double? RamTotalGb { get; set; }

        [JsonPropertyName("uptime_formatted")]
        public string? UptimeFormatted { get; set; }

        [JsonPropertyName("uptime_hours")]
        public double? UptimeHours { get; set; }

        [JsonPropertyName("cpu_temperature_c")]
        public double? CpuTemperatureC { get; set; }

        [JsonPropertyName("active_user")]
        public string? ActiveUser { get; set; }

        [JsonPropertyName("network_latency_ms")]
        public double NetworkLatencyMs { get; set; }

        [JsonPropertyName("disks")]
        public List<DiskInfoDto> Disks { get; set; } = new();

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

    public class DiskInfoDto
    {
        [JsonPropertyName("drive")]
        public string Drive { get; set; } = "C:";

        [JsonPropertyName("used_gb")]
        public double UsedGb { get; set; }

        [JsonPropertyName("total_gb")]
        public double TotalGb { get; set; }

        [JsonPropertyName("percent")]
        public double Percent { get; set; }
    }
}
