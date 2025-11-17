import pymysql
import os
from datetime import datetime
from dotenv import load_dotenv

def create_monitor_tables():
    """创建监控相关的数据库表"""
    load_dotenv()
    
    config = {
        'host': os.getenv('DB_HOST'),
        'user': os.getenv('DB_USER'),
        'password': os.getenv('DB_PASSWORD'),
        'database': os.getenv('DB_DATABASE'),
        'charset': os.getenv('DB_CHARSET'),
        'port': int(os.getenv('DB_PORT', 3306))
    }
    
    try:
        connection = pymysql.connect(**config)
        print("✅ 数据库连接成功")
        
        with connection.cursor() as cursor:
            
            # 1. 创建monitor_tables表（管理表）
            print("1. 创建monitor_tables管理表...")
            cursor.execute("""
                CREATE TABLE IF NOT EXISTS monitor_tables (
                    id INT AUTO_INCREMENT PRIMARY KEY,
                    table_name VARCHAR(50) NOT NULL UNIQUE,
                    description VARCHAR(200),
                    create_time TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    is_active BOOLEAN DEFAULT TRUE
                )
            """)
            print("   ✅ monitor_tables表创建成功")
            
            # 2. 创建当前月份的监控数据表
            current_month = datetime.now().strftime("%Y%m")
            table_name = f"monitor_{current_month}"
            
            print(f"2. 创建监控数据表 {table_name}...")
            cursor.execute(f"""
                CREATE TABLE IF NOT EXISTS `{table_name}` (
                    id INT AUTO_INCREMENT PRIMARY KEY,
                    server_id INT NOT NULL,
                    name VARCHAR(100) NOT NULL,
                    is_active BOOLEAN DEFAULT TRUE,
                    usage_percentage DECIMAL(5,2) DEFAULT 0.0,
                    tags JSON,
                    host VARCHAR(100),
                    port INT,
                    protocol VARCHAR(20),
                    description TEXT,
                    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    current_connections INT DEFAULT 0,
                    max_connections INT DEFAULT 0,
                    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                    health_record_total_counter_ring JSON,
                    health_record_healthy_counter_ring JSON,
                    health_percentage_24h DECIMAL(5,2) DEFAULT 0.0,
                    last_check_time TIMESTAMP NULL,
                    INDEX idx_server_id (server_id),
                    INDEX idx_updated_at (updated_at),
                    INDEX idx_is_active (is_active)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
            """)
            print(f"   ✅ {table_name}表创建成功")
            
            # 3. 向monitor_tables表插入当前月份表信息
            print("3. 注册监控表到管理表...")
            cursor.execute("""
                INSERT IGNORE INTO monitor_tables (table_name, description) 
                VALUES (%s, %s)
            """, (table_name, f"{current_month[:4]}年{current_month[4:]}月监控数据"))
            print("   ✅ 监控表注册成功")
            
            # 4. 插入一些示例数据用于测试
            print("4. 插入示例数据...")
            cursor.execute(f"""
                INSERT IGNORE INTO `{table_name}` (
                    server_id, name, is_active, usage_percentage, tags, host, port, 
                    protocol, description, current_connections, max_connections,
                    health_record_total_counter_ring, health_record_healthy_counter_ring,
                    health_percentage_24h, last_check_time
                ) VALUES 
                (1, '主数据库服务器', TRUE, 75.5, '["数据库", "主节点"]', '192.168.1.100', 3306, 
                 'mysql', '主要业务数据库', 150, 200, '[100, 95, 98, 102]', '[95, 90, 95, 98]', 96.5, NOW()),
                (2, 'Redis缓存服务器', TRUE, 45.2, '["缓存", "内存"]', '192.168.1.101', 6379, 
                 'redis', '缓存服务节点', 80, 1000, '[100, 98, 99, 101]', '[98, 96, 98, 99]', 98.2, NOW()),
                (3, 'Web应用服务器', TRUE, 62.8, '["Web", "应用"]', '192.168.1.102', 80, 
                 'http', 'Web应用服务', 25, 50, '[100, 99, 100, 98]', '[99, 98, 99, 97]', 98.5, NOW()),
                (4, '备份数据库服务器', FALSE, 12.3, '["数据库", "备份"]', '192.168.1.103', 3307, 
                 'mysql', '备份数据库', 5, 200, '[100, 100, 100, 100]', '[100, 100, 100, 100]', 100.0, NOW())
            """)
            print("   ✅ 示例数据插入成功")
            
            # 5. 验证表创建和数据插入
            print("5. 验证表和数据...")
            
            # 检查monitor_tables表
            cursor.execute("SELECT COUNT(*) FROM monitor_tables")
            monitor_tables_count = cursor.fetchone()[0]
            print(f"   📊 monitor_tables表记录数: {monitor_tables_count}")
            
            # 检查监控数据表
            cursor.execute(f"SELECT COUNT(*) FROM `{table_name}`")
            monitor_data_count = cursor.fetchone()[0]
            print(f"   📊 {table_name}表记录数: {monitor_data_count}")
            
            # 显示表结构
            cursor.execute("SHOW TABLES")
            tables = [table[0] for table in cursor.fetchall()]
            print(f"   📋 数据库中的所有表: {tables}")
            
        connection.commit()
        connection.close()
        
        print("\n🎉 数据库表创建完成！")
        return True
        
    except pymysql.Error as e:
        print(f"❌ 表创建失败: {e}")
        return False

def test_api_connection():
    """测试API是否能正常访问数据库"""
    print("\n🔍 测试API数据库连接...")
    
    try:
        # 模拟API调用
        from app import db_config, NodeAPI
        
        node_api = NodeAPI(db_config)
        
        # 测试获取监控表列表
        tables = node_api.get_monitor_tables()
        print(f"✅ 获取监控表列表成功: {tables}")
        
        # 测试获取节点列表
        result = node_api.get_nodes_list(page=1, per_page=10)
        print(f"✅ 获取节点列表成功: 共{len(result.get('data', []))}条记录")
        
        return True
        
    except Exception as e:
        print(f"❌ API测试失败: {e}")
        return False

if __name__ == "__main__":
    print("开始创建EasyTier Uptime CE数据库表...\n")
    
    if create_monitor_tables():
        print("\n正在测试API连接...")
        if test_api_connection():
            print("\n🎊 所有数据库表设置完成！现在可以正常运行EasyTier Uptime CE了。")
        else:
            print("\n⚠️ API测试失败，但表已创建完成。")
    else:
        print("\n❌ 数据库表创建失败。")