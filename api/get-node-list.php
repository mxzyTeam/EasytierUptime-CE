<?php
$host = 'localhost';
$dbname = 'easytier';
$username = 'EasyTier';
$password = 'yTzEWfHKrpfBSxDr';

function parseUnquotedJsonArray($value) {
    if (empty($value)) return [];
    $fixed = str_replace(["'", "=>"], ["\"", ":"], $value);
    $parsed = json_decode($fixed, true);
    return is_array($parsed) ? $parsed : [];
}

try {
    $pdo = new PDO("mysql:host=$host;dbname=$dbname;charset=utf8mb4", $username, $password);
    $pdo->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);

    // 1. 获取分表
    $stmt = $pdo->query("SELECT `table_name` FROM `monitor_tables` ORDER BY `update_time` DESC");
    $tables = $stmt->fetchAll(PDO::FETCH_COLUMN);
    if (empty($tables)) throw new Exception("未找到分表数据");

    // 2. 接收前端参数（分页+搜索关键词）
    $page = isset($_GET['page']) ? max(1, intval($_GET['page'])) : 1;
    $perPage = isset($_GET['per_page']) ? max(10, min(100, intval($_GET['per_page']))) : 20;
    $offset = ($page - 1) * $perPage;
    $searchKey = isset($_GET['search']) ? trim($_GET['search']) : ''; // 接收搜索关键词

    // 3. 拼接分表查询SQL（添加搜索条件）
    $unionParts = [];
    foreach ($tables as $table) {
        $sqlPart = "SELECT 
            server_id, 
            name, 
            is_active, 
            usage_percentage, 
            tags,
            DATE_FORMAT(created_at, '%Y-%m-%d %H:%i') AS created_at,
            current_connections,
            max_connections,
            updated_at
        FROM `{$table}`";
        
        // 关键：添加搜索条件（匹配名称、标签、server_id）
        if (!empty($searchKey)) {
            $sqlPart .= " WHERE 
                name LIKE :search OR 
                tags LIKE :search OR 
                server_id = :server_id"; // 支持ID精确匹配
        }
        
        $unionParts[] = $sqlPart;
    }

    // 4. 去重+分页SQL
    $sql = "SELECT * FROM (
        SELECT 
            t.*,
            @row_num := IF(@prev_sid = t.server_id, @row_num + 1, 1) AS rn,
            @prev_sid := t.server_id
        FROM (
            " . implode(" UNION ALL ", $unionParts) . "
            ORDER BY server_id, updated_at DESC
        ) AS t,
        (SELECT @row_num := 0, @prev_sid := NULL) AS init
    ) AS unique_nodes
    WHERE rn = 1
    ORDER BY server_id ASC
    LIMIT {$offset}, {$perPage}";

    // 5. 总数量查询（带搜索条件）
    $countSql = "SELECT COUNT(DISTINCT server_id) AS total FROM (" . implode(" UNION ALL ", $unionParts) . ") AS all_nodes";
    if (!empty($searchKey)) {
        $countSql .= " WHERE 
            name LIKE :search OR 
            tags LIKE :search OR 
            server_id = :server_id";
    }

    // 6. 绑定搜索参数并执行
    $stmt = $pdo->prepare($sql);
    $countStmt = $pdo->prepare($countSql);
    $searchParam = "%{$searchKey}%"; // 模糊匹配
    $serverIdParam = is_numeric($searchKey) ? intval($searchKey) : 0; // 若关键词是数字，作为server_id匹配

    if (!empty($searchKey)) {
        $stmt->bindParam(':search', $searchParam);
        $stmt->bindParam(':server_id', $serverIdParam, PDO::PARAM_INT);
        $countStmt->bindParam(':search', $searchParam);
        $countStmt->bindParam(':server_id', $serverIdParam, PDO::PARAM_INT);
    }

    $stmt->execute();
    $nodes = $stmt->fetchAll(PDO::FETCH_ASSOC);
    $countStmt->execute();
    $total = $countStmt->fetch(PDO::FETCH_ASSOC)['total'];
    $totalPages = ceil($total / $perPage);

    // 7. 查询最新更新时间（monitor_tables的update_time）
    $latestUpdateStmt = $pdo->query("SELECT MAX(update_time) AS latest_update FROM `monitor_tables`");
    $latestUpdate = $latestUpdateStmt->fetch(PDO::FETCH_ASSOC)['latest_update'];
    date_default_timezone_set('Asia/Shanghai');
    $formattedLatestUpdate = !empty($latestUpdate) ? date('Y-m-d H:i:s', strtotime($latestUpdate)) : '无记录';

    // 8. 数据处理
    $result = [];
    foreach ($nodes as $node) {
        $node['tags'] = parseUnquotedJsonArray($node['tags'] ?? '');
        $node['usage_percentage'] = (float)$node['usage_percentage'];
        $node['is_active'] = (int)$node['is_active'];
        $node['current_connections'] = (int)$node['current_connections'];
        $node['max_connections'] = (int)$node['max_connections'];

        if ($node['is_active'] == 0) {
            $node['load_level'] = 'none';
            $node['load_text'] = '--';
        } elseif ($node['usage_percentage'] > 100) {
            $node['load_level'] = 'high';
            $node['load_text'] = "高负载(" . number_format($node['usage_percentage'], 1) . "%)";
        } elseif ($node['usage_percentage'] > 50) {
            $node['load_level'] = 'medium';
            $node['load_text'] = "中负载(" . number_format($node['usage_percentage'], 1) . "%)";
        } else {
            $node['load_level'] = 'low';
            $node['load_text'] = "低负载(" . number_format($node['usage_percentage'], 1) . "%)";
        }

        unset($node['rn']);
        $result[] = $node;
    }

    // 9. 返回结果
    header("Content-Type: application/json; charset=utf-8");
    header("Access-Control-Allow-Origin: *");
    echo json_encode([
        'success' => true,
        'data' => $result,
        'pagination' => [
            'total' => $total,
            'page' => $page,
            'per_page' => $perPage,
            'total_pages' => $totalPages
        ],
        'latest_update' => $formattedLatestUpdate
    ]);

} catch(PDOException $e) {
    header("Content-Type: application/json; charset=utf-8");
    echo json_encode(['success' => false, 'error' => $e->getMessage()]);
} catch(Exception $e) {
    header("Content-Type: application/json; charset=utf-8");
    echo json_encode(['success' => false, 'error' => $e->getMessage()]);
}

$pdo = null;
?>