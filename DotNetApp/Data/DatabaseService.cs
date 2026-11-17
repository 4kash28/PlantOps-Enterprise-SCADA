using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using DotNetApp.Models;

namespace DotNetApp.Data
{
    public class DatabaseService
    {
        private readonly string _connectionString;
        private readonly string _dbPath;

        public DatabaseService(string? customDbPath = null)
        {
            if (string.IsNullOrWhiteSpace(customDbPath))
            {
                var processPath = Environment.ProcessPath;
                var appDir = !string.IsNullOrEmpty(processPath) 
                    ? Path.GetDirectoryName(processPath) ?? AppDomain.CurrentDomain.BaseDirectory
                    : AppDomain.CurrentDomain.BaseDirectory;
                _dbPath = Path.Combine(appDir, "app_local.db");
            }
            else
            {
                _dbPath = customDbPath;
            }

            _connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = _dbPath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Shared
            }.ToString();

            InitializeDatabase();
        }

        public string DbPath => _dbPath;

        private void EnsureCleanDatabaseFile()
        {
            try
            {
                if (File.Exists(_dbPath))
                {
                    bool isCorrupt = false;
                    var fi = new FileInfo(_dbPath);
                    if (fi.Length < 100)
                    {
                        isCorrupt = true;
                    }
                    else
                    {
                        using var fs = new FileStream(_dbPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        var header = new byte[16];
                        int bytesRead = fs.Read(header, 0, 16);
                        if (bytesRead < 16 || System.Text.Encoding.ASCII.GetString(header) != "SQLite format 3\0")
                        {
                            isCorrupt = true;
                        }
                    }

                    if (isCorrupt)
                    {
                        SqliteConnection.ClearAllPools();
                        try { File.Delete(_dbPath); } catch { }
                        try { File.Delete(_dbPath + "-shm"); } catch { }
                        try { File.Delete(_dbPath + "-wal"); } catch { }
                    }
                }
            }
            catch { }
        }

        public void InitializeDatabase()
        {
            EnsureCleanDatabaseFile();

            try
            {
                RunInitInternal();
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 26 || ex.Message.Contains("file is not a database"))
            {
                SqliteConnection.ClearAllPools();
                try { File.Delete(_dbPath); } catch { }
                try { File.Delete(_dbPath + "-shm"); } catch { }
                try { File.Delete(_dbPath + "-wal"); } catch { }
                RunInitInternal();
            }
        }

        private void RunInitInternal()
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            // Enable WAL mode for high concurrency
            using (var walCmd = connection.CreateCommand())
            {
                walCmd.CommandText = "PRAGMA journal_mode=WAL;";
                walCmd.ExecuteNonQuery();
            }

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS downtime_logs (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    station_id TEXT NOT NULL,
                    station_name TEXT NOT NULL,
                    failure_reason TEXT NOT NULL,
                    start_time TEXT NOT NULL,
                    end_time TEXT,
                    duration_seconds REAL DEFAULT 0,
                    duration_formatted TEXT DEFAULT '00:00:00',
                    status TEXT NOT NULL,
                    created_at TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS settings (
                    key TEXT PRIMARY KEY,
                    value TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS stations (
                    station_id TEXT PRIMARY KEY,
                    station_name TEXT NOT NULL,
                    ip_address TEXT NOT NULL,
                    hostname TEXT NOT NULL,
                    status TEXT NOT NULL,
                    connection_type TEXT NOT NULL,
                    connection_label TEXT,
                    connection_details TEXT,
                    network_latency_ms REAL,
                    last_seen TEXT NOT NULL,
                    zebra_monitored INTEGER NOT NULL,
                    data_json TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS deleted_stations (
                    station_id TEXT PRIMARY KEY,
                    deleted_at TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS threshold_breach_logs (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    station_id TEXT NOT NULL,
                    station_name TEXT NOT NULL,
                    parameter_name TEXT NOT NULL,
                    breach_type TEXT NOT NULL,
                    recorded_value TEXT NOT NULL,
                    threshold_limit TEXT NOT NULL,
                    breach_time TEXT NOT NULL,
                    recovery_time TEXT,
                    duration_above_limit TEXT DEFAULT 'Active',
                    status TEXT NOT NULL,
                    created_at TEXT NOT NULL
                );

                UPDATE stations SET station_name = REPLACE(station_name, ' & Zebra ZT411 Labeling', ' & Packaging') WHERE station_name LIKE '%Zebra%';
                UPDATE stations SET station_name = REPLACE(station_name, ' & Zebra ZT411', '') WHERE station_name LIKE '%Zebra%';
                UPDATE downtime_logs SET station_name = REPLACE(station_name, ' & Zebra ZT411', '') WHERE station_name LIKE '%Zebra%';
                UPDATE downtime_logs SET failure_reason = REPLACE(failure_reason, 'Zebra ZT411 Industrial Printer Fault', 'Peripheral Communication Fault') WHERE failure_reason LIKE '%Zebra%';
            ";
            cmd.ExecuteNonQuery();

            MigrateDowntimeToThresholdBreaches(connection);

            SeedDefaultSettings(connection);
        }

        private void MigrateDowntimeToThresholdBreaches(SqliteConnection connection)
        {
            try
            {
                using var checkCmd = connection.CreateCommand();
                checkCmd.CommandText = "SELECT COUNT(*) FROM threshold_breach_logs;";
                var count = Convert.ToInt64(checkCmd.ExecuteScalar());
                if (count == 0)
                {
                    using var migrateCmd = connection.CreateCommand();
                    migrateCmd.CommandText = @"
                        INSERT INTO threshold_breach_logs (station_id, station_name, parameter_name, breach_type, recorded_value, threshold_limit, breach_time, recovery_time, duration_above_limit, status, created_at)
                        SELECT 
                            station_id,
                            station_name,
                            CASE 
                                WHEN failure_reason LIKE '%RAM%' THEN 'RAM Physical Commit'
                                WHEN failure_reason LIKE '%CPU%' THEN 'CPU Workload'
                                WHEN failure_reason LIKE '%Heartbeat%' OR failure_reason LIKE '%Offline%' THEN 'Station Heartbeat Offline'
                                ELSE 'System Parameter'
                            END,
                            'CRITICAL',
                            CASE 
                                WHEN failure_reason LIKE '%RAM%' THEN 'RAM Over Limit'
                                WHEN failure_reason LIKE '%CPU%' THEN 'CPU Over Limit'
                                WHEN failure_reason LIKE '%Heartbeat%' THEN 'Offline (>10s)'
                                ELSE 'Limit Exceeded'
                            END,
                            CASE 
                                WHEN failure_reason LIKE '%RAM%' THEN '88.0% Max'
                                WHEN failure_reason LIKE '%CPU%' THEN '85.0% Max'
                                WHEN failure_reason LIKE '%Heartbeat%' THEN '10s Timeout'
                                ELSE 'Configured Limit'
                            END,
                            start_time,
                            COALESCE(end_time, '--'),
                            COALESCE(duration_formatted, 'Active'),
                            CASE WHEN status = 'ACTIVE' THEN 'ACTIVE' ELSE 'NORMALIZED' END,
                            created_at
                        FROM downtime_logs;
                    ";
                    migrateCmd.ExecuteNonQuery();
                }
            }
            catch { }
        }

        private void SeedDefaultSettings(SqliteConnection connection)
        {
            using var checkCmd = connection.CreateCommand();
            checkCmd.CommandText = "SELECT COUNT(*) FROM settings WHERE key = 'thresholds';";
            var count = Convert.ToInt64(checkCmd.ExecuteScalar());

            if (count == 0)
            {
                var defaults = new ThresholdSettings();
                var json = JsonSerializer.Serialize(defaults);

                using var insertCmd = connection.CreateCommand();
                insertCmd.CommandText = "INSERT INTO settings (key, value) VALUES ('thresholds', @val);";
                insertCmd.Parameters.AddWithValue("@val", json);
                insertCmd.ExecuteNonQuery();
            }
        }

        #region Downtime Logs CRUD

        public List<DowntimeLog> GetDowntimeLogs(string filter = "ALL")
        {
            var list = new List<DowntimeLog>();
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var cmd = connection.CreateCommand();
            string query = "SELECT id, station_id, station_name, failure_reason, start_time, end_time, duration_seconds, duration_formatted, status, created_at FROM downtime_logs";

            if (filter.Equals("ACTIVE", StringComparison.OrdinalIgnoreCase))
            {
                query += " WHERE status = 'ACTIVE'";
            }
            else if (filter.Equals("RESOLVED", StringComparison.OrdinalIgnoreCase))
            {
                query += " WHERE status = 'RESOLVED'";
            }

            query += " ORDER BY id DESC;";
            cmd.CommandText = query;

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new DowntimeLog
                {
                    Id = reader.GetInt64(0),
                    StationId = reader.GetString(1),
                    StationName = reader.GetString(2),
                    FailureReason = reader.GetString(3),
                    StartTime = reader.GetString(4),
                    EndTime = reader.IsDBNull(5) ? null : reader.GetString(5),
                    DurationSeconds = reader.GetDouble(6),
                    DurationFormatted = reader.GetString(7),
                    Status = reader.GetString(8),
                    CreatedAt = reader.GetString(9)
                });
            }

            return list;
        }

        public long InsertDowntimeStart(string stationId, string stationName, string reason)
        {
            var now = DateTime.UtcNow.ToString("o");
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO downtime_logs (station_id, station_name, failure_reason, start_time, status, created_at)
                VALUES (@sid, @sname, @reason, @start, 'ACTIVE', @start);
                SELECT last_insert_rowid();
            ";
            cmd.Parameters.AddWithValue("@sid", stationId);
            cmd.Parameters.AddWithValue("@sname", stationName);
            cmd.Parameters.AddWithValue("@reason", reason);
            cmd.Parameters.AddWithValue("@start", now);

            return Convert.ToInt64(cmd.ExecuteScalar());
        }

        public void CloseDowntime(long incidentId)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            string? startTimeStr = null;
            using (var selectCmd = connection.CreateCommand())
            {
                selectCmd.CommandText = "SELECT start_time FROM downtime_logs WHERE id = @id AND status = 'ACTIVE';";
                selectCmd.Parameters.AddWithValue("@id", incidentId);
                var obj = selectCmd.ExecuteScalar();
                if (obj != null && obj != DBNull.Value)
                {
                    startTimeStr = obj.ToString();
                }
            }

            if (string.IsNullOrEmpty(startTimeStr)) return;

            var now = DateTime.UtcNow;
            var endIso = now.ToString("o");
            double durationSec = 0;

            if (DateTime.TryParse(startTimeStr, out var startDt))
            {
                durationSec = Math.Max(0, (now - startDt.ToUniversalTime()).TotalSeconds);
            }

            int h = (int)(durationSec / 3600);
            int m = (int)((durationSec % 3600) / 60);
            int s = (int)(durationSec % 60);
            string formatted = $"{h:D2}:{m:D2}:{s:D2}";

            using var updateCmd = connection.CreateCommand();
            updateCmd.CommandText = @"
                UPDATE downtime_logs
                SET end_time = @end, duration_seconds = @sec, duration_formatted = @fmt, status = 'RESOLVED'
                WHERE id = @id;
            ";
            updateCmd.Parameters.AddWithValue("@end", endIso);
            updateCmd.Parameters.AddWithValue("@sec", Math.Round(durationSec, 2));
            updateCmd.Parameters.AddWithValue("@fmt", formatted);
            updateCmd.Parameters.AddWithValue("@id", incidentId);
            updateCmd.ExecuteNonQuery();
        }

        #endregion

        #region Threshold Breach Logs & Station Frequency Statistics

        public List<ThresholdBreachLog> GetThresholdBreachLogs(string filter = "ALL")
        {
            var list = new List<ThresholdBreachLog>();
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var cmd = connection.CreateCommand();
            string query = "SELECT id, station_id, station_name, parameter_name, breach_type, recorded_value, threshold_limit, breach_time, recovery_time, duration_above_limit, status, created_at FROM threshold_breach_logs";

            if (filter.Equals("ACTIVE", StringComparison.OrdinalIgnoreCase))
            {
                query += " WHERE status = 'ACTIVE'";
            }
            else if (filter.Equals("NORMALIZED", StringComparison.OrdinalIgnoreCase) || filter.Equals("RESOLVED", StringComparison.OrdinalIgnoreCase))
            {
                query += " WHERE status = 'NORMALIZED'";
            }

            query += " ORDER BY id DESC;";
            cmd.CommandText = query;

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new ThresholdBreachLog
                {
                    Id = reader.GetInt64(0),
                    StationId = reader.GetString(1),
                    StationName = reader.GetString(2),
                    ParameterName = reader.GetString(3),
                    BreachType = reader.GetString(4),
                    RecordedValue = reader.GetString(5),
                    ThresholdLimit = reader.GetString(6),
                    BreachTime = FormatLocalTimestamp(reader.IsDBNull(7) ? "--" : reader.GetString(7)),
                    RecoveryTime = reader.IsDBNull(8) ? "--" : FormatLocalTimestamp(reader.GetString(8)),
                    DurationAboveThreshold = reader.IsDBNull(9) ? "Active" : reader.GetString(9),
                    Status = reader.GetString(10),
                    CreatedAt = reader.IsDBNull(11) ? "--" : reader.GetString(11)
                });
            }

