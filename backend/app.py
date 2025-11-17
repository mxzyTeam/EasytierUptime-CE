#!/usr/bin/env python3
"""
EasyTier Uptime CE - Python Flask API Server
替换原有的PHP后端，提供相同的API接口功能
"""

import os
import pymysql
from datetime import datetime
from flask import Flask, request, jsonify
from flask_cors import CORS
from typing import Dict, List, Any, Optional
import json

class DBConfig:
    """数据库配置类"""
    def __init__(self):
        self.host = os.getenv('DB_HOST', 'localhost')
        self.user = os.getenv('DB_USER', 'EasyTier')
        self.password = os.getenv('DB_PASSWORD', 'yTzEWfHKrpfBSxDr')
        self.database = os.getenv('DB_DATABASE', 'easytier')
        self.charset = os.getenv('DB_CHARSET', 'utf8mb4')
        self.port = int(os.getenv('DB_PORT', '3306'))

class NodeAPI:
    """节点API业务逻辑类"""
    
    def __init__(self, db_config: DBConfig):
        self.db_config = db_config
    
    def get_db_connection(self):
        """获取数据库连接"""
        return pymysql.connect(**self.db_config.__dict__)
    
    def safe_value(self, value, default=None):
        """安全处理值"""
        if value is None or value == '' or (isinstance(value, float) and value != value):
            return default
        return value
    
    def safe_text_value(self, value):
        """安全处理文本值"""
        if value is None or value == '' or (isinstance(value, (list, dict)) and not value):
            return '[]'
        if isinstance(value, (list, dict)):
            return json.dumps(value, ensure_ascii=False)
        return str(value)
    
    def parse_tags(self, tags_str: str) -> List[str]:
        """解析标签字符串"""
        if not tags_str or tags_str == '[]':
            return []
        
        try:
            # 尝试解析JSON格式
            if tags_str.startswith('[') and tags_str.endswith(']'):
                return json.loads(tags_str)
            # 处理非标准格式
            tags_str = tags_str.strip()
            if tags_str.startswith('["') and tags_str.endswith('"]'):
                tags_str = tags_str[2:-2]  # 移除开头和结尾的["和"]
                return [tag.strip() for tag in tags_str.split('","') if tag.strip()]
            # 其他格式直接按逗号分割
            return [tag.strip() for tag in tags_str.split(',') if tag.strip()]
        except:
            return []
    
    def calculate_load_level(self, usage_percentage: float, is_active: bool) -> Dict[str, Any]:
        """计算负载等级"""
        if not is_active:
            return {"load_level": "none", "load_text": "--"}
        
        usage = self.safe_value(usage_percentage, 0)
        
        if usage < 30:
            return {"load_level": "low", "load_text": f"低负载({usage:.1f}%)"}
        elif usage < 70:
            return {"load_level": "medium", "load_text": f"中负载({usage:.1f}%)"}
        else:
            return {"load_level": "high", "load_text": f"高负载({usage:.1f}%)"}
    
    def get_monitor_tables(self) -> List[str]:
        """获取监控分表列表"""
        conn = self.get_db_connection()
        try:
            with conn.cursor() as cursor:
                cursor.execute("SELECT table_name FROM monitor_tables ORDER BY create_time DESC")
                tables = [row[0] for row in cursor.fetchall()]
                return tables
        finally:
            conn.close()
    
    def get_nodes_list(self, page: int = 1, per_page: int = 20, search: str = None) -> Dict[str, Any]:
        """获取节点列表（分页+搜索）"""
        offset = (page - 1) * per_page
        
        # 获取所有监控表
        tables = self.get_monitor_tables()
        if not tables:
            return {
                "success": True,
                "data": [],
                "pagination": {
                    "current_page": page,
                    "per_page": per_page,
                    "total": 0,
                    "total_pages": 0
                },
                "latest_update": datetime.now().strftime('%Y-%m-%d %H:%M:%S')
            }
        
        conn = self.get_db_connection()
        try:
            with conn.cursor() as cursor:
                # 构建UNION ALL查询
                union_queries = []
                all_params = []
                
                for table in tables:
                    query = f"""
                    SELECT 
                        server_id, name, is_active, usage_percentage, tags, host, port, 
                        protocol, description, created_at, current_connections, max_connections, updated_at
                    FROM `{table}` 
                    WHERE 1=1
                    """
                    
                    table_params = []
                    if search:
                        query += """
                        AND (
                            name LIKE %s OR 
                            tags LIKE %s OR 
                            host LIKE %s OR 
                            description LIKE %s OR
                            server_id = %s
                        )
                        """
                        table_params.extend([f"%{search}%"] * 4 + [search])
                    
                    union_queries.append(query)
                    all_params.extend(table_params)
                
                # 完整的UNION ALL查询
                full_query = """
                SELECT t.*, @prev_sid := t.server_id
                FROM (
                    {union_query}
                ) t
                WHERE t.server_id != @prev_sid OR @prev_sid IS NULL
                ORDER BY t.updated_at DESC
                LIMIT %s OFFSET %s
                """.format(union_query=" UNION ALL ".join(union_queries))
                
                all_params.extend([per_page, offset])
                
                cursor.execute("SET @prev_sid := NULL")
                cursor.execute(full_query, all_params)
                
                nodes = []
                for row in cursor.fetchall():
                    node = {
                        "server_id": str(row[0]),
                        "name": row[1],
                        "is_active": bool(row[2]),
                        "usage_percentage": float(row[3]) if row[3] else 0.0,
                        "tags": self.parse_tags(row[4]),
                        "host": row[5],
                        "port": row[6],
                        "protocol": row[7],
                        "description": row[8],
                        "created_at": row[9].strftime('%Y-%m-%d %H:%M') if row[9] else '',
                        "current_connections": int(row[10]) if row[10] else 0,
                        "max_connections": int(row[11]) if row[11] else 0,
                        "updated_at": row[12].strftime('%Y-%m-%d %H:%M:%S') if row[12] else '',
                        "@prev_sid := t.server_id": str(row[0])
                    }
                    
                    # 计算负载等级
                    load_info = self.calculate_load_level(node["usage_percentage"], node["is_active"])
                    node.update(load_info)
                    
                    nodes.append(node)
                
                # 获取总数
                count_query = """
                SELECT COUNT(DISTINCT server_id) 
                FROM (
                    {union_query}
                ) t
                """.format(union_query=" UNION ALL ".join(union_queries))
                
                count_params = []
                if search:
                    # 为每个表添加搜索参数
                    for table in tables:
                        count_params.extend([f"%{search}%"] * 4 + [search])
                
                cursor.execute(count_query, count_params)
                total_count = cursor.fetchone()[0]
                
                return {
                    "success": True,
                    "data": nodes,
                    "pagination": {
                        "current_page": page,
                        "per_page": per_page,
                        "total": total_count,
                        "total_pages": (total_count + per_page - 1) // per_page
                    },
                    "latest_update": datetime.now().strftime('%Y-%m-%d %H:%M:%S')
                }
                
        finally:
            conn.close()
    
    def get_node_detail(self, node_id: int) -> Dict[str, Any]:
        """获取节点详细信息"""
        tables = self.get_monitor_tables()
        if not tables:
            return {"success": False, "error": "No monitor tables found"}
        
        conn = self.get_db_connection()
        try:
            with conn.cursor() as cursor:
                # 使用字段名而不是位置索引，避免表结构不一致的问题
                common_fields = [
                    "server_id", "name", "is_active", "usage_percentage", "tags", 
                    "host", "port", "protocol", "description", "created_at",
                    "current_connections", "max_connections", "updated_at",
                    "health_record_total_counter_ring", "health_record_healthy_counter_ring",
                    "health_percentage_24h", "last_check_time"
                ]
                
                # 尝试在每个表中查找节点，按更新时间倒序
                for table in tables:
                    fields_str = ", ".join(common_fields)
                    query = f"""
                    SELECT {fields_str} FROM `{table}` 
                    WHERE server_id = %s 
                    ORDER BY updated_at DESC 
                    LIMIT 1
                    """
                    
                    cursor.execute(query, [node_id])
                    row = cursor.fetchone()
                    
                    if row:
                        # 构建节点详情响应 - 使用字段名映射
                        node_detail = {}
                        for i, field in enumerate(common_fields):
                            value = row[i]
                            
                            if field == "server_id":
                                node_detail["id"] = str(value) if value else ""
                                node_detail["server_id"] = str(value) if value else ""
                            elif field == "is_active":
                                node_detail[field] = bool(value) if value is not None else False
                            elif field in ["usage_percentage", "health_percentage_24h"]:
                                node_detail[field] = float(value) if value else 0.0
                            elif field in ["current_connections", "max_connections"]:
                                node_detail[field] = int(value) if value else 0
                            elif field in ["tags", "health_record_total_counter_ring", "health_record_healthy_counter_ring"]:
                                node_detail[field] = self.parse_tags(value)
                            elif field in ["created_at", "updated_at", "last_check_time"]:
                                if hasattr(value, 'strftime'):
                                    node_detail[field] = value.strftime('%Y-%m-%d %H:%M:%S')
                                else:
                                    node_detail[field] = str(value) if value else ""
                            else:
                                node_detail[field] = value if value is not None else ""
                        
                        # 添加缺失的字段默认值
                        node_detail.update({
                            "version": "1.0",
                            "is_approved": True,
                            "allow_relay": True,
                            "current_health_status": "healthy",
                            "last_response_time": 0,
                            "ring_granularity": 900
                        })
                        
                        return {
                            "success": True,
                            "data": node_detail,
                            "latest_update": datetime.now().strftime('%Y-%m-%d %H:%M:%S')
                        }
                
                # 如果所有表都没有找到节点
                return {"success": False, "error": "Node not found"}
                
        finally:
            conn.close()

