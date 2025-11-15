<?php
$host = 'localhost';
$dbname = 'easytier';
$username = 'EasyTier';
$password = 'yTzEWfHKrpfBSxDr';

try {
    // 1. 连接数据库
    $pdo = new PDO("mysql:host=$host;dbname=$dbname;charset=utf8mb4", $username, $password);
    $pdo->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);

    // 新增：查询 monitor_tables 表中最新的 update_time
    $latestUpdateStmt = $pdo->query("SELECT MAX(update_time) AS latest_update FROM `monitor_tables`");
    $latestUpdate = $latestUpdateStmt->fetch(PDO::FETCH_ASSOC)['latest_update'];
    date_default_timezone_set('Asia/Shanghai');
    $formattedLatestUpdate = !empty($latestUpdate) ? date('Y-m-d H:i:s', strtotime($latestUpdate)) : '无记录';

    // 2. 从元信息表获取所有分表（按最后更新时间倒序，优先查最新的表）
    $stmt = $pdo->query("
        SELECT `table_name` 
        FROM `monitor_tables` 
        ORDER BY `update_time` DESC -- 最新更新的表排在前面
    ");
    $tables = $stmt->fetchAll(PDO::FETCH_COLUMN); // 结果如 ['monitor_202511', 'monitor_202508']

    if (empty($tables)) {
        throw new Exception("未找到任何分表");
    }

    // 3. 获取节点 ID
    $nodeId = isset($_GET['id']) ? intval($_GET['id']) : 0;
    if ($nodeId <= 0) {
        throw new Exception("节点 ID 无效");
    }

    // 4. 动态拼接 SQL（合并所有分表，按记录更新时间倒序）
    $unionParts = [];
    foreach ($tables as $table) {
        $unionParts[] = "SELECT * FROM `{$table}` WHERE server_id = :nodeId";
    }
    $sql = "SELECT * FROM (
        " . implode(" UNION ALL ", $unionParts) . "
    ) AS all_nodes 
    ORDER BY updated_at DESC 
    LIMIT 1";

    // 5. 执行查询（后续数据处理和之前一致）
    $stmt = $pdo->prepare($sql);
    $stmt->bindParam(':nodeId', $nodeId, PDO::PARAM_INT);
    $stmt->execute();
    $node = $stmt->fetch(PDO::FETCH_ASSOC);

    if (!$node) {
        throw new Exception("未找到该节点");
    }

    // 6. 数据处理（和之前相同，省略）
    // 解析标签（适配无引号JSON数组）
    function parseUnquotedJsonArray($value) {
        if (empty($value)) return [];
        // 处理常见的非标准JSON格式（单引号、=>、无引号key）
        $fixed = str_replace([
            "'",          // 单引号转双引号
            "=>",         // => 转 :
            "[ ",         // [ 后加引号（处理无引号key）
            ", ",         // , 后加引号
            " ]"          // ] 前加引号
        ], [
            '"', 
            ':', 
            '["', 
            '","', 
            '"]'
        ], $value);
        // 移除数组前后的引号（如果有）
        $fixed = trim($fixed, '"');
        // 解析JSON
        $parsed = json_decode($fixed, true);
        // 若解析失败，尝试直接提取字符串（兼容纯文本格式）
        if (!is_array($parsed)) {
            preg_match_all('/["\']([^"\']+)["\']/', $value, $matches);
            $parsed = $matches[1] ?? [];
        }
        return $parsed;
    }
    $node['tags'] = parseUnquotedJsonArray($node['tags'] ?? '');

    // 解析健康检查数组（用正则提取数字，兼容各种格式）
    function extractNumberArray($text) {
        if (empty($text)) return [];
        // 匹配所有数字（包括整数和小数）
        preg_match_all('/\d+(\.\d+)?/', $text, $matches);
        // 转为浮点数数组
        return array_map(function($num) {
            return (float)$num;
        }, $matches[0] ?? []);
    }
    // 总检查次数数组
    $node['health_record_total_counter_ring'] = extractNumberArray($node['health_record_total_counter_ring'] ?? '');
    // 成功检查次数数组
    $node['health_record_healthy_counter_ring'] = extractNumberArray($node['health_record_healthy_counter_ring'] ?? '');
    
    // 时间格式化
    $node['created_at'] = date('Y-m-d H:i:s', strtotime($node['created_at'] ?? ''));
    $node['updated_at'] = date('Y-m-d H:i:s', strtotime($node['updated_at'] ?? ''));
    $node['last_check_time'] = date('Y-m-d H:i:s', strtotime($node['last_check_time'] ?? ''));

    // 数字字段强制转换（容错）
    $node['usage_percentage'] = (float)($node['usage_percentage'] ?? 0);
    $node['health_percentage_24h'] = (float)($node['health_percentage_24h'] ?? 0);
    $node['last_response_time'] = (int)($node['last_response_time'] ?? 0);
    $node['current_connections'] = (int)($node['current_connections'] ?? 0);
    $node['max_connections'] = (int)($node['max_connections'] ?? 0);
    $node['ring_granularity'] = (int)($node['ring_granularity'] ?? 900); // 默认15分钟

    // 主机地址/端口（直接使用数据库字段）
    $node['host'] = $node['host'] ?? '';
    $node['port'] = $node['port'] ?? '';

    // 7. 返回结果
    header("Content-Type: application/json; charset=utf-8");
    header("Access-Control-Allow-Origin: *");
    echo json_encode([
        'success' => true,
        'data' => $node,
        'latest_update' => $formattedLatestUpdate
    ]);

} catch(PDOException $e) {
    header("Content-Type: application/json; charset=utf-8");
    echo json_encode([
        'success' => false,
        'error' => $e->getMessage(),
        'latest_update' => $formattedLatestUpdate
    ]);
} catch(Exception $e) {
    header("Content-Type: application/json; charset=utf-8");
    echo json_encode([
        'success' => false,
        'error' => $e->getMessage(),
        'latest_update' => $formattedLatestUpdate
    ]);
}

$pdo = null;
?>