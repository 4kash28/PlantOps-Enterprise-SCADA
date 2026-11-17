using System;
using System.Text.Json.Serialization;

namespace DotNetApp.Models
{
    public class NetworkProfile
    {
        [JsonPropertyName("ip")]
        public string Ip { get; set; } = "192.168.19.170";

        [JsonPropertyName("subnet_mask")]
        public string SubnetMask { get; set; } = "255.255.255.0";

        [JsonPropertyName("gateway")]
        public string Gateway { get; set; } = "192.168.19.1";

        [JsonPropertyName("port")]
        public int Port { get; set; } = 5050;

        [JsonPropertyName("subnet_cidr")]
        public string SubnetCidr { get; set; } = "192.168.19.0/24";

        [JsonPropertyName("enforce_subnet")]
        public bool EnforceSubnet { get; set; } = true;

        [JsonPropertyName("prompt_on_startup")]
        public bool PromptOnStartup { get; set; } = false;

        [JsonPropertyName("adapter_name")]
        public string AdapterName { get; set; } = "Wi-Fi";

        [JsonPropertyName("connection_type")]
        public string ConnectionType { get; set; } = "WIFI"; // WIFI or LAN

        [JsonPropertyName("details")]
        public string Details { get; set; } = "SSID: Amber Wifi (82%)";
    }
}
