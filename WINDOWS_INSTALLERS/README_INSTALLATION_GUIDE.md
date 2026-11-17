# PlantOps RAC Assembly Line Monitoring - Professional Setup Guide
## Windows Software Installation Manual (Hindi & English)

Aapke paas do enterprise-grade Setup Wizards tayyar hain:
1. **`LineNodeAgent_Setup_v2.4.exe`** - Production Assembly Line PCs ke liye.
2. **`PlantOpsMonitor_Setup_v2.4.exe`** - Central Monitoring Workstation / Server ke liye.

---

### Software 1: Central Monitoring Workstation (`PlantOpsMonitor_Setup_v2.4.exe`)

Ye software aapke main computer ya monitoring screen pe install hoga:

#### Installation Steps:
1. File par **Double-Click** karein: `PlantOpsMonitor_Setup_v2.4.exe`
2. **Welcome Screen:** "Next >" dabayein.
3. **Installation Directory:** Default path `C:\PlantOps\PlantOpsMonitor` rahega. Aap chahein to Browse karke badal sakte hain.
4. **Network & Firewall Settings:**
   - Server Port: `8090` (pre-filled).
   - "Automatically configure Windows Firewall" checkbox tick rahega (ye automatically Windows Firewall me inbound rule bana dega).
   - "Create Desktop Shortcut" checkbox tick rahega.
5. **Install:** "Install" button dabayein. Progress bar 100% hone par files extract hongi, desktop shortcut banega, aur Windows Add/Remove programs me software register ho jaayega.
6. **Finish:** "Launch PlantOps Monitor Dashboard now" tick karke "Finish" karein.
7. Aapke desktop par **"PlantOps Monitor Dashboard"** icon ban jaayega!

---

### Software 2: Production Line PC Client (`LineNodeAgent_Setup_v2.4.exe`)

Ye software assembly line ke sabhi 5-6 computers (Station 1 se 6) pe install hoga:

#### Installation Steps:
1. File ko pen drive se line PC pe copy karein aur **Double-Click** karein: `LineNodeAgent_Setup_v2.4.exe`
2. **Welcome Screen:** "Next >" dabayein.
3. **Destination Directory:** Default path `C:\PlantOps\LineNodeAgent` rahega.
4. **Station & Network Configuration:**
   - **Central Server IP:** Apne Main Dashboard computer ka local IP address dalein (Jaise: `192.168.1.100`).
   - **Station Role:** Dropdown se us computer ka station select karein:
     * `LINE1_STATION_01` (Chassis Press & Base Assembly)
     * `LINE1_STATION_02` (Evaporator Coil & Copper Brazing)
     * `LINE1_STATION_03` (Compressor & Motor Mounting)
     * `LINE1_STATION_04` (Electrical & Inverter PCB Wiring)
     * `LINE1_STATION_05` (Vacuum Decay & R32 Charge)
     * `LINE1_STATION_06` (Final QA Run-Test & Zebra ZT411 Labeling)
   - **Zebra ZT411 Printer Monitoring:** Station 6 select karte hi ye checkbox apne aap tick ho jaayega.
   - **Start LineNode Agent on Windows Boot:** Checkbox tick rahega taaki PC restart hone par agent background me bina login ke khud chalu ho jaaye.
5. **Install:** "Install" par click karein. Setup automatically:
   - Saari binaries install karega.
   - Aapke select kiye hue IP aur Station ID ke saath `config.json` create karega.
   - Desktop Shortcut banayega.
   - Windows Startup Task register karega.
6. **Finish:** "Launch LineNode Agent now" tick karke "Finish" karein.

---

### Control Panel & Uninstallation:
Dono softwares standard Windows applications ki tarah:
- Windows **Settings $\rightarrow$ Apps $\rightarrow$ Installed Apps** (Control Panel) me dikhenge.
- Inhe uninstall karne ke liye unke installation folder me `Uninstall.bat` ya Windows Control Panel se uninstalled kiya ja sakta hai.
