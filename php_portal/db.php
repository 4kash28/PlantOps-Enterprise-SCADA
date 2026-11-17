<?php
/**
 * PlantOps™ Enterprise SCADA - SQLite PDO Connector
 */

require_once __DIR__ . '/config.php';

function getDbConnection() {
    $dbPath = DB_PATH;
    if (!file_exists($dbPath)) {
        // Fallback to local db if not in standard directory
        $dbPath = __DIR__ . '/plantops_monitor.db';
    }

    try {
        $pdo = new PDO("sqlite:" . $dbPath);
        $pdo->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);
        $pdo->setAttribute(PDO::ATTR_DEFAULT_FETCH_MODE, PDO::FETCH_ASSOC);
        $pdo->exec("PRAGMA journal_mode = WAL;");
        return $pdo;
    } catch (PDOException $e) {
        return null;
    }
}