# 创建Flask应用
app = Flask(__name__, static_folder='../frontend', static_url_path='')
CORS(app)  # 启用CORS支持

# 初始化配置和API类
db_config = DBConfig()
node_api = NodeAPI(db_config)

@app.route('/api/nodes', methods=['GET'])
def nodes_list():
    """节点列表API"""
    try:
        page = int(request.args.get('page', 1))
        per_page = min(int(request.args.get('per_page', 20)), 100)  # 限制最大100
        search = request.args.get('search', None)
        
        result = node_api.get_nodes_list(page, per_page, search)
        return jsonify(result)
    except Exception as e:
        return jsonify({
            "success": False,
            "error": str(e)
        }), 500

@app.route('/api/node', methods=['GET'])
def node_detail():
    """节点详情API"""
    try:
        node_id = request.args.get('id')
        if not node_id:
            return jsonify({
                "success": False,
                "error": "Missing node id parameter"
            }), 400
        
        result = node_api.get_node_detail(int(node_id))
        return jsonify(result)
    except Exception as e:
        return jsonify({
            "success": False,
            "error": str(e)
        }), 500

@app.route('/api/health', methods=['GET'])
def health_check():
    """健康检查API"""
    return jsonify({
        "success": True,
        "status": "healthy",
        "timestamp": datetime.now().strftime('%Y-%m-%d %H:%M:%S')
    })

@app.route('/', methods=['GET'])
def index():
    """根路径重定向到前端页面"""
    return app.send_static_file('index.html')

if __name__ == '__main__':
    # 开发环境配置
    app.config['JSON_AS_ASCII'] = False  # 支持中文
    app.run(
        host='0.0.0.0',
        port=5000,
        debug=True
    )