using System;
using System.Text.Json.Serialization;

namespace DotNetApp.Models
{
    public class DowntimeLog
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("station_id")]
        public string StationId { get; set; } = string.Empty;

        [JsonPropertyName("station_name")]
        public string StationName { get; set; } = string.Empty;

        [JsonPropertyName("failure_reason")]
        public string FailureReason { get; set; } = string.Empty;

        [JsonPropertyName("start_time")]
        public string StartTime { get; set; } = string.Empty;

        [JsonPropertyName("end_time")]
        public string? EndTime { get; set; }

        [JsonPropertyName("duration_seconds")]
        public double DurationSeconds { get; set; } = 0.0;

        [JsonPropertyName("duration_formatted")]
        public string DurationFormatted { get; set; } = "00:00:00";

        [JsonPropertyName("status")]
        public string Status { get; set; } = "ACTIVE"; // ACTIVE or RESOLVED

        [JsonPropertyName("created_at")]
        public string CreatedAt { get; set; } = string.Empty;
    }
}
