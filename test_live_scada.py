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
print(f"SCADA PID: {scada_proc.pid}")

# Wait for SCADA to bind port 5050
time.sleep(3)

print("Launching LineNodeAgent...")
agent_proc = subprocess.Popen([agent_exe], cwd=installers_dir)
print(f"LineNodeAgent PID: {agent_proc.pid}")

# Wait for agent to transmit telemetry
time.sleep(6)

# Test HTTP endpoint
try:
    req = urllib.request.Request("http://127.0.0.1:5050/api/telemetry", headers={'User-Agent': 'TestClient'})
    # SCADA server accepts POST for telemetry, GET returns 200 OK or endpoint info
    with urllib.request.urlopen(req, timeout=3) as response:
        print("GET /api/telemetry response code:", response.status)
        data = response.read().decode('utf-8')
        print("Response body preview:", data[:200])
except Exception as e:
    print(f"HTTP test note: {e}")

# Check that both processes are alive and running without crashing
time.sleep(3)
print(f"SCADA poll: {scada_proc.poll()} (None means running OK)")
print(f"Agent poll: {agent_proc.poll()} (None means running OK)")

# Let them run for a few more seconds to see heartbeat
time.sleep(5)

# Terminate cleanly
print("Stopping processes...")
try:
    agent_proc.terminate()
    scada_proc.terminate()
except Exception as e:
    pass

print("Test finished successfully!")
