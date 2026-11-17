using System.Text.Json.Serialization;

namespace DotNetApp.Models
{
    public class ZebraPrinter
    {
        [JsonPropertyName("status")]
        public string Status { get; set; } = "DISCONNECTED";

        [JsonPropertyName("printer_name")]
        public string PrinterName { get; set; } = "Zebra ZT411 Industrial (203 DPI)";

        [JsonPropertyName("port_name")]
        public string PortName { get; set; } = "USB001";

        [JsonPropertyName("is_default")]
        public bool IsDefault { get; set; } = false;

        [JsonPropertyName("error_description")]
        public string ErrorDescription { get; set; } = "Printer offline or disconnected";
    }
}
