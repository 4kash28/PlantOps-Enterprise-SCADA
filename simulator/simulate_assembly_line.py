"""
RAC Assembly Line - Multi-Station Telemetry Simulator
Simulates all 6 production workstations with realistic sensor telemetry & Zebra ZT411 events.
Allows immediate end-to-end verification of the PlantOps Monitor without physical line hardware.
"""

import sys
import time
import json
import random
import datetime
import threading
import urllib.request
import urllib.error
from typing import Dict, Any, List

SERVER_URL = "http://127.0.0.1:8090/api/telemetry"

STATIONS = [
    {
        "station_id": "LINE1_STATION_01",
        "station_name": "ST-01: Chassis Press & Base Assembly",
        "ip": "192.168.1.101",
        "zebra_monitored": False,
        "base_cpu": 22.0,
        "base_ram": 38.0
    },
    {
        "station_id": "LINE1_STATION_02",
        "station_name": "ST-02: Evaporator Coil & Copper Brazing",
        "ip": "192.168.1.102",
        "zebra_monitored": False,
        "base_cpu": 35.0,
        "base_ram": 45.0
    },
    {
        "station_id": "LINE1_STATION_03",
        "station_name": "ST-03: Compressor & Motor Mounting",
        "ip": "192.168.1.103",
        "zebra_monitored": False,
        "base_cpu": 18.0,
        "base_ram": 32.0
    },
    {
        "station_id": "LINE1_STATION_04",
        "station_name": "ST-04: Electrical & Inverter PCB Wiring",
        "ip": "192.168.1.104",
        "zebra_monitored": False,
        "base_cpu": 40.0,
        "base_ram": 52.0
    },
    {
        "station_id": "LINE1_STATION_05",
        "station_name": "ST-05: Vacuum Decay & R32 Charge",
        "ip": "192.168.1.105",
        "zebra_monitored": False,
        "base_cpu": 28.0,
        "base_ram": 48.0
    },
    {
        "station_id": "LINE1_STATION_06",
        "station_name": "ST-06: Final QA Run-Test & Zebra ZT411 Labeling",
        "ip": "192.168.1.106",
        "zebra_monitored": True,
        "base_cpu": 30.0,
        "base_ram": 42.0
    }
]

# Shared simulation state flags
sim_state = {
    "zebra_status": "READY",
    "zebra_msg": "Zebra ZT411 is Ready and Online",
    "offline_stations": set(),
    "high_cpu_stations": set(),
    "running": True
}


def send_telemetry(payload: Dict[str, Any], url: str = SERVER_URL) -> bool:
    try:
        data = json.dumps(payload).encode("utf-8")
        req = urllib.request.Request(
            url,
            data=data,
            headers={"Content-Type": "application/json", "User-Agent": "RAC-Line-Simulator/1.0"}
        )
        with urllib.request.urlopen(req, timeout=1.5) as resp:
            return resp.status == 200
    except Exception:
        return False


def station_worker(station_info: Dict[str, Any], server_url: str):
    sid = station_info["station_id"]
    sname = station_info["station_name"]
    ip = station_info["ip"]
    is_zebra = station_info["zebra_monitored"]
    boot_sec = random.randint(100000, 500000)

    while sim_state["running"]:
        if sid in sim_state["offline_stations"]:
            # Suppress heartbeat to simulate network loss or PC freeze
            time.sleep(1.0)
            continue

        boot_sec += 3
        days = boot_sec // 86400
        hrs = (boot_sec % 86400) // 3600
        mins = (boot_sec % 3600) // 60
        secs = boot_sec % 60
        uptime_fmt = f"{days}d {hrs:02d}:{mins:02d}:{secs:02d}"

        # Jitter CPU and RAM
        if sid in sim_state["high_cpu_stations"]:
            cpu = round(min(98.5, station_info["base_cpu"] + 55.0 + random.uniform(-2, 3)), 1)
        else:
            cpu = round(max(5.0, min(80.0, station_info["base_cpu"] + random.uniform(-4, 5))), 1)

        ram = round(max(20.0, min(85.0, station_info["base_ram"] + random.uniform(-2, 2))), 1)
        latency = round(random.uniform(1.2, 4.5), 1)

        # Zebra status
        if is_zebra:
            z_status = sim_state["zebra_status"]
            z_msg = sim_state["zebra_msg"]
            zebra_payload = {
                "monitored": True,
                "connected": z_status != "DISCONNECTED",
                "status": z_status,
                "status_message": z_msg,
                "model_name": "Zebra ZT411 203dpi",
                "device_id": "USB\\VID_0A5F&PID_0164\\18J202201234",
                "spooler_flags": [z_status] if z_status != "READY" else []
            }
        else:
            zebra_payload = {
                "monitored": False,
                "connected": False,
                "status": "NOT_MONITORED",
                "status_message": "No peripheral configured",
                "model_name": None,
                "device_id": None,
                "spooler_flags": []
            }

        payload = {
            "station_id": sid,
            "station_name": sname,
            "ip_address": ip,
            "timestamp": datetime.datetime.now(datetime.timezone.utc).isoformat(),
            "agent_version": "2.4.0-SIMULATED",
            "system_telemetry": {
                "cpu_percent": cpu,
                "cpu_temp_c": round(42.0 + (cpu * 0.25), 1),
                "ram_total_gb": 16.0,
                "ram_used_gb": round(16.0 * (ram / 100.0), 2),
                "ram_percent": ram,
                "disks": [
                    {
                        "drive": "C:",
                        "mountpoint": "C:\\",
                        "total_gb": 476.2,
                        "used_gb": 182.4,
                        "free_gb": 293.8,
                        "percent": 38.3
                    }
                ],
                "uptime_seconds": boot_sec,
                "uptime_formatted": uptime_fmt,
                "network_latency_ms": latency
            },
            "zebra_printer": zebra_payload
        }

        send_telemetry(payload, server_url)
        time.sleep(3.0)


