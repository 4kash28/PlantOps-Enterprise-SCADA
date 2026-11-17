"""
LineNode Agent - Industrial Assembly Line Telemetry Agent
Production Grade Client for Room Air Conditioner (RAC) Assembly Line PCs.
Monitors system metrics, hardware status, and Zebra ZT411 label printers.
"""

import sys
import os
import json
import time
import socket
import logging
import datetime
import subprocess
import threading
import argparse
from pathlib import Path
from typing import Dict, Any, Optional, List

def run_hidden_subprocess(cmd: List[str], timeout: float = 2.0) -> subprocess.CompletedProcess:
    """Executes a child process with Windows flags that guarantee ZERO visible console/terminal window."""
    startupinfo = None
    creationflags = 0
    if sys.platform == "win32":
        creationflags = 0x08000000  # CREATE_NO_WINDOW
        startupinfo = subprocess.STARTUPINFO()
        startupinfo.dwFlags |= subprocess.STARTF_USESHOWWINDOW
        startupinfo.wShowWindow = 0  # SW_HIDE

    try:
        return subprocess.run(
            cmd,
            capture_output=True,
            text=True,
            timeout=timeout,
            startupinfo=startupinfo,
            creationflags=creationflags
        )
    except Exception as e:
        return subprocess.CompletedProcess(args=cmd, returncode=1, stdout="", stderr=str(e))

# Core telemetry
try:
    import psutil
except ImportError:
    print("[FATAL] 'psutil' is not installed. Run: pip install psutil")
    sys.exit(1)

# Networking
try:
    import urllib.request
    import urllib.error
except ImportError:
    pass

# Direct Windows API structures for WinSpooler if win32print is not available
import ctypes
from ctypes import wintypes

# Determine Application Directory (handles PyInstaller frozen .exe vs source run)
if getattr(sys, 'frozen', False):
    APP_DIR = Path(sys.executable).parent.resolve()
    BUNDLE_DIR = Path(getattr(sys, '_MEIPASS', APP_DIR))
else:
    APP_DIR = Path(__file__).parent.resolve()
    BUNDLE_DIR = APP_DIR

# Configure Logging
LOG_FORMAT = "%(asctime)s [%(levelname)s] [%(name)s] %(message)s"
logging.basicConfig(level=logging.INFO, format=LOG_FORMAT)
logger = logging.getLogger("LineNodeAgent")

# System Tray Support
try:
    import pystray
    from PIL import Image, ImageDraw
    HAS_TRAY = True
except ImportError:
    HAS_TRAY = False
try:
    from logging.handlers import RotatingFileHandler
    log_file = APP_DIR / "line_node_agent.log"
    file_handler = RotatingFileHandler(log_file, maxBytes=5 * 1024 * 1024, backupCount=3, encoding="utf-8")
    file_handler.setFormatter(logging.Formatter(LOG_FORMAT))
    logger.addHandler(file_handler)
except Exception as e:
    logger.warning(f"Failed to initialize file logger: {e}")

# WinSpooler Status Flags (Microsoft Win32 Print Spooler API)
PRINTER_STATUS_PAUSED            = 0x00000001
PRINTER_STATUS_ERROR             = 0x00000002
PRINTER_STATUS_PENDING_DELETION  = 0x00000004
PRINTER_STATUS_PAPER_JAM         = 0x00000008
PRINTER_STATUS_PAPER_OUT         = 0x00000010
PRINTER_STATUS_MANUAL_FEED       = 0x00000020
PRINTER_STATUS_PAPER_PROBLEM     = 0x00000040
PRINTER_STATUS_OFFLINE           = 0x00000080
PRINTER_STATUS_IO_ACTIVE         = 0x00000100
PRINTER_STATUS_BUSY              = 0x00000200
PRINTER_STATUS_PRINTING          = 0x00000400
PRINTER_STATUS_OUTPUT_BIN_FULL   = 0x00000800
PRINTER_STATUS_NOT_AVAILABLE     = 0x00001000
PRINTER_STATUS_WAITING           = 0x00002000
PRINTER_STATUS_PROCESSING        = 0x00004000
PRINTER_STATUS_INITIALIZING      = 0x00008000
PRINTER_STATUS_WARMING_UP        = 0x00010000
PRINTER_STATUS_TONER_LOW         = 0x00020000
PRINTER_STATUS_NO_TONER          = 0x00040000
PRINTER_STATUS_PAGE_PUNT         = 0x00080000
PRINTER_STATUS_USER_INTERVENTION = 0x00100000
PRINTER_STATUS_OUT_OF_MEMORY     = 0x00200000
PRINTER_STATUS_DOOR_OPEN         = 0x00400000
PRINTER_STATUS_SERVER_UNKNOWN    = 0x00800000
PRINTER_STATUS_POWER_SAVE        = 0x01000000

PRINTER_STATUS_MAP = {
    PRINTER_STATUS_PAUSED: "PAUSED",
    PRINTER_STATUS_ERROR: "ERROR",
    PRINTER_STATUS_PAPER_JAM: "PAPER_JAM",
    PRINTER_STATUS_PAPER_OUT: "PAPER_OUT",
    PRINTER_STATUS_MANUAL_FEED: "MANUAL_FEED",
    PRINTER_STATUS_PAPER_PROBLEM: "PAPER_PROBLEM",
    PRINTER_STATUS_OFFLINE: "OFFLINE",
    PRINTER_STATUS_IO_ACTIVE: "IO_ACTIVE",
    PRINTER_STATUS_BUSY: "BUSY",
    PRINTER_STATUS_PRINTING: "PRINTING",
    PRINTER_STATUS_OUTPUT_BIN_FULL: "OUTPUT_BIN_FULL",
    PRINTER_STATUS_NOT_AVAILABLE: "NOT_AVAILABLE",
    PRINTER_STATUS_WAITING: "WAITING",
    PRINTER_STATUS_PROCESSING: "PROCESSING",
    PRINTER_STATUS_INITIALIZING: "INITIALIZING",
    PRINTER_STATUS_WARMING_UP: "WARMING_UP",
    PRINTER_STATUS_TONER_LOW: "TONER_LOW",
    PRINTER_STATUS_NO_TONER: "NO_TONER",
    PRINTER_STATUS_PAGE_PUNT: "PAGE_PUNT",
    PRINTER_STATUS_USER_INTERVENTION: "USER_INTERVENTION",
    PRINTER_STATUS_OUT_OF_MEMORY: "OUT_OF_MEMORY",
    PRINTER_STATUS_DOOR_OPEN: "DOOR_OPEN",
    PRINTER_STATUS_SERVER_UNKNOWN: "SERVER_UNKNOWN",
    PRINTER_STATUS_POWER_SAVE: "POWER_SAVE",
}


