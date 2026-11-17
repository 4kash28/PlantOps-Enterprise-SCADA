import time
import subprocess
import urllib.request
import json
import os

installers_dir = r"C:\Users\ITSUPA\.gemini\antigravity\scratch\rac_industrial_monitor\WINDOWS_INSTALLERS"
scada_exe = os.path.join(installers_dir, "PlantOpsEnterpriseSCADA.exe")

print("Launching PlantOpsEnterpriseSCADA...")
scada_proc = subprocess.Popen([scada_exe], cwd=installers_dir)
time.sleep(3)

try:
    with urllib.request.urlopen("http://127.0.0.1:5050/api/stations", timeout=3) as resp:
        content = resp.read().decode('utf-8')
        print("Raw Stations JSON:")
        print(content)
finally:
    scada_proc.terminate()
