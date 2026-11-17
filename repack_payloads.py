import os
import zipfile
import shutil
from pathlib import Path

base_dir = Path(r"C:\Users\ITSUPA\.gemini\antigravity\scratch\rac_industrial_monitor")
installers_dir = base_dir / "installers"

# 1. Repack dashboard_payload.zip
dash_zip = installers_dir / "dashboard_payload.zip"
print(f"Creating {dash_zip} ...")
with zipfile.ZipFile(dash_zip, "w", zipfile.ZIP_DEFLATED) as z:
    z.write(base_dir / "plantops_monitor" / "dist" / "PlantOpsMonitor.exe", "PlantOpsMonitor.exe")
    z.write(base_dir / "plantops_monitor" / "config.json", "config.json")
    z.write(base_dir / "plantops_monitor" / "plantops_monitor.ico", "plantops_monitor.ico")
    z.write(base_dir / "plantops_monitor" / "ALLOW_FIREWALL_LAN_ACCESS.bat", "ALLOW_FIREWALL_LAN_ACCESS.bat")
    static_dir = base_dir / "plantops_monitor" / "static"
    for root, dirs, files in os.walk(static_dir):
        for f in files:
            full = Path(root) / f
            rel = "static/" + str(full.relative_to(static_dir)).replace("\\", "/")
            z.write(full, rel)
print("dashboard_payload.zip created successfully!")

# 2. Repack agent_payload.zip
agent_zip = installers_dir / "agent_payload.zip"
print(f"Creating {agent_zip} ...")
with zipfile.ZipFile(agent_zip, "w", zipfile.ZIP_DEFLATED) as z:
    z.write(base_dir / "line_node_agent" / "dist" / "LineNodeAgent.exe", "LineNodeAgent.exe")
    z.write(base_dir / "line_node_agent" / "config.json", "config.json")
    z.write(base_dir / "line_node_agent" / "linenode_agent.ico", "linenode_agent.ico")
    
    start_bat = base_dir / "line_node_agent" / "START_AGENT.bat"
    if not start_bat.exists():
        with open(start_bat, "w", encoding="utf-8") as f:
            f.write('@echo off\r\ncd /d "%~dp0"\r\nstart "" "LineNodeAgent.exe"\r\n')
    z.write(start_bat, "START_AGENT.bat")
    
    cfg_bat = base_dir / "line_node_agent" / "Configure_Network.bat"
    if not cfg_bat.exists():
        with open(cfg_bat, "w", encoding="utf-8") as f:
            f.write('@echo off\r\ncd /d "%~dp0"\r\nstart "" "LineNodeAgent.exe" --config-gui\r\n')
    z.write(cfg_bat, "Configure_Network.bat")

print("agent_payload.zip created successfully!")