class ConfigManager:
    """Handles agent configuration loading and runtime fallbacks."""
    DEFAULT_CONFIG = {
        "server_ip": "127.0.0.1",
        "server_port": 8090,
        "station_id": "LINE1_STATION_06",
        "station_name": "RAC Packaging & Labeling",
        "sync_interval_sec": 3,
        "ping_timeout_ms": 1000,
        "prompt_permissions_on_startup": True,
        "permissions": {
            "cpu": True,
            "ram": True,
            "disk": True,
            "network": True,
            "uptime": True,
            "zebra": False,
            "allow_remote_stop": True
        },
        "printer_monitoring": {
            "enabled": False,
            "zebra_vid": "0A5F",
            "name_pattern": "ZT411"
        },
        "log_level": "INFO",
        "simulation_mode": False
    }

    @classmethod
    def load_config(cls, config_path: str = "config.json") -> Dict[str, Any]:
        path = Path(config_path)
        if not path.is_file():
            # Check APP_DIR and BUNDLE_DIR
            for base in (APP_DIR, BUNDLE_DIR):
                candidate = base / config_path
                if candidate.is_file():
                    path = candidate
                    break

        if path.is_file():
            try:
                with open(path, "r", encoding="utf-8") as f:
                    user_conf = json.load(f)
                    config = cls.DEFAULT_CONFIG.copy()
                    config.update(user_conf)
                    logger.info(f"Loaded configuration from {path.resolve()}")
                    return config
            except Exception as e:
                logger.error(f"Error reading configuration file {path}: {e}. Using defaults.")

        logger.warning(f"No config.json found or failed to load. Creating default config.json in {APP_DIR}.")
        try:
            target_conf = APP_DIR / "config.json"
            with open(target_conf, "w", encoding="utf-8") as f:
                json.dump(cls.DEFAULT_CONFIG, f, indent=2)
        except Exception as e:
            logger.warning(f"Could not persist default config.json: {e}")
        return cls.DEFAULT_CONFIG


class HardwareTelemetry:
    """Collects CPU, RAM, Disk, Uptime, Temperature, and Network Latency."""

    @staticmethod
    def get_local_ip(target_server_ip: str = "127.0.0.1", target_server_port: int = 8090) -> str:
        """Dynamically discovers active LAN IP on the network without requiring internet."""
        if target_server_ip and target_server_ip not in ("127.0.0.1", "localhost", "::1"):
            try:
                s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
                s.settimeout(0.5)
                s.connect((target_server_ip, target_server_port))
                ip = s.getsockname()[0]
                s.close()
                if not ip.startswith("127.") and not ip.startswith("169.254."):
                    return ip
            except Exception:
                pass

        # Inspect network interfaces for active non-loopback, non-APIPA IPv4
        try:
            for iface, addrs in psutil.net_if_addrs().items():
                for a in addrs:
                    if a.family == socket.AF_INET:
                        ip = a.address
                        if not ip.startswith("127.") and not ip.startswith("169.254."):
                            return ip
        except Exception:
            pass

        return "127.0.0.1"

    _last_temp_check = 0.0
    _cached_temp = None
    _last_conn_check = 0.0
    _cached_conn = None

    @classmethod
    def get_connection_type(cls, active_ip: str) -> Dict[str, Any]:
        """Detects whether this workstation connects via LAN (Ethernet) or Wi-Fi."""
        now = time.time()
        if cls._cached_conn and (now - cls._last_conn_check < 4.0):
            return cls._cached_conn

        info = {
            "type": "LAN",
            "label": "LAN",
            "adapter": "",
            "is_wifi": False,
            "is_lan": True,
            "details": ""
        }
        try:
            cmd = ["netsh", "wlan", "show", "interfaces"]
            res = run_hidden_subprocess(cmd, timeout=1.5)
            out = res.stdout
            if "State" in out and "connected" in out.lower():
                ssid = ""
                signal = ""
                for line in out.splitlines():
                    line = line.strip()
                    if line.startswith("SSID") and not line.startswith("SSID name") and not line.startswith("BSSID"):
                        parts = line.split(":", 1)
                        if len(parts) == 2: ssid = parts[1].strip()
                    elif line.startswith("Signal"):
                        parts = line.split(":", 1)
                        if len(parts) == 2: signal = parts[1].strip()
                
                info["type"] = "WIFI"
                info["label"] = "Wi-Fi"
                info["is_wifi"] = True
                info["is_lan"] = False
                info["details"] = f"Wi-Fi: {ssid} ({signal})" if ssid else "Wi-Fi"
                cls._cached_conn = info
                cls._last_conn_check = now
                return info
        except Exception:
            pass

        try:
            addrs = psutil.net_if_addrs()
            for name, addr_list in addrs.items():
                for a in addr_list:
                    if a.address == active_ip:
                        info["adapter"] = name
                        lower = name.lower()
                        if any(w in lower for w in ['wi-fi', 'wifi', 'wireless', 'wlan']):
                            info["type"] = "WIFI"
                            info["label"] = "Wi-Fi"
                            info["is_wifi"] = True
                            info["is_lan"] = False
                            info["details"] = f"Wi-Fi ({name})"
                        else:
                            info["type"] = "LAN"
                            info["label"] = "LAN"
                            info["is_lan"] = True
                            info["is_wifi"] = False
                            info["details"] = f"Ethernet ({name})"
                        break
        except Exception:
            pass

        cls._cached_conn = info
        cls._last_conn_check = now
        return info

    @classmethod
    def get_cpu_temp(cls) -> Optional[float]:
        """Attempts WMI query for ACPI or OpenHardwareMonitor temperature with caching and zero console window."""
        if sys.platform != "win32":
            return None

        now = time.time()
        if now - cls._last_temp_check < 20.0:
            return cls._cached_temp

        cls._last_temp_check = now

        # 1. Try MSAcpi_ThermalZoneTemperature via PowerShell CIM
        try:
            cmd = [
                "powershell", "-NoProfile", "-Command",
                "Get-CimInstance -Namespace root/wmi -ClassName MSAcpi_ThermalZoneTemperature -ErrorAction SilentlyContinue | Select-Object -ExpandProperty CurrentTemperature"
            ]
            res = run_hidden_subprocess(cmd, timeout=1.5)
            output = res.stdout.strip()
            if output and output.isdigit():
                # Value is in tenths of degrees Kelvin
                val = float(output)
                celsius = (val / 10.0) - 273.15
                if -20.0 <= celsius <= 125.0:
                    cls._cached_temp = round(celsius, 1)
                    return cls._cached_temp
        except Exception:
            pass

        # 2. Try OpenHardwareMonitor WMI if installed
        try:
            cmd_ohm = [
                "powershell", "-NoProfile", "-Command",
                "Get-CimInstance -Namespace root/OpenHardwareMonitor -ClassName Sensor -Filter \"SensorType='Temperature' and Name like '%CPU%'\" -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Value"
            ]
            res_ohm = run_hidden_subprocess(cmd_ohm, timeout=1.5)
            lines = res_ohm.stdout.strip().split()
            if lines:
                val = float(lines[0])
                if 0.0 <= val <= 125.0:
                    cls._cached_temp = round(val, 1)
                    return cls._cached_temp
        except Exception:
            pass

        cls._cached_temp = None
        return None

    @staticmethod
    def get_network_latency(server_ip: str, server_port: int, timeout_ms: int = 1000) -> float:
        """Measures TCP handshake latency to server port, falls back to ICMP ping with zero console window."""
        start = time.perf_counter()
        try:
            s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            s.settimeout(timeout_ms / 1000.0)
            s.connect((server_ip, server_port))
            latency = (time.perf_counter() - start) * 1000.0
            s.close()
            return round(latency, 2)
        except Exception:
            # Fallback to ICMP ping with CREATE_NO_WINDOW
            try:
                start_ping = time.perf_counter()
                cmd = ["ping", "-n", "1", "-w", str(timeout_ms), server_ip]
                p = run_hidden_subprocess(cmd, timeout=1.5)
                if p.returncode == 0:
                    return round((time.perf_counter() - start_ping) * 1000.0, 2)
            except Exception:
                pass
        return -1.0  # -1 represents timeout or unreachable

    @classmethod
    def collect(cls, server_ip: str, server_port: int, timeout_ms: int = 1000, permissions: Optional[Dict[str, bool]] = None) -> Dict[str, Any]:
        perms = permissions or {
            "cpu": True, "ram": True, "disk": True, "network": True, "uptime": True, "zebra": False, "allow_remote_stop": True
        }

        # CPU Telemetry
        if perms.get("cpu", True):
            cpu_pct = psutil.cpu_percent(interval=None)
            cpu_temp = cls.get_cpu_temp()
        else:
            cpu_pct = None
            cpu_temp = None

        # RAM Telemetry
        if perms.get("ram", True):
            vmem = psutil.virtual_memory()
            ram_total_gb = round(vmem.total / (1024 ** 3), 2)
            ram_used_gb = round(vmem.used / (1024 ** 3), 2)
            ram_pct = vmem.percent
        else:
            ram_total_gb = None
            ram_used_gb = None
            ram_pct = None

        # Storage (Fixed Partitions)
        disks = []
        if perms.get("disk", True):
            for part in psutil.disk_partitions(all=False):
                if "cdrom" in part.opts or part.fstype == "":
                    continue
                try:
                    usage = psutil.disk_usage(part.mountpoint)
                    disks.append({
                        "drive": part.device,
                        "mountpoint": part.mountpoint,
                        "total_gb": round(usage.total / (1024 ** 3), 2),
                        "used_gb": round(usage.used / (1024 ** 3), 2),
                        "free_gb": round(usage.free / (1024 ** 3), 2),
                        "percent": usage.percent
                    })
                except (PermissionError, FileNotFoundError):
                    continue

        # Uptime
        if perms.get("uptime", True):
            boot_time = psutil.boot_time()
            uptime_sec = int(time.time() - boot_time)
            days = uptime_sec // 86400
            hours = (uptime_sec % 86400) // 3600
            mins = (uptime_sec % 3600) // 60
            secs = uptime_sec % 60
            uptime_fmt = f"{days}d {hours:02d}:{mins:02d}:{secs:02d}"
        else:
            uptime_sec = 0
            uptime_fmt = "Hidden"

        # Latency
        if perms.get("network", True):
            latency = cls.get_network_latency(server_ip, server_port, timeout_ms)
        else:
            latency = -1.0

        return {
            "cpu_percent": cpu_pct,
            "cpu_temp_c": cpu_temp,
            "ram_total_gb": ram_total_gb,
            "ram_used_gb": ram_used_gb,
            "ram_percent": ram_pct,
            "disks": disks,
            "uptime_seconds": uptime_sec,
            "uptime_formatted": uptime_fmt,
            "network_latency_ms": latency
        }


