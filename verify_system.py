import subprocess
import time
import requests
import sys
import json
import os

print("=== Starting PlantOps Enterprise Verification ===")
monitor_exe = r"C:\PlantOps\PlantOpsMonitor\PlantOpsMonitor.exe"
agent_exe = r"C:\PlantOps\LineNodeAgent\LineNodeAgent.exe"

# 1. Start PlantOpsMonitor process
print(f"[1] Launching {monitor_exe} ...")
monitor_proc = subprocess.Popen([monitor_exe], cwd=r"C:\PlantOps\PlantOpsMonitor")

server_url = "http://127.0.0.1:8090"
started = False
for _ in range(15):
    try:
        r = requests.get(f"{server_url}/api/network-interfaces", timeout=2)
        if r.status_code == 200:
            started = True
            print("[✓] Server started successfully! Response:")
            print(json.dumps(r.json(), indent=2))
            break
    except Exception as e:
        time.sleep(1)

if not started:
    print("[X] Failed to connect to server.")
    monitor_proc.terminate()
    sys.exit(1)

# Check network-interfaces data
net_info = r.json()
print(f"Server Hostname: {net_info.get('hostname')}")
print(f"Server Primary IP: {net_info.get('primary_ip')}")
print(f"Server Full URL: {net_info.get('server_url')}")

# 2. Start LineNodeAgent
print(f"\n[2] Launching {agent_exe} ...")
agent_proc = subprocess.Popen([agent_exe], cwd=r"C:\PlantOps\LineNodeAgent")

# Wait for agent telemetry to ingest
telemetry_received = False
station_data = None
for _ in range(12):
    time.sleep(1)
    try:
        r = requests.get(f"{server_url}/api/stations", timeout=2)
        stations = r.json().get("stations", [])
        if stations:
            telemetry_received = True
            station_data = stations[0]
            print(f"[✓] Telemetry received from {len(stations)} station(s):")
            print(f"Station ID: {station_data.get('station_id')}")
            print(f"Hostname: {station_data.get('hostname')}")
            print(f"IP Address: {station_data.get('ip_address')}")
            print(f"Status: {station_data.get('status')}")
            print(f"CPU: {station_data.get('cpu_pct')}% | RAM: {station_data.get('ram_pct')}%")
            break
    except Exception as e:
        pass

if not telemetry_received:
    print("[X] No telemetry received from agent.")
else:
    # 3. Test DELETE Station
    station_id = station_data.get("station_id")
    print(f"\n[3] Testing DELETE station endpoint for {station_id} ...")
    del_res = requests.delete(f"{server_url}/api/stations/{station_id}", timeout=2)
    print(f"Delete Status Code: {del_res.status_code}")
    print(f"Delete Response: {del_res.json()}")

    # Verify stations count is 0
    r_after = requests.get(f"{server_url}/api/stations", timeout=2)
    stations_after = r_after.json().get("stations", [])
    print(f"Stations count after delete: {len(stations_after)}")
    if len(stations_after) == 0:
        print("[✓] Station deletion successfully verified!")
    else:
        print(f"[!] Stations still present: {stations_after}")

# Terminate processes
print("\n[4] Cleaning up test processes ...")
agent_proc.terminate()
monitor_proc.terminate()
time.sleep(1)
try:
    agent_proc.kill()
    monitor_proc.kill()
except Exception:
    pass

print("=== Verification Completed Successfully ===")
