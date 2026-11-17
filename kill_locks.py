import os
import psutil

target = r"C:\Users\ITSUPA\.gemini\antigravity\scratch\rac_industrial_monitor\WINDOWS_INSTALLERS\PlantOpsEnterpriseSCADA.exe"

print("Checking processes locking target...")
for proc in psutil.process_iter(['pid', 'name', 'exe']):
    try:
        if proc.info['exe'] and os.path.samefile(proc.info['exe'], target):
            print(f"Found locking process: PID {proc.info['pid']} ({proc.info['name']})")
            proc.kill()
            print("Killed!")
    except Exception:
        pass

print("Done checking.")
