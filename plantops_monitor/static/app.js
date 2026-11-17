/**
 * ============================================================================
 * PLANTOPS™ ENTERPRISE SCADA - OFFICIAL FRONTEND ENGINE
 * Clean Corporate Theme (Dual Light/Dark) with Full Real-Time Telemetry
 * ============================================================================
 */

// Global State
let socket = null;
let reconnectTimer = null;
let currentSummary = {};
let currentStations = [];
let audioAlertsEnabled = false;
let previousCriticalCount = 0;
let currentFilter = 'ALL';
let isSimulationMode = false;
let simulationInterval = null;

// Theme Controller
function initTheme() {
    const saved = localStorage.getItem('plantops_theme') || 'light';
    applyTheme(saved);
}

function toggleTheme() {
    const isDark = document.documentElement.classList.contains('dark');
    applyTheme(isDark ? 'light' : 'dark');
}

function applyTheme(theme) {
    const icon = document.getElementById('theme-icon');
    const label = document.getElementById('theme-label');

    if (theme === 'dark') {
        document.documentElement.classList.add('dark');
        localStorage.setItem('plantops_theme', 'dark');
        if (icon) icon.innerText = '🌙';
        if (label) label.innerText = 'Theme: Dark';
    } else {
        document.documentElement.classList.remove('dark');
        localStorage.setItem('plantops_theme', 'light');
        if (icon) icon.innerText = '☀️';
        if (label) label.innerText = 'Theme: Light';
    }
}

// Audio Alert Context
let audioCtx = null;
function getAudioContext() {
    if (!audioCtx) {
        const AudioContextClass = window.AudioContext || window.webkitAudioContext;
        if (AudioContextClass) {
            audioCtx = new AudioContextClass();
        }
    }
    if (audioCtx && audioCtx.state === 'suspended') {
        audioCtx.resume();
    }
    return audioCtx;
}

/**
 * Synthesizes an official industrial alert siren tone (ISA-18.2 compliant tone)
 */
function playIndustrialAlertChime() {
    if (!audioAlertsEnabled) return;
    try {
        const ctx = getAudioContext();
        if (!ctx) return;

        const now = ctx.currentTime;
        
        // Primary Siren Tone (880 Hz - A5)
        const osc1 = ctx.createOscillator();
        const gain1 = ctx.createGain();
        osc1.type = 'triangle';
        osc1.frequency.setValueAtTime(880, now);
        gain1.gain.setValueAtTime(0.2, now);
        gain1.gain.exponentialRampToValueAtTime(0.001, now + 0.35);
        osc1.connect(gain1);
        gain1.connect(ctx.destination);
        osc1.start(now);
        osc1.stop(now + 0.35);

        // Secondary Harmonic Tone (659.25 Hz - E5)
        const osc2 = ctx.createOscillator();
        const gain2 = ctx.createGain();
        osc2.type = 'sine';
        osc2.frequency.setValueAtTime(659.25, now + 0.16);
        gain2.gain.setValueAtTime(0.25, now + 0.16);
        gain2.gain.exponentialRampToValueAtTime(0.001, now + 0.55);
        osc2.connect(gain2);
        gain2.connect(ctx.destination);
        osc2.start(now + 0.16);
        osc2.stop(now + 0.55);
    } catch (e) {
        console.warn('Audio chime error:', e);
    }
}

function fmt(val, fallback = '0.0') {
    return (val !== null && val !== undefined) ? val : fallback;
}

/**
 * WebSocket Connection & Streaming Telemetry Loop (Self-Healing Industrial Stream)
 */
function connectWebSocket() {
    if (reconnectTimer) {
        clearTimeout(reconnectTimer);
        reconnectTimer = null;
    }

    const protocol = window.location.protocol === 'https:' ? 'wss:' : 'ws:';
    const wsUrl = `${protocol}//${window.location.host}/ws/dashboard`;

    const wsStatusEl = document.getElementById('ws-connection-indicator');
    const wsStatusText = document.getElementById('ws-connection-text');

    try {
        if (socket && (socket.readyState === WebSocket.OPEN || socket.readyState === WebSocket.CONNECTING)) {
            socket.close();
        }
    } catch (e) { }

    try {
        socket = new WebSocket(wsUrl);
    } catch (e) {
        console.warn('[SCADA Core] Failed to instantiate WebSocket:', e);
        reconnectTimer = setTimeout(connectWebSocket, 2000);
        return;
    }

    socket.onopen = () => {
        console.log('[SCADA Core] WebSocket connection established.');
        if (wsStatusEl) wsStatusEl.className = 'status-led status-led-green';
        if (wsStatusText) wsStatusText.innerText = 'SCADA Core Stream Active';
        if (reconnectTimer) {
            clearTimeout(reconnectTimer);
            reconnectTimer = null;
        }
    };

    socket.onmessage = (event) => {
        try {
            const msg = JSON.parse(event.data);
            handleIncomingMessage(msg);
        } catch (e) {
            console.error('Error parsing WS message:', e);
        }
    };

    socket.onclose = () => {
        console.warn('[SCADA Core] WebSocket link lost. Auto-reconnecting in 2s...');
        if (wsStatusEl) wsStatusEl.className = 'status-led status-led-red';
        if (wsStatusText) wsStatusText.innerText = 'Reconnecting link...';
        if (reconnectTimer) {
            clearTimeout(reconnectTimer);
            reconnectTimer = null;
        }
        reconnectTimer = setTimeout(connectWebSocket, 2000);
    };

    socket.onerror = (err) => {
        console.error('[SCADA Core] WebSocket error event encountered:', err);
        try { socket.close(); } catch (e) { }
    };
}

function handleIncomingMessage(msg) {
    if (isSimulationMode) return;

    if (msg.host_connection) {
        updateSystemConnectionBadge(msg.host_connection);
    }

    if (msg.type === 'INITIAL_STATE' || msg.type === 'TICK') {
        currentSummary = msg.summary || {};
        currentStations = msg.stations || [];
        updateSummaryKPIs(currentSummary);
        renderStationGrid(currentStations);
        checkAlertConditions(msg.alerts || [], currentSummary);
    } else if (msg.type === 'STATION_UPDATE') {
        currentSummary = msg.summary || {};
        updateSummaryKPIs(currentSummary);
        updateSingleStation(msg.station);
    } else if (msg.type === 'STATION_DELETED') {
        currentStations = currentStations.filter(s => s.station_id !== msg.station_id);
        currentSummary = msg.summary || {};
        updateSummaryKPIs(currentSummary);
        renderStationGrid(currentStations);
    } else if (msg.type === 'NETWORK_PROFILE_UPDATED') {
        updateNetworkRibbon(msg.profile);
    }
}

/**
 * Render Summary KPI Cards (ISA-101 Standard)
 */
