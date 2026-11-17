"""
PlantOps Monitor - Central Server & Industrial Dashboard Backend
Production Grade Monitoring Server for Room Air Conditioner (RAC) Assembly Lines.
Features FastAPI, WebSockets, SQLite Downtime Persistence, CSV Export & Real-time Alerts.
"""

import sys
import os
import io
import csv
import json
import time
import socket
import threading
import sqlite3
import logging
import datetime
import subprocess
import re
import ipaddress
from pathlib import Path
from typing import Dict, Any, List, Optional
from contextlib import asynccontextmanager

from fastapi import FastAPI, WebSocket, WebSocketDisconnect, HTTPException, Response
from fastapi.staticfiles import StaticFiles
from fastapi.responses import HTMLResponse, StreamingResponse
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel, Field

# Setup Logging
LOG_FORMAT = "%(asctime)s [%(levelname)s] [PlantOpsServer] %(message)s"
logging.basicConfig(level=logging.INFO, format=LOG_FORMAT)
logger = logging.getLogger("PlantOpsServer")

# Application Directories (handles PyInstaller frozen .exe vs source run)
if getattr(sys, 'frozen', False):
    APP_DIR = Path(sys.executable).parent.resolve()
    BUNDLE_DIR = Path(getattr(sys, '_MEIPASS', APP_DIR))
else:
    APP_DIR = Path(__file__).parent.resolve()
    BUNDLE_DIR = APP_DIR

DB_PATH = APP_DIR / "plantops_monitor.db"
CONFIG_PATH = APP_DIR / "config.json" if (APP_DIR / "config.json").exists() else (BUNDLE_DIR / "config.json")


# -------------------------------------------------------------------------
# Database Layer (Thread-Safe WAL Mode)
# -------------------------------------------------------------------------
def get_db():
    conn = sqlite3.connect(DB_PATH, timeout=15.0)
    conn.row_factory = sqlite3.Row
    return conn


def init_db():
    """Initializes SQLite schema for downtime logs and runtime settings in WAL mode."""
    conn = get_db()
    cursor = conn.cursor()
    cursor.execute("PRAGMA journal_mode=WAL;")
    cursor.execute("PRAGMA synchronous=NORMAL;")
    cursor.execute("PRAGMA busy_timeout=15000;")
    cursor.execute("""
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
    )
    """)

    cursor.execute("""
    CREATE TABLE IF NOT EXISTS settings (
        key TEXT PRIMARY KEY,
        value TEXT NOT NULL
    )
    """)
    conn.commit()
    conn.close()
    logger.info("Database initialized successfully at %s", DB_PATH)


def record_downtime_start(station_id: str, station_name: str, reason: str) -> int:
    """Inserts a new active downtime incident."""
    now = datetime.datetime.now(datetime.timezone.utc)
    start_time_iso = now.isoformat()
    conn = get_db()
    cursor = conn.cursor()
    cursor.execute("""
    INSERT INTO downtime_logs (station_id, station_name, failure_reason, start_time, status, created_at)
    VALUES (?, ?, ?, ?, 'ACTIVE', ?)
    """, (station_id, station_name, reason, start_time_iso, start_time_iso))
    incident_id = cursor.lastrowid
    conn.commit()
    conn.close()
    logger.warning(f"[DOWNTIME DETECTED] Incident #{incident_id} opened for {station_id} ({station_name}): {reason}")
    return incident_id


