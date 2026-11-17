import time
import subprocess
import urllib.request
import json
import os

installers_dir = r"C:\Users\ITSUPA\.gemini\antigravity\scratch\rac_industrial_monitor\WINDOWS_INSTALLERS"
scada_exe = os.path.join(installers_dir, "PlantOpsEnterpriseSCADA.exe")
agent_exe = os.path.join(installers_dir, "LineNodeAgent.exe")

print("Launching PlantOpsEnterpriseSCADA...")
scada_proc = subprocess.Popen([scada_exe], cwd=installers_dir)
time.sleep(3)

print("Launching LineNodeAgent...")
agent_proc = subprocess.Popen([agent_exe], cwd=installers_dir)
time.sleep(5)

# 1. Test /api/health
with urllib.request.urlopen("http://127.0.0.1:5050/api/health", timeout=3) as resp:
    print("Health:", resp.read().decode('utf-8'))

# 2. Test /api/stations
with urllib.request.urlopen("http://127.0.0.1:5050/api/stations", timeout=3) as resp:
    data = json.loads(resp.read().decode('utf-8'))
    print(f"Total Registered Stations in SCADA: {len(data.get('stations', []))}")
    for s in data.get('stations', []):
        print(f" -> Station: {s.get('id')} | Name: {s.get('name')} | IP: {s.get('ip')} | Status: {s.get('status')} | Latency: {s.get('network_latency_ms')}ms")

time.sleep(2)
print("Stopping test processes...")
agent_proc.terminate()
scada_proc.terminate()
print("Done!")
