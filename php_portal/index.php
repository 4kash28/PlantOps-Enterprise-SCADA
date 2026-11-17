<?php
/**
 * PlantOps™ Enterprise SCADA - Official PHP Portal
 * Enterprise Industrial Assembly Line Management Suite
 */

require_once __DIR__ . '/config.php';
require_once __DIR__ . '/db.php';

$pdo = getDbConnection();
$totalStations = 0;
$onlineStations = 0;
$availability = 100.0;

if ($pdo) {
    try {
        $totalStations = (int)$pdo->query("SELECT COUNT(*) FROM stations")->fetchColumn();
        $onlineStations = (int)$pdo->query("SELECT COUNT(*) FROM stations WHERE status = 'NORMAL'")->fetchColumn();
        $availability = $totalStations > 0 ? round(($onlineStations / $totalStations) * 100, 1) : 100.0;
    } catch (Exception $e) {}
}
?>
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title><?= htmlspecialchars(APP_NAME) ?> - Industrial PHP Suite v<?= htmlspecialchars(APP_VERSION) ?></title>
    
    <script>
        if (localStorage.getItem('plantops_theme') === 'dark') {
            document.documentElement.classList.add('dark');
        } else {
            document.documentElement.classList.remove('dark');
        }
    </script>

    <script src="https://cdn.tailwindcss.com"></script>
    <script>
        tailwind.config = {
            darkMode: 'class',
            theme: {
                extend: {
                    colors: {
                        scada: {
                            blue: '#2563eb',
                            emerald: '#16a34a',
                            amber: '#d97706',
                            crimson: '#dc2626'
                        }
                    }
                }
            }
        }
    </script>
    <link rel="stylesheet" href="/static/styles.css">
    <style>
        /* Standalone fallback styles if static/styles.css is served via PHP */
        body { font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif; }
    </style>