def record_downtime_end(incident_id: int):
    """Closes an active downtime incident and computes duration."""
    conn = get_db()
    cursor = conn.cursor()
    cursor.execute("SELECT start_time FROM downtime_logs WHERE id = ? AND status = 'ACTIVE'", (incident_id,))
    row = cursor.fetchone()
    if not row:
        conn.close()
        return

    now = datetime.datetime.now(datetime.timezone.utc)
    end_time_iso = now.isoformat()
    try:
        start_dt = datetime.datetime.fromisoformat(row["start_time"])
        duration_sec = max(0.0, (now - start_dt).total_seconds())
    except Exception:
        duration_sec = 0.0

    hours = int(duration_sec // 3600)
    minutes = int((duration_sec % 3600) // 60)
    seconds = int(duration_sec % 60)
    dur_fmt = f"{hours:02d}:{minutes:02d}:{seconds:02d}"

    cursor.execute("""
    UPDATE downtime_logs 
    SET end_time = ?, duration_seconds = ?, duration_formatted = ?, status = 'RESOLVED'
    WHERE id = ?
    """, (end_time_iso, round(duration_sec, 2), dur_fmt, incident_id))
    conn.commit()
    conn.close()
    logger.info(f"[DOWNTIME RESOLVED] Incident #{incident_id} closed. Duration: {dur_fmt}")


# -------------------------------------------------------------------------
# State Manager & Watchdog
# -------------------------------------------------------------------------
class StationState:
    def __init__(self, config_stations: List[Dict[str, Any]], thresholds: Dict[str, Any]):
        self.thresholds = thresholds
        self.stations: Dict[str, Dict[str, Any]] = {}
        self.deleted_stations: set = set()
        # Start completely clean. Workstations are dynamically auto-discovered when LineNode Agents connect.
        for s in (config_stations or []):
            sid = s.get("station_id")
            if sid:
                self.stations[sid] = {
                    "station_id": sid,
                    "station_name": s.get("station_name", sid),
                    "ip_address": s.get("ip_address", "Pending Connection"),
                    "status": "OFFLINE",
                    "last_heartbeat": 0,
                    "zebra_monitored": s.get("zebra_monitored", False),
                    "active_offline_incident_id": None,
                    "active_printer_incident_id": None,
                    "system_telemetry": {
                        "cpu_percent": 0.0,
                        "cpu_temp_c": None,
                        "ram_total_gb": 0.0,
                        "ram_used_gb": 0.0,
                        "ram_percent": 0.0,
                        "disks": [],
                        "uptime_seconds": 0,
                        "uptime_formatted": "0d 00:00:00",
                        "network_latency_ms": -1.0
                    },
                    "zebra_printer": {
                        "monitored": s.get("zebra_monitored", False),
                        "connected": False,
                        "status": "DISCONNECTED",
                        "status_message": "Awaiting initial agent transmission",
                        "model_name": "Zebra ZT411",
                        "device_id": None,
                        "spooler_flags": []
                    }
                }

    def update_telemetry(self, payload: Dict[str, Any]) -> Dict[str, Any]:
        """Ingests telemetry from LineNode Agent with dynamic multi-workstation disambiguation."""
        sid = payload.get("station_id", "LINE1_STATION_01")
        if sid in self.deleted_stations:
            logger.debug(f"Telemetry ignored: Station '{sid}' is marked deleted/deregistered.")
            return {"status": "deregistered", "station_id": sid, "should_stop": True}

        incoming_host = payload.get("hostname", "Unknown Host")
        incoming_ip = payload.get("ip_address", "Unknown")
        now = time.time()

        # Disambiguate if another active machine is already using this station_id
        if sid in self.stations:
            existing = self.stations[sid]
            existing_host = existing.get("hostname", "")
            is_active = (now - existing.get("last_heartbeat", 0) < 20.0)
            if is_active and existing_host and incoming_host and existing_host.upper() != incoming_host.upper():
                # Conflict! Different PC sending same station_id. Disambiguate so both get their own card.
                sid = f"{sid}_{incoming_host.upper()}"
                payload["station_id"] = sid
                if "station_name" in payload:
                    payload["station_name"] = f"{payload['station_name']} ({incoming_host})"

        # Subnet check if enforce_subnet is active
        net_prof = SERVER_CONFIG.get("network_profile", {})
        if net_prof.get("enforce_subnet") and net_prof.get("subnet_cidr") and net_prof["subnet_cidr"] != "N/A":
            try:
                allowed_net = ipaddress.IPv4Network(net_prof["subnet_cidr"], strict=False)
                if incoming_ip and incoming_ip != "127.0.0.1" and not incoming_ip.startswith("127."):
                    if ipaddress.IPv4Address(incoming_ip) not in allowed_net:
                        logger.warning(f"Telemetry filtered: {incoming_ip} ({incoming_host}) not in subnet {allowed_net}")
                        return self.stations.get(sid, {})
            except Exception:
                pass

        if sid not in self.stations:
            # Dynamically register new station
            self.stations[sid] = {
                "station_id": sid,
                "station_name": payload.get("station_name", sid),
                "hostname": incoming_host,
                "ip_address": incoming_ip,
                "connection_type": payload.get("connection_type", "LAN"),
                "connection_label": payload.get("connection_label", "LAN"),
                "connection_details": payload.get("connection_details", ""),
                "permissions": payload.get("permissions", {
                    "cpu": True, "ram": True, "disk": True, "network": True, "uptime": True, "zebra": False, "allow_remote_stop": True
                }),
                "status": "NORMAL",
                "last_heartbeat": now,
                "zebra_monitored": payload.get("zebra_printer", {}).get("monitored", False),
                "active_offline_incident_id": None,
                "active_printer_incident_id": None,
                "system_telemetry": payload.get("system_telemetry", {}),
                "zebra_printer": payload.get("zebra_printer", {})
            }

        station = self.stations[sid]
        was_offline = (station["status"] == "OFFLINE")

        # Recover from offline if active incident exists
        if station["active_offline_incident_id"]:
            record_downtime_end(station["active_offline_incident_id"])
            station["active_offline_incident_id"] = None

        station["last_heartbeat"] = now
        station["hostname"] = payload.get("hostname", station.get("hostname", "Unknown Host"))
        station["ip_address"] = payload.get("ip_address", station["ip_address"])
        station["connection_type"] = payload.get("connection_type", station.get("connection_type", "LAN"))
        station["connection_label"] = payload.get("connection_label", station.get("connection_label", "LAN"))
        station["connection_details"] = payload.get("connection_details", station.get("connection_details", ""))
        station["permissions"] = payload.get("permissions", station.get("permissions", {
            "cpu": True, "ram": True, "disk": True, "network": True, "uptime": True, "zebra": False, "allow_remote_stop": True
        }))
        station["system_telemetry"] = payload.get("system_telemetry", station["system_telemetry"])
        
        if "zebra_printer" in payload:
            station["zebra_printer"] = payload["zebra_printer"]
            station["zebra_monitored"] = payload["zebra_printer"].get("monitored", station["zebra_monitored"])

        # Determine station health status with permission gating
        perms = station.get("permissions", {})
        cpu = station["system_telemetry"].get("cpu_percent")
        cpu_val = cpu if cpu is not None else 0.0
        ram = station["system_telemetry"].get("ram_percent")
        ram_val = ram if ram is not None else 0.0
        lat = station["system_telemetry"].get("network_latency_ms")
        lat_val = lat if (lat is not None and lat >= 0) else 0.0
        cpu_warn = self.thresholds.get("cpu_warning_pct", 85.0)
        ram_warn = self.thresholds.get("ram_warning_pct", 85.0)

        z_status = station["zebra_printer"].get("status", "READY")
        z_monitored = station["zebra_monitored"] and perms.get("zebra", False)

        # Track Zebra Printer Downtime
        if z_monitored:
            if z_status in ("DISCONNECTED", "OFFLINE", "PAPER_OUT", "DOOR_OPEN", "ERROR"):
                if not station["active_printer_incident_id"]:
                    reason = f"Zebra ZT411 Fault: {z_status} ({station['zebra_printer'].get('status_message', '')})"
                    station["active_printer_incident_id"] = record_downtime_start(
                        station["station_id"], station["station_name"], reason
                    )
            elif z_status == "READY":
                if station["active_printer_incident_id"]:
                    record_downtime_end(station["active_printer_incident_id"])
                    station["active_printer_incident_id"] = None

        # Determine aggregate visual status
        if z_monitored and z_status in ("DISCONNECTED", "ERROR", "OFFLINE"):
            station["status"] = "CRITICAL"
        elif (perms.get("cpu", True) and cpu_val >= cpu_warn) or (perms.get("ram", True) and ram_val >= ram_warn) or (perms.get("network", True) and lat_val > self.thresholds.get("ping_warning_ms", 50.0)):
            station["status"] = "WARNING"
        elif z_monitored and z_status in ("PAPER_OUT", "DOOR_OPEN"):
            station["status"] = "WARNING"
        else:
            station["status"] = "NORMAL"

        return station

    def check_watchdog(self) -> List[Dict[str, Any]]:
        """Invoked periodically to detect offline line PCs."""
        now = time.time()
        timeout = self.thresholds.get("heartbeat_timeout_sec", 10)
        alerts = []

        for sid, station in self.stations.items():
            if station["status"] != "OFFLINE":
                if station.get("last_heartbeat", 0) > 0 and (now - station["last_heartbeat"]) > timeout:
                    station["status"] = "OFFLINE"
                    reason = f"Line PC Offline (Heartbeat Timeout > {timeout}s)"
                    if not station["active_offline_incident_id"]:
                        station["active_offline_incident_id"] = record_downtime_start(
                            station["station_id"], station["station_name"], reason
                        )
                    alerts.append({
                        "station_id": sid,
                        "station_name": station["station_name"],
                        "type": "STATION_OFFLINE",
                        "message": f"{station['station_name']} is OFFLINE - No heartbeat for > {timeout}s"
                    })

        return alerts

    def get_summary(self) -> Dict[str, Any]:
        """Calculates global RAC Assembly Line KPIs."""
        total = len(self.stations)
        online = sum(1 for s in self.stations.values() if s["status"] in ("NORMAL", "WARNING"))
        warnings = sum(1 for s in self.stations.values() if s["status"] == "WARNING")
        critical = sum(1 for s in self.stations.values() if s["status"] == "CRITICAL")
        offline = sum(1 for s in self.stations.values() if s["status"] == "OFFLINE")

        # Availability % (100% when no stations connected yet or all online)
        availability_pct = round((online / total * 100.0) if total > 0 else 100.0, 1)

        return {
            "total_stations": total,
            "online_stations": online,
            "warning_stations": warnings,
            "critical_stations": critical,
            "offline_stations": offline,
            "line_availability_pct": availability_pct,
            "timestamp": datetime.datetime.now(datetime.timezone.utc).isoformat()
        }


# -------------------------------------------------------------------------
# Load Configuration & State Initialization
# -------------------------------------------------------------------------
_cached_host_connection = None
_last_host_connection_check = 0.0

def get_host_connection_status(force: bool = False) -> Dict[str, Any]:
    """Detects whether the central SCADA host machine is connected via LAN (Ethernet) or Wi-Fi."""
    global _cached_host_connection, _last_host_connection_check
    now = time.time()
    if not force and _cached_host_connection and (now - _last_host_connection_check < 2.5):
        return _cached_host_connection

    info = {
        "type": "DISCONNECTED",
        "label": "Disconnected",
        "adapter_name": "",
        "ip": "127.0.0.1",
        "details": "No Active Network",
        "is_lan": False,
        "is_wifi": False
    }

    # 1. Active Outbound IP
    try:
        s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        s.settimeout(0.5)
        s.connect(("8.8.8.8", 80))
        active_ip = s.getsockname()[0]
        s.close()
        info["ip"] = active_ip
    except Exception:
        active_ip = "127.0.0.1"

    # 2. Check Wi-Fi state via netsh wlan show interfaces
    is_wifi = False
    wifi_ssid = ""
    wifi_signal = ""
    try:
        startupinfo = None
        if sys.platform == "win32":
            startupinfo = subprocess.STARTUPINFO()
            startupinfo.dwFlags |= subprocess.STARTF_USESHOWWINDOW
        res = subprocess.run(["netsh", "wlan", "show", "interfaces"], capture_output=True, text=True, timeout=2, startupinfo=startupinfo)
        out = res.stdout
        if "State" in out and "connected" in out.lower():
            is_wifi = True
            for line in out.splitlines():
                line = line.strip()
                if line.startswith("SSID") and not line.startswith("SSID name") and not line.startswith("BSSID"):
                    parts = line.split(":", 1)
                    if len(parts) == 2: wifi_ssid = parts[1].strip()
                elif line.startswith("Signal"):
                    parts = line.split(":", 1)
                    if len(parts) == 2: wifi_signal = parts[1].strip()
    except Exception:
        pass

    # 3. Match against psutil network interfaces
    try:
        import psutil
        addrs = psutil.net_if_addrs()
        stats = psutil.net_if_stats()
        matched_name = ""

        for name, addr_list in addrs.items():
            for a in addr_list:
                if a.address == active_ip and not a.address.startswith("127."):
                    matched_name = name
                    break
            if matched_name:
                break

        if not matched_name:
            for name, st in stats.items():
                if st.isup and "loopback" not in name.lower():
                    matched_name = name
                    break

        info["adapter_name"] = matched_name

        if is_wifi:
            info["type"] = "WIFI"
            info["label"] = "Wi-Fi"
            info["is_wifi"] = True
            info["is_lan"] = False
            detail_str = f"SSID: {wifi_ssid}" if wifi_ssid else "Wi-Fi Connected"
            if wifi_signal:
                detail_str += f" ({wifi_signal})"
            info["details"] = detail_str
        elif matched_name:
            lower = matched_name.lower()
            if any(w in lower for w in ['wi-fi', 'wifi', 'wireless', 'wlan']):
                info["type"] = "WIFI"
                info["label"] = "Wi-Fi"
                info["is_wifi"] = True
                info["is_lan"] = False
                info["details"] = "Wi-Fi Connected"
            else:
                info["type"] = "LAN"
                info["label"] = "LAN"
                info["is_lan"] = True
                info["is_wifi"] = False
                info["details"] = f"Ethernet ({matched_name})" if matched_name else "Ethernet LAN"
        else:
            info["type"] = "DISCONNECTED"
            info["label"] = "Disconnected"
            info["details"] = "No Active Network Adapter"
    except Exception as e:
        logger.warning(f"Error checking network status: {e}")

    _cached_host_connection = info
    _last_host_connection_check = now
    return info


def detect_system_network_adapters() -> List[Dict[str, Any]]:
    """Detects active physical adapters, IPv4, Subnet, and Default Gateway via Windows ipconfig."""
    adapters = []
    try:
        startupinfo = None
        if sys.platform == "win32":
            startupinfo = subprocess.STARTUPINFO()
            startupinfo.dwFlags |= subprocess.STARTF_USESHOWWINDOW
        res = subprocess.run(["ipconfig"], capture_output=True, text=True, timeout=3, startupinfo=startupinfo)
        out = res.stdout
        current = None
        for line in out.splitlines():
            line_s = line.strip()
            if not line.startswith(" ") and line_s.endswith(":"):
                current = {"name": line_s[:-1], "ip": None, "subnet": None, "gateway": None}
                adapters.append(current)
            elif current:
                if "IPv4 Address" in line or "IP Address" in line:
                    m = re.search(r':\s*([0-9.]+)', line)
                    if m: current["ip"] = m.group(1)
                elif "Subnet Mask" in line:
                    m = re.search(r':\s*([0-9.]+)', line)
                    if m: current["subnet"] = m.group(1)
                elif "Default Gateway" in line:
                    m = re.search(r':\s*([0-9.]+)', line)
                    if m: current["gateway"] = m.group(1)
        active = [
            a for a in adapters 
            if a.get("ip") and not a["ip"].startswith("127.") and not a["ip"].startswith("169.254.")
        ]
        return active
    except Exception as e:
        logger.warning(f"Error parsing ipconfig: {e}")
        return []


def load_server_config() -> Dict[str, Any]:
    cfg = {}
    if CONFIG_PATH.is_file():
        try:
            with open(CONFIG_PATH, "r", encoding="utf-8") as f:
                cfg = json.load(f)
        except Exception as e:
            logger.error(f"Error reading {CONFIG_PATH}: {e}")

    # Fallback default configuration
    default_cfg = {
        "server": {"host": "0.0.0.0", "port": 8090, "dashboard_title": "PlantOps Enterprise SCADA"},
        "thresholds": {
            "heartbeat_timeout_sec": 10,
            "cpu_warning_pct": 85.0,
            "ram_warning_pct": 85.0,
            "ping_warning_ms": 50.0,
            "ping_critical_ms": 200.0
        },
        "network_profile": {
            "ip": "192.168.19.170",
            "subnet_mask": "255.255.255.0",
            "gateway": "192.168.19.1",
            "port": 8090,
            "subnet_cidr": "192.168.19.0/24",
            "enforce_subnet": False,
            "prompt_on_startup": True
        },
        "stations": []
    }
    for k, v in default_cfg.items():
        if k not in cfg:
            cfg[k] = v
        elif isinstance(v, dict):
            for sub_k, sub_v in v.items():
                if sub_k not in cfg[k]:
                    cfg[k][sub_k] = sub_v
    return cfg


SERVER_CONFIG = load_server_config()
state_manager = StationState(SERVER_CONFIG.get("stations", []), SERVER_CONFIG.get("thresholds", {}))


# -------------------------------------------------------------------------
# WebSocket Connection Manager
# -------------------------------------------------------------------------
class WebSocketManager:
    def __init__(self):
        self.active_connections: List[WebSocket] = []

    async def connect(self, websocket: WebSocket):
        await websocket.accept()
        self.active_connections.append(websocket)

    def disconnect(self, websocket: WebSocket):
        if websocket in self.active_connections:
            self.active_connections.remove(websocket)

    async def broadcast(self, message: Dict[str, Any]):
        dead_sockets = []
        for connection in self.active_connections:
            try:
                await connection.send_json(message)
            except Exception:
                dead_sockets.append(connection)
        for dead in dead_sockets:
            self.disconnect(dead)


ws_manager = WebSocketManager()


# -------------------------------------------------------------------------
# FastAPI Application & Background Tasks
# -------------------------------------------------------------------------
import asyncio

async def watchdog_task():
    """Runs every 1 second to inspect station timeouts and broadcast updates."""
    while True:
        try:
            alerts = state_manager.check_watchdog()
            payload = {
                "type": "TICK",
                "summary": state_manager.get_summary(),
                "stations": list(state_manager.stations.values()),
                "alerts": alerts,
                "host_connection": get_host_connection_status()
            }
            await ws_manager.broadcast(payload)
        except Exception as e:
            logger.error(f"Watchdog tick error: {e}")
        await asyncio.sleep(1.0)


def start_udp_discovery_responder(http_port: int = 8090, udp_port: int = 8091):
    """Listens for LAN UDP discovery broadcasts and announces this server."""
    def responder_loop():
        sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        try:
            sock.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
            sock.bind(("0.0.0.0", udp_port))
            logger.info(f"UDP Auto-Discovery Responder active on 0.0.0.0:{udp_port}")
            while True:
                data, addr = sock.recvfrom(1024)
                text = data.decode("utf-8", errors="ignore").strip()
                if "PLANTOPS_DISCOVER" in text:
                    net_prof = SERVER_CONFIG.get("network_profile", {})
                    primary_ip = net_prof.get("ip")
                    if not primary_ip or primary_ip.startswith("127."):
                        adapters = detect_system_network_adapters()
                        primary_ip = adapters[0]["ip"] if adapters else "127.0.0.1"

                    info = json.dumps({
                        "app": "PlantOps Monitor",
                        "server_ip": primary_ip,
                        "server_port": http_port,
                        "hostname": socket.gethostname(),
                        "subnet": net_prof.get("subnet_cidr", "N/A"),
                        "gateway": net_prof.get("gateway", "N/A")
                    })
                    reply = f"PLANTOPS_OFFER:{info}".encode("utf-8")
                    sock.sendto(reply, addr)
        except Exception as e:
            logger.debug(f"UDP discovery responder loop exit: {e}")
        finally:
            try:
                sock.close()
            except Exception:
                pass

    t = threading.Thread(target=responder_loop, daemon=True, name="UdpDiscoveryResponder")
    t.start()


@asynccontextmanager
async def lifespan(app: FastAPI):
    init_db()
    server_port = SERVER_CONFIG.get("server", {}).get("port", 8090)
    start_udp_discovery_responder(http_port=server_port, udp_port=8091)
    task = asyncio.create_task(watchdog_task())
    logger.info("PlantOps Server lifespan initiated with background watchdog and UDP discovery.")
    yield
    task.cancel()
    logger.info("PlantOps Server background watchdog stopped.")


app = FastAPI(
    title="PlantOps Monitor API",
    description="RAC Assembly Line Monitoring Server",
    version="2.4.0",
    lifespan=lifespan
)

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)


# -------------------------------------------------------------------------
# REST API Endpoints
# -------------------------------------------------------------------------
@app.post("/api/telemetry")
async def receive_telemetry(payload: Dict[str, Any]):
    """Receives JSON heartbeat packet from LineNode Agents."""
    sid = payload.get("station_id")
    if not sid:
        raise HTTPException(status_code=400, detail="Missing station_id in payload")

    if sid in state_manager.deleted_stations:
        return {
            "status": "deregistered",
            "station_id": sid,
            "action": "stop",
            "should_stop": True,
            "message": f"Workstation '{sid}' was deleted/removed from SCADA monitoring by operator."
        }

    updated_station = state_manager.update_telemetry(payload)
    if updated_station.get("status") == "deregistered":
        return {
            "status": "deregistered",
            "station_id": sid,
            "action": "stop",
            "should_stop": True
        }
    
    # Broadcast immediate station update to UI
    await ws_manager.broadcast({
        "type": "STATION_UPDATE",
        "station": updated_station,
        "summary": state_manager.get_summary()
    })
    return {"status": "success", "station_id": sid, "processed_at": datetime.datetime.now(datetime.timezone.utc).isoformat()}


@app.get("/api/stations")
async def get_stations():
    """Returns current state of all stations, line summary, and host network connection status."""
    return {
        "summary": state_manager.get_summary(),
        "stations": list(state_manager.stations.values()),
        "host_connection": get_host_connection_status()
    }


@app.get("/api/connection-status")
async def get_connection_status():
    """Returns real-time host network connection state (LAN vs Wi-Fi, IP, adapter)."""
    return get_host_connection_status(force=True)


@app.delete("/api/stations/{station_id}")
async def delete_station(station_id: str):
    """Deletes / Deregisters a workstation node from the SCADA monitoring system."""
    state_manager.deleted_stations.add(station_id)
    if station_id in state_manager.stations:
        st = state_manager.stations.pop(station_id)
        if st.get("active_offline_incident_id"):
            record_downtime_end(st["active_offline_incident_id"])
        if st.get("active_printer_incident_id"):
            record_downtime_end(st["active_printer_incident_id"])
        logger.info(f"Station {station_id} permanently removed/deregistered from SCADA by operator.")
        
    summary = state_manager.get_summary()
    # Broadcast removal to all connected dashboards
    await ws_manager.broadcast({
        "type": "STATION_DELETED",
        "station_id": station_id,
        "summary": summary,
        "stations": list(state_manager.stations.values())
    })
    return {"status": "deleted", "station_id": station_id, "summary": summary}


@app.post("/api/stations/{station_id}/restore")
async def restore_station(station_id: str):
    """Restores a previously deleted/deregistered station."""
    if station_id in state_manager.deleted_stations:
        state_manager.deleted_stations.remove(station_id)
        logger.info(f"Station {station_id} restored / unblocked in SCADA.")
        return {"status": "restored", "station_id": station_id}
    return {"status": "not_deleted", "station_id": station_id}


@app.get("/api/network-interfaces")
async def get_network_interfaces():
    """Returns local host network adapters and active IP addresses for easy configuration."""
    interfaces = []
    primary_ip = "127.0.0.1"
    try:
        import psutil
        for iface, addrs in psutil.net_if_addrs().items():
            for a in addrs:
                if a.family == socket.AF_INET:
                    ip = a.address
                    is_loopback = ip.startswith("127.")
                    is_apipa = ip.startswith("169.254.")
                    is_valid = not is_loopback and not is_apipa
                    interfaces.append({
                        "interface": iface,
                        "ip": ip,
                        "netmask": a.netmask,
                        "is_primary": is_valid
                    })
                    if is_valid and primary_ip == "127.0.0.1":
                        primary_ip = ip
    except Exception as e:
        logger.warning(f"Error enumerating interfaces: {e}")

    server_port = SERVER_CONFIG.get("server", {}).get("port", 8090)
    return {
        "hostname": socket.gethostname(),
        "primary_ip": primary_ip,
        "server_port": server_port,
        "server_url": f"http://{primary_ip}:{server_port}",
        "interfaces": interfaces
    }


class NetworkConfigPayload(BaseModel):
    ip: str
    subnet_mask: str = "255.255.255.0"
    gateway: str = "192.168.1.1"
    port: int = 8090
    enforce_subnet: bool = False
    prompt_on_startup: bool = True


@app.get("/api/network-details")
async def get_network_details():
    """Returns detected physical network adapters and active configured profile."""
    adapters = detect_system_network_adapters()
    net_profile = SERVER_CONFIG.get("network_profile", {})
    port = SERVER_CONFIG.get("server", {}).get("port", 8090)

    current_ip = net_profile.get("ip")
    current_subnet = net_profile.get("subnet_mask", "255.255.255.0")
    current_gw = net_profile.get("gateway", "192.168.1.1")

    if (not current_ip or current_ip.startswith("127.")) and adapters:
        current_ip = adapters[0]["ip"]
        current_subnet = adapters[0].get("subnet") or "255.255.255.0"
        current_gw = adapters[0].get("gateway") or "N/A"

    cidr = "N/A"
    try:
        if current_ip and current_subnet:
            net = ipaddress.IPv4Network(f"{current_ip}/{current_subnet}", strict=False)
            cidr = str(net)
    except Exception:
        pass

    return {
        "hostname": socket.gethostname(),
        "server_port": port,
        "configured": {
            "ip": current_ip or "127.0.0.1",
            "subnet_mask": current_subnet,
            "gateway": current_gw,
            "port": port,
            "subnet_cidr": cidr,
            "server_url": f"http://{current_ip or '127.0.0.1'}:{port}",
            "enforce_subnet": net_profile.get("enforce_subnet", False),
            "prompt_on_startup": net_profile.get("prompt_on_startup", True)
        },
        "adapters": adapters
    }


@app.post("/api/network-config")
async def set_network_config(payload: NetworkConfigPayload):
    """Saves user-configured server IP, subnet mask, gateway, and port."""
    cidr = "N/A"
    try:
        net = ipaddress.IPv4Network(f"{payload.ip.strip()}/{payload.subnet_mask.strip()}", strict=False)
        cidr = str(net)
    except Exception as e:
        raise HTTPException(status_code=400, detail=f"Invalid IP or Subnet configuration: {e}")

    profile = {
        "ip": payload.ip.strip(),
        "subnet_mask": payload.subnet_mask.strip(),
        "gateway": payload.gateway.strip(),
        "port": payload.port,
        "subnet_cidr": cidr,
        "enforce_subnet": payload.enforce_subnet,
        "prompt_on_startup": payload.prompt_on_startup,
        "configured_at": datetime.datetime.now(datetime.timezone.utc).isoformat()
    }
    SERVER_CONFIG["network_profile"] = profile
    SERVER_CONFIG.setdefault("server", {})["port"] = payload.port

    try:
        with open(CONFIG_PATH, "w", encoding="utf-8") as f:
            json.dump(SERVER_CONFIG, f, indent=2)
    except Exception as e:
        logger.error(f"Error saving config.json: {e}")

    # Broadcast updated network profile to dashboard
    await ws_manager.broadcast({
        "type": "NETWORK_PROFILE_UPDATED",
        "profile": profile
    })
    logger.info(f"[NETWORK-CONFIG] Profile applied: IP={profile['ip']} | Subnet={profile['subnet_cidr']} | GW={profile['gateway']}")
    return {"status": "success", "profile": profile}


@app.get("/api/downtime-logs")
async def get_downtime_logs(limit: int = 100, status: Optional[str] = None):
    """Retrieves recorded downtime & stoppage logs from SQLite."""
    conn = get_db()
    cursor = conn.cursor()
    if status:
        cursor.execute(
            "SELECT * FROM downtime_logs WHERE status = ? ORDER BY id DESC LIMIT ?",
            (status.upper(), limit)
        )
    else:
        cursor.execute("SELECT * FROM downtime_logs ORDER BY id DESC LIMIT ?", (limit,))
    rows = [dict(r) for r in cursor.fetchall()]
    conn.close()
    return {"count": len(rows), "logs": rows}


@app.get("/api/downtime-logs/export-csv")
async def export_downtime_csv():
    """Exports all downtime and line stoppage logs as a downloadable CSV."""
    conn = get_db()
    cursor = conn.cursor()
    cursor.execute("SELECT * FROM downtime_logs ORDER BY id DESC")
    rows = cursor.fetchall()
    conn.close()

    output = io.StringIO()
    writer = csv.writer(output)
    writer.writerow([
        "Incident ID",
        "Station ID",
        "Station Name",
        "Failure Reason",
        "Start Time (UTC)",
        "End Time (UTC)",
        "Duration (Seconds)",
        "Total Duration (HH:MM:SS)",
        "Incident Status",
        "Logged At"
    ])

    for r in rows:
        writer.writerow([
            r["id"],
            r["station_id"],
            r["station_name"],
            r["failure_reason"],
            r["start_time"],
            r["end_time"] or "ONGOING",
            r["duration_seconds"],
            r["duration_formatted"],
            r["status"],
            r["created_at"]
        ])

    output.seek(0)
    filename = f"PlantOps_Downtime_Report_{datetime.datetime.now().strftime('%Y%m%d_%H%M%S')}.csv"
    return StreamingResponse(
        iter([output.getvalue()]),
        media_type="text/csv",
        headers={"Content-Disposition": f"attachment; filename={filename}"}
    )


class SettingsPayload(BaseModel):
    heartbeat_timeout_sec: Optional[int] = Field(None, ge=3, le=60)
    cpu_warning_pct: Optional[float] = Field(None, ge=10.0, le=99.0)
    ram_warning_pct: Optional[float] = Field(None, ge=10.0, le=99.0)
    ping_warning_ms: Optional[float] = Field(None, ge=5.0, le=1000.0)


@app.get("/api/settings")
async def get_settings():
    return state_manager.thresholds


@app.post("/api/settings")
async def update_settings(payload: SettingsPayload):
    for k, v in payload.model_dump(exclude_unset=True).items():
        state_manager.thresholds[k] = v
    logger.info("Updated server thresholds: %s", state_manager.thresholds)
    return {"status": "updated", "thresholds": state_manager.thresholds}


# -------------------------------------------------------------------------
# WebSocket Live Dashboard Stream
# -------------------------------------------------------------------------
@app.websocket("/ws/dashboard")
async def websocket_dashboard_endpoint(websocket: WebSocket):
    await ws_manager.connect(websocket)
    try:
        # Send initial full state immediately
        await websocket.send_json({
            "type": "INITIAL_STATE",
            "summary": state_manager.get_summary(),
            "stations": list(state_manager.stations.values()),
            "thresholds": state_manager.thresholds,
            "host_connection": get_host_connection_status()
        })
        while True:
            # Keep socket open and process client requests (e.g. ping/pong)
            msg = await websocket.receive_text()
            if msg == "ping":
                await websocket.send_text("pong")
    except WebSocketDisconnect:
        ws_manager.disconnect(websocket)
    except Exception as e:
        logger.debug(f"WebSocket client error: {e}")
        ws_manager.disconnect(websocket)


# -------------------------------------------------------------------------
# Static Files & Dashboard UI Mounting
# -------------------------------------------------------------------------
static_dir = APP_DIR / "static" if (APP_DIR / "static").exists() else (BUNDLE_DIR / "static")
if not static_dir.exists():
    static_dir.mkdir(parents=True, exist_ok=True)

app.mount("/static", StaticFiles(directory=str(static_dir)), name="static")

@app.get("/", response_class=HTMLResponse)
async def serve_root():
    index_file = static_dir / "index.html"
    if index_file.is_file():
        with open(index_file, "r", encoding="utf-8") as f:
            return HTMLResponse(content=f.read())
    return HTMLResponse("<h1>PlantOps Monitor Server Running</h1><p>Visit /static/index.html</p>")


def run_server(host: str = "0.0.0.0", port: int = 8090):
    import uvicorn
    uvicorn.run(app, host=host, port=port, log_level="info")


if __name__ == "__main__":
    import argparse
    parser = argparse.ArgumentParser(description="PlantOps Central Monitoring Server")
    parser.add_argument("--host", default="0.0.0.0", help="Binding IP address (default 0.0.0.0 for LAN)")
    parser.add_argument("--port", type=int, default=8090, help="Port to listen on (default 8090)")
    args = parser.parse_args()
    run_server(args.host, args.port)