class ZebraPrinterMonitor:
    r"""
    Monitors connected Zebra ZT411 label printers using:
    1. WMI PnP Device checks (USB Vendor ID: USB\VID_0A5F*).
    2. Windows Print Spooler API (WinSpooler Status / Error Codes).
    """

    def __init__(self, zebra_vid: str = "0A5F", name_pattern: str = "ZT411", enabled: bool = True):
        self.zebra_vid = zebra_vid.upper()
        self.name_pattern = name_pattern.upper()
        self.enabled = enabled

    def check_wmi_usb_device(self) -> Dict[str, Any]:
        """Queries Windows PnP to verify if Zebra USB is plugged in and recognized."""
        result = {
            "usb_present": False,
            "device_id": None,
            "device_name": None,
            "pnp_status": "NOT_FOUND",
            "error_code": None
        }

        if sys.platform != "win32":
            return result

        try:
            # Check PnP entities matching Zebra VID 0A5F
            ps_script = f"""
            $devs = Get-CimInstance Win32_PnPEntity | Where-Object {{ $_.DeviceID -match "VID_{self.zebra_vid}" }}
            if ($devs) {{
                $d = $devs | Select-Object -First 1
                [PSCustomObject]@{{
                    Present = $true
                    DeviceID = $d.DeviceID
                    Name = $d.Name
                    Status = $d.Status
                    ConfigManagerErrorCode = $d.ConfigManagerErrorCode
                }} | ConvertTo-Json -Compress
            }} else {{
                '{{"Present": false}}'
            }}
            """
            proc = run_hidden_subprocess(
                ["powershell", "-NoProfile", "-Command", ps_script],
                timeout=2.0
            )
            raw = proc.stdout.strip()
            if raw:
                data = json.loads(raw)
                if data.get("Present"):
                    result["usb_present"] = True
                    result["device_id"] = data.get("DeviceID")
                    result["device_name"] = data.get("Name")
                    result["pnp_status"] = data.get("Status", "OK")
                    result["error_code"] = data.get("ConfigManagerErrorCode", 0)
        except Exception as e:
            logger.debug(f"WMI USB check exception: {e}")

        return result

    def check_spooler_printers(self) -> List[Dict[str, Any]]:
        """Queries Windows Print Spooler using PowerShell/WMI fallback for installed printers."""
        printers = []
        if sys.platform != "win32":
            return printers

        try:
            ps_script = """
            Get-CimInstance Win32_Printer | Select-Object Name, PortName, DriverName, PrinterStatus, ExtendedPrinterStatus, WorkOffline, DetectedErrorState | ConvertTo-Json -Compress
            """
            proc = run_hidden_subprocess(
                ["powershell", "-NoProfile", "-Command", ps_script],
                timeout=2.0
            )
            raw = proc.stdout.strip()
            if raw:
                try:
                    data = json.loads(raw)
                    if isinstance(data, dict):
                        printers = [data]
                    elif isinstance(data, list):
                        printers = data
                except Exception:
                    pass
        except Exception as e:
            logger.debug(f"Spooler check exception: {e}")

        return printers

    def get_status(self, simulation_state: Optional[str] = None) -> Dict[str, Any]:
        """
        Determines the complete status of the Zebra ZT411 printer.
        Status Enum: READY, OFFLINE, PAPER_OUT, DOOR_OPEN, ERROR, DISCONNECTED
        """
        if not self.enabled:
            return {
                "monitored": False,
                "connected": False,
                "status": "NOT_MONITORED",
                "status_message": "Zebra printer monitoring disabled for this station",
                "model_name": "None",
                "device_id": None,
                "spooler_flags": []
            }

        if simulation_state:
            return {
                "monitored": True,
                "connected": simulation_state != "DISCONNECTED",
                "status": simulation_state,
                "status_message": f"Simulated status: {simulation_state}",
                "model_name": "Zebra ZT411 203dpi (Simulated)",
                "device_id": f"USB\\VID_{self.zebra_vid}&PID_0164\\SIM12345",
                "spooler_flags": [simulation_state] if simulation_state != "READY" else []
            }

        # Step 1: Check physical USB PnP enumeration
        pnp = self.check_wmi_usb_device()

        # Step 2: Check Windows Spooler
        spooler_printers = self.check_spooler_printers()
        matching_printer = None
        for p in spooler_printers:
            pname = (p.get("Name") or "").upper()
            dname = (p.get("DriverName") or "").upper()
            if self.name_pattern in pname or self.name_pattern in dname or "ZEBRA" in pname:
                matching_printer = p
                break

        # Step 3: Formulate combined status
        if not pnp["usb_present"] and not matching_printer:
            return {
                "monitored": True,
                "connected": False,
                "status": "DISCONNECTED",
                "status_message": "Zebra ZT411 not detected (USB unplugged or powered off)",
                "model_name": "Zebra ZT411",
                "device_id": None,
                "spooler_flags": ["DEVICE_NOT_FOUND"]
            }

        status = "READY"
        status_message = "Zebra ZT411 is Ready and Online"
        flags = []

        if matching_printer:
            work_offline = matching_printer.get("WorkOffline", False)
            err_state = matching_printer.get("DetectedErrorState", 0)
            ext_status = matching_printer.get("ExtendedPrinterStatus", 0)

            if work_offline:
                status = "OFFLINE"
                flags.append("OFFLINE")
                status_message = "Printer is set to Work Offline in Windows"
            elif err_state in (4, 5):  # Paper out / Out of paper
                status = "PAPER_OUT"
                flags.append("PAPER_OUT")
                status_message = "Media / Paper Out or Ribbon Empty"
            elif err_state in (6, 7):  # Door open
                status = "DOOR_OPEN"
                flags.append("DOOR_OPEN")
                status_message = "Printer Head or Enclosure Door Open"
            elif err_state in (1, 2, 3) or ext_status in (1, 2):
                status = "ERROR"
                flags.append("ERROR")
                status_message = "Printer hardware error or paper jam"

        if not pnp["usb_present"] and status == "READY":
            # Spooler printer exists, but USB PnP is not seen
            status = "OFFLINE"
            status_message = "Printer driver installed but USB is unplugged"

        return {
            "monitored": True,
            "connected": pnp["usb_present"],
            "status": status,
            "status_message": status_message,
            "model_name": matching_printer.get("Name") if matching_printer else (pnp.get("device_name") or "Zebra ZT411"),
            "device_id": pnp.get("device_id"),
            "spooler_flags": flags
        }


