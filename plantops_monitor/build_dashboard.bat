@echo off
REM ==============================================================================
REM PlantOps Monitor - Standalone Executable Build Script
REM Compiles desktop_launcher.py + server.py + static/ into PlantOpsMonitor.exe
REM ==============================================================================

echo [1/3] Verifying Python Environment and Dependencies...
python -m pip install -r requirements.txt

echo.
echo [2/3] Compiling PlantOpsMonitor.exe via PyInstaller...
python -m PyInstaller server.spec --noconfirm --clean

echo.
if exist "dist\PlantOpsMonitor.exe" (
    echo [3/3] SUCCESS! PlantOpsMonitor.exe compiled successfully:
    echo Location: %CD%\dist\PlantOpsMonitor.exe
    copy config.json dist\config.json
    if not exist "dist\static" xcopy /E /I /Y static dist\static
    echo Copied config.json and static assets to dist directory.
) else (
    echo [ERROR] Compilation failed. Check PyInstaller logs above.
)
pause