function updateSummaryKPIs(summary) {
    const availEl = document.getElementById('kpi-availability');
    const totalEl = document.getElementById('kpi-total-stations');
    const onlineEl = document.getElementById('kpi-online-stations');
    const offlineEl = document.getElementById('kpi-offline-stations');
    const criticalEl = document.getElementById('kpi-critical-stations');
    const headerBanner = document.getElementById('line-status-banner');

    const totalStations = summary.total_stations !== undefined ? summary.total_stations : 0;
    const onlineStations = summary.online_stations !== undefined ? summary.online_stations : 0;
    const offlineStations = summary.offline_stations !== undefined ? summary.offline_stations : 0;
    const criticalStations = summary.critical_stations !== undefined ? summary.critical_stations : 0;
    const warningStations = summary.warning_stations !== undefined ? summary.warning_stations : 0;

    if (availEl) availEl.innerText = `${fmt(summary.line_availability_pct, '100.0')}%`;
    if (totalEl) totalEl.innerText = totalStations;
    if (onlineEl) onlineEl.innerText = onlineStations;
    if (offlineEl) offlineEl.innerText = offlineStations;
    if (criticalEl) criticalEl.innerText = criticalStations;

    const criticalOrOffline = totalStations > 0 ? (criticalStations + offlineStations) : 0;

    if (headerBanner) {
        if (criticalOrOffline > 0) {
            headerBanner.className = 'p-3 rounded-lg flex items-center justify-between line-alarm-banner text-white shadow-lg';
            headerBanner.innerHTML = `
                <div class="flex items-center gap-3">
                    <span class="status-led status-led-red"></span>
                    <span class="font-extrabold text-sm tracking-wide uppercase font-mono">
                        LINE ALERT: ${criticalOrOffline} Station(s) experiencing stoppage / critical error!
                    </span>
                </div>
                <span class="text-xs font-mono bg-red-950 text-red-100 px-3 py-1 rounded border border-red-400 font-bold">
                    IMMEDIATE ACTION REQUIRED
                </span>
            `;
            headerBanner.classList.remove('hidden');
        } else if (totalStations > 0 && warningStations > 0) {
            headerBanner.className = 'p-3 bg-amber-50 dark:bg-amber-950/80 border border-amber-400 dark:border-amber-600/70 text-amber-900 dark:text-amber-200 rounded-lg flex items-center justify-between shadow-sm';
            headerBanner.innerHTML = `
                <div class="flex items-center gap-3">
                    <span class="status-led status-led-amber"></span>
                    <span class="font-semibold text-sm tracking-wide font-mono">
                        CAUTION: High resource utilization detected on active stations.
                    </span>
                </div>
                <span class="text-xs font-mono bg-amber-200 dark:bg-amber-900 text-amber-900 dark:text-amber-100 px-2.5 py-1 rounded border border-amber-400 dark:border-amber-500 font-bold">
                    WARNING
                </span>
            `;
            headerBanner.classList.remove('hidden');
        } else {
            headerBanner.className = 'hidden';
            headerBanner.innerHTML = '';
        }
    }
}

/**
 * Evaluates audio trigger conditions
 */
function checkAlertConditions(alerts, summary) {
    const totalStations = summary.total_stations || 0;
    const totalCritical = totalStations > 0 ? ((summary.critical_stations || 0) + (summary.offline_stations || 0)) : 0;
    if (totalCritical > previousCriticalCount) {
        playIndustrialAlertChime();
        loadDowntimeLogs();
    }
    previousCriticalCount = totalCritical;
}

/**
 * Render Station Cards Grid
 */
function renderStationGrid(stations) {
    const container = document.getElementById('stations-grid-container');
    if (!container) return;

    if (!stations || stations.length === 0) {
        container.innerHTML = `
            <div class="col-span-full bg-white dark:bg-[#121a2c] border border-slate-200 dark:border-[#202d45] rounded-xl p-8 text-center flex flex-col items-center justify-center space-y-4 shadow-sm">
                <div class="w-14 h-14 rounded-lg bg-blue-50 dark:bg-[#0b101c] border border-blue-200 dark:border-[#263757] flex items-center justify-center text-blue-600 dark:text-cyan-400">
                    <svg class="w-7 h-7" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.8" d="M9 3v2m6-2v2M9 19v2m6-2v2M5 9H3m2 6H3m18-6h-2m2 6h-2M7 19h10a2 2 0 002-2V7a2 2 0 00-2-2H7a2 2 0 00-2 2v10a2 2 0 002 2zM9 9h6v6H9V9z" />
                    </svg>
                </div>
                <div class="space-y-1">
                    <h3 class="text-base font-bold text-slate-900 dark:text-white uppercase tracking-wider font-mono">SCADA Central Ingestion Engine Active // Port 8090</h3>
                    <p class="text-xs text-slate-600 dark:text-slate-400 max-w-lg mx-auto leading-relaxed font-sans">
                        Awaiting live telemetry packets from production line workstations.
                        When <span class="text-blue-700 dark:text-cyan-300 font-mono font-bold">LineNodeAgent.exe</span> runs on any line workstation in this industrial subnet, it automatically registers and streams telemetry here.
                    </p>
                </div>
                <div class="flex items-center justify-center pt-2">
                    <button onclick="copyServerUrl()" class="px-4 py-2 bg-slate-100 hover:bg-slate-200 dark:bg-[#1e2a42] dark:hover:bg-[#283755] text-slate-800 dark:text-slate-200 rounded text-xs font-mono font-bold border border-slate-300 dark:border-[#304060] flex items-center gap-1.5 transition shadow-sm">
                        <svg class="w-4 h-4 text-blue-600 dark:text-cyan-400" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 5H6a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2v-1M8 5a2 2 0 002 2h2a2 2 0 002-2M8 5a2 2 0 012-2h2a2 2 0 012 2m0 0h2a2 2 0 012 2v3m2 4H10m0 0l3-3m-3 3l3 3" /></svg>
                        <span>Copy Ingestion URL (Port 8090)</span>
                    </button>
                </div>
            </div>
        `;
        return;
    }

    container.innerHTML = stations.map(s => buildStationCardHtml(s)).join('');
}

function updateSingleStation(updatedStation) {
    if (isSimulationMode) return;
    const existingIndex = currentStations.findIndex(s => s.station_id === updatedStation.station_id);
    if (existingIndex >= 0) {
        currentStations[existingIndex] = updatedStation;
    } else {
        currentStations.push(updatedStation);
    }

    const existingCard = document.getElementById(`station-card-${updatedStation.station_id}`);
    if (existingCard) {
        const tempDiv = document.createElement('div');
        tempDiv.innerHTML = buildStationCardHtml(updatedStation);
        existingCard.replaceWith(tempDiv.firstElementChild);
    } else {
        renderStationGrid(currentStations);
    }
}

/**
 * Official Siemens / Rockwell Industrial Faceplate Card Generator
 * Fully adaptive for pristine Light & Dark themes
 */
