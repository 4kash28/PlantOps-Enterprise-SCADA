@echo off
title PlantOps Enterprise SCADA Uninstaller
echo ============================================================================
echo   PLANTOPS ENTERPRISE SCADA v2.4 LTS — UNINSTALLATION
echo ============================================================================
echo.
set /p CONFIRM="Are you sure you want to completely uninstall PlantOps Enterprise SCADA? (Y/N): "
if /i not "%CONFIRM%"=="Y" (
    echo Uninstallation cancelled by user.
    pause
    exit /b
)

echo.
echo [1/5] Stopping running SCADA processes...
taskkill /F /IM PlantOpsEnterpriseSCADA.exe >nul 2>&1

echo [2/5] Removing Windows Desktop and Start Menu Shortcuts...
powershell -NoProfile -Command "$d = [System.IO.Path]::Combine([Environment]::GetFolderPath('Desktop'), 'PlantOps Enterprise SCADA.lnk'); if (Test-Path $d) { Remove-Item $d -Force }; $p = [System.IO.Path]::Combine([Environment]::GetFolderPath('Programs'), 'PlantOps Enterprise SCADA.lnk'); if (Test-Path $p) { Remove-Item $p -Force }" >nul 2>&1

echo [3/5] Removing Windows Defender Firewall Rules...
netsh advfirewall firewall delete rule name="PlantOps SCADA Inbound (TCP 5050)" >nul 2>&1
netsh advfirewall firewall delete rule name="PlantOps SCADA Inbound (TCP 8090)" >nul 2>&1
netsh advfirewall firewall delete rule name="PlantOps Enterprise SCADA Application" >nul 2>&1

echo [4/5] Removing from Windows Programs & Features (Registry)...
reg delete "HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\PlantOpsEnterpriseSCADA" /f >nul 2>&1
reg delete "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PlantOpsEnterpriseSCADA" /f >nul 2>&1

echo [5/5] Removing Local License & Session configurations...
if exist "%~dp0license_activation.json" del /f /q "%~dp0license_activation.json" >nul 2>&1
if exist "%~dp0LICENSE_KEY.txt" del /f /q "%~dp0LICENSE_KEY.txt" >nul 2>&1

echo.
echo ============================================================================
echo   UNINSTALLATION SUCCESSFUL!
echo   PlantOps Enterprise SCADA has been removed from Windows Programs & Features.
echo   You may now delete this folder: %~dp0
echo ============================================================================
powershell -NoProfile -Command "Add-Type -AssemblyName System.Windows.Forms; [System.Windows.Forms.MessageBox]::Show('PlantOps Enterprise SCADA has been successfully removed from your computer.', 'PlantOps SCADA Uninstalled', [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Information)" >nul 2>&1
pause
