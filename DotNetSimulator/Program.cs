using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DotNetSimulator
{
    public class StationDef
    {
        public string StationId { get; set; } = "";
        public string StationName { get; set; } = "";
        public string Ip { get; set; } = "";
        public bool IsZebra { get; set; }
        public double BaseCpu { get; set; }
        public double BaseRam { get; set; }
    }

    internal class Program
    {
        private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(2) };
        private static string _targetUrl = "http://127.0.0.1:8090/api/telemetry";
        private static volatile bool _running = true;

        private static readonly List<StationDef> Stations = new()
        {
            new StationDef { StationId = "LINE1_STATION_01", StationName = "ST-01: Chassis Press & Base Assembly", Ip = "192.168.1.101", IsZebra = false, BaseCpu = 22.0, BaseRam = 38.0 },
            new StationDef { StationId = "LINE1_STATION_02", StationName = "ST-02: Evaporator Coil & Copper Brazing", Ip = "192.168.1.102", IsZebra = false, BaseCpu = 35.0, BaseRam = 45.0 },
            new StationDef { StationId = "LINE1_STATION_03", StationName = "ST-03: Compressor & Motor Mounting", Ip = "192.168.1.103", IsZebra = false, BaseCpu = 18.0, BaseRam = 32.0 },
            new StationDef { StationId = "LINE1_STATION_04", StationName = "ST-04: Electrical & Inverter PCB Wiring", Ip = "192.168.1.104", IsZebra = false, BaseCpu = 40.0, BaseRam = 52.0 },
            new StationDef { StationId = "LINE1_STATION_05", StationName = "ST-05: Vacuum Decay & R32 Charge", Ip = "192.168.1.105", IsZebra = false, BaseCpu = 28.0, BaseRam = 48.0 },
            new StationDef { StationId = "LINE1_STATION_06", StationName = "ST-06: Final QA Run-Test & Packaging", Ip = "192.168.1.106", IsZebra = false, BaseCpu = 30.0, BaseRam = 42.0 },
        };

        private static async Task Main(string[] args)
        {
            Console.Title = "PlantOps™ RAC Assembly Line Simulator (100% .NET 8 / C#)";
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(@"
================================================================================
   PLANTOPS™ RAC 6-STATION ASSEMBLY LINE SIMULATOR (.NET 8 / C#)
================================================================================
  Simulating: ST-01 through ST-06 Assembly Stations
  Target URL: " + _targetUrl + @"
================================================================================");
            Console.ResetColor();

            if (args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]))
            {
                _targetUrl = args[0];
            }

            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                _running = false;
                Console.WriteLine("\n[SIMULATOR] Stopping telemetry streams...");
            };

            var tasks = new List<Task>();
            foreach (var st in Stations)
            {
                tasks.Add(Task.Run(() => RunStationLoopAsync(st)));
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("[SIMULATOR] All 6 stations actively broadcasting telemetry. Press Ctrl+C to stop.\n");
            Console.ResetColor();

            await Task.WhenAll(tasks);
        }

        private static async Task RunStationLoopAsync(StationDef st)
        {
            var rnd = new Random(st.StationId.GetHashCode());
            long cycle = 0;

            while (_running)
            {
                cycle++;
                double cpu = Math.Clamp(st.BaseCpu + (rnd.NextDouble() * 10 - 5), 5.0, 95.0);
                double ram = Math.Clamp(st.BaseRam + (rnd.NextDouble() * 4 - 2), 20.0, 90.0);
                double disk = 42.5;

                var telemetry = new Dictionary<string, object>
                {
                    ["station_id"] = st.StationId,
                    ["station_name"] = st.StationName,
                    ["ip_address"] = st.Ip,
                    ["connection_type"] = "WIFI",
                    ["hostname"] = $"{st.StationId}-PC",
                    ["cpu_percent"] = Math.Round(cpu, 1),
                    ["ram_percent"] = Math.Round(ram, 1),
                    ["disk_percent"] = disk,
                    ["uptime_seconds"] = 124500 + cycle * 3,
                    ["timestamp"] = DateTime.UtcNow.ToString("o")
                };

                try
                {
                    var json = JsonSerializer.Serialize(telemetry);
                    using var content = new StringContent(json, Encoding.UTF8, "application/json");
                    var resp = await _http.PostAsync(_targetUrl, content);
                    if (resp.IsSuccessStatusCode)
                    {
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [{st.StationId}] Ingested OK (CPU: {cpu:F1}%, RAM: {ram:F1}%)");
                        Console.ResetColor();
                    }
                }
                catch
                {
                    // SCADA Core might be restarting or connecting
                }

                await Task.Delay(2500 + rnd.Next(1000));
            }
        }
    }
}
