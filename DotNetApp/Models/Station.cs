using System;
using System.Text.Json.Serialization;

namespace DotNetApp.Models
{
    public class Station
    {
        [JsonPropertyName("station_id")]
        public string StationId { get; set; } = string.Empty;

        [JsonPropertyName("station_name")]
        public string StationName { get; set; } = string.Empty;

        [JsonPropertyName("ip_address")]
        public string IpAddress { get; set; } = "127.0.0.1";

        [JsonPropertyName("hostname")]
        public string Hostname { get; set; } = "WORKSTATION";

        [JsonPropertyName("status")]
        public string Status { get; set; } = "OFFLINE"; // NORMAL, WARNING, CRITICAL, OFFLINE

        [JsonPropertyName("connection_type")]
        public string ConnectionType { get; set; } = "LAN"; // LAN or WIFI

        [JsonPropertyName("connection_label")]
        public string ConnectionLabel { get; set; } = "LAN";

        [JsonPropertyName("connection_details")]
        public string ConnectionDetails { get; set; } = "Ethernet LAN";

        [JsonPropertyName("network_latency_ms")]
        public double NetworkLatencyMs { get; set; } = 0.5;

        [JsonPropertyName("last_seen")]
        public DateTime LastSeen { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("zebra_monitored")]
        public bool ZebraMonitored { get; set; } = false;

        [JsonPropertyName("zebra_printer")]
        public ZebraPrinter ZebraPrinter { get; set; } = new();

        [JsonPropertyName("system_telemetry")]
        public SystemTelemetry SystemTelemetry { get; set; } = new();

        [JsonPropertyName("permissions")]
        public StationPermissions Permissions { get; set; } = new();

        [JsonPropertyName("enabled_metrics")]
        public System.Collections.Generic.Dictionary<string, object?> EnabledMetrics { get; set; } = new();

        [JsonPropertyName("offline_reason")]
        public string OfflineReason { get; set; } = string.Empty; // "PC_SHUTDOWN", "AGENT_STOPPED", "TIMEOUT"

        [JsonPropertyName("offline_message")]
        public string OfflineMessage { get; set; } = string.Empty;

        // Runtime Tracking
        [JsonIgnore]
        public long? ActiveIncidentId { get; set; }
    }
}
