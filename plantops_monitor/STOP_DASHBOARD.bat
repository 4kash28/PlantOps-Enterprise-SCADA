@echo off
echo ==============================================
echo   STOPPING PLANTOPS SCADA MONITOR...
echo ==============================================
taskkill /F /IM PlantOpsMonitor.exe >nul 2>&1
echo PlantOps Monitor Dashboard has been stopped.
timeout /t 2 >nul
