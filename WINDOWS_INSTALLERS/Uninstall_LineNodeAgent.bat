@echo off
setlocal
cd /d "%~dp0"
title PlantOps Line Node Agent Uninstaller

if exist "%~dp0LineNodeAgent.exe" (
    "%~dp0LineNodeAgent.exe" --uninstall %*
    goto :finish
)

echo ============================================================================
echo   PLANTOPS LINE NODE AGENT v2.4 - WORKSTATION UNINSTALLATION
echo ============================================================================
echo.
echo [1/4] Stopping active LineNodeAgent processes...
taskkill /F /IM LineNodeAgent.exe >nul 2>&1

echo [2/4] Removing Windows Startup & Programs and Features registrations...
reg delete "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v "PlantOpsLineNodeAgent" /f >nul 2>&1
reg delete "HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\PlantOpsLineNodeAgent" /f >nul 2>&1
reg delete "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PlantOpsLineNodeAgent" /f >nul 2>&1

echo [3/4] Removing Desktop and Start Menu Shortcuts...
powershell -NoProfile -Command "$d = [System.IO.Path]::Combine([Environment]::GetFolderPath('Desktop'), 'PlantOps Line Node Agent.lnk'); if (Test-Path $d) { Remove-Item $d -Force }; $p = [System.IO.Path]::Combine([Environment]::GetFolderPath('Programs'), 'PlantOps Line Node Agent.lnk'); if (Test-Path $p) { Remove-Item $p -Force }" >nul 2>&1

echo [4/4] Removing configuration files...
if exist "%~dp0agent_installed.json" del /f /q "%~dp0agent_installed.json" >nul 2>&1
if exist "%~dp0agent_config.db" del /f /q "%~dp0agent_config.db" >nul 2>&1
if exist "%~dp0agent_config.db-wal" del /f /q "%~dp0agent_config.db-wal" >nul 2>&1
if exist "%~dp0agent_config.db-shm" del /f /q "%~dp0agent_config.db-shm" >nul 2>&1
if exist "%~dp0agent_error.log" del /f /q "%~dp0agent_error.log" >nul 2>&1

echo.
echo Uninstallation completed successfully.
pause

:finish
exit /b 0
