@echo off
setlocal
title PlantOps SCADA - Firewall & LAN Configuration
echo ===================================================================
echo   PLANTOPS ENTERPRISE SCADA - LAN FIREWALL CONFIGURATION UTILITY
echo ===================================================================
echo.
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo [INFO] Requesting Administrator Privileges to configure Windows Defender Firewall...
    powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process cmd.exe -ArgumentList '/c \"\"%~f0\"\"' -Verb RunAs"
    exit /b
)

echo [1/3] Configuring Inbound TCP Port 8090 (Telemetry & Web UI)...
netsh advfirewall firewall delete rule name="PlantOps SCADA Port 8090" >nul 2>&1
netsh advfirewall firewall add rule name="PlantOps SCADA Port 8090" dir=in action=allow protocol=TCP localport=8090 profile=any

echo [2/3] Configuring Inbound UDP Port 8091 (LAN Auto-Discovery Broadcast)...
netsh advfirewall firewall delete rule name="PlantOps SCADA Port 8091 UDP" >nul 2>&1
netsh advfirewall firewall add rule name="PlantOps SCADA Port 8091 UDP" dir=in action=allow protocol=UDP localport=8091 profile=any

echo [3/3] Permitting PlantOps Applications across all network profiles...
netsh advfirewall firewall delete rule name="PlantOps SCADA Executable Rule" >nul 2>&1
netsh advfirewall firewall add rule name="PlantOps SCADA Executable Rule" dir=in action=allow program="%~dp0PlantOpsMonitor.exe" profile=any >nul 2>&1

echo.
echo ===================================================================
echo   [SUCCESS] Windows Defender Firewall is now configured!
echo   * Inbound TCP Port 8090 (Dashboard & Telemetry) is OPEN.
echo   * Inbound UDP Port 8091 (LAN Auto-Discovery) is OPEN.
echo   * Remote assembly line PCs can now connect automatically!
echo ===================================================================
echo.
echo Press any key to exit this window...
pause >nul
