@echo off
title LineNode Agent - Telemetry Permissions & Setup
cd /d "%~dp0"
echo ========================================================
echo   OPENING WORKSTATION PERMISSIONS & MONITORING SETUP...
echo ========================================================
if exist "LineNodeAgent.exe" (
    start "" "LineNodeAgent.exe" --config-gui
) else (
    python agent.py --config-gui
)
