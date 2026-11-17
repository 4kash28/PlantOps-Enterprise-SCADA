# Standalone Packaging & Industrial Deployment Guide
## PlantOps Industrial Monitoring System for RAC Assembly Lines

This document provides exact instructions to build standalone, zero-dependency Windows `.exe` binaries for both the **LineNode Agent** and the **PlantOps Monitor** workstation dashboard.

---

### Project Directory Structure

```
rac_industrial_monitor/
│
├── line_node_agent/                   # Client application for production line PCs
│   ├── agent.py                       # Main agent telemetry & Zebra ZT411 detection logic
│   ├── config.json                    # Station configuration (IP, ID, intervals)
│   ├── agent.spec                     # PyInstaller specification file
│   ├── requirements.txt               # Agent Python dependencies
│   └── build_agent.bat                # 1-Click build script
│
├── plantops_monitor/                  # Central server & workstation dashboard
│   ├── server.py                      # FastAPI backend, SQLite downtime tracker & REST/WS APIs
│   ├── desktop_launcher.py            # Desktop window launcher (Edge WebView2 / native window)
│   ├── config.json                    # Central server configuration & pre-registered stations
│   ├── server.spec                    # PyInstaller specification file bundling static assets
│   ├── requirements.txt               # Server dependencies
│   ├── build_dashboard.bat            # 1-Click build script
│   └── static/                        # Industrial Dark Theme UI
│       ├── index.html                 # Main dashboard markup (Tailwind, KPI cards, table)
│       ├── app.js                     # WebSocket client, Web Audio alert chime, CSV export
│       └── styles.css                 # Industrial high-contrast styling & beacon animations
│
├── simulator/                         # RAC Assembly Line Multi-Station Simulator
│   └── simulate_assembly_line.py      # Simulates all 6 stations with interactive fault injection
│
└── docs/                              # Production deployment documentation
    ├── LAN_FIREWALL_SETUP_GUIDE.md    # Windows Firewall & IP routing guide
    └── DEPLOYMENT_AND_PACKAGING_GUIDE.md
```

---

### Part 1: Compiling LineNode Agent (`LineNodeAgent.exe`)

The agent is compiled into a single portable `.exe` with all dependencies (psutil, WMI hooks, Windows Print Spooler ctypes) bundled inside.

#### Method A: Using the Automated Build Batch Script
Open command prompt or double click:
```cmd
cd C:\Users\ITSUPA\.gemini\antigravity\scratch\rac_industrial_monitor\line_node_agent
build_agent.bat
```

#### Method B: Direct PyInstaller CLI Command
```bash
cd line_node_agent
pip install -r requirements.txt
pyinstaller --onefile --noconfirm --clean --name="LineNodeAgent" --add-data "config.json;." agent.py
```

#### Deployment to Production Line PCs:
1. Locate the compiled executable at `line_node_agent/dist/LineNodeAgent.exe`.
2. Copy `LineNodeAgent.exe` and `config.json` to the target line PC (e.g., `C:\PlantOps\LineNodeAgent\`).
3. Edit `config.json` on each machine to match its station role:
   ```json
   {
     "server_ip": "192.168.1.100",
     "server_port": 8080,
     "station_id": "LINE1_STATION_06",
     "station_name": "RAC Packaging & Labeling",
     "sync_interval_sec": 3,
     "ping_timeout_ms": 1000,
     "printer_monitoring": {
       "enabled": true,
       "zebra_vid": "0A5F",
       "name_pattern": "ZT411"
     }
   }
   ```
4. Double click `LineNodeAgent.exe` or configure as a startup service.

---

### Part 2: Compiling PlantOps Monitor Workstation (`PlantOpsMonitor.exe`)

The central dashboard executable bundles the FastAPI web server, WebSocket engine, SQLite database engine, Edge WebView2 desktop container, and all static frontend assets (HTML, CSS, JS) into a self-contained `.exe`.

#### Method A: Using the Automated Build Batch Script
```cmd
cd C:\Users\ITSUPA\.gemini\antigravity\scratch\rac_industrial_monitor\plantops_monitor
build_dashboard.bat
```

#### Method B: Direct PyInstaller CLI Command via `server.spec`
```bash
cd plantops_monitor
pip install -r requirements.txt
pyinstaller server.spec --noconfirm --clean
```

#### Running PlantOps Monitor:
1. Double-click `dist/PlantOpsMonitor.exe`.
2. It automatically starts the internal server on `0.0.0.0:8080` and opens a native, high-performance desktop window using Microsoft Edge WebView2.
3. Dual-Access Capability: Operators can monitor from the workstation desktop window, while any supervisor or line technician on the LAN can simultaneously navigate to `http://<server-ip>:8080` in their browser or mobile tablet.

---

### Part 3: Immediate Verification with the 6-Station Simulator

You can test and demonstrate the entire system immediately on a single PC without needing physical line machines:

1. **Start PlantOps Monitor**:
   ```bash
   cd plantops_monitor
   python desktop_launcher.py
   # Or run: python server.py
   ```
   The dashboard UI will open at `http://127.0.0.1:8080`.

2. **Launch the Multi-Station Assembly Line Simulator**:
   In a separate terminal:
   ```bash
   cd simulator
   python simulate_assembly_line.py
   ```
   All 6 RAC production stations (Chassis Press, Coil Brazing, Compressor Mount, Electrical Wiring, Vacuum Decay & R32, Zebra ZT411 Packaging) will immediately illuminate green on the dashboard.

3. **Verify Fault & Downtime Logic**:
   - In the simulator terminal, press **`1`**: Zebra ZT411 enters `PAPER OUT`. The Station 6 card turns Amber/Red, the audio alarm chime sounds, and a downtime incident is logged.
   - Press **`3`**: Zebra printer returns to `READY`. The incident resolves and total downtime duration (e.g. `00:00:24`) is recorded in the SQLite table.
   - Press **`4`**: Station 3 heartbeats stop. After 10 seconds, the watchdog triggers a "Line PC Offline" critical alarm and flashes the top banner.
   - Press **`5`**: Station 3 reconnects and normal status resumes.
   - Click the **"Export CSV"** button on the dashboard to download the audit log report.
