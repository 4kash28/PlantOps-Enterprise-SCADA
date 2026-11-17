import os
import shutil
import zipfile

base_dir = r"C:\Users\ITSUPA\.gemini\antigravity\scratch\rac_industrial_monitor"
installers_dir = os.path.join(base_dir, "WINDOWS_INSTALLERS")
scada_exe_src = os.path.join(base_dir, "Release_ScadaDashboard", "PlantOpsEnterpriseSCADA.exe")
agent_exe_src = os.path.join(base_dir, "Release_LineNodeAgent", "LineNodeAgent.exe")

scada_exe_dest = os.path.join(installers_dir, "PlantOpsEnterpriseSCADA.exe")
agent_exe_dest = os.path.join(installers_dir, "LineNodeAgent.exe")

print("Copying executables...")
shutil.copy2(scada_exe_src, scada_exe_dest)
shutil.copy2(agent_exe_src, agent_exe_dest)

files_to_zip = [
    "PlantOpsEnterpriseSCADA.exe",
    "LineNodeAgent.exe",
    "license_activation.json",
    "agent_installed.json",
    "Uninstall.bat",
    "Uninstall_LineNodeAgent.bat",
    "ALLOW_FIREWALL_LAN_ACCESS.bat",
    "README_INSTALLATION_GUIDE.md",
    "LICENSE_KEY.txt",
    "linenode_agent.ico",
    "plantops_monitor.ico",
    "linenode_agent.png",
    "plantops_monitor.png"
]

zip_path1 = os.path.join(installers_dir, "PlantOps_RAC_Enterprise_Software_v2.4.zip")
zip_path2 = os.path.join(base_dir, "PlantOps_RAC_Enterprise_Software_v2.4.zip")

print("Building zip package...")
with zipfile.ZipFile(zip_path1, 'w', zipfile.ZIP_DEFLATED) as zipf:
    for filename in files_to_zip:
        file_path = os.path.join(installers_dir, filename)
        if os.path.exists(file_path):
            zipf.write(file_path, arcname=filename)
            print(f"Added {filename} ({os.path.getsize(file_path):,} bytes)")
        else:
            print(f"Warning: file not found: {filename}")

shutil.copy2(zip_path1, zip_path2)
print("Finished packaging!")
print(f"Zip 1: {zip_path1} -> {os.path.getsize(zip_path1):,} bytes")
print(f"Zip 2: {zip_path2} -> {os.path.getsize(zip_path2):,} bytes")