def auto_discover_plantops_server(port: int = 8090, timeout: float = 2.0) -> Optional[str]:
    """
    Auto-discovers PlantOps Central Server on the local LAN using:
    1. Fast UDP broadcast query on port 8091 (sub-10ms response).
    2. High-speed parallel HTTP scan across local /24 subnet checking /api/network-interfaces.
    Returns the discovered server IP string, or None.
    """
    logger.info("[DISCOVERY] Scanning local network for PlantOps Central Monitoring Server...")
    
    # 1. Try fast UDP broadcast probe first
    try:
        s_udp = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        s_udp.setsockopt(socket.SOL_SOCKET, socket.SO_BROADCAST, 1)
        s_udp.settimeout(0.4)
        msg = b"PLANTOPS_DISCOVER"
        try:
            s_udp.sendto(msg, ("255.255.255.255", 8091))
        except Exception:
            pass

        local_ip = HardwareTelemetry.get_local_ip()
        if not local_ip.startswith("127."):
            parts = local_ip.split(".")
            if len(parts) == 4:
                subnet_bcast = f"{parts[0]}.{parts[1]}.{parts[2]}.255"
                try:
                    s_udp.sendto(msg, (subnet_bcast, 8091))
                except Exception:
                    pass

        data, addr = s_udp.recvfrom(1024)
        s_udp.close()
        text = data.decode("utf-8", errors="ignore")
        if "PLANTOPS_OFFER" in text:
            raw_json = text.split("PLANTOPS_OFFER:", 1)[-1].strip()
            parsed = json.loads(raw_json)
            server_ip = parsed.get("server_ip") or addr[0]
            logger.info(f"[DISCOVERY] Found PlantOps Server via UDP Broadcast at {server_ip} (Host: {parsed.get('hostname')})")
            return server_ip
    except Exception:
        pass

    # 2. Fast Parallel HTTP Subnet Probing (60 threads, finishes in ~1.5 seconds)
    local_ip = HardwareTelemetry.get_local_ip()
    if local_ip.startswith("127."):
        return None

    parts = local_ip.split(".")
    if len(parts) != 4:
        return None
    subnet = ".".join(parts[:3])

    def probe_candidate(host_num):
        target_ip = f"{subnet}.{host_num}"
        try:
            sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            sock.settimeout(0.3)
            res = sock.connect_ex((target_ip, port))
            sock.close()
            if res == 0:
                url = f"http://{target_ip}:{port}/api/network-interfaces"
                req = urllib.request.Request(url, headers={"User-Agent": "PlantOpsAgentDiscovery/2.4"})
                with urllib.request.urlopen(req, timeout=0.8) as resp:
                    if resp.status == 200:
                        body = json.loads(resp.read().decode("utf-8"))
                        if "server_port" in body or "primary_ip" in body:
                            return target_ip
        except Exception:
            pass
        return None

    try:
        from concurrent.futures import ThreadPoolExecutor, as_completed
        with ThreadPoolExecutor(max_workers=60) as executor:
            futures = {executor.submit(probe_candidate, h): h for h in range(1, 255)}
            for f in as_completed(futures):
                res = f.result()
                if res:
                    logger.info(f"[DISCOVERY] Found PlantOps Server via HTTP Subnet Scan at {res}")
                    return res
    except Exception as e:
        logger.debug(f"Subnet scan error: {e}")

    logger.warning("[DISCOVERY] No PlantOps Server could be automatically located on current subnet.")
    return None


