using System;
using System.Text.Json.Serialization;

namespace DotNetApp.Models
{
    public class SummaryKPIs
    {
        [JsonPropertyName("total_stations")]
        public int TotalStations { get; set; } = 0;

        [JsonPropertyName("online_stations")]
        public int OnlineStations { get; set; } = 0;

        [JsonPropertyName("warning_stations")]
        public int WarningStations { get; set; } = 0;

        [JsonPropertyName("critical_stations")]
        public int CriticalStations { get; set; } = 0;

        [JsonPropertyName("offline_stations")]
        public int OfflineStations { get; set; } = 0;

        [JsonPropertyName("line_availability_pct")]
        public double LineAvailabilityPct { get; set; } = 100.0;

        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