</head>
<body class="bg-[#f1f5f9] dark:bg-[#090d16] text-slate-900 dark:text-slate-100 min-h-screen flex flex-col antialiased selection:bg-blue-600 selection:text-white pb-10">

    <!-- Top Corporate Header -->
    <header class="bg-[#0f172a] border-b border-slate-800 sticky top-0 z-40 shadow-md">
        <div class="max-w-[1780px] mx-auto px-4 sm:px-6 lg:px-8 h-16 flex items-center justify-between gap-4">
            
            <div class="flex items-center gap-3.5 flex-shrink-0">
                <div class="w-10 h-10 rounded-lg bg-blue-600/20 border border-blue-500/40 flex items-center justify-center text-cyan-400 font-bold">
                    🐘
                </div>
                <div>
                    <div class="flex items-center gap-2">
                        <h1 class="text-sm sm:text-base font-extrabold text-white tracking-wide uppercase font-mono">
                            PlantOps<span class="text-cyan-400">™</span> PHP SCADA Enterprise
                        </h1>
                        <span class="text-[10px] font-mono px-2 py-0.5 rounded bg-blue-900 text-blue-200 border border-blue-700 font-bold">
                            PHP v<?= PHP_MAJOR_VERSION . '.' . PHP_MINOR_VERSION ?>
                        </span>
                        <span class="hidden md:inline-flex items-center gap-1.5 text-[10px] font-mono px-2 py-0.5 rounded bg-emerald-950 text-emerald-300 border border-emerald-800 font-semibold">
                            ● PHP ENGINE ACTIVE
                        </span>
                    </div>
                    <p class="text-xs text-slate-400 font-medium">Plant 01 (Chennai Works) • Assembly Division • RAC Line 01</p>
                </div>
            </div>

            <!-- Guaranteed Single-Line Central Connection Pill -->
            <div id="system-connection-pill" class="flex items-center gap-2 px-3.5 py-1 bg-slate-900/95 rounded-full border border-slate-700 text-xs shadow-inner whitespace-nowrap flex-shrink-0">
                <span class="status-led status-led-green"></span>
                <span id="system-conn-icon" class="text-xs">📶</span>
                <span id="system-conn-badge" class="font-extrabold text-[11px] tracking-wide text-cyan-400 uppercase font-sans">WI-FI</span>
                <span class="text-slate-600">|</span>
                <span id="system-conn-details" class="text-[11px] text-slate-300 font-medium">Detecting connection...</span>
                <span class="text-slate-600">|</span>
                <span id="system-conn-ip" class="text-[11px] text-emerald-400 font-mono font-bold"><?= htmlspecialchars($_SERVER['SERVER_ADDR'] ?? '127.0.0.1') ?></span>
            </div>

            <!-- Toolbar -->
            <div class="flex items-center gap-2">
                <button id="btn-theme-toggle" class="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-semibold bg-slate-800 hover:bg-slate-700 text-slate-200 border border-slate-700 shadow-sm">
                    <span id="theme-icon">☀️</span>
                    <span id="theme-label" class="hidden sm:inline">Theme: Light</span>
                </button>
                <button id="btn-simulation-toggle" class="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-semibold bg-slate-800 hover:bg-slate-700 text-amber-300 border border-amber-600/50 shadow-sm">
                    ⚡ <span id="simulation-label">Simulation Mode</span>
                </button>
            </div>
        </div>
    </header>

    <!-- KPI Metric Summary -->
    <main class="flex-1 max-w-[1780px] w-full mx-auto px-4 sm:px-6 lg:px-8 py-5 space-y-5">
        <section class="grid grid-cols-2 md:grid-cols-5 gap-3.5">
            <div class="bg-white dark:bg-[#121a2c] border border-slate-200 dark:border-[#202d45] rounded-lg p-4 shadow-sm">
                <div class="text-[11px] font-bold text-slate-500 uppercase font-mono">Line Availability</div>
                <div id="kpi-availability" class="text-3xl font-black font-mono text-emerald-600 mt-1"><?= $availability ?>%</div>
                <div class="text-[11px] text-slate-400 mt-1">Target: 98.5% (ISO 22400)</div>
            </div>

            <div class="bg-white dark:bg-[#121a2c] border border-slate-200 dark:border-[#202d45] rounded-lg p-4 shadow-sm">
                <div class="text-[11px] font-bold text-slate-500 uppercase font-mono">Workstations</div>
                <div id="kpi-total-stations" class="text-3xl font-black font-mono text-slate-900 dark:text-white mt-1"><?= $totalStations ?></div>
                <div class="text-[11px] text-slate-400 mt-1">Configured Workstations</div>
            </div>

            <div class="bg-white dark:bg-[#121a2c] border border-slate-200 dark:border-[#202d45] rounded-lg p-4 shadow-sm">
                <div class="text-[11px] font-bold text-slate-500 uppercase font-mono">Nodes Online</div>
                <div id="kpi-online-stations" class="text-3xl font-black font-mono text-emerald-600 mt-1"><?= $onlineStations ?></div>
                <div class="text-[11px] text-slate-400 mt-1">Real-time Telemetry</div>
            </div>

            <div class="bg-white dark:bg-[#121a2c] border border-slate-200 dark:border-[#202d45] rounded-lg p-4 shadow-sm">
                <div class="text-[11px] font-bold text-slate-500 uppercase font-mono">Active Stoppages</div>
                <div id="kpi-critical-stations" class="text-3xl font-black font-mono text-emerald-600 mt-1">0</div>
                <div class="text-[11px] text-slate-400 mt-1">Nominal Operation</div>
            </div>

            <div class="bg-white dark:bg-[#121a2c] border border-slate-200 dark:border-[#202d45] rounded-lg p-4 shadow-sm">
                <div class="text-[11px] font-bold text-slate-500 uppercase font-mono">Subnet Binding</div>
                <div class="text-lg font-bold font-mono text-blue-600 mt-1"><?= htmlspecialchars($CONFIG['network_profile']['subnet_cidr']) ?></div>
                <div class="text-[11px] text-slate-400 mt-1">Enforced LAN Range</div>
            </div>
        </section>

        <!-- Workstations Section -->
        <section>
            <div class="flex items-center justify-between mb-3">
                <h2 class="text-sm font-bold uppercase tracking-wider text-slate-900 dark:text-white font-mono flex items-center gap-2">
                    <span class="w-2.5 h-2.5 bg-blue-600 rounded-sm"></span>
                    Production Line Workstations (PHP REST Feed)
                </h2>
            </div>
            <div id="stations-grid-container" class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
                <!-- Dynamically loaded -->
            </div>
        </section>
    </main>

    <!-- Footer -->
    <footer class="fixed bottom-0 left-0 right-0 h-7 bg-[#0f172a] border-t border-slate-800 z-30 px-4 flex items-center justify-between text-[11px] font-mono text-slate-300">
        <div class="flex items-center gap-3">
            <span class="text-emerald-400 font-bold">● PHP SCADA PORTAL ACTIVE</span>
            <span>|</span>
            <span>HOST: <?= htmlspecialchars($CONFIG['network_profile']['ip']) ?>:<?= $CONFIG['server']['port'] ?></span>
        </div>
        <div class="text-slate-400">
            PHP Version: <?= phpversion() ?> | SQLite Persistence
        </div>
    </footer>

    <script src="/static/app.js"></script>
</body>
</html>