class LineNodeAgent:
    """Core Agent Loop with WebSocket + HTTP POST fallback & auto-reconnect."""

    def __init__(self, config: Dict[str, Any]):
        self.config = config
        self.station_id = config.get("station_id", "LINE1_STATION_06")
        self.station_name = config.get("station_name", "RAC Packaging & Labeling")
        self.server_ip = config.get("server_ip", "127.0.0.1")
        self.server_port = int(config.get("server_port", 8090))
        self.sync_interval = int(config.get("sync_interval_sec", 3))
        self.ping_timeout = int(config.get("ping_timeout_ms", 1000))
        self.sim_mode = config.get("simulation_mode", False)
        self.sim_state = config.get("sim_zebra_state", None)

        self.permissions = config.get("permissions", {
            "cpu": True, "ram": True, "disk": True, "network": True, "uptime": True, "zebra": False, "allow_remote_stop": True
        })

        p_conf = config.get("printer_monitoring", {})
        self.printer_mon = ZebraPrinterMonitor(
            zebra_vid=p_conf.get("zebra_vid", "0A5F"),
            name_pattern=p_conf.get("name_pattern", "ZT411"),
            enabled=self.permissions.get("zebra", False)
        )

        self.running = False
        self.config_file_path = APP_DIR / "config.json"
        self._last_config_mtime = self.config_file_path.stat().st_mtime if self.config_file_path.is_file() else 0.0

    def reload_config(self, new_config: Optional[Dict[str, Any]] = None):
        """Reloads configuration dynamically from dict or file without restarting process."""
        if new_config is None and self.config_file_path.is_file():
            try:
                with open(self.config_file_path, "r", encoding="utf-8") as f:
                    new_config = json.load(f)
            except Exception as e:
                logger.warning(f"Failed to reload config: {e}")
                return

        if new_config:
            self.config = new_config
            self.station_id = new_config.get("station_id", self.station_id)
            self.station_name = new_config.get("station_name", self.station_name)
            self.server_ip = new_config.get("server_ip", self.server_ip)
            self.server_port = int(new_config.get("server_port", self.server_port))
            self.sync_interval = int(new_config.get("sync_interval_sec", self.sync_interval))
            self.permissions = new_config.get("permissions", self.permissions)
            
            p_conf = new_config.get("printer_monitoring", {})
            self.printer_mon.enabled = self.permissions.get("zebra", False)
            self.printer_mon.zebra_vid = p_conf.get("zebra_vid", "0A5F").upper()
            self.printer_mon.name_pattern = p_conf.get("name_pattern", "ZT411").upper()
            logger.info(f"[RELOAD] Configuration updated dynamically: Server={self.server_ip}:{self.server_port}, Station={self.station_id}, Permissions={self.permissions}")

    def build_payload(self) -> Dict[str, Any]:
        """Constructs telemetry heartbeat packet with dynamic local IP and Windows PC hostname."""
        current_ip = HardwareTelemetry.get_local_ip(self.server_ip, self.server_port)
        telemetry = HardwareTelemetry.collect(self.server_ip, self.server_port, self.ping_timeout, permissions=self.permissions)
        
        # Zebra status
        if self.permissions.get("zebra", False):
            self.printer_mon.enabled = True
            zebra_status = self.printer_mon.get_status(simulation_state=self.sim_state)
        else:
            self.printer_mon.enabled = False
            zebra_status = {
                "monitored": False,
                "connected": False,
                "status": "NOT_MONITORED",
                "status_message": "Zebra monitoring disabled by station permission policy",
                "model_name": "None",
                "device_id": None,
                "spooler_flags": []
            }

        hostname = socket.gethostname()
        if self.permissions.get("network", True):
            conn_info = HardwareTelemetry.get_connection_type(current_ip)
        else:
            conn_info = {
                "type": "LAN",
                "label": "LAN",
                "details": "Connection telemetry masked by local station policy",
                "is_wifi": False,
                "is_lan": True
            }

        payload = {
            "station_id": self.station_id,
            "station_name": self.station_name,
            "hostname": hostname,
            "ip_address": current_ip,
            "connection_type": conn_info.get("type", "LAN"),
            "connection_label": conn_info.get("label", "LAN"),
            "connection_details": conn_info.get("details", ""),
            "timestamp": datetime.datetime.now(datetime.timezone.utc).isoformat(),
            "agent_version": "2.4.0-RAC-PROD",
            "permissions": self.permissions,
            "system_telemetry": telemetry,
            "zebra_printer": zebra_status
        }
        return payload

    def send_http_heartbeat(self, payload: Dict[str, Any]) -> bool:
        """Sends payload via HTTP POST to central server."""
        url = f"http://{self.server_ip}:{self.server_port}/api/telemetry"
        data = json.dumps(payload).encode("utf-8")
        req = urllib.request.Request(
            url,
            data=data,
            headers={"Content-Type": "application/json", "User-Agent": "LineNodeAgent/2.4"}
        )
        try:
            with urllib.request.urlopen(req, timeout=3.0) as resp:
                if resp.status in (200, 201):
                    try:
                        raw = resp.read()
                        res_data = json.loads(raw.decode("utf-8"))
                        if res_data.get("action") == "stop" or res_data.get("should_stop"):
                            if self.permissions.get("allow_remote_stop", True):
                                logger.info(f"Central SCADA server ordered agent termination ({res_data.get('message', 'Workstation removed by operator')}). Exiting.")
                                self.running = False
                                os._exit(0)
                            else:
                                logger.info("Central SCADA server requested termination, but remote stop permission is disabled locally.")
                    except Exception:
                        pass
                    return True
        except Exception as e:
            logger.debug(f"HTTP heartbeat failed to {url}: {e}")
            return False
        return False

    def _persist_server_ip(self, new_ip: str):
        """Updates and saves discovered server IP to config.json so it persists across reboots."""
        try:
            conf = {}
            if self.config_file_path.is_file():
                with open(self.config_file_path, "r", encoding="utf-8") as f:
                    conf = json.load(f)
            conf["server_ip"] = new_ip
            with open(self.config_file_path, "w", encoding="utf-8") as f:
                json.dump(conf, f, indent=2)
            self._last_config_mtime = self.config_file_path.stat().st_mtime
            logger.info(f"[PERSIST] Saved discovered server IP {new_ip} to {self.config_file_path}")
        except Exception as e:
            logger.warning(f"Failed to persist server IP: {e}")

    def run(self):
        """Main resilient agent loop with dynamic config watching and automatic LAN discovery."""
        self.running = True
        logger.info(f"Starting LineNode Agent for [{self.station_id} - {self.station_name}] (Host: {socket.gethostname()})")
        logger.info(f"Target Server: {self.server_ip}:{self.server_port} | Interval: {self.sync_interval}s")

        # Startup check: if configured IP is default/localhost and unreachable, auto-discover immediately
        if self.server_ip in ("127.0.0.1", "localhost", "192.168.1.100"):
            initial_payload = self.build_payload()
            if not self.send_http_heartbeat(initial_payload):
                logger.warning(f"[STARTUP] Initial server IP {self.server_ip} is unreachable. Scanning LAN for PlantOps Server...")
                discovered = auto_discover_plantops_server(self.server_port)
                if discovered:
                    self.server_ip = discovered
                    self._persist_server_ip(discovered)
                    logger.info(f"[STARTUP] Auto-configured Server IP to {discovered}!")

        consecutive_failures = 0
        last_config_check = time.time()

        while self.running:
            try:
                # Watch config.json for external updates
                now = time.time()
                if now - last_config_check > 4.0:
                    last_config_check = now
                    if self.config_file_path.is_file():
                        try:
                            mtime = self.config_file_path.stat().st_mtime
                            if mtime > self._last_config_mtime:
                                self._last_config_mtime = mtime
                                self.reload_config()
                        except Exception:
                            pass

                payload = self.build_payload()
                success = self.send_http_heartbeat(payload)

                if success:
                    if consecutive_failures > 0:
                        logger.info(f"[CONNECTED] Re-established connection to PlantOps Server ({self.server_ip}:{self.server_port})")
                        consecutive_failures = 0
                    
                    z_stat = payload["zebra_printer"]["status"]
                    cpu_raw = payload["system_telemetry"]["cpu_percent"]
                    ram_raw = payload["system_telemetry"]["ram_percent"]
                    lat_raw = payload["system_telemetry"]["network_latency_ms"]
                    cpu_s = f"{cpu_raw}%" if cpu_raw is not None else "Disabled"
                    ram_s = f"{ram_raw}%" if ram_raw is not None else "Disabled"
                    lat_s = f"{lat_raw}ms" if lat_raw >= 0 else "N/A"
                    logger.info(
                        f"Heartbeat sent -> IP: {payload['ip_address']} | CPU: {cpu_s} | RAM: {ram_s} | Zebra: {z_stat} | Latency: {lat_s}"
                    )
                else:
                    consecutive_failures += 1
                    if consecutive_failures % 5 == 1:
                        logger.warning(
                            f"[DISCONNECTED] Unable to reach PlantOps Server at {self.server_ip}:{self.server_port} (Attempt {consecutive_failures}). Retrying..."
                        )

                    # Trigger LAN auto-discovery after 2 failed attempts, and every 10 attempts thereafter
                    if consecutive_failures == 2 or (consecutive_failures > 2 and consecutive_failures % 10 == 0):
                        logger.info(f"[AUTO-DISCOVERY] Triggering LAN auto-discovery for PlantOps Server (Port {self.server_port})...")
                        discovered = auto_discover_plantops_server(self.server_port)
                        if discovered and discovered != self.server_ip:
                            logger.info(f"[AUTO-DISCOVERY] Discovered Server at {discovered}:{self.server_port}! Reconnecting...")
                            self.server_ip = discovered
                            self._persist_server_ip(discovered)
                            consecutive_failures = 0

            except Exception as e:
                logger.error(f"Unexpected error in agent loop: {e}", exc_info=True)

            time.sleep(self.sync_interval)

    def stop(self):
        self.running = False


