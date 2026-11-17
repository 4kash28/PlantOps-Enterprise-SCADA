<?php
/**
 * PlantOps™ Enterprise SCADA - PHP REST API Endpoint
 * Handles live telemetry queries, downtime logs, and summary KPIs
 */

header('Content-Type: application/json');
header('Access-Control-Allow-Origin: *');
header('Access-Control-Allow-Methods: GET, POST, DELETE, OPTIONS');
header('Access-Control-Allow-Headers: Content-Type');

if ($_SERVER['REQUEST_METHOD'] === 'OPTIONS') {
    http_response_code(200);
    exit;
}

require_once __DIR__ . '/config.php';
require_once __DIR__ . '/db.php';

$action = $_GET['action'] ?? 'summary';
$pdo = getDbConnection();

if (!$pdo) {
    echo json_encode([
        'status' => 'error',
        'message' => 'Database connection failed. Ensure SQLite is available.',
        'configured' => $CONFIG['network_profile']
    ]);
    exit;
}

switch ($action) {
    case 'summary':
        try {
            $stmt = $pdo->query("SELECT * FROM stations ORDER BY station_id ASC");
            $stations = $stmt->fetchAll();

            $total = count($stations);
            $online = 0;
            $critical = 0;
            $warning = 0;

            foreach ($stations as &$s) {
                if ($s['status'] === 'NORMAL') $online++;
                elseif ($s['status'] === 'CRITICAL') $critical++;
                elseif ($s['status'] === 'WARNING') { $online++; $warning++; }

                // Decode JSON fields if present
                if (!empty($s['system_telemetry'])) {
                    $s['system_telemetry'] = json_decode($s['system_telemetry'], true);
                }
                if (!empty($s['zebra_printer'])) {
                    $s['zebra_printer'] = json_decode($s['zebra_printer'], true);
                }
            }

            $offline = $total - $online;
            $availability = $total > 0 ? round((($online) / $total) * 100, 1) : 100.0;

            echo json_encode([
                'status' => 'success',
                'summary' => [
                    'total_stations' => $total,
                    'online_stations' => $online,
                    'offline_stations' => $offline,
                    'critical_stations' => $critical,
                    'warning_stations' => $warning,
                    'line_availability_pct' => $availability
                ],
                'stations' => $stations,
                'network_profile' => $CONFIG['network_profile']
            ]);
        } catch (Exception $e) {
            echo json_encode(['status' => 'error', 'message' => $e->getMessage()]);
        }
        break;

    case 'downtime':
        try {
            $limit = isset($_GET['limit']) ? (int)$_GET['limit'] : 50;
            $filter = $_GET['status'] ?? 'ALL';

            $sql = "SELECT * FROM downtime_incidents ";
            if ($filter === 'ACTIVE') {
                $sql .= "WHERE status = 'ACTIVE' ";
            } elseif ($filter === 'RESOLVED') {
                $sql .= "WHERE status = 'RESOLVED' ";
            }
            $sql .= "ORDER BY id DESC LIMIT :limit";

            $stmt = $pdo->prepare($sql);
            $stmt->bindValue(':limit', $limit, PDO::PARAM_INT);
            $stmt->execute();
            $logs = $stmt->fetchAll();

            echo json_encode([
                'status' => 'success',
                'total' => count($logs),
                'logs' => $logs
            ]);
        } catch (Exception $e) {
            echo json_encode(['status' => 'error', 'message' => $e->getMessage()]);
        }
        break;

    case 'network-details':
        echo json_encode([
            'status' => 'success',
            'hostname' => gethostname(),
            'configured' => $CONFIG['network_profile'],
            'adapters' => [
                [
                    'name' => 'Industrial Ethernet Adapter',
                    'ip' => $CONFIG['network_profile']['ip'],
                    'subnet_mask' => $CONFIG['network_profile']['subnet_mask'],
                    'gateway' => $CONFIG['network_profile']['gateway']
                ]
            ]
        ]);
        break;

    default:
        echo json_encode(['status' => 'error', 'message' => 'Unknown API action']);
        break;
}
