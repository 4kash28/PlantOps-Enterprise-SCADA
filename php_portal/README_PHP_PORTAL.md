# PlantOps™ Enterprise SCADA - Native PHP Portal

This directory contains the 100% Native PHP implementation of the PlantOps Enterprise SCADA monitoring portal.

## Deployment Options

### Option 1: Standalone PHP Built-in Server
If you have PHP installed on your system:
1. Double-click `START_PHP_SERVER.bat`.
2. It starts the PHP built-in web server on `http://127.0.0.1:8080`.
3. It opens automatically in your browser.

### Option 2: XAMPP / WAMP / Apache / IIS
1. Copy this `php_portal` directory into your web root (e.g., `C:\xampp\htdocs\scada`).
2. Start Apache from the XAMPP Control Panel.
3. Open `http://localhost/scada/` in any browser on your factory network.

## Database Integration
The PHP portal reads and writes to `plantops_monitor.db` (SQLite with WAL mode). 
All telemetry sent by `LineNodeAgent.exe` from factory line computers is displayed live in this PHP portal as well as in the standalone desktop app!