function buildStationCardHtml(s) {
    const status = s.status || 'OFFLINE';
    const tele = s.system_telemetry || {};
    const zebra = s.zebra_printer || {};
    const disks = tele.disks || [];
    const mainDisk = disks.length > 0 ? disks[0] : null;
    const hostname = s.hostname || 'Unknown Host';

    // Faceplate frame class
    let faceplateClass = 'faceplate-nominal';
    let ledClass = 'status-led-green';
    let statusText = 'RUNNING - NOMINAL';
    let statusTextColor = 'bg-emerald-50 text-emerald-800 border-emerald-300 dark:bg-emerald-950/80 dark:text-emerald-400 dark:border-emerald-800/60';

    if (status === 'WARNING') {
        faceplateClass = 'faceplate-warning';
        ledClass = 'status-led-amber';
        statusText = 'DEGRADED / HIGH LOAD';
        statusTextColor = 'bg-amber-50 text-amber-800 border-amber-300 dark:bg-amber-950/80 dark:text-amber-400 dark:border-amber-800/60';
    } else if (status === 'CRITICAL') {
        faceplateClass = 'faceplate-critical';
        ledClass = 'status-led-red';
        statusText = 'STOPPAGE ALERT';
        statusTextColor = 'bg-red-50 text-red-800 border-red-300 dark:bg-red-950/80 dark:text-red-400 dark:border-red-800/60';
    } else if (status === 'OFFLINE') {
        faceplateClass = 'faceplate-offline';
        ledClass = 'status-led-gray';
        statusText = 'OFFLINE / STANDBY';
        statusTextColor = 'bg-slate-100 text-slate-700 border-slate-300 dark:bg-slate-900 dark:text-slate-400 dark:border-slate-750';
    }

    // Telemetry Permissions gating
    const perms = s.permissions || { cpu: true, ram: true, disk: true, network: true, uptime: true, zebra: false, allow_remote_stop: true };
    const hasCpuPerm = perms.cpu !== false && tele.cpu_percent !== null && tele.cpu_percent !== undefined;
    const hasRamPerm = perms.ram !== false && tele.ram_percent !== null && tele.ram_percent !== undefined;
    const hasDiskPerm = perms.disk !== false;
    const hasNetPerm = perms.network !== false;
    const hasUptimePerm = perms.uptime !== false && tele.uptime_formatted && tele.uptime_formatted !== 'Hidden';
    const hasZebraPerm = perms.zebra === true && s.zebra_monitored;

    // Gauge meter values & colorings
    const cpuPct = hasCpuPerm ? (tele.cpu_percent || 0) : 0;
    const ramPct = hasRamPerm ? (tele.ram_percent || 0) : 0;
    const cpuBarColor = cpuPct > 85 ? 'bg-red-600' : (cpuPct > 70 ? 'bg-amber-500' : 'bg-blue-600 dark:bg-cyan-500');
    const ramBarColor = ramPct > 85 ? 'bg-red-600' : (ramPct > 70 ? 'bg-amber-500' : 'bg-blue-600');

    // Zebra Industrial Peripheral Box
    let zebraHtml = '';
    if (hasZebraPerm) {
        const zStat = zebra.status || 'DISCONNECTED';
        let zBadge = 'zebra-status-error';
        let zText = 'USB DISCONNECTED';
        if (zStat === 'READY') { zBadge = 'zebra-status-ready'; zText = 'ONLINE & READY'; }
        else if (zStat === 'PAPER_OUT') { zBadge = 'zebra-status-warning'; zText = 'PAPER / RIBBON OUT'; }
        else if (zStat === 'OFFLINE') { zBadge = 'zebra-status-warning'; zText = 'SPOOLER OFFLINE'; }
        else if (zStat === 'ERROR' || zStat === 'DOOR_OPEN') { zBadge = 'zebra-status-error'; zText = 'HARDWARE FAULT'; }

        zebraHtml = `
            <div class="mt-3 pt-2.5 border-t border-slate-200 dark:border-[#1e2a42] bg-slate-50 dark:bg-[#090e1a] -mx-4 -mb-4 px-4 py-2.5 rounded-b-lg">
                <div class="flex items-center justify-between mb-1">
                    <div class="flex items-center gap-1.5">
                        <svg class="w-3.5 h-3.5 text-blue-600 dark:text-cyan-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M17 17h2a2 2 0 002-2v-4a2 2 0 00-2-2H5a2 2 0 00-2 2v4a2 2 0 002 2h2m2 4h6a2 2 0 002-2v-4a2 2 0 00-2-2H9a2 2 0 00-2 2v4a2 2 0 002 2zm8-12V5a2 2 0 00-2-2H9a2 2 0 00-2 2v4h10z" />
                        </svg>
                        <span class="text-[11px] font-sans font-bold text-slate-800 dark:text-slate-200 tracking-wide">Zebra ZT411 Industrial</span>
                    </div>
                    <span class="text-[10px] px-2 py-0.5 rounded font-sans font-bold ${zBadge}">
                        ${zText}
                    </span>
                </div>
                <div class="flex items-center justify-between text-[10px] font-sans text-slate-500 dark:text-slate-400">
                    <span>Interface: USB 2.0 (VID_0A5F)</span>
                    <span>Spooler: winspool.drv</span>
                </div>
            </div>
        `;
    }

    // Ping Latency badge
    const latency = tele.network_latency_ms;
    const latencyBadge = hasNetPerm
        ? (latency >= 0 
            ? `<span class="text-xs font-mono font-bold ${latency < 20 ? 'text-emerald-700 dark:text-emerald-400' : (latency < 80 ? 'text-amber-700 dark:text-amber-400' : 'text-red-700 dark:text-red-400')}">${latency} ms TCP</span>`
            : `<span class="text-xs font-mono text-slate-400">Standby</span>`)
        : `<span class="text-[10px] font-mono text-slate-400 bg-slate-100 dark:bg-slate-800 px-1.5 py-0.5 rounded">Masked</span>`;

    // Disk space display
    let diskHtml = '';
    if (hasDiskPerm) {
        if (mainDisk) {
            diskHtml = `
                <div>
                    <div class="flex justify-between text-[11px] text-slate-600 dark:text-slate-300 mb-1 font-sans">
                        <span class="font-semibold">NVMe/SSD (${mainDisk.drive})</span>
                        <span class="font-mono text-slate-500 dark:text-slate-400 font-semibold">${mainDisk.used_gb} / ${mainDisk.total_gb} GB (${mainDisk.percent}%)</span>
                    </div>
                    <div class="meter-track">
                        <div class="meter-fill ${mainDisk.percent > 90 ? 'bg-red-600' : 'bg-slate-400'}" style="width: ${mainDisk.percent}%"></div>
                    </div>
                </div>
            `;
        }
    } else {
        diskHtml = `
            <div>
                <div class="flex justify-between text-[11px] text-slate-500 dark:text-slate-400 mb-1 font-sans">
                    <span class="font-semibold">NVMe/SSD Storage</span>
                    <span class="text-[10px] text-slate-400 dark:text-slate-500 italic font-mono bg-slate-100 dark:bg-slate-800 px-1.5 py-0.5 rounded border border-slate-200 dark:border-slate-700">Permission Disabled</span>
                </div>
            </div>
        `;
    }

    // CPU HTML
    const cpuHtml = hasCpuPerm ? `
        <div>
            <div class="flex justify-between text-[11px] text-slate-600 dark:text-slate-300 mb-1 font-sans">
                <span class="font-semibold">CPU Workload</span>
                <span class="font-bold font-mono text-slate-900 dark:text-white">${cpuPct}% ${tele.cpu_temp_c ? `<span class="text-blue-600 dark:text-cyan-400">(${tele.cpu_temp_c}°C)</span>` : ''}</span>
            </div>
            <div class="meter-track">
                <div class="meter-fill ${cpuBarColor}" style="width: ${Math.min(cpuPct, 100)}%"></div>
            </div>
        </div>
    ` : `
        <div>
            <div class="flex justify-between text-[11px] text-slate-500 dark:text-slate-400 mb-1 font-sans">
                <span class="font-semibold">CPU Workload</span>
                <span class="text-[10px] text-slate-400 dark:text-slate-500 italic font-mono bg-slate-100 dark:bg-slate-800 px-1.5 py-0.5 rounded border border-slate-200 dark:border-slate-700">Permission Disabled</span>
            </div>
            <div class="meter-track">
                <div class="meter-fill bg-slate-300 dark:bg-slate-700/50" style="width: 0%"></div>
            </div>
        </div>
    `;

    // RAM HTML
    const ramHtml = hasRamPerm ? `
        <div>
            <div class="flex justify-between text-[11px] text-slate-600 dark:text-slate-300 mb-1 font-sans">
                <span class="font-semibold">RAM Physical Commit</span>
                <span class="font-bold font-mono text-slate-900 dark:text-white">${ramPct}% <span class="text-slate-500 dark:text-slate-400 font-mono text-[10px]">(${tele.ram_used_gb}/${tele.ram_total_gb} GB)</span></span>
            </div>
            <div class="meter-track">
                <div class="meter-fill ${ramBarColor}" style="width: ${Math.min(ramPct, 100)}%"></div>
            </div>
        </div>
    ` : `
        <div>
            <div class="flex justify-between text-[11px] text-slate-500 dark:text-slate-400 mb-1 font-sans">
                <span class="font-semibold">RAM Physical Commit</span>
                <span class="text-[10px] text-slate-400 dark:text-slate-500 italic font-mono bg-slate-100 dark:bg-slate-800 px-1.5 py-0.5 rounded border border-slate-200 dark:border-slate-700">Permission Disabled</span>
            </div>
            <div class="meter-track">
                <div class="meter-fill bg-slate-300 dark:bg-slate-700/50" style="width: 0%"></div>
            </div>
        </div>
    `;

    return `
        <div id="station-card-${s.station_id}" class="faceplate-card ${faceplateClass} p-4 flex flex-col justify-between">
            <div>
                <!-- Top Faceplate Bar: ID, Host, Status & Deregister -->
                <div class="flex items-start justify-between mb-2">
                    <div>
                        <div class="flex items-center gap-2 flex-wrap">
                            <span class="status-led ${ledClass}"></span>
                            <span class="text-sm font-extrabold tracking-tight text-slate-900 dark:text-white font-sans">${s.station_id}</span>
                            <span class="text-[10px] font-sans px-2 py-0.5 rounded bg-slate-100 dark:bg-[#0b101c] text-blue-700 dark:text-cyan-300 border border-slate-300 dark:border-[#202d45] font-bold" title="Physical Machine Windows Hostname">
                                HOST: ${hostname}
                            </span>
                            <span class="text-[9px] font-sans px-1.5 py-0.5 rounded bg-blue-50 dark:bg-blue-950/60 text-blue-600 dark:text-cyan-400 border border-blue-200 dark:border-blue-900/60 font-semibold" title="Workstation Telemetry Policy Configured">
                                🛡️ Policy Active
                            </span>
                        </div>
                        <p class="text-xs text-slate-600 dark:text-slate-300 font-semibold mt-1 line-clamp-1" title="${s.station_name}">${s.station_name}</p>
                    </div>
                    <div class="flex items-center gap-1.5 flex-shrink-0">
                        <span class="text-[10px] font-bold font-sans px-2.5 py-0.5 rounded border ${statusTextColor}">
                            ${statusText}
                        </span>
                        <button onclick="confirmDeleteStation('${s.station_id}')" class="p-1 rounded text-slate-400 hover:text-red-600 hover:bg-red-50 dark:hover:bg-red-950/40 border border-transparent hover:border-red-300 dark:hover:border-red-900 transition" title="Delete / Deregister Node from SCADA">
                            <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" /></svg>
                        </button>
                    </div>
                </div>

                <!-- Network Sub-Strip -->
                <div class="flex items-center justify-between text-xs bg-slate-50 dark:bg-[#090e1a] rounded px-2.5 py-1.5 mb-3 border border-slate-200 dark:border-[#1b2538] font-sans">
                    <div class="flex items-center gap-2">
                        ${(s.connection_type === 'WIFI' || (s.connection_label && s.connection_label.toLowerCase().includes('wi-fi'))) 
                            ? `<span class="px-1.5 py-0.5 rounded text-[10px] font-bold bg-blue-100 dark:bg-blue-950/70 text-blue-700 dark:text-cyan-300 border border-blue-200 dark:border-blue-800 flex items-center gap-1 font-sans" title="${s.connection_details || 'Connected via Wi-Fi'}">📶 Wi-Fi</span>` 
                            : `<span class="px-1.5 py-0.5 rounded text-[10px] font-bold bg-emerald-100 dark:bg-emerald-950/70 text-emerald-700 dark:text-emerald-300 border border-emerald-200 dark:border-emerald-800 flex items-center gap-1 font-sans" title="${s.connection_details || 'Connected via LAN'}">🔌 LAN</span>`}
                        <span class="text-emerald-700 dark:text-emerald-400 font-bold font-mono">${s.ip_address}</span>
                        <span class="text-[10px] text-slate-500 font-sans">(${hostname})</span>
                    </div>
                    ${latencyBadge}
                </div>

                <!-- Live Instrumentation Gauges -->
                <div class="space-y-2.5">
                    <!-- CPU Workload -->
                    ${cpuHtml}

                    <!-- RAM Physical Commit -->
                    ${ramHtml}

                    <!-- Main Storage Partition -->
                    ${diskHtml}
                </div>
            </div>

            <!-- Card Bottom: Host Uptime & Zebra Status Box -->
            <div class="mt-3">
                <div class="flex items-center justify-between text-[11px] text-slate-500 dark:text-slate-400 font-sans pt-1">
                    <span class="font-medium">Host Uptime:</span>
                    <span class="font-bold font-mono text-slate-800 dark:text-slate-200">${hasUptimePerm ? (tele.uptime_formatted || '0d 00:00:00') : '<span class=\"italic text-[10px] text-slate-400\">Policy Hidden</span>'}</span>
                </div>
                ${zebraHtml}
            </div>
        </div>
    `;
}

