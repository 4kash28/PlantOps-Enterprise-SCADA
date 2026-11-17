import subprocess
import time
import requests
import json
import sys

print("=== Testing SCADA Network Initialization Wizard Endpoints ===")

server_proc = subprocess.Popen(
    [sys.executable, "server.py"],
    cwd=r"C:\Users\ITSUPA\.gemini\antigravity\scratch\rac_industrial_monitor\plantops_monitor"
)

server_url = "http://127.0.0.1:8090"
started = False
for _ in range(12):
    try:
        r = requests.get(f"{server_url}/api/network-details", timeout=1)
        if r.status_code == 200:
            started = True
            break
    except Exception:
        time.sleep(1)

if not started:
    print("[X] Server failed to start.")
    server_proc.terminate()
    sys.exit(1)

# 1. Test GET /api/network-details
res = requests.get(f"{server_url}/api/network-details")
print(f"[1] GET /api/network-details (Status: {res.status_code}):")
net_data = res.json()
print("  Hostname:", net_data.get("hostname"))
print("  Configured IP:", net_data.get("configured", {}).get("ip"))
print("  Configured Subnet:", net_data.get("configured", {}).get("subnet_cidr"))
print("  Configured Gateway:", net_data.get("configured", {}).get("gateway"))
print("  Detected Physical Adapters count:", len(net_data.get("adapters", [])))
for a in net_data.get("adapters", []):
    print(f"    - {a.get('name')}: IP={a.get('ip')}, Subnet={a.get('subnet')}, GW={a.get('gateway')}")

# 2. Test POST /api/network-config
print("\n[2] Testing POST /api/network-config ...")
cfg_payload = {
    "ip": "192.168.19.170",
    "subnet_mask": "255.255.255.0",
    "gateway": "192.168.19.1",
    "port": 8090,
    "enforce_subnet": True,
    "prompt_on_startup": True
}
res_post = requests.post(f"{server_url}/api/network-config", json=cfg_payload)
print(f"  Status: {res_post.status_code}")
print("  Response:", json.dumps(res_post.json(), indent=2))

# 3. Test telemetry from a valid node in the subnet
print("\n[3] Testing telemetry from valid node in 192.168.19.0/24 subnet ...")
valid_payload = {
    "station_id": "LINE1_STATION_01",
    "station_name": "RAC Assembly ST-01",
    "hostname": "LINE1-WORKSTATION",
    "ip_address": "192.168.19.185",
    "agent_version": "2.4.0",
    "system_telemetry": {"cpu_percent": 35.0, "ram_percent": 62.0, "network_latency_ms": 1.5},
    "zebra_printer": {"monitored": False, "status": "READY"}
}
r_valid = requests.post(f"{server_url}/api/telemetry", json=valid_payload)
print("  Valid Station Telemetry Status:", r_valid.status_code)

# 4. Test telemetry from an invalid foreign node (e.g. 10.0.0.99) with enforce_subnet=True
print("\n[4] Testing telemetry from foreign IP (10.0.0.99) with enforce_subnet=True ...")
foreign_payload = {
    "station_id": "ROGUE_PC",
    "station_name": "Rogue PC",
    "hostname": "FOREIGN-PC",
    "ip_address": "10.0.0.99",
    "agent_version": "2.4.0",
    "system_telemetry": {"cpu_percent": 99.0, "ram_percent": 99.0},
    "zebra_printer": {"monitored": False, "status": "READY"}
}
r_foreign = requests.post(f"{server_url}/api/telemetry", json=foreign_payload)
print("  Foreign Station Telemetry Status:", r_foreign.status_code)

# Check stations count
res_st = requests.get(f"{server_url}/api/stations")
stations = res_st.json().get("stations", [])
print(f"\n[5] Total stations active on dashboard: {len(stations)}")
for s in stations:
    print(f"  - Station: {s.get('station_id')} | IP: {s.get('ip_address')} | Host: {s.get('hostname')}")

if any(s.get('station_id') == "LINE1_STATION_01" for s in stations) and not any(s.get('station_id') == "ROGUE_PC" for s in stations):
    print("\n[✓] Subnet filtering & validation test PASSED! Only configured subnet PCs are accepted!")
else:
    print("\n[!] Check filtering results above.")

server_proc.terminate()
try:
    server_proc.kill()
except Exception:
    pass

print("\n=== All Tests Finished ===")
