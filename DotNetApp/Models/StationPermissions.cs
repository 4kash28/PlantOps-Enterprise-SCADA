using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DotNetApp.Models
{
    public class StationPermissions
    {
        [JsonPropertyName("cpu")]
        public bool Cpu { get; set; } = true;

        [JsonPropertyName("ram")]
        public bool Ram { get; set; } = true;

        [JsonPropertyName("disk")]
        public bool Disk { get; set; } = true;

        [JsonPropertyName("network")]
        public bool Network { get; set; } = true;

        [JsonPropertyName("uptime")]
        public bool Uptime { get; set; } = true;

        [JsonPropertyName("zebra")]
        public bool Zebra { get; set; } = false;

        [JsonPropertyName("cpu_temperature")]
        public bool CpuTemperature { get; set; } = false;

        [JsonPropertyName("active_user")]
        public bool ActiveUser { get; set; } = false;

        [JsonPropertyName("critical_processes")]
        public bool CriticalProcesses { get; set; } = false;

        [JsonPropertyName("windows_services")]
        public bool WindowsServices { get; set; } = false;

        [JsonPropertyName("usb_peripherals")]
        public bool UsbPeripherals { get; set; } = false;

        [JsonPropertyName("allow_remote_stop")]
        public bool AllowRemoteStop { get; set; } = true;

        [JsonExtensionData]
        public Dictionary<string, object> ExtraPermissions { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public bool IsFunctionEnabled(string key)
        {
            if (ExtraPermissions.TryGetValue(key, out var val))
            {
                if (val is System.Text.Json.JsonElement elem)
                {
                    if (elem.ValueKind == System.Text.Json.JsonValueKind.True) return true;
                    if (elem.ValueKind == System.Text.Json.JsonValueKind.False) return false;
                }
                if (val is bool b) return b;
            }

            return key.ToUpperInvariant() switch
            {
                "CPU_USAGE" or "CPU" => Cpu,
                "RAM_USAGE" or "RAM" => Ram,
                "DISK_SPACE" or "DISK" => Disk,
                "NETWORK_PING" or "NETWORK" => Network,
                "SYSTEM_UPTIME" or "UPTIME" => Uptime,
                "USB_PRINTERS" or "ZEBRA" => Zebra,
                "CPU_TEMPERATURE" => CpuTemperature,
                "ACTIVE_USER_SESSION" or "ACTIVE_USER" => ActiveUser,
                "CRITICAL_PROCESSES" => CriticalProcesses,
                "WINDOWS_SERVICES" => WindowsServices,
                "USB_PERIPHERALS" => UsbPeripherals,
                _ => false
            };
        }
    }
}