/**
 * 6 Official RAC Workstations for Simulation & Demonstration
 */
const SIMULATED_STATIONS = [
    {
        station_id: "LINE1_STATION_01",
        station_name: "ST-01: Chassis Press & Base Assembly",
        ip_address: "192.168.19.101",
        hostname: "RAC-OP-ST01",
        status: "NORMAL",
        zebra_monitored: false,
        system_telemetry: {
            cpu_percent: 24.5,
            ram_percent: 42.1,
            ram_used_gb: 6.7,
            ram_total_gb: 16.0,
            uptime_formatted: "1d 08:14:22",
            network_latency_ms: 8.2,
            disks: [{ drive: "C:", used_gb: 98.4, total_gb: 256.0, percent: 38.4 }]
        }
    },
    {
        station_id: "LINE1_STATION_02",
        station_name: "ST-02: Evaporator Coil & Copper Brazing",
        ip_address: "192.168.19.102",
        hostname: "RAC-OP-ST02",
        status: "NORMAL",
        zebra_monitored: false,
        system_telemetry: {
            cpu_percent: 36.8,
            ram_percent: 54.3,
            ram_used_gb: 8.6,
            ram_total_gb: 16.0,
            uptime_formatted: "1d 08:12:05",
            network_latency_ms: 11.4,
            disks: [{ drive: "C:", used_gb: 112.1, total_gb: 256.0, percent: 43.8 }]
        }
    },
    {
        station_id: "LINE1_STATION_03",
        station_name: "ST-03: Compressor & Motor Mounting",
        ip_address: "192.168.19.103",
        hostname: "RAC-OP-ST03",
        status: "NORMAL",
        zebra_monitored: false,
        system_telemetry: {
            cpu_percent: 48.2,
            ram_percent: 61.0,
            ram_used_gb: 9.7,
            ram_total_gb: 16.0,
            uptime_formatted: "0d 22:45:10",
            network_latency_ms: 14.1,
            disks: [{ drive: "C:", used_gb: 135.5, total_gb: 256.0, percent: 52.9 }]
        }
    },
    {
        station_id: "LINE1_STATION_04",
        station_name: "ST-04: Electrical & Inverter PCB Wiring",
        ip_address: "192.168.19.104",
        hostname: "RAC-OP-ST04",
        status: "NORMAL",
        zebra_monitored: false,
        system_telemetry: {
            cpu_percent: 18.4,
            ram_percent: 38.2,
            ram_used_gb: 6.1,
            ram_total_gb: 16.0,
            uptime_formatted: "2d 04:19:33",
            network_latency_ms: 7.9,
            disks: [{ drive: "C:", used_gb: 85.0, total_gb: 256.0, percent: 33.2 }]
        }
    },
    {
        station_id: "LINE1_STATION_05",
        station_name: "ST-05: Vacuum Decay & R32 Charge",
        ip_address: "192.168.19.105",
        hostname: "RAC-OP-ST05",
        status: "NORMAL",
        zebra_monitored: false,
        system_telemetry: {
            cpu_percent: 52.1,
            ram_percent: 68.4,
            ram_used_gb: 10.9,
            ram_total_gb: 16.0,
            uptime_formatted: "1d 16:30:12",
            network_latency_ms: 12.8,
            disks: [{ drive: "C:", used_gb: 142.3, total_gb: 256.0, percent: 55.6 }]
        }
    },
    {
        station_id: "LINE1_STATION_06",
        station_name: "ST-06: Final QA Run-Test & Zebra ZT411 Labeling",
        ip_address: "192.168.19.106",
        hostname: "RAC-OP-ST06",
        status: "NORMAL",
        zebra_monitored: true,
        zebra_printer: {
            status: "READY",
            printer_name: "Zebra ZT411 Industrial (203 DPI)",
            port_name: "USB001"
        },
        system_telemetry: {
            cpu_percent: 41.5,
            ram_percent: 58.7,
            ram_used_gb: 9.3,
            ram_total_gb: 16.0,
            uptime_formatted: "1d 19:12:44",
            network_latency_ms: 9.5,
            disks: [{ drive: "C:", used_gb: 160.2, total_gb: 256.0, percent: 62.5 }]
        }
    }
];

