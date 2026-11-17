using System.Text.Json.Serialization;

namespace DotNetApp.Models
{
    public class DiskInfo
    {
        [JsonPropertyName("drive")]
        public string Drive { get; set; } = "C:";

        [JsonPropertyName("used_gb")]
        public double UsedGb { get; set; } = 0.0;

        [JsonPropertyName("total_gb")]
        public double TotalGb { get; set; } = 256.0;

        [JsonPropertyName("percent")]
        public double Percent { get; set; } = 0.0;
    }
}
