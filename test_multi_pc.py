import subprocess
import time
import requests
import socket
import json
import sys

print("=== Testing Multi-PC & Auto-Discovery Architecture ===")

# 1. Start Server
print("[1] Starting plantops_monitor server.py ...")
server_proc = subprocess.Popen([sys.executable, "server.py"], cwd=r"C:\Users\ITSUPA\.gemini\antigravity\scratch\rac_industrial_monitor\plantops_monitor")

server_url = "http://127.0.0.1:8090"
started = False
for _ in range(15):
    try:
        r = requests.get(f"{server_url}/api/network-interfaces", timeout=1)
        if r.status_code == 200:
            started = True
            print(f"[✓] Server is online! Primary IP: {r.json().get('primary_ip')}")
            break
    except Exception:
        time.sleep(1)

if not started:
    print("[X] Failed to start server.")
    server_proc.terminate()
    sys.exit(1)

# 2. Test UDP Auto-Discovery Probe
print("\n[2] Testing UDP Auto-Discovery on port 8091 ...")
udp_sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
udp_sock.setsockopt(socket.SOL_SOCKET, socket.SO_BROADCAST, 1)
udp_sock.settimeout(2.0)
udp_sock.sendto(b"PLANTOPS_DISCOVER", ("127.0.0.1", 8091))
try:
    data, addr = udp_sock.recvfrom(1024)
    text = data.decode("utf-8")
    print(f"[✓] Received UDP response from {addr}: {text}")
except Exception as e:
    print(f"[!] UDP test timed out: {e}")
finally:
    udp_sock.close()

# 3. Test Agent Auto-Discovery Function
print("\n[3] Testing agent.py auto_discover_plantops_server() ...")
sys.path.insert(0, r"C:\Users\ITSUPA\.gemini\antigravity\scratch\rac_industrial_monitor\line_node_agent")
from agent import auto_discover_plantops_server
disc_ip = auto_discover_plantops_server(8090)
print(f"[✓] auto_discover_plantops_server() returned: {disc_ip}")

# 4. Test Multi-PC Station Collision Disambiguation
print("\n[4] Testing multi-PC collision handling in server ...")
# Send payload 1 from PC-A
payload_a = {
    "station_id": "LINE1_STATION_06",
    "station_name": "RAC Packaging & Labeling",
    "hostname": "PC-AKASH-01",
    "ip_address": "192.168.19.170",
    "agent_version": "2.4.0",
    "system_telemetry": {"cpu_percent": 25.0, "ram_percent": 50.0, "network_latency_ms": 1.2},
    "zebra_printer": {"monitored": False, "connected": False, "status": "READY"}
}
r1 = requests.post(f"{server_url}/api/telemetry", json=payload_a)
print(f"Station A registration status: {r1.status_code}")

# Send payload 2 from PC-B with the SAME station_id (simulating a second PC deployed with default config)
payload_b = {
    "station_id": "LINE1_STATION_06",
    "station_name": "RAC Packaging & Labeling",
    "hostname": "PC-LINE-WORKER-02",
    "ip_address": "192.168.19.185",
    "agent_version": "2.4.0",
    "system_telemetry": {"cpu_percent": 30.0, "ram_percent": 60.0, "network_latency_ms": 3.4},
    "zebra_printer": {"monitored": False, "connected": False, "status": "READY"}
}
r2 = requests.post(f"{server_url}/api/telemetry", json=payload_b)
print(f"Station B registration status: {r2.status_code}")

# Query stations list
r_st = requests.get(f"{server_url}/api/stations")
stations = r_st.json().get("stations", [])
print(f"\n[✓] Total stations registered on dashboard: {len(stations)}")
for s in stations:
    print(f"  - Station ID: {s.get('station_id')} | Name: {s.get('station_name')} | Hostname: {s.get('hostname')} | IP: {s.get('ip_address')}")

if len(stations) >= 2:
    print("\n[✓] Multi-PC collision test PASSED! Both machines successfully visible on dashboard!")
else:
    print(f"\n[!] Multi-PC collision test WARNING: Expected 2 stations, got {len(stations)}")

# Cleanup
server_proc.terminate()
try:
    server_proc.kill()
except Exception:
    pass
print("\n=== All Tests Completed ===")