function toggleSimulationMode() {
    isSimulationMode = !isSimulationMode;
    const btn = document.getElementById('btn-simulation-toggle');
    const label = document.getElementById('simulation-label');

    if (isSimulationMode) {
        if (btn) {
            btn.classList.add('bg-amber-600', 'text-white', 'border-amber-400');
            btn.classList.remove('bg-slate-800', 'text-amber-300');
        }
        if (label) label.innerText = 'Simulation: ACTIVE';
        
        renderStationGrid(SIMULATED_STATIONS);
        updateSummaryKPIs({
            total_stations: 6,
            online_stations: 6,
            offline_stations: 0,
            warning_stations: 0,
            critical_stations: 0,
            line_availability_pct: 100.0
        });

        if (!simulationInterval) {
            simulationInterval = setInterval(() => {
                if (!isSimulationMode) return;
                SIMULATED_STATIONS.forEach(s => {
                    const deltaCpu = (Math.random() * 6 - 3);
                    s.system_telemetry.cpu_percent = Math.max(12, Math.min(80, Math.round((s.system_telemetry.cpu_percent + deltaCpu) * 10) / 10));
                    const deltaLat = (Math.random() * 2 - 1);
                    s.system_telemetry.network_latency_ms = Math.max(5, Math.min(25, Math.round((s.system_telemetry.network_latency_ms + deltaLat) * 10) / 10));
                });
                renderStationGrid(SIMULATED_STATIONS);
            }, 3000);
        }
    } else {
        if (simulationInterval) {
            clearInterval(simulationInterval);
            simulationInterval = null;
        }
        if (btn) {
            btn.classList.remove('bg-amber-600', 'text-white', 'border-amber-400');
            btn.classList.add('bg-slate-800', 'text-amber-300');
        }
        if (label) label.innerText = 'Simulation: OFF';

        renderStationGrid(currentStations);
        if (socket && socket.readyState === WebSocket.OPEN) {
            socket.send(JSON.stringify({ type: 'GET_STATE' }));
        }
    }
}

/**
 * Downtime Incident Log Table (21 CFR Part 11 Compliant)
 */
async function loadDowntimeLogs() {
    try {
        let url = '/api/downtime-logs?limit=50';
        if (currentFilter !== 'ALL') {
            url += `&status=${currentFilter}`;
        }
        const res = await fetch(url);
        const data = await res.json();
        renderDowntimeTable(data.logs || []);
    } catch (e) {
        console.error('Failed to load downtime logs:', e);
    }
}

