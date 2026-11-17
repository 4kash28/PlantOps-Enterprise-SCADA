@echo off
:: ============================================================================
:: PlantOps™ Enterprise SCADA - Windows Firewall LAN Inbound Rule Creator
:: Allows RAC Line Node Workstations to stream telemetry to port 8090
:: ============================================================================
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo [ELEVATION REQUIRED] Requesting Administrator Privileges...
    powershell -Command "Start-Process '%~f0' -Verb RunAs"
    exit /b
)

title PlantOps Enterprise SCADA - Firewall Configuration
color 0b
echo ============================================================================
echo   PLANTOPS ENTERPRISE SCADA v2.4 — WINDOWS FIREWALL CONFIGURATION
echo ============================================================================
echo.
echo Adding inbound firewall rule for SCADA Core Port 8090 (TCP)...
netsh advfirewall firewall delete rule name="PlantOps SCADA Inbound (TCP 8090)" >nul 2>&1
netsh advfirewall firewall add rule name="PlantOps SCADA Inbound (TCP 8090)" dir=in action=allow protocol=TCP localport=8090 profile=any description="PlantOps SCADA Telemetry Stream Ingestion"

echo Adding application firewall exception for PlantOpsEnterpriseSCADA.exe...
netsh advfirewall firewall delete rule name="PlantOps Enterprise SCADA Application" >nul 2>&1
netsh advfirewall firewall add rule name="PlantOps Enterprise SCADA Application" dir=in action=allow program="%~dp0PlantOpsEnterpriseSCADA.exe" enable=yes profile=any

echo.
echo ============================================================================
echo   FIREWALL RULES CONFIGURED SUCCESSFULLY!
echo   Workstations on the industrial subnet can now push live telemetry.
echo ============================================================================
echo.
pause
