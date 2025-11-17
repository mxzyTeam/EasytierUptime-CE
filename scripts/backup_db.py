#!/usr/bin/env python3
"""
EasyTier 数据库备份脚本
自动备份监控数据到SQL文件
"""

import pymysql
import os
import sys
from datetime import datetime, timedelta
import subprocess

def get_db_config():
    """获取数据库配置"""
    return {
        'host': 'localhost',
        'user': 'root',
        'password': '123456',
        'database': 'uptime',
        'charset': 'utf8mb4'
    }

def create_backup_directory():
    """创建备份目录"""
    backup_dir = 'db_backups'
    if not os.path.exists(backup_dir):
        os.makedirs(backup_dir)
    return backup_dir

def backup_database():
    """备份整个数据库"""
    timestamp = datetime.now().strftime('%Y%m%d_%H%M%S')
    backup_dir = create_backup_directory()
    backup_file = os.path.join(backup_dir, f'uptime_backup_{timestamp}.sql')
    
    print(f"开始备份数据库到: {backup_file}")
    
    try:
        # 使用mysqldump命令备份
        db_config = get_db_config()
        
        cmd = [
            'mysqldump',
            f'--host={db_config["host"]}',
            f'--user={db_config["user"]}',
            f'--password={db_config["password"]}',
            '--single-transaction',
            '--routines',
            '--triggers',
            '--events',
            db_config['database']
        ]
        
        with open(backup_file, 'w', encoding='utf-8') as f:
            result = subprocess.run(cmd, stdout=f, stderr=subprocess.PIPE, text=True)
        
        if result.returncode == 0:
            file_size = os.path.getsize(backup_file) / 1024  # KB
            print(f"✓ 数据库备份成功: {backup_file} ({file_size:.1f}KB)")
            return backup_file
        else:
            print(f"✗ 数据库备份失败: {result.stderr}")
            return None
            
    except FileNotFoundError:
        print("✗ 未找到mysqldump命令，请确保MySQL客户端已安装")
        return None
    except Exception as e:
        print(f"✗ 备份过程中出错: {e}")
        return None

def backup_monitor_tables():
    """只备份监控相关的表"""
    timestamp = datetime.now().strftime('%Y%m%d_%H%M%S')
    backup_dir = create_backup_directory()
    backup_file = os.path.join(backup_dir, f'monitor_data_{timestamp}.sql')
    
    print(f"开始备份监控数据到: {backup_file}")
    
    try:
        db_config = get_db_config()
        connection = pymysql.connect(**db_config)
        cursor = connection.cursor()
        
        # 获取所有监控表
        cursor.execute("SHOW TABLES LIKE 'monitor_%'")
        monitor_tables = [table[0] for table in cursor.fetchall()]
        
        if not monitor_tables:
            print("⚠ 未找到监控数据表")
            connection.close()
            return None
        
        # 备份每个监控表
        with open(backup_file, 'w', encoding='utf-8') as f:
            f.write("-- EasyTier 监控数据备份\n")
            f.write(f"-- 备份时间: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}\n")
            f.write(f"-- 表数量: {len(monitor_tables)}\n\n")
            
            for table in monitor_tables:
                f.write(f"-- 备份表: {table}\n")
                
                # 获取表结构
                cursor.execute(f"SHOW CREATE TABLE {table}")
                create_table_sql = cursor.fetchone()[1]
                f.write(f"{create_table_sql};\n\n")
                
                # 备份数据
                cursor.execute(f"SELECT COUNT(*) FROM {table}")
                row_count = cursor.fetchone()[0]
                
                if row_count > 0:
                    f.write(f"-- 数据行数: {row_count}\n")
                    cursor.execute(f"SELECT * FROM {table}")
                    
                    for row in cursor.fetchall():
                        # 构建INSERT语句
                        values = []
                        for value in row:
                            if value is None:
                                values.append('NULL')
                            elif isinstance(value, str):
                                # 转义特殊字符
                                escaped_value = value.replace("'", "''").replace('"', '\\"')
                                values.append(f"'{escaped_value}'")
                            elif isinstance(value, datetime):
                                values.append(f"'{value.strftime('%Y-%m-%d %H:%M:%S')}'")
                            else:
                                values.append(str(value))
                        
                        insert_sql = f"INSERT INTO {table} VALUES ({', '.join(values)});\n"
                        f.write(insert_sql)
                    
                    f.write("\n")
        
        connection.close()
        
        file_size = os.path.getsize(backup_file) / 1024  # KB
        print(f"✓ 监控数据备份成功: {backup_file} ({file_size:.1f}KB, 表数: {len(monitor_tables)})")
        return backup_file
        
    except Exception as e:
        print(f"✗ 监控数据备份失败: {e}")
        return None

def cleanup_old_backups(days=30):
    """清理旧的备份文件"""
    backup_dir = 'db_backups'
    if not os.path.exists(backup_dir):
        return
    
    cutoff_time = datetime.now() - timedelta(days=days)
    deleted_count = 0
    
    for filename in os.listdir(backup_dir):
        filepath = os.path.join(backup_dir, filename)
        if os.path.isfile(filepath):
            mtime = datetime.fromtimestamp(os.path.getmtime(filepath))
            if mtime < cutoff_time:
                os.remove(filepath)
                deleted_count += 1
                print(f"删除旧备份: {filename}")
    
    if deleted_count > 0:
        print(f"✓ 清理完成，删除了 {deleted_count} 个旧备份文件")

def main():
    print("=" * 60)
    print("EasyTier 数据库备份工具")
    print("=" * 60)
    
    # 备份选项
    print("\n选择备份方式:")
    print("1. 完整数据库备份")
    print("2. 仅监控数据备份")
    print("3. 清理旧备份文件")
    
    choice = input("\n请输入选择 (1-3): ").strip()
    
    if choice == '1':
        backup_file = backup_database()
    elif choice == '2':
        backup_file = backup_monitor_tables()
    elif choice == '3':
        days = input("清理多少天前的备份文件? (默认30天): ").strip()
        try:
            days = int(days) if days else 30
        except ValueError:
            days = 30
        cleanup_old_backups(days)
    else:
        print("无效选择")
        return
    
    print("\n" + "=" * 60)
    print("备份完成")
    print("=" * 60)

if __name__ == "__main__":
    main()