using System;
using System.Collections.Concurrent;
using System.Net.NetworkInformation;
using System.Threading.Tasks;

namespace DotNetApp.Services
{
    public static class StationPingService
    {
        private static readonly ConcurrentDictionary<string, (bool isAlive, double latencyMs, DateTime lastCheck)> _cache = new();

        public static bool IsHostReachable(string ipOrHost)
        {
            if (string.IsNullOrWhiteSpace(ipOrHost)) return false;

            // Localhost / Loopback is always network-reachable
            if (ipOrHost == "127.0.0.1" || 
                ipOrHost.StartsWith("127.") || 
                ipOrHost.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                ipOrHost.Equals(Environment.MachineName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var cleanHost = ipOrHost.Replace("http://", "").Replace("https://", "").Split(':')[0].Trim();
            if (string.IsNullOrWhiteSpace(cleanHost)) return false;

            var now = DateTime.UtcNow;
            if (_cache.TryGetValue(cleanHost, out var entry) && (now - entry.lastCheck).TotalSeconds < 4.0)
            {
                return entry.isAlive;
            }

            // Fire-and-forget non-blocking background ping
            _ = Task.Run(async () =>
            {
                try
                {
                    using var ping = new Ping();
                    var reply = await ping.SendPingAsync(cleanHost, 200);
                    bool alive = (reply.Status == IPStatus.Success);
                    double ms = alive ? Math.Max(0.5, (double)reply.RoundtripTime) : 999.0;
                    _cache[cleanHost] = (alive, ms, DateTime.UtcNow);
                }
                catch
                {
                    _cache[cleanHost] = (false, 999.0, DateTime.UtcNow);
                }
            });

            return entry.isAlive;
        }

        public static double GetCachedLatency(string ipOrHost)
        {
            var cleanHost = (ipOrHost ?? "").Replace("http://", "").Replace("https://", "").Split(':')[0].Trim();
            if (_cache.TryGetValue(cleanHost, out var entry))
            {
                return entry.latencyMs;
            }
            return 0.5;
        }
    }
}
