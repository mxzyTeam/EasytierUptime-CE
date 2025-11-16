import requests
import pymysql
from datetime import datetime
import time
import schedule
from requests.exceptions import RequestException

# 数据库配置
DB_CONFIG = {
    'host': 'localhost',
    'user': 'EasyTier',
    'password': 'yTzEWfHKrpfBSxDr',
    'db': 'easytier',
    'charset': 'utf8mb4'
}

# 目标接口
API_URL = 'https://uptime.lisfox.top/api/nodes?page=1&per_page=1000'
# API_URL = 'http://et.a2942.top:5902/1.json'
# 延长超时时间到120秒
TIMEOUT = 120
# 重试次数
RETRY_TIMES = 3

def get_current_table_name():
    """获取当前日期的分表名（格式：monitor_YYYYMMDD）"""
    return f"monitor_{datetime.now().strftime('%Y%m%d')}"

def create_table_if_not_exists(table_name):
    """创建分表（如果不存在），修复 TIMESTAMP 字段数量限制问题"""
    conn = pymysql.connect(**DB_CONFIG)
    try:
        with conn.cursor() as cursor:
            # 检查表是否存在
            cursor.execute(f"SHOW TABLES LIKE '{table_name}'")
            if not cursor.fetchone():
                # 修复点：
                # 1. 只保留 updated_at 为 TIMESTAMP 并启用 ON UPDATE CURRENT_TIMESTAMP
                # 2. last_check_time、created_at 改为 DATETIME 类型（不设数据库默认值，由代码控制）
                # 3. TEXT 字段保持 NOT NULL（由代码确保非空）
                create_sql = f"""
                CREATE TABLE `{table_name}` (
                    `id` int(11) NOT NULL AUTO_INCREMENT,
                    `server_id` int(11) NOT NULL,
                    `name` varchar(100) NOT NULL DEFAULT '',
                    `host` varchar(50) DEFAULT NULL,
                    `port` int(11) DEFAULT 0,
                    `protocol` varchar(20) NOT NULL DEFAULT 'tcp',
                    `version` varchar(20) NOT NULL DEFAULT '',
                    `max_connections` int(11) NOT NULL DEFAULT 0,
                    `current_connections` int(11) NOT NULL DEFAULT 0,
                    `is_active` tinyint(1) NOT NULL DEFAULT 0,
                    `is_approved` tinyint(1) NOT NULL DEFAULT 0,
                    `allow_relay` tinyint(1) NOT NULL DEFAULT 0,
                    `usage_percentage` decimal(10,6) NOT NULL DEFAULT 0.000000,
                    `current_health_status` varchar(20) NOT NULL DEFAULT 'unknown',
                    `last_check_time` DATETIME NOT NULL,  # 改为 DATETIME，无数据库默认值
                    `last_response_time` int(11) NOT NULL DEFAULT 0,
                    `health_percentage_24h` decimal(5,2) NOT NULL DEFAULT 0.00,
                    `health_record_total_counter_ring` text NOT NULL,
                    `health_record_healthy_counter_ring` text NOT NULL,
                    `tags` text NOT NULL,
                    `created_at` DATETIME NOT NULL,  # 改为 DATETIME，无数据库默认值
                    `updated_at` TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,  # 唯一 TIMESTAMP 字段
                    PRIMARY KEY (`id`),
                    KEY `idx_server_id` (`server_id`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
                """
                cursor.execute(create_sql)
                conn.commit()
                print(f"创建新表：{table_name}")

                # 添加到元信息表
                now = datetime.now().strftime('%Y-%m-%d %H:%M:%S')
                cursor.execute("""
                    INSERT INTO `monitor_tables` (`table_name`, `create_time`, `update_time`)
                    VALUES (%s, %s, %s)
                """, (table_name, now, now))
                conn.commit()
                print(f"元信息表添加记录：{table_name}")
    finally:
        conn.close()

def update_monitor_tables(table_name):
    """更新元信息表的 update_time"""
    conn = pymysql.connect(** DB_CONFIG)
    try:
        with conn.cursor() as cursor:
            now = datetime.now().strftime('%Y-%m-%d %H:%M:%S')
            cursor.execute("""
                UPDATE `monitor_tables`
                SET `update_time` = %s
                WHERE `table_name` = %s
            """, (now, table_name))
            conn.commit()
    finally:
        conn.close()

def fetch_api_data():
    """获取接口数据，带重试机制"""
    for attempt in range(RETRY_TIMES):
        try:
            response = requests.get(API_URL, timeout=TIMEOUT)
            response.raise_for_status()
            return response.json()
        except RequestException as e:
            if attempt < RETRY_TIMES - 1:
                print(f"请求失败，正在重试（第{attempt+2}次）：{str(e)}")
                time.sleep(5)
            else:
                print(f"请求多次失败：{str(e)}")
                return None