def launch_permission_setup_gui(config_path: Optional[Path] = None, on_save_callback=None, is_startup: bool = False) -> bool:
    """Industrial GUI for configuring target server IP, station identifiers, and granular dashboard telemetry permissions."""
    import tkinter as tk
    from tkinter import messagebox

    conf_file = Path(config_path or (APP_DIR / "config.json"))
    conf = {}
    if conf_file.is_file():
        try:
            with open(conf_file, "r", encoding="utf-8") as f:
                conf = json.load(f)
        except Exception:
            pass

    perms = conf.get("permissions", {
        "cpu": True,
        "ram": True,
        "disk": True,
        "network": True,
        "uptime": True,
        "zebra": False,
        "allow_remote_stop": True
    })

    root = tk.Tk()
    root.title("LineNode Agent - Telemetry Permissions & Station Setup")
    
    # Calculate screen center
    root.update_idletasks()
    sw = root.winfo_screenwidth()
    sh = root.winfo_screenheight()
    w = 640
    h = min(730, sh - 60)
    x = max(0, (sw - w) // 2)
    y = max(0, (sh - h) // 2)
    root.geometry(f"{w}x{h}+{x}+{y}")
    root.minsize(580, 560)
    root.configure(bg="#0b1220")

    if sys.platform == "win32":
        try:
            import ctypes
            ctypes.windll.shell32.SetCurrentProcessExplicitAppUserModelID("PlantOps.LineNode.PermissionsSetup")
        except Exception:
            pass
        ico = APP_DIR / "linenode_agent.ico"
        if ico.is_file():
            try:
                root.iconbitmap(str(ico))
            except Exception:
                pass

    proceed_flag = [False]

    # Header
    header = tk.Frame(root, bg="#111c30", height=68)
    header.pack(fill=tk.X)
    header.pack_propagate(False)

    head_top = tk.Frame(header, bg="#111c30")
    head_top.pack(fill=tk.X, padx=20, pady=(10, 0))
    title_lbl = tk.Label(head_top, text="LineNode Agent // Telemetry Permissions", font=("Segoe UI", 12, "bold"), bg="#111c30", fg="#38bdf8")
    title_lbl.pack(side=tk.LEFT)
    mode_badge = tk.Label(head_top, text="● ACCESS POLICY", font=("Consolas", 8, "bold"), bg="#1e293b", fg="#34d399", padx=6, pady=2)
    mode_badge.pack(side=tk.RIGHT)

    sub_lbl = tk.Label(header, text="Select which workstation telemetry metrics this PC is allowed to transmit to the Central SCADA Dashboard", font=("Segoe UI", 8), bg="#111c30", fg="#94a3b8")
    sub_lbl.pack(anchor=tk.W, padx=20, pady=(2, 0))

    # Canvas + Scrollbar Container
    container = tk.Frame(root, bg="#0b1220")
    container.pack(fill=tk.BOTH, expand=True, padx=15, pady=8)

    canvas = tk.Canvas(container, bg="#0b1220", highlightthickness=0)
    scrollbar = tk.Scrollbar(container, orient="vertical", command=canvas.yview)
    scrollable_frame = tk.Frame(canvas, bg="#0b1220")

    scrollable_frame.bind(
        "<Configure>",
        lambda e: canvas.configure(scrollregion=canvas.bbox("all"))
    )
    canvas_window = canvas.create_window((0, 0), window=scrollable_frame, anchor="nw")

    def on_canvas_configure(event):
        canvas.itemconfig(canvas_window, width=event.width)
    canvas.bind("<Configure>", on_canvas_configure)
    canvas.configure(yscrollcommand=scrollbar.set)

    canvas.pack(side=tk.LEFT, fill=tk.BOTH, expand=True)
    scrollbar.pack(side=tk.RIGHT, fill=tk.Y)

    def _on_mousewheel(event):
        canvas.yview_scroll(int(-1 * (event.delta / 120)), "units")
    canvas.bind_all("<MouseWheel>", _on_mousewheel)

    # --- Section 1: Live Hardware & Network Identity ---
    host_name = socket.gethostname()
    current_detected_ip = HardwareTelemetry.get_local_ip(conf.get("server_ip", "127.0.0.1"), conf.get("server_port", 8090))
    conn_info = HardwareTelemetry.get_connection_type(current_detected_ip)
    conn_type_str = "📶 Wi-Fi" if conn_info.get("is_wifi") else "🔌 LAN (Ethernet)"
    if conn_info.get("details"):
        conn_type_str += f" - {conn_info['details']}"

    info_frame = tk.Frame(scrollable_frame, bg="#162238", bd=1, relief=tk.SOLID)
    info_frame.pack(fill=tk.X, pady=(0, 10), ipady=5, ipadx=10)

    tk.Label(info_frame, text=f"THIS PC HOSTNAME: {host_name}  |  ACTIVE IP: {current_detected_ip}", font=("Consolas", 9, "bold"), bg="#162238", fg="#f8fafc").pack(anchor=tk.W)
    tk.Label(info_frame, text=f"CONNECTION TYPE: {conn_type_str}", font=("Segoe UI", 8), bg="#162238", fg="#34d399").pack(anchor=tk.W)

    # --- Section 2: Target Central SCADA Server ---
    srv_frame = tk.LabelFrame(scrollable_frame, text=" Central SCADA Server Target ", font=("Segoe UI", 9, "bold"), bg="#0b1220", fg="#38bdf8", bd=1)
    srv_frame.pack(fill=tk.X, pady=(0, 10), padx=2, ipady=4, ipadx=10)

    tk.Label(srv_frame, text="SCADA Server IPv4 / Hostname:", font=("Segoe UI", 8), bg="#0b1220", fg="#cbd5e1").grid(row=0, column=0, sticky="w", pady=(2, 2))
    srv_ip_var = tk.StringVar(value=conf.get("server_ip", current_detected_ip))
    srv_ip_entry = tk.Entry(srv_frame, textvariable=srv_ip_var, font=("Consolas", 10), bg="#1e293b", fg="#ffffff", insertbackground="#38bdf8", bd=1)
    srv_ip_entry.grid(row=1, column=0, sticky="ew", padx=(0, 10))

    tk.Label(srv_frame, text="Port:", font=("Segoe UI", 8), bg="#0b1220", fg="#cbd5e1").grid(row=0, column=1, sticky="w", pady=(2, 2))
    srv_port_var = tk.StringVar(value=str(conf.get("server_port", 8090)))
    srv_port_entry = tk.Entry(srv_frame, textvariable=srv_port_var, font=("Consolas", 10), width=8, bg="#1e293b", fg="#ffffff", insertbackground="#38bdf8", bd=1)
    srv_port_entry.grid(row=1, column=1, sticky="w")

    srv_frame.columnconfigure(0, weight=1)

    btn_box = tk.Frame(srv_frame, bg="#0b1220")
    btn_box.grid(row=2, column=0, columnspan=2, sticky="ew", pady=(6, 2))

    status_var = tk.StringVar(value="")
    status_lbl = tk.Label(btn_box, textvariable=status_var, font=("Segoe UI", 8, "italic"), bg="#0b1220", fg="#94a3b8")
    status_lbl.pack(side=tk.RIGHT)

    def do_auto_detect():
        status_lbl.configure(fg="#38bdf8")
        status_var.set("Scanning LAN for PlantOps Server...")
        root.update_idletasks()

        def scan_worker():
            port = int(srv_port_var.get().strip() or "8090")
            discovered = auto_discover_plantops_server(port)
            if discovered:
                srv_ip_var.set(discovered)
                status_lbl.configure(fg="#34d399")
                status_var.set(f"Discovered: {discovered}!")
                do_test_connection()
            else:
                status_lbl.configure(fg="#fbbf24")
                status_var.set("No SCADA server found on LAN.")

        threading.Thread(target=scan_worker, daemon=True).start()

    def do_test_connection():
        ip = srv_ip_var.get().strip()
        port = srv_port_var.get().strip() or "8090"
        status_var.set("Testing connection...")
        root.update_idletasks()
        start = time.perf_counter()
        try:
            url = f"http://{ip}:{port}/api/stations"
            req = urllib.request.Request(url, headers={"User-Agent": "LineNodeAgent-Test"})
            with urllib.request.urlopen(req, timeout=2.0) as resp:
                if resp.status == 200:
                    lat = round((time.perf_counter() - start) * 1000, 1)
                    status_lbl.configure(fg="#34d399")
                    status_var.set(f"Connected! ({lat} ms)")
                    return
        except Exception as e:
            status_lbl.configure(fg="#f87171")
            status_var.set(f"Unreachable ({type(e).__name__})")

    tk.Button(btn_box, text="🔍 Auto-Detect Server", command=do_auto_detect, font=("Segoe UI", 8, "bold"), bg="#0284c7", fg="#ffffff", relief=tk.FLAT, padx=8, pady=2).pack(side=tk.LEFT, padx=(0, 6))
    tk.Button(btn_box, text="⚡ Test Ping", command=do_test_connection, font=("Segoe UI", 8), bg="#334155", fg="#f8fafc", relief=tk.FLAT, padx=8, pady=2).pack(side=tk.LEFT)

    # --- Section 3: Workstation Identification ---
    st_frame = tk.LabelFrame(scrollable_frame, text=" Workstation Tag & Identification ", font=("Segoe UI", 9, "bold"), bg="#0b1220", fg="#38bdf8", bd=1)
    st_frame.pack(fill=tk.X, pady=(0, 10), padx=2, ipady=4, ipadx=10)

    tk.Label(st_frame, text="Station ID (Unique Tag):", font=("Segoe UI", 8), bg="#0b1220", fg="#cbd5e1").grid(row=0, column=0, sticky="w", pady=(2, 2))
    st_id_var = tk.StringVar(value=conf.get("station_id", "LINE1_STATION_06"))
    tk.Entry(st_frame, textvariable=st_id_var, font=("Consolas", 10), bg="#1e293b", fg="#ffffff", insertbackground="#38bdf8", bd=1).grid(row=1, column=0, sticky="ew", padx=(0, 10))

    tk.Label(st_frame, text="Station Display Name:", font=("Segoe UI", 8), bg="#0b1220", fg="#cbd5e1").grid(row=0, column=1, sticky="w", pady=(2, 2))
    st_name_var = tk.StringVar(value=conf.get("station_name", "RAC Packaging & Labeling"))
    tk.Entry(st_frame, textvariable=st_name_var, font=("Segoe UI", 9), bg="#1e293b", fg="#ffffff", insertbackground="#38bdf8", bd=1).grid(row=1, column=1, sticky="ew")

    st_frame.columnconfigure(0, weight=1)
    st_frame.columnconfigure(1, weight=1)

    # --- Section 4: DASHBOARD TELEMETRY & VISIBILITY PERMISSIONS (The Core User Request) ---
    perm_frame = tk.LabelFrame(scrollable_frame, text=" Dashboard Telemetry & Visibility Permissions ", font=("Segoe UI", 9, "bold"), bg="#0b1220", fg="#38bdf8", bd=1)
    perm_frame.pack(fill=tk.X, pady=(0, 10), padx=2, ipady=6, ipadx=10)

    tk.Label(perm_frame, text="Select which metrics this workstation is permitted to transmit and display on the Central SCADA Dashboard:", font=("Segoe UI", 8), bg="#0b1220", fg="#94a3b8", wraplength=540, justify="left").pack(anchor="w", padx=4, pady=(2, 6))

    def make_check(parent, text, subtext, var, color="#f1f5f9", bold=False):
        f = tk.Frame(parent, bg="#0b1220")
        f.pack(fill=tk.X, pady=3, padx=4)
        font = ("Segoe UI", 9, "bold") if bold else ("Segoe UI", 9)
        cb = tk.Checkbutton(f, text=text, variable=var, font=font, bg="#0b1220", fg=color, selectcolor="#1e293b", activebackground="#0b1220", activeforeground="#38bdf8")
        cb.pack(anchor="w")
        if subtext:
            sub = tk.Label(f, text=subtext, font=("Segoe UI", 8), bg="#0b1220", fg="#64748b")
            sub.pack(anchor="w", padx=(24, 0))
        return f

    cpu_var = tk.BooleanVar(value=perms.get("cpu", True))
    make_check(perm_frame, "☑ CPU Workload & Thermals", "Transmit real-time CPU utilization % and core temperature sensors to dashboard", cpu_var)

    ram_var = tk.BooleanVar(value=perms.get("ram", True))
    make_check(perm_frame, "☑ RAM Physical Memory Commit", "Transmit memory commit %, used GB, and physical RAM capacity", ram_var)

    disk_var = tk.BooleanVar(value=perms.get("disk", True))
    make_check(perm_frame, "☑ Storage & NVMe/SSD Health", "Transmit drive C:\\ used space, free capacity, and volume utilization", disk_var)

    net_var = tk.BooleanVar(value=perms.get("network", True))
    make_check(perm_frame, "☑ Network Connection & Ping Latency", "Transmit connection adapter type (LAN / Wi-Fi), IP address, and TCP ping latency", net_var)

    uptime_var = tk.BooleanVar(value=perms.get("uptime", True))
    make_check(perm_frame, "☑ System OS Uptime & Hostname", "Display workstation boot elapsed timer and Windows computer name on dashboard card", uptime_var)

    zebra_var = tk.BooleanVar(value=perms.get("zebra", False))
    make_check(perm_frame, "🖨 Zebra ZT411 Industrial Label Printer Watchdog", "⚠️ Turn ON ONLY if a physical Zebra industrial printer is connected via USB/WinSpool. (Disabled by default to prevent false stoppage alarms)", zebra_var, color="#f59e0b", bold=True)

    stop_var = tk.BooleanVar(value=perms.get("allow_remote_stop", True))
    make_check(perm_frame, "☑ Allow Central SCADA Remote Stop & Deregister", "Permit central SCADA operators to deregister or terminate this node from the dashboard", stop_var)

    # --- Section 5: Startup Preference ---
    pref_frame = tk.Frame(scrollable_frame, bg="#0b1220")
    pref_frame.pack(fill=tk.X, pady=(4, 10), padx=4)

    prompt_var = tk.BooleanVar(value=conf.get("prompt_permissions_on_startup", True))
    tk.Checkbutton(pref_frame, text="Prompt this permissions window every time LineNode Agent starts", variable=prompt_var, font=("Segoe UI", 9, "italic"), bg="#0b1220", fg="#38bdf8", selectcolor="#1e293b", activebackground="#0b1220", activeforeground="#38bdf8").pack(anchor="w")
    tk.Label(pref_frame, text="(If unchecked, agent starts directly in the background with these saved permissions)", font=("Segoe UI", 8), bg="#0b1220", fg="#64748b").pack(anchor="w", padx=(24, 0))

    # --- Bottom Actions ---
    actions = tk.Frame(root, bg="#111c30", height=60)
    actions.pack(fill=tk.X, side=tk.BOTTOM)
    actions.pack_propagate(False)

    def on_cancel():
        proceed_flag[0] = False
        try:
            canvas.unbind_all("<MouseWheel>")
        except Exception:
            pass
        root.destroy()

    def save_and_start():
        new_conf = dict(conf)
        new_conf["server_ip"] = srv_ip_var.get().strip() or "127.0.0.1"
        new_conf["server_port"] = int(srv_port_var.get().strip() or "8090")
        new_conf["station_id"] = st_id_var.get().strip() or "LINE1_STATION_06"
        new_conf["station_name"] = st_name_var.get().strip() or "Workstation"
        new_conf["prompt_permissions_on_startup"] = prompt_var.get()
        new_conf["permissions"] = {
            "cpu": cpu_var.get(),
            "ram": ram_var.get(),
            "disk": disk_var.get(),
            "network": net_var.get(),
            "uptime": uptime_var.get(),
            "zebra": zebra_var.get(),
            "allow_remote_stop": stop_var.get()
        }
        if "printer_monitoring" not in new_conf:
            new_conf["printer_monitoring"] = {"zebra_vid": "0A5F", "name_pattern": "ZT411"}
        new_conf["printer_monitoring"]["enabled"] = zebra_var.get()
        new_conf["sync_interval_sec"] = conf.get("sync_interval_sec", 3)

        try:
            with open(conf_file, "w", encoding="utf-8") as f:
                json.dump(new_conf, f, indent=2)
            if on_save_callback:
                on_save_callback(new_conf)
            proceed_flag[0] = True
            try:
                canvas.unbind_all("<MouseWheel>")
            except Exception:
                pass
            root.destroy()
        except Exception as e:
            messagebox.showerror("Error", f"Failed to save permissions: {e}")

    root.protocol("WM_DELETE_WINDOW", on_cancel)

    tk.Button(actions, text="❌ Exit / Cancel", command=on_cancel, font=("Segoe UI", 9), bg="#334155", fg="#f8fafc", relief=tk.FLAT, padx=14, pady=5).pack(side=tk.LEFT, padx=20, pady=10)
    btn_save_text = "🚀 Save Permissions & Start Monitoring" if is_startup else "💾 Save & Apply Permissions"
    tk.Button(actions, text=btn_save_text, command=save_and_start, font=("Segoe UI", 9, "bold"), bg="#10b981", fg="#ffffff", relief=tk.FLAT, padx=18, pady=5).pack(side=tk.RIGHT, padx=20, pady=10)

    root.mainloop()
    return proceed_flag[0]


launch_network_config_gui = launch_permission_setup_gui


class SystemTrayApp:
    """Manages the Windows System Tray notification area icon for background execution."""

    def __init__(self, agent: LineNodeAgent):
        self.agent = agent
        self.tray_icon = None

    def open_permissions_config(self, icon=None, item=None):
        launch_permission_setup_gui(
            config_path=getattr(self.agent, "config_file_path", APP_DIR / "config.json"),
            on_save_callback=self.agent.reload_config,
            is_startup=False
        )

    def create_image(self, color="green"):
        candidates = [
            APP_DIR / "linenode_agent.ico",
            BUNDLE_DIR / "linenode_agent.ico"
        ]
        for c in candidates:
            if c.is_file():
                try:
                    return Image.open(c).resize((64, 64))
                except Exception:
                    pass

        img = Image.new('RGBA', (64, 64), (0, 0, 0, 0))
        draw = ImageDraw.Draw(img)
        fill = (16, 185, 129, 255) if color == "green" else (239, 68, 68, 255)
        outline = (6, 95, 70, 255) if color == "green" else (153, 27, 27, 255)
        draw.ellipse((6, 6, 58, 58), fill=fill, outline=outline, width=4)
        draw.ellipse((22, 22, 42, 42), fill=(255, 255, 255, 230))
        return img

    def open_logs(self, icon=None, item=None):
        log_path = APP_DIR / "line_node_agent.log"
        if log_path.is_file():
            startupinfo = None
            if sys.platform == "win32":
                startupinfo = subprocess.STARTUPINFO()
            subprocess.Popen(["notepad.exe", str(log_path)], startupinfo=startupinfo)

    def open_dashboard(self, icon=None, item=None):
        url = f"http://{self.agent.server_ip}:{self.agent.server_port}"
        import webbrowser
        webbrowser.open(url)

    def on_exit(self, icon=None, item=None):
        logger.info("Exiting via system tray menu...")
        self.agent.stop()
        if self.tray_icon:
            self.tray_icon.stop()

    def run(self):
        t = threading.Thread(target=self.agent.run, daemon=True)
        t.start()

        if not HAS_TRAY:
            logger.info("Running in headless background mode (no tray).")
            try:
                while self.agent.running:
                    time.sleep(1)
            except KeyboardInterrupt:
                self.agent.stop()
            return

        menu = pystray.Menu(
            pystray.MenuItem(f"Station: {self.agent.station_id}", lambda i, it: None, enabled=False),
            pystray.MenuItem(f"Host: {socket.gethostname()}", lambda i, it: None, enabled=False),
            pystray.MenuItem(f"Server: {self.agent.server_ip}:{self.agent.server_port}", lambda i, it: None, enabled=False),
            pystray.Menu.SEPARATOR,
            pystray.MenuItem("🛡️ Telemetry & Station Permissions", self.open_permissions_config),
            pystray.MenuItem("🌐 Open SCADA Dashboard", self.open_dashboard),
            pystray.MenuItem("📄 View Live Log (Notepad)", self.open_logs),
            pystray.Menu.SEPARATOR,
            pystray.MenuItem("🛑 Exit LineNode Agent", self.on_exit)
        )

        self.tray_icon = pystray.Icon(
            "LineNodeAgent",
            self.create_image("green"),
            f"LineNode Agent: {self.agent.station_id} ({socket.gethostname()})",
            menu=menu
        )
        self.tray_icon.run()


def main():
    if sys.platform == "win32":
        try:
            import ctypes
            ctypes.windll.shell32.SetCurrentProcessExplicitAppUserModelID("PlantOps.LineNode.Agent.v24")
        except Exception:
            pass

    parser = argparse.ArgumentParser(description="LineNode Agent - RAC Assembly Line Station Monitor")
    parser.add_argument("--config", default="config.json", help="Path to configuration JSON file")
    parser.add_argument("--config-gui", action="store_true", help="Launch Telemetry Permissions & Setup GUI")
    parser.add_argument("--no-prompt", action="store_true", help="Skip permissions prompt on startup and run directly")
    parser.add_argument("--station", help="Override station ID (e.g., LINE1_STATION_06)")
    parser.add_argument("--server", help="Override server IP (e.g., 192.168.1.100)")
    parser.add_argument("--port", type=int, help="Override server port (e.g., 8090)")
    parser.add_argument("--no-tray", action="store_true", help="Run in pure CLI mode without tray icon")
    parser.add_argument("--sim-zebra", choices=["READY", "OFFLINE", "PAPER_OUT", "DOOR_OPEN", "ERROR", "DISCONNECTED"],
                        help="Simulate Zebra printer state for testing")
    args = parser.parse_args()

    conf_path = APP_DIR / args.config
    config = ConfigManager.load_config(args.config)

    # Check whether we should prompt the permissions GUI on startup
    prompt_startup = config.get("prompt_permissions_on_startup", True)
    if args.config_gui or (prompt_startup and not args.no_prompt and not args.no_tray):
        proceed = launch_permission_setup_gui(conf_path, is_startup=True)
        if not proceed:
            logger.info("Agent startup cancelled by user from permissions dialog.")
            sys.exit(0)
        # Reload updated configuration
        config = ConfigManager.load_config(args.config)

    if args.station:
        config["station_id"] = args.station
    if args.server:
        config["server_ip"] = args.server
    if args.port:
        config["server_port"] = args.port
    if args.sim_zebra:
        config["sim_zebra_state"] = args.sim_zebra

    agent = LineNodeAgent(config)
    agent.config_file_path = conf_path
    
    if args.no_tray or not HAS_TRAY:
        try:
            agent.run()
        except KeyboardInterrupt:
            logger.info("Agent termination requested by user. Exiting cleanly.")
            agent.stop()
    else:
        tray = SystemTrayApp(agent)
        tray.run()


if __name__ == "__main__":
    main()
