<?php
/**
 * PlantOps™ Enterprise SCADA - PHP Portal Configuration
 */

define('APP_NAME', 'PlantOps Enterprise SCADA');
define('APP_VERSION', '2.4.0');
define('DB_PATH', dirname(__DIR__) . '/plantops_monitor/plantops_monitor.db');

$CONFIG = [
    'server' => [
        'host' => '0.0.0.0',
        'port' => 8080,
        'dashboard_title' => 'PlantOps Enterprise SCADA'
    ],
    'thresholds' => [
        'heartbeat_timeout_sec' => 10,
        'cpu_warning_pct' => 85.0,
        'ram_warning_pct' => 85.0,
        'ping_warning_ms' => 50.0
    ],
    'network_profile' => [
        'ip' => '192.168.19.170',
        'subnet_mask' => '255.255.255.0',
        'gateway' => '192.168.19.1',
        'subnet_cidr' => '192.168.19.0/24'
    ]
];