def safe_value(value, default=0):
    """安全处理值：None/空值返回默认值，否则返回原值"""
    if value is None or value == '' or (isinstance(value, float) and value != value):
        return default
    return value

def safe_text_value(value):
    """专门处理 TEXT 字段：None/空值返回 '[]'，否则返回字符串格式"""
    if value is None or value == '' or (isinstance(value, list) and len(value) == 0):
        return '[]'
    # 如果是列表/字典，转为 JSON 字符串（保持格式）
    if isinstance(value, (list, dict)):
        import json
        return json.dumps(value, ensure_ascii=False)
    # 其他类型直接转字符串
    return str(value)

def fetch_and_save_data():
    """爬取接口数据并保存到分表"""
    try:
        # 1. 获取接口数据
        data = fetch_api_data()
        if not data or not data.get('success'):
            print(f"接口返回错误或空数据：{data.get('error') if data else '未知错误'}")
            return

        nodes = data.get('data', {}).get('items', [])
        if not nodes:
            print("接口返回空数据")
            return

        # 2. 处理分表
        table_name = get_current_table_name()
        create_table_if_not_exists(table_name)
        update_monitor_tables(table_name)

        # 3. 保存数据到分表
        conn = pymysql.connect(**DB_CONFIG)
        try:
            with conn.cursor() as cursor:
                current_datetime = datetime.now().strftime('%Y-%m-%d %H:%M:%S')  # 统一默认时间
                for node in nodes:
                    # 时间字段处理（确保非空，无数据时用当前时间）
                    def parse_iso_time(iso_time):
                        if not iso_time or iso_time == '':
                            return current_datetime
                        try:
                            return datetime.fromisoformat(iso_time.replace('Z', '+00:00')).strftime('%Y-%m-%d %H:%M:%S')
                        except (ValueError, TypeError):
                            return current_datetime
                    
                    # TEXT 字段处理
                    total_ring = safe_text_value(node.get('health_record_total_counter_ring'))
                    healthy_ring = safe_text_value(node.get('health_record_healthy_counter_ring'))
                    tags = safe_text_value(node.get('tags'))

                    # 其他字段强制非空处理
                    server_id = safe_value(node.get('id'), 0)
                    name = safe_value(node.get('name'), '')
                    host = safe_value(node.get('host'), '')
                    port = safe_value(node.get('port'), 0)
                    protocol = safe_value(node.get('protocol'), 'tcp')
                    version = safe_value(node.get('version'), '')
                    max_connections = safe_value(node.get('max_connections'), 0)
                    current_connections = safe_value(node.get('current_connections'), 0)
                    is_active = int(safe_value(node.get('is_active'), False))
                    is_approved = int(safe_value(node.get('is_approved'), False))
                    allow_relay = int(safe_value(node.get('allow_relay'), False))
                    usage_percentage = safe_value(node.get('usage_percentage'), 0.0)
                    current_health_status = safe_value(node.get('current_health_status'), 'unknown')
                    last_check_time = parse_iso_time(node.get('last_check_time'))  # 代码层面确保非空
                    last_response_time = safe_value(node.get('last_response_time'), 0)
                    health_percentage_24h = safe_value(node.get('health_percentage_24h'), 0.0)

                    # 先删旧记录
                    cursor.execute(f"DELETE FROM `{table_name}` WHERE `server_id` = %s", (server_id,))

                    # 插入新数据（所有 NOT NULL 字段均由代码确保非空）
                    cursor.execute(f"""
                        INSERT INTO `{table_name}` (
                            server_id, name, host, port, protocol, version,
                            max_connections, current_connections, is_active, is_approved, allow_relay,
                            usage_percentage, current_health_status, last_check_time,
                            last_response_time, health_percentage_24h,
                            health_record_total_counter_ring, health_record_healthy_counter_ring,
                            tags, created_at, updated_at
                        ) VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s)
                    """, (
                        server_id, name, host, port, protocol, version,
                        max_connections, current_connections, is_active, is_approved, allow_relay,
                        usage_percentage, current_health_status, last_check_time,
                        last_response_time, health_percentage_24h,
                        total_ring, healthy_ring, tags,
                        parse_iso_time(node.get('created_at')),  # 代码层面确保非空
                        parse_iso_time(node.get('updated_at'))   # 兼容接口数据，无数据时用当前时间
                    ))
                conn.commit()
                print(f"成功更新 {len(nodes)} 条数据到 {table_name}")
        finally:
            conn.close()

    except Exception as e:
        print(f"执行失败：{str(e)}")

def job():
    """定时任务执行函数"""
    print(f"===== 开始执行任务：{datetime.now().strftime('%Y-%m-%d %H:%M:%S')} =====")
    fetch_and_save_data()
    print(f"===== 任务执行结束 =====")

if __name__ == "__main__":
    job()
    schedule.every(30).minutes.do(job)
    print("定时任务已启动，每30分钟执行一次...")
    while True:
        schedule.run_pending()
        time.sleep(1)