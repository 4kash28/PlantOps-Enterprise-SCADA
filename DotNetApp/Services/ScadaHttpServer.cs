using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DotNetApp.Data;
using DotNetApp.Models;

namespace DotNetApp.Services
{
    public class ScadaHttpServer : IDisposable
    {
        private HttpListener? _listener;
        private CancellationTokenSource? _cts;
        private readonly DatabaseService _db;
        private readonly int _port;

        public event Action<Station>? TelemetryReceived;
        public event Action<string>? StationDeletedByClient;

        public ScadaHttpServer(DatabaseService db, int port = 5050)
        {
            _db = db;
            _port = port;
        }

        public bool IsRunning => _listener != null && _listener.IsListening;

        public void Start()
        {
            if (IsRunning) return;

            _cts = new CancellationTokenSource();

            // Try wildcards first (succeeds if running as admin or if urlacl is reserved)
            var wildcardPrefixes = new[]
            {
                $"http://*:{_port}/",
                $"http://+:{_port}/"
            };

            foreach (var wp in wildcardPrefixes)
            {
                try
                {
                    var l = new HttpListener();
                    l.Prefixes.Add(wp);
                    l.Start();
                    _listener = l;
                    Task.Run(() => ListenLoop(_cts.Token));
                    return;
                }
                catch { }
            }

            // Non-admin mode: Bind both localhost and 127.0.0.1 for loopback & local agent communication
            try
            {
                var l = new HttpListener();
                l.Prefixes.Add($"http://localhost:{_port}/");
                l.Prefixes.Add($"http://127.0.0.1:{_port}/");
                l.Start();
                _listener = l;
                Task.Run(() => ListenLoop(_cts.Token));
                return;
            }
            catch { }

            // Individual fallback
            try
            {
                var l = new HttpListener();
                l.Prefixes.Add($"http://localhost:{_port}/");
                l.Start();
                _listener = l;
                Task.Run(() => ListenLoop(_cts.Token));
                return;
            }
            catch { }

            try
            {
                var l = new HttpListener();
                l.Prefixes.Add($"http://127.0.0.1:{_port}/");
                l.Start();
                _listener = l;
                Task.Run(() => ListenLoop(_cts.Token));
                return;
            }
            catch { }
        }

