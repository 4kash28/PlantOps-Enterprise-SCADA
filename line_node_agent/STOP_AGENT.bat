@echo off
echo ==============================================
echo   STOPPING LINENODE AGENT...
echo ==============================================
taskkill /F /IM LineNodeAgent.exe >nul 2>&1
echo LineNode Agent has been stopped successfully.
timeout /t 2 >nul
