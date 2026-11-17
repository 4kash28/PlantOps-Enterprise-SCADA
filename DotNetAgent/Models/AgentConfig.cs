using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DotNetAgent.Models
{
    public class AgentConfig
    {
        public string ServerHost { get; set; } = "127.0.0.1";
        public int ServerPort { get; set; } = 8090;
        public string StationId { get; set; } = "LINE1_STATION_06";
        public string StationName { get; set; } = "ST-06: Final QA Run-Test & Zebra ZT411";
        public int SyncIntervalSec { get; set; } = 3;
        public bool ZebraMonitored { get; set; } = true;

        // Granular Data Governance Permissions
        public AgentPermissions Permissions { get; set; } = new();
    }

    public class AgentPermissions
    {
        [JsonPropertyName("cpu")]
        public bool AllowCpu { get; set; } = true;

        [JsonPropertyName("ram")]
        public bool AllowRam { get; set; } = true;

        [JsonPropertyName("disk")]
        public bool AllowDisk { get; set; } = true;

        [JsonPropertyName("network")]
        public bool AllowNetwork { get; set; } = true;

        [JsonPropertyName("uptime")]
        public bool AllowUptime { get; set; } = true;

        [JsonPropertyName("zebra")]
        public bool AllowZebra { get; set; } = true;

        [JsonPropertyName("allow_remote_stop")]
        public bool AllowRemoteStop { get; set; } = true;
    }

    public class TelemetryPacket
    {
        [JsonPropertyName("station_id")]
        public string StationId { get; set; } = "";

        [JsonPropertyName("station_name")]
        public string StationName { get; set; } = "";

        [JsonPropertyName("ip_address")]
        public string IpAddress { get; set; } = "127.0.0.1";

        [JsonPropertyName("hostname")]
        public string Hostname { get; set; } = "";

        [JsonPropertyName("status")]
        public string Status { get; set; } = "NORMAL";

        [JsonPropertyName("connection_type")]
        public string ConnectionType { get; set; } = "LAN";

        [JsonPropertyName("connection_label")]
        public string ConnectionLabel { get; set; } = "LAN";

        [JsonPropertyName("connection_details")]
        public string ConnectionDetails { get; set; } = "";

        [JsonPropertyName("network_latency_ms")]
        public double NetworkLatencyMs { get; set; } = 0.5;

        [JsonPropertyName("last_seen")]
        public DateTime LastSeen { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("zebra_monitored")]
        public bool ZebraMonitored { get; set; }

        [JsonPropertyName("zebra_printer")]
        public ZebraPrinterDto? ZebraPrinter { get; set; }

        [JsonPropertyName("system_telemetry")]
        public SystemTelemetryDto SystemTelemetry { get; set; } = new();

        [JsonPropertyName("permissions")]
        public AgentPermissions Permissions { get; set; } = new();
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
        public string UptimeFormatted { get; set; } = "0d 00:00:00";

        [JsonPropertyName("network_latency_ms")]
        public double NetworkLatencyMs { get; set; }

        [JsonPropertyName("disks")]
        public List<DiskInfoDto> Disks { get; set; } = new();
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

    public class ZebraPrinterDto
    {
        [JsonPropertyName("status")]
        public string Status { get; set; } = "READY";

        [JsonPropertyName("printer_name")]
        public string PrinterName { get; set; } = "Zebra ZT411 Industrial (300 DPI)";

        [JsonPropertyName("port_name")]
        public string PortName { get; set; } = "USB001";

        [JsonPropertyName("is_default")]
        public bool IsDefault { get; set; } = true;

        [JsonPropertyName("error_description")]
        public string ErrorDescription { get; set; } = "Online • Media Loaded • Ready";
    }
}