            return list;
        }

        public long InsertThresholdBreach(string stationId, string stationName, string parameterName, string breachType, string recordedValue, string thresholdLimit)
        {
            var now = DateTime.UtcNow.ToString("o");
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO threshold_breach_logs (station_id, station_name, parameter_name, breach_type, recorded_value, threshold_limit, breach_time, duration_above_limit, status, created_at)
                VALUES (@sid, @sname, @param, @btype, @val, @limit, @btime, 'Active', 'ACTIVE', @btime);
                SELECT last_insert_rowid();
            ";
            cmd.Parameters.AddWithValue("@sid", stationId);
            cmd.Parameters.AddWithValue("@sname", stationName);
            cmd.Parameters.AddWithValue("@param", parameterName);
            cmd.Parameters.AddWithValue("@btype", breachType);
            cmd.Parameters.AddWithValue("@val", recordedValue);
            cmd.Parameters.AddWithValue("@limit", thresholdLimit);
            cmd.Parameters.AddWithValue("@btime", now);

            return Convert.ToInt64(cmd.ExecuteScalar());
        }

        public void CloseThresholdBreach(long breachId)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            string? breachTimeStr = null;
            using (var selectCmd = connection.CreateCommand())
            {
                selectCmd.CommandText = "SELECT breach_time FROM threshold_breach_logs WHERE id = @id AND status = 'ACTIVE';";
                selectCmd.Parameters.AddWithValue("@id", breachId);
                var obj = selectCmd.ExecuteScalar();
                if (obj != null && obj != DBNull.Value)
                {
                    breachTimeStr = obj.ToString();
                }
            }

            if (string.IsNullOrEmpty(breachTimeStr)) return;

            var now = DateTime.UtcNow;
            var endIso = now.ToString("o");
            double durationSec = 0;

            if (DateTime.TryParse(breachTimeStr, out var startDt))
            {
                durationSec = Math.Max(0, (now - startDt.ToUniversalTime()).TotalSeconds);
            }

            int h = (int)(durationSec / 3600);
            int m = (int)((durationSec % 3600) / 60);
            int s = (int)(durationSec % 60);
            string formatted = $"{h:D2}:{m:D2}:{s:D2}";

            using var updateCmd = connection.CreateCommand();
            updateCmd.CommandText = @"
                UPDATE threshold_breach_logs
                SET recovery_time = @rec,
                    duration_above_limit = @dur,
                    status = 'NORMALIZED'
                WHERE id = @id;
            ";
            updateCmd.Parameters.AddWithValue("@rec", endIso);
            updateCmd.Parameters.AddWithValue("@dur", formatted);
            updateCmd.Parameters.AddWithValue("@id", breachId);
            updateCmd.ExecuteNonQuery();
        }

        public List<StationBreachSummary> GetStationBreachSummaries()
        {
            var list = new List<StationBreachSummary>();
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT 
                    station_id, 
                    station_name, 
                    COUNT(*) as total_breaches,
                    SUM(CASE WHEN parameter_name LIKE '%CPU%' THEN 1 ELSE 0 END) as cpu_count,
                    SUM(CASE WHEN parameter_name LIKE '%RAM%' THEN 1 ELSE 0 END) as ram_count,
                    SUM(CASE WHEN parameter_name LIKE '%Temp%' OR parameter_name LIKE '%Prefetch%' THEN 1 ELSE 0 END) as temp_count,
                    SUM(CASE WHEN parameter_name LIKE '%Disk%' OR parameter_name LIKE '%Storage%' THEN 1 ELSE 0 END) as disk_count,
                    SUM(CASE WHEN parameter_name LIKE '%Heartbeat%' OR parameter_name LIKE '%Offline%' OR parameter_name LIKE '%Timeout%' THEN 1 ELSE 0 END) as heartbeat_count,
                    MAX(breach_time) as last_breach,
                    SUM(CASE WHEN status = 'ACTIVE' THEN 1 ELSE 0 END) as active_count
                FROM threshold_breach_logs
                GROUP BY station_id, station_name
                ORDER BY total_breaches DESC;
            ";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                int activeCount = reader.IsDBNull(9) ? 0 : Convert.ToInt32(reader.GetInt64(9));
                list.Add(new StationBreachSummary
                {
                    StationId = reader.GetString(0),
                    StationName = reader.GetString(1),
                    TotalBreachCount = Convert.ToInt32(reader.GetInt64(2)),
                    CpuBreachCount = reader.IsDBNull(3) ? 0 : Convert.ToInt32(reader.GetInt64(3)),
                    RamBreachCount = reader.IsDBNull(4) ? 0 : Convert.ToInt32(reader.GetInt64(4)),
                    TempBreachCount = reader.IsDBNull(5) ? 0 : Convert.ToInt32(reader.GetInt64(5)),
                    DiskBreachCount = reader.IsDBNull(6) ? 0 : Convert.ToInt32(reader.GetInt64(6)),
                    HeartbeatBreachCount = reader.IsDBNull(7) ? 0 : Convert.ToInt32(reader.GetInt64(7)),
                    LastBreachTime = reader.IsDBNull(8) ? "--" : FormatLocalTimestamp(reader.GetString(8)),
                    CurrentState = activeCount > 0 ? "IN VIOLATION" : "NORMAL"
                });
            }

            return list;
        }

        private static string FormatLocalTimestamp(string? isoUtc)
        {
            if (string.IsNullOrWhiteSpace(isoUtc) || isoUtc == "--") return "--";
            if (DateTime.TryParse(isoUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
            {
                return dt.ToLocalTime().ToString("yyyy-MM-dd hh:mm:ss tt");
            }
            if (DateTime.TryParse(isoUtc, out var dtFallback))
            {
                return dtFallback.ToLocalTime().ToString("yyyy-MM-dd hh:mm:ss tt");
            }
            return isoUtc;
        }

        public void ClearAllAuditLogs()
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                DELETE FROM threshold_breach_logs;
                DELETE FROM downtime_logs;
                DELETE FROM sqlite_sequence WHERE name IN ('threshold_breach_logs', 'downtime_logs');
            ";
            cmd.ExecuteNonQuery();
        }

        #endregion

        #region Settings CRUD

        public void SaveSetting(string key, string value)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO settings (key, value) VALUES (@key, @val)
                ON CONFLICT(key) DO UPDATE SET value = excluded.value;
            ";
            cmd.Parameters.AddWithValue("@key", key);
            cmd.Parameters.AddWithValue("@val", value);
            cmd.ExecuteNonQuery();
        }

        public string GetSetting(string key, string defaultValue = "")
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT value FROM settings WHERE key = @key;";
            cmd.Parameters.AddWithValue("@key", key);

            var result = cmd.ExecuteScalar();
            return result != null ? result.ToString() ?? defaultValue : defaultValue;
        }

        public ThresholdSettings GetThresholdSettings()
        {
            var json = GetSetting("thresholds", "");
            if (string.IsNullOrWhiteSpace(json)) return new ThresholdSettings();

            try
            {
                return JsonSerializer.Deserialize<ThresholdSettings>(json) ?? new ThresholdSettings();
            }
            catch
            {
                return new ThresholdSettings();
            }
        }

        public void SaveThresholdSettings(ThresholdSettings settings)
        {
            var json = JsonSerializer.Serialize(settings);
            SaveSetting("thresholds", json);
        }

        #endregion

        #region Stations & Deregistered Nodes CRUD

        public List<Station> GetSavedStations()
        {
            var list = new List<Station>();
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT data_json FROM stations ORDER BY station_id;";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var json = reader.GetString(0);
                try
                {
                    var st = JsonSerializer.Deserialize<Station>(json);
                    if (st != null)
                    {
                        if (!string.IsNullOrEmpty(st.StationName))
                        {
                            st.StationName = st.StationName
                                .Replace(" & Zebra ZT411 Labeling", " & Packaging")
                                .Replace(" & Zebra ZT411", "")
                                .Replace("Zebra ZT411", "")
                                .Trim();
                        }
                        list.Add(st);
                    }
                }
                catch { }
            }

            return list;
        }

        public void UpsertStation(Station station)
        {
            if (!string.IsNullOrEmpty(station.StationName))
            {
                station.StationName = station.StationName
                    .Replace(" & Zebra ZT411 Labeling", " & Packaging")
                    .Replace(" & Zebra ZT411", "")
                    .Replace("Zebra ZT411", "")
                    .Trim();
            }

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var json = JsonSerializer.Serialize(station);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO stations (station_id, station_name, ip_address, hostname, status, connection_type, connection_label, connection_details, network_latency_ms, last_seen, zebra_monitored, data_json)
                VALUES (@id, @name, @ip, @host, @status, @ctype, @clabel, @cdetails, @latency, @lastseen, @zebra, @json)
                ON CONFLICT(station_id) DO UPDATE SET
                    station_name = excluded.station_name,
                    ip_address = excluded.ip_address,
                    hostname = excluded.hostname,
                    status = excluded.status,
                    connection_type = excluded.connection_type,
                    connection_label = excluded.connection_label,
                    connection_details = excluded.connection_details,
                    network_latency_ms = excluded.network_latency_ms,
                    last_seen = excluded.last_seen,
                    zebra_monitored = excluded.zebra_monitored,
                    data_json = excluded.data_json;
            ";
            cmd.Parameters.AddWithValue("@id", station.StationId);
            cmd.Parameters.AddWithValue("@name", station.StationName);
            cmd.Parameters.AddWithValue("@ip", station.IpAddress);
            cmd.Parameters.AddWithValue("@host", station.Hostname);
            cmd.Parameters.AddWithValue("@status", station.Status);
            cmd.Parameters.AddWithValue("@ctype", station.ConnectionType);
            cmd.Parameters.AddWithValue("@clabel", station.ConnectionLabel ?? "");
            cmd.Parameters.AddWithValue("@cdetails", station.ConnectionDetails ?? "");
            cmd.Parameters.AddWithValue("@latency", station.NetworkLatencyMs);
            cmd.Parameters.AddWithValue("@lastseen", station.LastSeen.ToString("o"));
            cmd.Parameters.AddWithValue("@zebra", station.ZebraMonitored ? 1 : 0);
            cmd.Parameters.AddWithValue("@json", json);

            cmd.ExecuteNonQuery();
        }

        public void DeleteStation(string stationId)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                DELETE FROM stations WHERE station_id = @id;
                DELETE FROM threshold_breach_logs WHERE station_id = @id;
                DELETE FROM downtime_logs WHERE station_id = @id;
            ";
            cmd.Parameters.AddWithValue("@id", stationId);
            cmd.ExecuteNonQuery();

            AddDeletedStation(stationId);
        }

        public HashSet<string> GetDeletedStations()
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT station_id FROM deleted_stations;";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                set.Add(reader.GetString(0));
            }

            return set;
        }

        public void AddDeletedStation(string stationId)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO deleted_stations (station_id, deleted_at)
                VALUES (@id, @now)
                ON CONFLICT(station_id) DO UPDATE SET deleted_at = excluded.deleted_at;
            ";
            cmd.Parameters.AddWithValue("@id", stationId);
            cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("o"));
            cmd.ExecuteNonQuery();
        }

        public void RestoreDeletedStation(string stationId)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM deleted_stations WHERE station_id = @id;";
            cmd.Parameters.AddWithValue("@id", stationId);
            cmd.ExecuteNonQuery();
        }

        #endregion
    }
}
