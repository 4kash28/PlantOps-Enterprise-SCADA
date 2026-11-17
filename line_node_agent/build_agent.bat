@echo off
REM ==============================================================================
REM LineNode Agent - Standalone Executable Build Script
REM Compiles agent.py into a self-contained, portable Windows executable
REM ==============================================================================

echo [1/3] Verifying Python Environment and Dependencies...
python -m pip install -r requirements.txt

echo.
echo [2/3] Compiling LineNodeAgent.exe via PyInstaller...
python -m PyInstaller --onefile --noconfirm --clean ^
    --name="LineNodeAgent" ^
    --add-data "config.json;." ^
    agent.py

echo.
if exist "dist\LineNodeAgent.exe" (
    echo [3/3] SUCCESS! LineNodeAgent.exe compiled successfully:
    echo Location: %CD%\dist\LineNodeAgent.exe
    copy config.json dist\config.json
    echo Copied config.json to dist directory for immediate deployment.
) else (
    echo [ERROR] Compilation failed. Check PyInstaller logs above.
)
pause
