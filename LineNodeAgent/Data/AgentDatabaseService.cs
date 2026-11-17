using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;
using LineNodeAgent.Models;

namespace LineNodeAgent.Data
{
    public class AgentDatabaseService
    {
        private readonly string _dbPath;
        private readonly string _connectionString;

        public AgentDatabaseService(string? customPath = null)
        {
            if (string.IsNullOrWhiteSpace(customPath))
            {
                var appDir = AppDomain.CurrentDomain.BaseDirectory;
                _dbPath = Path.Combine(appDir, "agent_config.db");
            }
            else
            {
                _dbPath = customPath;
            }

            _connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = _dbPath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Shared,
                DefaultTimeout = 15
            }.ToString();

            InitializeDatabase();
        }

        public string DbPath => _dbPath;

        public void InitializeDatabase()
        {
            try
            {
                using var conn = new SqliteConnection(_connectionString);
                conn.Open();

                using (var walCmd = conn.CreateCommand())
                {
                    walCmd.CommandText = "PRAGMA journal_mode=WAL;";
                    walCmd.ExecuteNonQuery();
                }

                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS settings (
                        key TEXT PRIMARY KEY,
                        value TEXT NOT NULL
                    );

                    CREATE TABLE IF NOT EXISTS function_permissions (
                        key TEXT PRIMARY KEY,
                        display_name TEXT NOT NULL,
                        category TEXT NOT NULL,
                        is_enabled INTEGER NOT NULL DEFAULT 1,
                        description TEXT NOT NULL
                    );

                    DELETE FROM function_permissions WHERE key = 'USB_Printers';

                    UPDATE settings 
                    SET value = REPLACE(REPLACE(value, ' & Zebra ZT411 Labeling', ' & Packaging'), ' & Zebra ZT411', '') 
                    WHERE key = 'station_friendly_name' AND value LIKE '%Zebra%';
                ";
                cmd.ExecuteNonQuery();

                SeedDefaultFunctions(conn);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AgentDatabaseService] Init error: {ex.Message}");
            }
        }

        private void SeedDefaultFunctions(SqliteConnection conn)
        {
            var defaultFunctions = new List<(string key, string name, string cat, int enabled, string desc)>
            {
                ("CPU_Usage", "CPU Load (%)", "Hardware Core", 1, "Real-time processor core load percentage"),
                ("RAM_Usage", "RAM Memory (GB & %)", "Hardware Core", 1, "Physical RAM commit, used GB & total GB"),
                ("Disk_Space", "C: Drive Storage", "Hardware Core", 1, "NVMe / SSD storage free space and %"),
                ("CPU_Temperature", "CPU Temperature (°C)", "Hardware Core", 1, "Thermal sensor / WMI CPU core temperature"),
                ("System_Uptime", "System Running Uptime", "Hardware Core", 1, "Continuous operating system uptime (dd:hh:mm:ss)"),
                ("Network_Ping", "Network Latency & Ping", "Network & Comms", 1, "Gateway & SCADA server roundtrip latency (ms)"),
                ("Active_User_Session", "Active Windows Operator", "Security & System", 1, "Logged-in Windows user session & idle duration"),
                ("Critical_Processes", "Critical Shop-Floor Apps", "Security & System", 1, "Key manufacturing software & process running state"),
                ("Windows_Services", "Core Windows Services", "Security & System", 1, "Print Spooler and network service health"),
                ("USB_Peripherals", "USB Devices & Scanners", "Peripherals & Devices", 1, "Connected barcode scanners & USB peripheral health"),
                ("Temp_Prefetch_Junk", "Temp & Prefetch Junk Storage", "Storage & Maintenance", 1, "Count & disk usage of %TEMP%, C:\\Windows\\Temp, and Prefetch .pf files")
            };

            foreach (var fn in defaultFunctions)
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO function_permissions (key, display_name, category, is_enabled, description)
                    VALUES (@k, @n, @c, @e, @d)
                    ON CONFLICT(key) DO NOTHING;
                ";
                cmd.Parameters.AddWithValue("@k", fn.key);
                cmd.Parameters.AddWithValue("@n", fn.name);
                cmd.Parameters.AddWithValue("@c", fn.cat);
                cmd.Parameters.AddWithValue("@e", fn.enabled);
                cmd.Parameters.AddWithValue("@d", fn.desc);
                cmd.ExecuteNonQuery();
            }
        }

        public AgentSettings LoadSettings()
        {
            var s = new AgentSettings();
            try
            {
                using var conn = new SqliteConnection(_connectionString);
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT key, value FROM settings;";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    var k = reader.GetString(0);
                    var v = reader.GetString(1);
                    switch (k)
                    {
                        case "node_name": s.NodeName = v; break;
                        case "station_id": s.LineStationId = v; break;
                        case "station_friendly_name": s.StationFriendlyName = v; break;
                        case "server_url": s.ServerUrl = v; break;
                        case "polling_interval": if (int.TryParse(v, out var iv)) s.PollingIntervalSeconds = iv; break;
                        case "minimize_to_tray": if (bool.TryParse(v, out var mt)) s.MinimizeToTray = mt; break;
                    }
                }
            }
            catch { }
            return s;
        }

        public void SaveSettings(AgentSettings s)
        {
            try
            {
                using var conn = new SqliteConnection(_connectionString);
                conn.Open();
                using var tx = conn.BeginTransaction();

                SaveKey(conn, tx, "node_name", s.NodeName);
                SaveKey(conn, tx, "station_id", s.LineStationId);
                SaveKey(conn, tx, "station_friendly_name", s.StationFriendlyName);
                SaveKey(conn, tx, "server_url", s.ServerUrl);
                SaveKey(conn, tx, "polling_interval", s.PollingIntervalSeconds.ToString());
                SaveKey(conn, tx, "minimize_to_tray", s.MinimizeToTray.ToString());

                tx.Commit();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AgentDatabaseService] SaveSettings error: {ex.Message}");
            }
        }

        private void SaveKey(SqliteConnection conn, SqliteTransaction tx, string k, string v)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = @"
                INSERT INTO settings (key, value) VALUES (@k, @v)
                ON CONFLICT(key) DO UPDATE SET value = excluded.value;
            ";
            cmd.Parameters.AddWithValue("@k", k);
            cmd.Parameters.AddWithValue("@v", v);
            cmd.ExecuteNonQuery();
        }

        public List<AgentFunctionItem> LoadFunctions()
        {
            var list = new List<AgentFunctionItem>();
            try
            {
                using var conn = new SqliteConnection(_connectionString);
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT key, display_name, category, is_enabled, description FROM function_permissions ORDER BY rowid ASC;";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    list.Add(new AgentFunctionItem
                    {
                        Key = reader.GetString(0),
                        DisplayName = reader.GetString(1),
                        Category = reader.GetString(2),
                        IsEnabled = reader.GetInt32(3) == 1,
                        Description = reader.GetString(4)
                    });
                }
            }
            catch { }
            return list;
        }

        public void SaveFunctions(IEnumerable<AgentFunctionItem> functions)
        {
            try
            {
                using var conn = new SqliteConnection(_connectionString);
                conn.Open();
                using var tx = conn.BeginTransaction();

                foreach (var fn in functions)
                {
                    using var cmd = conn.CreateCommand();
                    cmd.Transaction = tx;
                    cmd.CommandText = @"
                        UPDATE function_permissions 
                        SET is_enabled = @e, display_name = @n, category = @c, description = @d
                        WHERE key = @k;
                    ";
                    cmd.Parameters.AddWithValue("@e", fn.IsEnabled ? 1 : 0);
                    cmd.Parameters.AddWithValue("@n", fn.DisplayName);
                    cmd.Parameters.AddWithValue("@c", fn.Category);
                    cmd.Parameters.AddWithValue("@d", fn.Description);
                    cmd.Parameters.AddWithValue("@k", fn.Key);
                    cmd.ExecuteNonQuery();
                }

                tx.Commit();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AgentDatabaseService] SaveFunctions error: {ex.Message}");
            }
        }
    }
}