function renderDowntimeTable(logs) {
    const tbody = document.getElementById('downtime-table-body');
    const emptyState = document.getElementById('downtime-empty-state');
    const headerCountEl = document.getElementById('header-log-count');
    if (headerCountEl) {
        headerCountEl.innerText = logs.length;
    }
    if (!tbody) return;

    if (logs.length === 0) {
        tbody.innerHTML = '';
        if (emptyState) emptyState.classList.remove('hidden');
        return;
    }
    if (emptyState) emptyState.classList.add('hidden');

    tbody.innerHTML = logs.map(log => {
        const isActive = log.status === 'ACTIVE';
        const statusBadge = isActive
            ? '<span class="inline-flex items-center gap-1.5 px-2 py-0.5 rounded text-[11px] font-bold bg-red-100 text-red-700 border border-red-300 dark:bg-red-950/90 dark:text-red-400 dark:border-red-800"><span class="status-led status-led-red"></span>ACTIVE</span>'
            : '<span class="inline-flex items-center gap-1 px-2 py-0.5 rounded text-[11px] font-semibold bg-slate-100 text-slate-700 border border-slate-300 dark:bg-[#141e34] dark:text-slate-300 dark:border-[#263757]">RESOLVED</span>';

        const startTimeFmt = formatIsoDate(log.start_time);
        const endTimeFmt = log.end_time ? formatIsoDate(log.end_time) : '<span class="text-red-600 font-semibold italic">Ongoing...</span>';

        return `
            <tr>
                <td class="text-slate-500 dark:text-slate-400">#${log.id}</td>
                <td>
                    <div class="font-bold text-slate-900 dark:text-white text-xs">${log.station_id}</div>
                    <div class="text-[11px] text-slate-500 dark:text-slate-400">${log.station_name}</div>
                </td>
                <td class="text-slate-700 dark:text-slate-200 font-medium">
                    ${log.failure_reason}
                </td>
                <td class="text-slate-600 dark:text-slate-300">${startTimeFmt}</td>
                <td class="text-slate-600 dark:text-slate-300">${endTimeFmt}</td>
                <td class="font-bold ${isActive ? 'text-red-600' : 'text-slate-700 dark:text-slate-200'}">
                    ${log.duration_formatted}
                </td>
                <td class="text-right">
                    ${statusBadge}
                </td>
            </tr>
        `;
    }).join('');
}

function formatIsoDate(isoStr) {
    if (!isoStr) return '-';
    try {
        const d = new Date(isoStr);
        return d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit', hour12: false });
    } catch {
        return isoStr;
    }
}

/**
 * Filter and Export Handlers
 */
function setLogFilter(filter) {
    currentFilter = filter;
    document.querySelectorAll('.filter-btn').forEach(btn => {
        if (btn.dataset.filter === filter) {
            btn.className = 'filter-btn px-3 py-1 rounded text-xs font-semibold bg-blue-600 text-white shadow-sm';
        } else {
            btn.className = 'filter-btn px-3 py-1 rounded text-xs font-medium text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white';
        }
    });
    loadDowntimeLogs();
}

function exportCsvReport() {
    window.location.href = '/api/downtime-logs/export-csv';
}

/**
 * Precision Digital Shift Clock
 */
function updateClock() {
    const clockEl = document.getElementById('live-shift-clock');
    if (clockEl) {
        const now = new Date();
        clockEl.innerText = now.toISOString().substr(11, 8) + ' UTC';
    }
}

/**
 * Audio Siren Toggle
 */
function toggleAudioAlerts() {
    audioAlertsEnabled = !audioAlertsEnabled;
    const btn = document.getElementById('btn-audio-toggle');
    const label = document.getElementById('audio-toggle-label');
    
    getAudioContext();

    if (audioAlertsEnabled) {
        if (btn) btn.className = 'flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs font-semibold bg-emerald-600 text-white border border-emerald-500 transition';
        if (label) label.innerText = 'Chime ON';
        playIndustrialAlertChime();
    } else {
        if (btn) btn.className = 'flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs font-semibold bg-slate-800 hover:bg-slate-700 text-slate-300 border border-slate-700 transition';
        if (label) label.innerText = 'Alert Chime OFF';
    }
}

// Native Industrial Dialogs (Replaces all browser confirm and alert popups)
function nativeConfirm(title, message, onOk, okBtnText = 'Remove Workstation') {
    const modal = document.getElementById('scada-confirm-modal');
    const titleEl = document.getElementById('scada-confirm-title');
    const msgEl = document.getElementById('scada-confirm-message');
    const okBtn = document.getElementById('btn-scada-confirm-ok');
    const cancelBtn = document.getElementById('btn-scada-confirm-cancel');

    if (titleEl) titleEl.innerText = title;
    if (msgEl) msgEl.innerText = message;
    if (okBtn) okBtn.innerText = okBtnText;

    const newOk = okBtn.cloneNode(true);
    okBtn.parentNode.replaceChild(newOk, okBtn);
    newOk.onclick = () => {
        modal.classList.add('hidden');
        if (onOk) onOk();
    };

    if (cancelBtn) {
        cancelBtn.onclick = () => {
            modal.classList.add('hidden');
        };
    }

    modal.classList.remove('hidden');
}

function nativeAlert(title, message) {
    const modal = document.getElementById('scada-alert-modal');
    const titleEl = document.getElementById('scada-alert-title');
    const msgEl = document.getElementById('scada-alert-message');
    const okBtn = document.getElementById('btn-scada-alert-ok');

    if (titleEl) titleEl.innerText = title;
    if (msgEl) msgEl.innerText = message;

    if (okBtn) {
        okBtn.onclick = () => {
            modal.classList.add('hidden');
        };
    }

    modal.classList.remove('hidden');
}

// Global window alert override to prevent any browser popup
window.alert = function(msg) {
    nativeAlert('SCADA System Notice', msg);
};

// Settings Modal Management
function openSettingsModal() {
    fetch('/api/settings')
        .then(r => r.json())
        .then(s => {
            document.getElementById('input-hb-timeout').value = s.heartbeat_timeout_sec || 10;
            document.getElementById('input-cpu-thresh').value = s.cpu_warning_pct || 85;
            document.getElementById('input-ram-thresh').value = s.ram_warning_pct || 85;
            document.getElementById('input-ping-thresh').value = s.ping_warning_ms || 50;
            document.getElementById('settings-modal').classList.remove('hidden');
        });
}

function closeSettingsModal() {
    document.getElementById('settings-modal').classList.add('hidden');
}

async function saveSettings(e) {
    e.preventDefault();
    const payload = {
        heartbeat_timeout_sec: parseInt(document.getElementById('input-hb-timeout').value),
        cpu_warning_pct: parseFloat(document.getElementById('input-cpu-thresh').value),
        ram_warning_pct: parseFloat(document.getElementById('input-ram-thresh').value),
        ping_warning_ms: parseFloat(document.getElementById('input-ping-thresh').value)
    };

    try {
        await fetch('/api/settings', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload)
        });
        closeSettingsModal();
    } catch (err) {
        nativeAlert('Configuration Notice', 'Failed to save settings: ' + err);
    }
}

