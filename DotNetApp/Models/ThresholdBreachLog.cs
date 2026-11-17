using System;

namespace DotNetApp.Models
{
    public class ThresholdBreachLog
    {
        public long Id { get; set; }
        public string StationId { get; set; } = string.Empty;
        public string StationName { get; set; } = string.Empty;
        public string ParameterName { get; set; } = string.Empty;       // e.g. "RAM Physical Commit", "CPU Workload", "Network Latency"
        public string BreachType { get; set; } = "CRITICAL";           // "CRITICAL" or "WARNING"
        public string RecordedValue { get; set; } = string.Empty;       // e.g. "88.4%"
        public string ThresholdLimit { get; set; } = string.Empty;      // e.g. "88.0%"
        public string BreachTime { get; set; } = string.Empty;          // ISO UTC
        public string RecoveryTime { get; set; } = "--";                // When normalized
        public string DurationAboveThreshold { get; set; } = "Active";  // e.g. "00:00:14" or "Active"
        public string Status { get; set; } = "ACTIVE";                  // "ACTIVE" or "NORMALIZED"
        public string CreatedAt { get; set; } = string.Empty;
    }

    public class StationBreachSummary
    {
        public string StationId { get; set; } = string.Empty;
        public string StationName { get; set; } = string.Empty;
        public int TotalBreachCount { get; set; }                       // How many times this system went above threshold
        public int CpuBreachCount { get; set; }
        public int RamBreachCount { get; set; }
        public int TempBreachCount { get; set; }
        public int DiskBreachCount { get; set; }
        public int HeartbeatBreachCount { get; set; }
        public string PeakViolation { get; set; } = "--";
        public string LastBreachTime { get; set; } = "--";
        public string CurrentState { get; set; } = "NORMAL";            // "NORMAL" or "IN VIOLATION"
        public string CurrentStateBadgeBg => CurrentState == "NORMAL" ? "#dcfce7" : "#fee2e2";
        public string CurrentStateBadgeFg => CurrentState == "NORMAL" ? "#15803d" : "#b91c1c";
    }
}
