@echo off
setlocal
title PlantOps SCADA - Firewall & LAN Configuration
echo ===================================================================
echo   PLANTOPS ENTERPRISE SCADA - LAN FIREWALL & HTTP CONFIGURATION
echo ===================================================================
echo.
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo [INFO] Requesting Administrator Privileges to configure Windows Defender Firewall...
    powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process cmd.exe -ArgumentList '/c \"\"%~f0\"\"' -Verb RunAs"
    exit /b
)

echo [1/4] Configuring Inbound TCP Port 5050 (Central Telemetry Ingestion)...
netsh advfirewall firewall delete rule name="PlantOps SCADA Port 5050" >nul 2>&1
netsh advfirewall firewall add rule name="PlantOps SCADA Port 5050" dir=in action=allow protocol=TCP localport=5050 profile=any

echo [2/4] Configuring Inbound TCP Port 8090 (Secondary / Web Ingestion)...
netsh advfirewall firewall delete rule name="PlantOps SCADA Port 8090" >nul 2>&1
netsh advfirewall firewall add rule name="PlantOps SCADA Port 8090" dir=in action=allow protocol=TCP localport=8090 profile=any

echo [3/4] Granting HTTP URL ACL Permissions for Remote Workstation PCs...
netsh http delete urlacl url=http://*:5050/ >nul 2>&1
netsh http add urlacl url=http://*:5050/ user=Everyone >nul 2>&1
netsh http delete urlacl url=http://+:5050/ >nul 2>&1
netsh http add urlacl url=http://+:5050/ user=Everyone >nul 2>&1

echo [4/4] Permitting PlantOps Applications across all network profiles...
netsh advfirewall firewall delete rule name="PlantOps SCADA Executable Rule" >nul 2>&1
netsh advfirewall firewall add rule name="PlantOps SCADA Executable Rule" dir=in action=allow program="%~dp0PlantOpsEnterpriseSCADA.exe" profile=any >nul 2>&1

echo.
echo ===================================================================
echo   [SUCCESS] Windows Defender Firewall & LAN Endpoints Configured!
echo   * Inbound TCP Port 5050 (SCADA Central Telemetry) is OPEN.
echo   * Remote assembly line PCs (10-11+ Systems) can now connect!
echo   * Multi-PC LAN communication enabled across all workstations.
echo ===================================================================
echo.
echo Press any key to exit this window...
pause >nul