// Station Deletion Handler (100% In-App Dialog, Zero Browser Alert)
async function confirmDeleteStation(stationId) {
    nativeConfirm(
        'Confirm Workstation Removal',
        `Are you sure you want to remove workstation node '${stationId}' from the SCADA monitoring engine?`,
        async () => {
            try {
                const res = await fetch(`/api/stations/${stationId}`, { method: 'DELETE' });
                if (res.ok) {
                    currentStations = currentStations.filter(s => s.station_id !== stationId);
                    renderStationGrid(currentStations);
                    const data = await res.json();
                    if (data.summary) updateSummaryKPIs(data.summary);
                } else {
                    nativeAlert('Action Failed', 'Failed to remove workstation: ' + res.statusText);
                }
            } catch (e) {
                nativeAlert('Communication Error', 'Communication error while removing workstation: ' + e);
            }
        },
        'Remove Workstation'
    );
}

// Real-Time System Network Connection Indicator
function updateSystemConnectionBadge(conn) {
    if (!conn) return;
    const iconEl = document.getElementById('system-conn-icon');
    const badgeEl = document.getElementById('system-conn-badge');
    const detailsEl = document.getElementById('system-conn-details');
    const ipEl = document.getElementById('system-conn-ip');

    if (ipEl && conn.ip) {
        ipEl.innerText = conn.ip;
    }

    const type = (conn.type || '').toUpperCase();
    if (type === 'LAN') {
        if (iconEl) iconEl.innerText = '🔌';
        if (badgeEl) {
            badgeEl.innerText = 'LAN';
            badgeEl.className = 'font-extrabold text-[11px] tracking-wide text-emerald-400 uppercase font-sans';
        }
        if (detailsEl) {
            detailsEl.innerText = conn.details || 'Ethernet LAN';
        }
    } else if (type === 'WIFI') {
        if (iconEl) iconEl.innerText = '📶';
        if (badgeEl) {
            badgeEl.innerText = 'WI-FI';
            badgeEl.className = 'font-extrabold text-[11px] tracking-wide text-cyan-400 uppercase font-sans';
        }
        if (detailsEl) {
            detailsEl.innerText = conn.details || 'Wi-Fi Connected';
        }
    } else {
        if (iconEl) iconEl.innerText = '⚠️';
        if (badgeEl) {
            badgeEl.innerText = 'DISCONNECTED';
            badgeEl.className = 'font-extrabold text-[11px] tracking-wide text-red-400 uppercase font-sans';
        }
        if (detailsEl) {
            detailsEl.innerText = 'No Active Network';
        }
    }
}

async function fetchLiveConnectionStatus() {
    try {
        const res = await fetch('/api/connection-status');
        if (res.ok) {
            const data = await res.json();
            updateSystemConnectionBadge(data);
        }
    } catch (e) {
        // quiet ignore
    }
}

// Network Configuration Wizard Management
let currentNetworkConfig = null;
let detectedNetworkAdapters = [];

function updateNetworkRibbon(profile) {
    if (!profile) return;
    const ipEl = document.getElementById('strip-server-ip');
    const subnetEl = document.getElementById('strip-subnet');
    const gwEl = document.getElementById('strip-gateway');
    const hostStr = `${profile.ip}:${profile.port || 8090}`;
    const subnetStr = profile.subnet_cidr || 'N/A';
    if (ipEl) ipEl.innerText = hostStr;
    if (subnetEl) subnetEl.innerText = subnetStr;
    if (gwEl) gwEl.innerText = profile.gateway || 'N/A';

    const footHost = document.getElementById('foot-host');
    const footSubnet = document.getElementById('foot-subnet');
    if (footHost) footHost.innerText = hostStr;
    if (footSubnet) footSubnet.innerText = subnetStr;
}

function calculateSubnetCidr(ipStr, maskStr) {
    try {
        const ipParts = ipStr.split('.').map(Number);
        const maskParts = maskStr.split('.').map(Number);
        if (ipParts.length === 4 && maskParts.length === 4 && !ipParts.some(isNaN) && !maskParts.some(isNaN)) {
            const netParts = ipParts.map((p, idx) => p & maskParts[idx]);
            const prefix = maskParts.reduce((acc, oct) => acc + (oct.toString(2).match(/1/g) || []).length, 0);
            return `${netParts.join('.')}/${prefix}`;
        }
    } catch (e) {}
    return 'N/A';
}

function updateLiveNetworkDisplay() {
    const ip = document.getElementById('cfg-ip')?.value?.trim() || '';
    const mask = document.getElementById('cfg-subnet')?.value?.trim() || '';
    const port = document.getElementById('cfg-port')?.value?.trim() || '8090';

    const cidrEl = document.getElementById('cfg-cidr-display');
    const rangeEl = document.getElementById('cfg-range-display');
    const urlEl = document.getElementById('net-server-url');

    if (ip && mask) {
        const cidr = calculateSubnetCidr(ip, mask);
        if (cidrEl) cidrEl.innerText = cidr;

        try {
            const ipParts = ip.split('.').map(Number);
            const maskParts = mask.split('.').map(Number);
            const netParts = ipParts.map((p, i) => p & maskParts[i]);
            const bcastParts = ipParts.map((p, i) => p | (~maskParts[i] & 255));
            const firstHost = `${netParts[0]}.${netParts[1]}.${netParts[2]}.${netParts[3] + 1}`;
            const lastHost = `${bcastParts[0]}.${bcastParts[1]}.${bcastParts[2]}.${bcastParts[3] - 1}`;
            if (rangeEl) rangeEl.innerText = `Range: ${firstHost} - ${lastHost}`;
        } catch (e) {
            if (rangeEl) rangeEl.innerText = '';
        }
    }
    if (urlEl && ip) {
        urlEl.innerText = `http://${ip}:${port}`;
    }
}

async function loadAndInitializeNetwork(isStartup = false) {
    try {
        const res = await fetch('/api/network-details');
        const data = await res.json();
        currentNetworkConfig = data.configured || {};
        detectedNetworkAdapters = data.adapters || [];

        updateNetworkRibbon(currentNetworkConfig);

        if (isStartup && currentNetworkConfig.prompt_on_startup) {
            openNetworkModal();
        }
    } catch (e) {
        console.error('Failed to load network details:', e);
    }
}

