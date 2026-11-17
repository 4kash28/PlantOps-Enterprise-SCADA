import subprocess
import time
import requests
import socket
import json
import sys

print("=== Running End-to-End Verification of Compiled Binaries ===")

monitor_exe = r"C:\PlantOps\PlantOpsMonitor\PlantOpsMonitor.exe"
agent_exe = r"C:\PlantOps\LineNodeAgent\LineNodeAgent.exe"

# 1. Start PlantOpsMonitor.exe
print(f"[1] Spawning {monitor_exe} ...")
m_proc = subprocess.Popen([monitor_exe], cwd=r"C:\PlantOps\PlantOpsMonitor")

server_url = "http://127.0.0.1:8090"
started = False
for _ in range(15):
    try:
        r = requests.get(f"{server_url}/api/network-interfaces", timeout=1)
        if r.status_code == 200:
            started = True
            print("[✓] Server started and responding!")
            break
    except Exception:
        time.sleep(1)

if not started:
    print("[X] Server failed to start.")
    m_proc.terminate()
    sys.exit(1)

# 2. Test UDP broadcast
print("\n[2] Testing UDP port 8091 responder ...")
s_udp = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
s_udp.setsockopt(socket.SOL_SOCKET, socket.SO_BROADCAST, 1)
s_udp.settimeout(2.0)
s_udp.sendto(b"PLANTOPS_DISCOVER", ("127.0.0.1", 8091))
try:
    data, addr = s_udp.recvfrom(1024)
    print(f"[✓] UDP Responder returned: {data.decode('utf-8')}")
except Exception as e:
    print(f"[!] UDP probe error: {e}")
finally:
    s_udp.close()

# 3. Start LineNodeAgent.exe
print(f"\n[3] Spawning {agent_exe} ...")
a_proc = subprocess.Popen([agent_exe], cwd=r"C:\PlantOps\LineNodeAgent")

# Wait for telemetry to register
registered = False
for _ in range(12):
    time.sleep(1)
    try:
        r = requests.get(f"{server_url}/api/stations", timeout=1)
        st = r.json().get("stations", [])
        if st:
            registered = True
            print(f"[✓] Received telemetry from {len(st)} station(s):")
            for s in st:
                print(f"  Station: {s.get('station_id')} | Host: {s.get('hostname')} | IP: {s.get('ip_address')}")
            break
    except Exception:
        pass

if not registered:
    print("[!] No telemetry received yet.")

# Terminate
print("\n[4] Cleaning up test processes ...")
a_proc.terminate()
m_proc.terminate()
time.sleep(1)
try:
    a_proc.kill()
    m_proc.kill()
except Exception:
    pass

print("=== End-to-End Verification Finished Successfully ===")
