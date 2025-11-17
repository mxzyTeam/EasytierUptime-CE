#!/usr/bin/env python3
"""
EasyTier Uptime CE - Python Flask服务器启动脚本
"""

import os
import sys
from app import app

def main():
    """主启动函数"""
    print("=" * 60)
    print("EasyTier Uptime CE - Python Flask服务器")
    print("=" * 60)
    
    # 检查环境变量
    required_env_vars = ['DB_HOST', 'DB_USER', 'DB_PASSWORD', 'DB_DATABASE']
    missing_vars = [var for var in required_env_vars if not os.getenv(var)]
    
    if missing_vars:
        print(f"警告: 以下环境变量未设置: {', '.join(missing_vars)}")
        print("将使用默认配置...")
    
    # 显示配置信息
    print("\n服务器配置:")
    print(f"  主机: {os.getenv('DB_HOST', 'localhost')}")
    print(f"  数据库: {os.getenv('DB_DATABASE', 'easytier')}")
    print(f"  用户: {os.getenv('DB_USER', 'EasyTier')}")
    print(f"  端口: {os.getenv('FLASK_PORT', '5000')}")
    
    # 检查静态文件
    static_files = ['index.html', 'css/index.css', 'js/index.js', 'js/node-detail.js']
    missing_files = [file for file in static_files if not os.path.exists(file)]
    
    if missing_files:
        print(f"\n警告: 以下静态文件缺失: {', '.join(missing_files)}")
    else:
        print("\n静态文件检查: 正常")
    
    print("\n启动服务器...")
    print("API端点:")
    print("  - GET /api/nodes - 节点列表（支持分页和搜索）")
    print("  - GET /api/node - 节点详情")
    print("  - GET /api/health - 健康检查")
    print("  - GET / - 前端页面")
    print("\n服务器运行中...")
    print("按 Ctrl+C 停止服务器")
    
    # 启动Flask应用
    app.run(
        host='0.0.0.0',
        port=int(os.getenv('FLASK_PORT', '5000')),
        debug=os.getenv('FLASK_DEBUG', 'False').lower() == 'true'
    )

if __name__ == '__main__':
    try:
        main()
    except KeyboardInterrupt:
        print("\n\n服务器已停止")
    except Exception as e:
        print(f"\n错误: {e}")
        sys.exit(1)