async function openNetworkModal() {
    try {
        const res = await fetch('/api/network-details');
        const data = await res.json();
        currentNetworkConfig = data.configured || {};
        detectedNetworkAdapters = data.adapters || [];

        const select = document.getElementById('cfg-adapter-select');
        if (select) {
            select.innerHTML = '<option value="">-- Choose Detected Industrial Adapter --</option>';
            detectedNetworkAdapters.forEach((a, idx) => {
                const opt = document.createElement('option');
                opt.value = idx;
                opt.innerText = `${a.name} (${a.ip}) - Subnet: ${a.subnet_mask}`;
                select.appendChild(opt);
            });
            select.onchange = () => {
                const selIdx = select.value;
                if (selIdx !== '' && detectedNetworkAdapters[selIdx]) {
                    const sel = detectedNetworkAdapters[selIdx];
                    document.getElementById('cfg-ip').value = sel.ip;
                    document.getElementById('cfg-subnet').value = sel.subnet_mask;
                    if (sel.gateway) document.getElementById('cfg-gateway').value = sel.gateway;
                    updateLiveNetworkDisplay();
                }
            };
        }

        const ipInput = document.getElementById('cfg-ip');
        const subnetInput = document.getElementById('cfg-subnet');
        const gwInput = document.getElementById('cfg-gateway');
        const portInput = document.getElementById('cfg-port');
        const enforceCheck = document.getElementById('cfg-enforce-subnet');
        const promptCheck = document.getElementById('cfg-prompt-startup');

        if (ipInput) ipInput.value = currentNetworkConfig.ip || '192.168.19.170';
        if (subnetInput) subnetInput.value = currentNetworkConfig.subnet_mask || '255.255.255.0';
        if (gwInput) gwInput.value = currentNetworkConfig.gateway || '192.168.19.1';
        if (portInput) portInput.value = currentNetworkConfig.port || 8090;
        if (enforceCheck) enforceCheck.checked = currentNetworkConfig.enforce_subnet !== false;
        if (promptCheck) promptCheck.checked = currentNetworkConfig.prompt_on_startup !== false;

        ipInput?.addEventListener('input', updateLiveNetworkDisplay);
        subnetInput?.addEventListener('input', updateLiveNetworkDisplay);
        portInput?.addEventListener('input', updateLiveNetworkDisplay);

        updateLiveNetworkDisplay();
        document.getElementById('network-modal').classList.remove('hidden');
    } catch (e) {
        console.error('Error opening network wizard:', e);
        document.getElementById('network-modal').classList.remove('hidden');
    }
}

function closeNetworkModal() {
    document.getElementById('network-modal').classList.add('hidden');
}

async function saveNetworkConfiguration() {
    const ip = document.getElementById('cfg-ip')?.value?.trim();
    const subnet = document.getElementById('cfg-subnet')?.value?.trim();
    const gateway = document.getElementById('cfg-gateway')?.value?.trim();
    const port = parseInt(document.getElementById('cfg-port')?.value?.trim() || '8090');
    const enforce = document.getElementById('cfg-enforce-subnet')?.checked ?? true;
    const promptStartup = document.getElementById('cfg-prompt-startup')?.checked ?? true;

    if (!ip || !subnet) {
        nativeAlert('Validation Error', 'Please specify a valid IPv4 address and Subnet Mask.');
        return;
    }

    const payload = {
        ip: ip,
        subnet_mask: subnet,
        gateway: gateway,
        port: port,
        enforce_subnet: enforce,
        prompt_on_startup: promptStartup
    };

    const btn = document.getElementById('btn-save-net-config');
    const originalText = btn ? btn.innerHTML : '';
    if (btn) btn.innerHTML = '<span>Saving...</span>';

    try {
        const res = await fetch('/api/network-config', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload)
        });
        if (res.ok) {
            const data = await res.json();
            currentNetworkConfig = data.profile || payload;
            updateNetworkRibbon(currentNetworkConfig);
            if (btn) btn.innerHTML = '<span>✓ Saved & Applied!</span>';
            setTimeout(() => {
                if (btn) btn.innerHTML = originalText;
                closeNetworkModal();
            }, 800);
        } else {
            const err = await res.json();
            nativeAlert('Configuration Error', 'Error saving network configuration: ' + (err.detail || res.statusText));
            if (btn) btn.innerHTML = originalText;
        }
    } catch (e) {
        nativeAlert('Network Communication Error', 'Network request failed: ' + e);
        if (btn) btn.innerHTML = originalText;
    }
}

function copyServerUrl() {
    const urlEl = document.getElementById('net-server-url');
    const text = urlEl ? urlEl.innerText.trim() : `http://${window.location.host}`;
    if (text) {
        navigator.clipboard.writeText(text).then(() => {
            const btn = document.getElementById('btn-copy-url');
            if (btn) {
                const orig = btn.innerHTML;
                btn.innerHTML = '<span>✓ Copied!</span>';
                btn.classList.add('bg-emerald-600', 'text-white');
                setTimeout(() => {
                    btn.innerHTML = orig;
                    btn.classList.remove('bg-emerald-600', 'text-white');
                }, 2000);
            }
        });
    }
}

function scrollToAuditLog() {
    const logSection = document.getElementById('section-audit-log');
    if (logSection) {
        logSection.scrollIntoView({ behavior: 'smooth', block: 'start' });
        logSection.classList.add('audit-highlight');
        setTimeout(() => {
            logSection.classList.remove('audit-highlight');
        }, 2200);
    }
}

// Document Load Initializer
document.addEventListener('DOMContentLoaded', () => {
    initTheme();
    connectWebSocket();
    loadDowntimeLogs();
    fetchLiveConnectionStatus();
    setInterval(updateClock, 1000);
    setInterval(loadDowntimeLogs, 5000);
    setInterval(fetchLiveConnectionStatus, 4000);

    // Instant Reconnect on Network Restore
    window.addEventListener('online', () => {
        console.log('[SCADA Core] Network online event detected. Restoring live dashboard connections...');
        connectWebSocket();
        loadDowntimeLogs();
        fetchLiveConnectionStatus();
    });

    // Initialize Network Ribbon & Prompt Wizard on startup
    loadAndInitializeNetwork(true);

    // Hook Audit Log Quick Nav Options
    document.getElementById('btn-audit-log-nav')?.addEventListener('click', scrollToAuditLog);
    document.getElementById('btn-audit-sub-nav')?.addEventListener('click', scrollToAuditLog);

    // Hook buttons
    document.getElementById('btn-theme-toggle')?.addEventListener('click', toggleTheme);
    document.getElementById('btn-audio-toggle')?.addEventListener('click', toggleAudioAlerts);
    document.getElementById('btn-export-csv')?.addEventListener('click', exportCsvReport);
    document.getElementById('btn-settings')?.addEventListener('click', openSettingsModal);
    document.getElementById('btn-close-modal')?.addEventListener('click', closeSettingsModal);
    document.getElementById('settings-form')?.addEventListener('submit', saveSettings);

    // Network Config Modal Hooks
    document.getElementById('btn-network')?.addEventListener('click', () => openNetworkModal());
    document.getElementById('btn-close-network-modal')?.addEventListener('click', closeNetworkModal);
    document.getElementById('btn-close-net-footer')?.addEventListener('click', closeNetworkModal);
    document.getElementById('btn-refresh-net')?.addEventListener('click', () => openNetworkModal());
    document.getElementById('btn-save-net-config')?.addEventListener('click', saveNetworkConfiguration);
    document.getElementById('btn-copy-url')?.addEventListener('click', copyServerUrl);

    document.querySelectorAll('.filter-btn').forEach(btn => {
        btn.addEventListener('click', () => setLogFilter(btn.dataset.filter));
    });
});
