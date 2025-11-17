#!/usr/bin/env python3
"""
EasyTier 项目信息脚本
显示项目结构、依赖、配置和运行状态信息
"""

import os
import sys
import json
import pymysql
import requests
from datetime import datetime
import subprocess

def get_project_structure():
    """获取项目目录结构"""
    structure = {}
    
    def scan_directory(path, level=0, max_level=3):
        if level > max_level:
            return
        
        items = []
        try:
            for item in os.listdir(path):
                item_path = os.path.join(path, item)
                
                if os.path.isfile(item_path):
                    size = os.path.getsize(item_path)
                    items.append({
                        'name': item,
                        'type': 'file',
                        'size': size,
                        'size_str': f"{size/1024:.1f}KB" if size < 1024*1024 else f"{size/1024/1024:.1f}MB"
                    })
                elif os.path.isdir(item_path):
                    items.append({
                        'name': item,
                        'type': 'directory',
                        'items': scan_directory(item_path, level + 1, max_level)
                    })
        except PermissionError:
            pass
        
        return items
    
    structure = scan_directory('.')
    return structure

def get_dependencies():
    """获取项目依赖信息"""
    dependencies = {}
    
    # 检查requirements.txt
    if os.path.exists('requirements.txt'):
        with open('requirements.txt', 'r', encoding='utf-8') as f:
            dependencies['requirements'] = [line.strip() for line in f if line.strip() and not line.startswith('#')]
    
    # 检查已安装的Python包
    try:
        result = subprocess.run([sys.executable, '-m', 'pip', 'list', '--format=json'], 
                              capture_output=True, text=True)
        if result.returncode == 0:
            installed_packages = json.loads(result.stdout)
            dependencies['installed'] = {pkg['name']: pkg['version'] for pkg in installed_packages}
    except:
        pass
    
    return dependencies

def get_config_info():
    """获取配置信息"""
    config = {}
    
    # 数据库配置
    config['database'] = {
        'host': 'localhost',
        'user': 'root',
        'database': 'uptime',
        'charset': 'utf8mb4'
    }
    
    # 检查.env文件
    if os.path.exists('.env'):
        with open('.env', 'r', encoding='utf-8') as f:
            env_content = f.read()
        config['env_file'] = '存在'
    else:
        config['env_file'] = '不存在'
    
    # 外部API配置
    config['external_apis'] = [
        'https://uptime.easytier.cn/api/nodes',
        'https://uptime.lisfox.top/api/nodes'
    ]
    
    return config

def get_runtime_status():
    """获取运行时状态"""
    status = {}
    
    # Web服务器状态
    try:
        response = requests.get('http://127.0.0.1:5000/api/nodes', timeout=5)
        status['web_server'] = {
            'status': '运行中' if response.status_code == 200 else '异常',
            'status_code': response.status_code,
            'response_time': response.elapsed.total_seconds() * 1000
        }
    except:
        status['web_server'] = {'status': '未运行', 'status_code': None}
    
    # 数据库状态
    try:
        db_config = get_config_info()['database']
        connection = pymysql.connect(**db_config)
        cursor = connection.cursor()
        
        # 获取表信息
        cursor.execute("SHOW TABLES LIKE 'monitor_%'")
        monitor_tables = cursor.fetchall()
        
        if monitor_tables:
            latest_table = monitor_tables[-1][0]
            cursor.execute(f"SELECT COUNT(*), MAX(created_at) FROM {latest_table}")
            count, latest_time = cursor.fetchone()
            
            status['database'] = {
                'status': '连接正常',
                'monitor_tables': len(monitor_tables),
                'latest_table': latest_table,
                'data_count': count,
                'latest_update': latest_time.strftime('%Y-%m-%d %H:%M:%S') if latest_time else '无数据'
            }
        else:
            status['database'] = {'status': '连接正常', 'monitor_tables': 0}
        
        connection.close()
    except Exception as e:
        status['database'] = {'status': f'连接失败: {e}'}
    
    # 外部API状态
    apis_status = []
    for api_url in get_config_info()['external_apis']:
        try:
            response = requests.get(api_url, timeout=10)
            apis_status.append({
                'url': api_url,
                'status': '正常' if response.status_code == 200 else f'异常({response.status_code})',
                'response_time': response.elapsed.total_seconds() * 1000
            })
        except Exception as e:
            apis_status.append({
                'url': api_url,
                'status': f'无法连接({str(e)})',
                'response_time': None
            })
    
    status['external_apis'] = apis_status
    
    return status

