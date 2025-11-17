#!/usr/bin/env python3
"""
EasyTier 项目状态检查脚本
检查Web服务器、数据库连接、外部API状态等
"""

import requests
import pymysql
import time
import os
import sys
from datetime import datetime

def check_web_server():
    """检查Flask Web服务器状态"""
    try:
        response = requests.get('http://127.0.0.1:5000/api/nodes', timeout=5)
        if response.status_code == 200:
            data = response.json()
            return True, f"✓ Web服务器正常 (节点数: {len(data)})"
        else:
            return False, f"✗ Web服务器异常 (状态码: {response.status_code})"
    except requests.exceptions.RequestException as e:
        return False, f"✗ Web服务器无法连接: {e}"

def check_database():
    """检查数据库连接"""
    try:
        # 从.env文件读取数据库配置
        db_config = {
            'host': 'localhost',
            'user': 'root',
            'password': '123456',
            'database': 'uptime',
            'charset': 'utf8mb4'
        }
        
        connection = pymysql.connect(**db_config)
        cursor = connection.cursor()
        
        # 检查表是否存在
        cursor.execute("SHOW TABLES LIKE 'monitor_%'")
        tables = cursor.fetchall()
        
        # 检查最新数据
        if tables:
            latest_table = tables[-1][0]
            cursor.execute(f"SELECT COUNT(*) FROM {latest_table}")
            count = cursor.fetchone()[0]
            
            cursor.execute(f"SELECT MAX(created_at) FROM {latest_table}")
            latest_time = cursor.fetchone()[0]
            
            connection.close()
            return True, f"✓ 数据库正常 (表数: {len(tables)}, 最新表数据: {count}条, 最后更新: {latest_time})"
        else:
            connection.close()
            return True, "✓ 数据库正常 (暂无监控数据)"
            
    except Exception as e:
        return False, f"✗ 数据库连接失败: {e}"

def check_external_apis():
    """检查外部API状态"""
    apis = [
        ('主API', 'https://uptime.easytier.cn/api/nodes'),
        ('备用API', 'https://uptime.lisfox.top/api/nodes')
    ]
    
    results = []
    for name, url in apis:
        try:
            start_time = time.time()
            response = requests.get(url, timeout=10)
            response_time = round((time.time() - start_time) * 1000, 2)
            
            if response.status_code == 200:
                results.append(f"✓ {name}: 正常 ({response_time}ms)")
            else:
                results.append(f"✗ {name}: 异常 (状态码: {response.status_code}, {response_time}ms)")
        except Exception as e:
            results.append(f"✗ {name}: 无法连接 ({str(e)})")
    
    return results

def check_log_files():
    """检查日志文件"""
    log_files = []
    for file in os.listdir('.'):
        if file.startswith('api_monitor_') and file.endswith('.log'):
            size = os.path.getsize(file)
            mtime = datetime.fromtimestamp(os.path.getmtime(file))
            log_files.append(f"{file} ({size/1024:.1f}KB, 修改时间: {mtime.strftime('%Y-%m-%d %H:%M')})")
    
    if log_files:
        return True, "✓ 日志文件正常: " + ", ".join(log_files)
    else:
        return False, "✗ 未找到日志文件"

def main():
    print("=" * 60)
    print("EasyTier 项目状态检查")
    print("=" * 60)
    
    # 检查Web服务器
    print("\n[1] Web服务器状态:")
    web_ok, web_msg = check_web_server()
    print(f"   {web_msg}")
    
    # 检查数据库
    print("\n[2] 数据库状态:")
    db_ok, db_msg = check_database()
    print(f"   {db_msg}")
    
    # 检查外部API
    print("\n[3] 外部API状态:")
    api_results = check_external_apis()
    for result in api_results:
        print(f"   {result}")
    
    # 检查日志文件
    print("\n[4] 日志文件状态:")
    log_ok, log_msg = check_log_files()
    print(f"   {log_msg}")
    
    # 总体状态
    print("\n" + "=" * 60)
    print("总体状态:")
    
    if web_ok and db_ok:
        print("✓ 系统运行正常")
    else:
        print("⚠ 系统存在异常，请检查上述问题")
    
    print("=" * 60)

if __name__ == "__main__":
    main()