        private async Task ListenLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested && _listener != null && _listener.IsListening)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    _ = ProcessRequestAsync(context);
                }
                catch (HttpListenerException)
                {
                    break;
                }
                catch (Exception)
                {
                    // Listener interrupted or stopped
                }
            }
        }

        private async Task ProcessRequestAsync(HttpListenerContext context)
        {
            var req = context.Request;
            var resp = context.Response;

            // CORS Headers for API accessibility
            resp.AddHeader("Access-Control-Allow-Origin", "*");
            resp.AddHeader("Access-Control-Allow-Methods", "GET, POST, DELETE, OPTIONS");
            resp.AddHeader("Access-Control-Allow-Headers", "Content-Type");

            if (req.HttpMethod == "OPTIONS")
            {
                resp.StatusCode = (int)HttpStatusCode.NoContent;
                resp.Close();
                return;
            }

            try
            {
                var path = req.Url?.AbsolutePath.TrimEnd('/') ?? "";

                if (req.HttpMethod == "POST" && path.Equals("/api/telemetry", StringComparison.OrdinalIgnoreCase))
                {
                    using var reader = new StreamReader(req.InputStream, req.ContentEncoding);
                    var body = await reader.ReadToEndAsync();
                    var station = JsonSerializer.Deserialize<Station>(body, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (station != null && !string.IsNullOrWhiteSpace(station.StationId))
                    {
                        var deletedSet = _db.GetDeletedStations();
                        if (deletedSet.Contains(station.StationId))
                        {
                            // If this is a real LineNodeAgent (has EnabledMetrics or non-default Hostname or header):
                            // Automatically restore it so an active workstation PC is never permanently blocked!
                            bool isRealAgent = (station.EnabledMetrics != null && station.EnabledMetrics.Count > 0) ||
                                               req.Headers["X-Line-Agent"] == "true" ||
                                               (!string.IsNullOrWhiteSpace(station.Hostname) && !station.Hostname.Equals("WORKSTATION", StringComparison.OrdinalIgnoreCase));

                            if (isRealAgent)
                            {
                                _db.RestoreDeletedStation(station.StationId);
                            }
                            else
                            {
                                // Station was deleted by operator - tell LineNodeAgent / simulator to stop
                                var delResp = JsonSerializer.Serialize(new
                                {
                                    status = "deregistered",
                                    station_id = station.StationId,
                                    should_stop = true
                                });
                                await WriteJsonResponseAsync(resp, delResp);
                                return;
                            }
                        }

                        if (!string.IsNullOrEmpty(station.StationName))
                        {
                            station.StationName = station.StationName
                                .Replace(" & Zebra ZT411 Labeling", " & Packaging")
                                .Replace(" & Zebra ZT411", "")
                                .Replace("Zebra ZT411", "")
                                .Trim();
                        }

                        station.LastSeen = DateTime.UtcNow;
                        TelemetryReceived?.Invoke(station);

                        var successResp = JsonSerializer.Serialize(new
                        {
                            status = "ok",
                            station_id = station.StationId
                        });
                        await WriteJsonResponseAsync(resp, successResp);
                        return;
                    }

                    resp.StatusCode = (int)HttpStatusCode.BadRequest;
                    await WriteJsonResponseAsync(resp, "{\"error\":\"Invalid payload\"}");
                    return;
                }
                else if (req.HttpMethod == "DELETE" && path.StartsWith("/api/stations/", StringComparison.OrdinalIgnoreCase))
                {
                    var sid = path.Substring("/api/stations/".Length);
                    StationDeletedByClient?.Invoke(sid);
                    await WriteJsonResponseAsync(resp, $"{{\"status\":\"deleted\",\"station_id\":\"{sid}\"}}");
                    return;
                }
                else if (req.HttpMethod == "POST" && path.StartsWith("/api/stations/", StringComparison.OrdinalIgnoreCase) && path.EndsWith("/restore", StringComparison.OrdinalIgnoreCase))
                {
                    var sid = path.Substring("/api/stations/".Length);
                    sid = sid.Substring(0, sid.Length - "/restore".Length).Trim('/');
                    _db.RestoreDeletedStation(sid);
                    await WriteJsonResponseAsync(resp, $"{{\"status\":\"restored\",\"station_id\":\"{sid}\"}}");
                    return;
                }
                else if (req.HttpMethod == "GET" && path.Equals("/api/stations", StringComparison.OrdinalIgnoreCase))
                {
                    var stations = _db.GetSavedStations();
                    var json = JsonSerializer.Serialize(new { stations });
                    await WriteJsonResponseAsync(resp, json);
                    return;
                }
                else if (req.HttpMethod == "GET" && path.Equals("/api/downtime-logs", StringComparison.OrdinalIgnoreCase))
                {
                    var filter = req.QueryString["status"] ?? "ALL";
                    var logs = _db.GetDowntimeLogs(filter);
                    var json = JsonSerializer.Serialize(new { logs });
                    await WriteJsonResponseAsync(resp, json);
                    return;
                }
                else if (req.HttpMethod == "GET" && path.Equals("/api/export/csv", StringComparison.OrdinalIgnoreCase))
                {
                    var logs = _db.GetDowntimeLogs("ALL");
                    var csv = CsvExportService.GenerateCsv(logs);
                    var bytes = Encoding.UTF8.GetBytes(csv);
                    resp.ContentType = "text/csv";
                    resp.AddHeader("Content-Disposition", "attachment; filename=\"scada_downtime_audit_log.csv\"");
                    resp.ContentLength64 = bytes.Length;
                    await resp.OutputStream.WriteAsync(bytes, 0, bytes.Length);
                    resp.Close();
                    return;
                }
                else if (req.HttpMethod == "GET" && path.Equals("/api/health", StringComparison.OrdinalIgnoreCase))
                {
                    await WriteJsonResponseAsync(resp, "{\"status\":\"healthy\",\"engine\":\"PlantOps .NET 8 SCADA\"}");
                    return;
                }

                resp.StatusCode = (int)HttpStatusCode.NotFound;
                await WriteJsonResponseAsync(resp, "{\"error\":\"Not Found\"}");
            }
            catch (Exception ex)
            {
                resp.StatusCode = (int)HttpStatusCode.InternalServerError;
                await WriteJsonResponseAsync(resp, $"{{\"error\":\"{ex.Message}\"}}");
            }
        }

        private static async Task WriteJsonResponseAsync(HttpListenerResponse resp, string json)
        {
            var bytes = Encoding.UTF8.GetBytes(json);
            resp.ContentType = "application/json";
            resp.ContentLength64 = bytes.Length;
            await resp.OutputStream.WriteAsync(bytes, 0, bytes.Length);
            resp.Close();
        }

        public void Stop()
        {
            try
            {
                _cts?.Cancel();
                _listener?.Stop();
                _listener?.Close();
            }
            catch { }
            finally
            {
                _listener = null;
            }
        }

        public void Dispose()
        {
            Stop();
            _cts?.Dispose();
        }
    }
}