def print_menu():
    print("\n" + "=" * 65)
    print("  RAC ASSEMBLY LINE SIMULATOR // INTERACTIVE COMMANDS:")
    print("  [1] Set Zebra ZT411 -> PAPER OUT")
    print("  [2] Set Zebra ZT411 -> OFFLINE / DISCONNECTED")
    print("  [3] Set Zebra ZT411 -> READY (Normal)")
    print("  [4] Cut network to Station 3 (Simulate Station Freeze > 10s)")
    print("  [5] Restore network to Station 3 (Simulate Recovery)")
    print("  [6] Toggle High CPU Load on Station 2 (95%)")
    print("  [Q] Quit Simulator")
    print("=" * 65 + "\n")


def main():
    import argparse
    parser = argparse.ArgumentParser(description="Multi-Station Telemetry Simulator")
    parser.add_argument("--url", default="http://127.0.0.1:8080/api/telemetry", help="Target Server API URL")
    args = parser.parse_args()

    print("=" * 65)
    print("  RAC ASSEMBLY LINE SIMULATOR INITIALIZING...")
    print(f"  Target Server Endpoint: {args.url}")
    print(f"  Simulating {len(STATIONS)} Assembly Workstations...")
    print("=" * 65)

    # Launch a worker thread for each workstation
    threads = []
    for s in STATIONS:
        t = threading.Thread(target=station_worker, args=(s, args.url), daemon=True)
        t.start()
        threads.append(t)

    print("[SUCCESS] All 6 station background threads active.")
    print_menu()

    try:
        while True:
            cmd = input("Select Command [1-6, Q]: ").strip().upper()
            if cmd == "1":
                sim_state["zebra_status"] = "PAPER_OUT"
                sim_state["zebra_msg"] = "Zebra ZT411 Media/Paper Out"
                print(">>> [SIM EVENT] Station 6 Zebra Printer state set to: PAPER_OUT")
            elif cmd == "2":
                sim_state["zebra_status"] = "DISCONNECTED"
                sim_state["zebra_msg"] = "Zebra ZT411 USB Disconnected / Unplugged"
                print(">>> [SIM EVENT] Station 6 Zebra Printer state set to: DISCONNECTED")
            elif cmd == "3":
                sim_state["zebra_status"] = "READY"
                sim_state["zebra_msg"] = "Zebra ZT411 is Ready and Online"
                print(">>> [SIM EVENT] Station 6 Zebra Printer state set to: READY")
            elif cmd == "4":
                sim_state["offline_stations"].add("LINE1_STATION_03")
                print(">>> [SIM EVENT] Station 3 heartbeats stopped. Downtime alert will trigger in 10s.")
            elif cmd == "5":
                sim_state["offline_stations"].discard("LINE1_STATION_03")
                print(">>> [SIM EVENT] Station 3 heartbeats restored. Downtime incident will resolve.")
            elif cmd == "6":
                if "LINE1_STATION_02" in sim_state["high_cpu_stations"]:
                    sim_state["high_cpu_stations"].discard("LINE1_STATION_02")
                    print(">>> [SIM EVENT] Station 2 CPU returned to normal (35%).")
                else:
                    sim_state["high_cpu_stations"].add("LINE1_STATION_02")
                    print(">>> [SIM EVENT] Station 2 CPU spiked to 95% (Warning threshold).")
            elif cmd == "Q":
                print("Exiting simulator...")
                sim_state["running"] = False
                break
            else:
                print("Invalid choice.")
                print_menu()
    except (KeyboardInterrupt, EOFError):
        sim_state["running"] = False
        print("\nSimulator stopped.")


if __name__ == "__main__":
    main()
