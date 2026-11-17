@echo off
title PlantOps SCADA - PHP Web Server
cd /d "%~dp0"

echo =====================================================================
echo    PLANTOPS(TM) ENTERPRISE SCADA - PHP WEB PORTAL
echo =====================================================================
echo.

where php >nul 2>nul
if %ERRORLEVEL% neq 0 (
    echo [ERROR] PHP runtime was not detected in your system PATH.
    echo.
    echo If you have XAMPP installed:
    echo Copy the 'php_portal' folder into C:\xampp\htdocs\scada\
    echo and open http://localhost/scada/ in your browser.
    echo.
    echo Otherwise, please install PHP from https://windows.php.net/download/
    echo or use the standalone PlantOpsMonitor.exe Windows application!
    echo.
    pause
    exit /b 1
)

echo Starting PHP built-in web server on http://127.0.0.1:8080 ...
echo Central SCADA SQLite database linked automatically.
echo.
start "" "http://127.0.0.1:8080"
php -S 0.0.0.0:8080 index.php
pause
