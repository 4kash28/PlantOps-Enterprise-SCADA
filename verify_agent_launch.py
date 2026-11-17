import subprocess
import time
import os

installers_dir = r"C:\Users\ITSUPA\.gemini\antigravity\scratch\rac_industrial_monitor\WINDOWS_INSTALLERS"
agent_exe = os.path.join(installers_dir, "LineNodeAgent.exe")

print("Launching LineNodeAgent...")
proc = subprocess.Popen([agent_exe], cwd=installers_dir)
print(f"PID: {proc.pid}")

time.sleep(5)
ret = proc.poll()
print(f"Poll status: {ret} (None means running successfully!)")

time.sleep(3)
proc.terminate()
print("Terminated successfully.")
