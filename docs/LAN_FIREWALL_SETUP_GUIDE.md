# Industrial LAN Networking & Windows Firewall Setup Guide
## Room Air Conditioner (RAC) Assembly Line Monitoring Architecture

This guide details the complete network setup, Windows Firewall configuration, and deployment topology required for seamless, 100% offline local IP communication between the 5-6 **LineNode Agents** and the central **PlantOps Monitor** workstation.

---

### 1. Network Topology & Addressing Scheme

Industrial manufacturing lines operate strictly within isolated local subnets (OT network) without direct internet routing to ensure deterministic latency and zero cyber-exposure.

```
       +-------------------------------------------------------+
       |             Unmanaged / Managed Industrial Switch      |
       |                   Subnet: 192.168.1.0/24              |
       +-------------------------------------------------------+
              |           |            |           |           |
              |           |            |           |           |
     +--------+   +-------+---+   +----+---+  +----+----+  +---+---------+
     | Station 01 |   | Station 02|   | Station|  | Station |  | Station 06  |
     | 192.168.1. |   | 192.168.1.|   | 03..05 |  |   ...   |  | (Zebra ZT411|
     |    101     |   |    102    |   |  ...   |  |         |  | 192.168.1.  |
     +------------+   +-----------+   +--------+  +---------+  |    106      |
                                                               +-------------+
                                       |
                                       |
                         +-----------------------------+
                         | Central PlantOps Monitor PC |
                         | Fixed Static IP:            |
                         |        192.168.1.100        |
                         | Port:  8080 (TCP / WS)      |
                         +-----------------------------+
```

#### Static IP Assignment Recommendations:
- **Central PlantOps Server**: `192.168.1.100` (Subnet: `255.255.255.0`, Gateway: None or Router IP)
- **Station 1 (Chassis Press)**: `192.168.1.101`
- **Station 2 (Coil Brazing)**: `192.168.1.102`
- **Station 3 (Compressor Mount)**: `192.168.1.103`
- **Station 4 (Electrical PCB)**: `192.168.1.104`
- **Station 5 (Vacuum Decay & R32)**: `192.168.1.105`
- **Station 6 (QA & Zebra ZT411)**: `192.168.1.106`

---

### 2. Windows Firewall Configuration on Central Server (192.168.1.100)

By default, Windows Firewall blocks incoming connections on non-standard ports. Run PowerShell as **Administrator** on the Central PlantOps Workstation to configure firewall rules:

#### Step A: Open Inbound Port 8080 for HTTP and WebSockets
```powershell
New-NetFirewallRule -Name "PlantOps_Monitor_Inbound_8080" `
    -DisplayName "PlantOps Industrial Monitor Inbound (Port 8080)" `
    -Description "Permits incoming telemetry heartbeats and WebSocket connections from LineNode Agents" `
    -Direction Inbound `
    -LocalPort 8080 `
    -Protocol TCP `
    -Action Allow `
    -Profile Private, Domain
```

#### Step B: Enable ICMP Echo Request (Ping Latency Diagnostic)
Allow LineNode Agents to measure low-level ICMP ping latency:
```powershell
New-NetFirewallRule -Name "Allow_ICMPv4_Inbound" `
    -DisplayName "File and Printer Sharing (Echo Request - ICMPv4-In)" `
    -Protocol ICMPv4 `
    -IcmpType 8 `
    -Direction Inbound `
    -Action Allow `
    -Profile Private, Domain
```

---

### 3. Client Station Outbound Verification

On each production line PC (Station 01 to 06), verify network route and connectivity to the server:

```powershell
# Test TCP Port 8080 accessibility
Test-NetConnection -ComputerName 192.168.1.100 -Port 8080

# Output should show:
# TcpTestSucceeded : True
```

If `TcpTestSucceeded` is `False`:
1. Ensure `PlantOpsMonitor.exe` or `python server.py` is currently running on the server.
2. Confirm both PCs are connected to the same switch or VLAN.
3. Check that the Windows Network Profile is set to **Private** rather than **Public**:
   ```powershell
   # Inspect active network profile
   Get-NetConnectionProfile

   # If set to Public, switch to Private:
   Set-NetConnectionProfile -InterfaceAlias "Ethernet" -NetworkCategory Private
   ```

---

### 4. Deploying LineNode Agent as an Automated Windows Service / Startup Task

To ensure the agent starts automatically upon machine boot and continues running uninterrupted even if an assembly line operator logs off:

#### Option 1: Native Windows Task Scheduler (Recommended - No third-party tools required)
Run in PowerShell (as Administrator) on each line PC:
```powershell
$action = New-ScheduledTaskAction -Execute "C:\PlantOps\LineNodeAgent\LineNodeAgent.exe" -WorkingDirectory "C:\PlantOps\LineNodeAgent"
$trigger = New-ScheduledTaskTrigger -AtStartup
$principal = New-ScheduledTaskPrincipal -UserId "SYSTEM" -LogonType ServiceAccount -RunLevel Highest
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -RestartCount 5 -RestartInterval (New-TimeSpan -Minutes 1)

Register-ScheduledTask -TaskName "LineNodeAgent_AutoStart" -Action $action -Trigger $trigger -Principal $principal -Settings $settings
```

#### Option 2: NSSM (Non-Sucking Service Manager)
```cmd
nssm install LineNodeAgent "C:\PlantOps\LineNodeAgent\LineNodeAgent.exe"
nssm set LineNodeAgent AppDirectory "C:\PlantOps\LineNodeAgent"
nssm set LineNodeAgent Start SERVICE_AUTO_START
nssm start LineNodeAgent
```

---

### 5. Zebra ZT411 USB Setup & Verification

1. **Hardware Connection**: Connect the Zebra ZT411 industrial printer via high-speed USB cable to the line PC.
2. **Driver Installation**: Install the official **ZebraDesigner Driver** or **Zebra ZDownloader / Setup Utilities**.
3. **Hardware ID Verification**:
   Verify the USB Vendor ID in PowerShell:
   ```powershell
   Get-CimInstance Win32_PnPEntity | Where-Object { $_.DeviceID -match "VID_0A5F" } | Select-Object Name, DeviceID, Status
   ```
   *Expected Output:*
   - `Name`: Zebra ZT411-203dpi
   - `DeviceID`: `USB\VID_0A5F&PID_0164\...`
   - `Status`: `OK`
4. The LineNode Agent will automatically bind to the WMI PnP device and Windows Print Spooler status flags.