def print_project_info():
    """打印项目信息"""
    print("=" * 70)
    print("EasyTier 项目信息")
    print("=" * 70)
    
    # 基本信息
    print("\n[1] 项目基本信息:")
    print(f"  项目路径: {os.path.abspath('.')}")
    print(f"  系统时间: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    print(f"  Python版本: {sys.version}")
    
    # 依赖信息
    print("\n[2] 依赖信息:")
    deps = get_dependencies()
    
    if 'requirements' in deps:
        print("  requirements.txt:")
        for dep in deps['requirements'][:10]:  # 显示前10个
            print(f"    - {dep}")
        if len(deps['requirements']) > 10:
            print(f"    ... 还有 {len(deps['requirements']) - 10} 个依赖")
    
    if 'installed' in deps:
        print("  关键已安装包:")
        key_packages = ['flask', 'requests', 'pymysql', 'schedule']
        for pkg in key_packages:
            if pkg in deps['installed']:
                print(f"    - {pkg}: {deps['installed'][pkg]}")
    
    # 配置信息
    print("\n[3] 配置信息:")
    config = get_config_info()
    print("  数据库配置:")
    for key, value in config['database'].items():
        if key != 'password':  # 不显示密码
            print(f"    {key}: {value}")
    
    print(f"  环境文件: {config['env_file']}")
    print("  外部API:")
    for api in config['external_apis']:
        print(f"    - {api}")
    
    # 运行时状态
    print("\n[4] 运行时状态:")
    status = get_runtime_status()
    
    print("  Web服务器:")
    web_status = status['web_server']
    print(f"    状态: {web_status['status']}")
    if web_status.get('status_code'):
        print(f"    状态码: {web_status['status_code']}")
    if web_status.get('response_time'):
        print(f"    响应时间: {web_status['response_time']:.2f}ms")
    
    print("  数据库:")
    db_status = status['database']
    print(f"    状态: {db_status['status']}")
    if 'monitor_tables' in db_status:
        print(f"    监控表数量: {db_status['monitor_tables']}")
    if 'latest_table' in db_status:
        print(f"    最新表: {db_status['latest_table']}")
    if 'data_count' in db_status:
        print(f"    数据量: {db_status['data_count']} 条")
    if 'latest_update' in db_status:
        print(f"    最后更新: {db_status['latest_update']}")
    
    print("  外部API状态:")
    for api_status in status['external_apis']:
        print(f"    {api_status['url']}")
        print(f"      状态: {api_status['status']}")
        if api_status.get('response_time'):
            print(f"      响应时间: {api_status['response_time']:.2f}ms")
    
    # 项目结构（简要）
    print("\n[5] 项目结构 (简要):")
    structure = get_project_structure()
    
    def print_structure(items, indent=0):
        for item in items[:15]:  # 显示前15个
            prefix = "  " * indent
            if item['type'] == 'file':
                print(f"{prefix}📄 {item['name']} ({item['size_str']})")
            else:
                print(f"{prefix}📁 {item['name']}/")
                if 'items' in item:
                    print_structure(item['items'], indent + 1)
    
    print_structure(structure)
    
    # 可用脚本
    print("\n[6] 可用管理脚本:")
    scripts = [
        ('start_all.bat', '一键启动所有服务'),
        ('check_status.py', '项目状态检查'),
        ('backup_db.py', '数据库备份'),
        ('clean_logs.py', '日志清理'),
        ('project_info.py', '项目信息显示')
    ]
    
    for script, description in scripts:
        if os.path.exists(script):
            print(f"  ✓ {script} - {description}")
        else:
            print(f"  ✗ {script} - {description} (未找到)")
    
    print("\n" + "=" * 70)
    print("信息显示完成")
    print("=" * 70)

def main():
    """主函数"""
    try:
        print_project_info()
        
        # 提供操作建议
        print("\n操作建议:")
        print("1. 启动服务: 双击 start_all.bat")
        print("2. 检查状态: python check_status.py")
        print("3. 备份数据: python backup_db.py")
        print("4. 清理日志: python clean_logs.py")
        print("5. 访问系统: http://127.0.0.1:5000")
        
    except KeyboardInterrupt:
        print("\n\n用户中断操作")
    except Exception as e:
        print(f"\n错误: {e}")

if __name__ == "__main__":
    main()