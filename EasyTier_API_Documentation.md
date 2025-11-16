# EasyTier Node API 文档

## 概述
- **协议**：HTTP/HTTPS
- **主机**：localhost（生产环境请替换实际域名）
- **基础路径**：/api/v1（默认路径，实际部署可能不同）
- **内容类型**：application/json
- **时区**：UTC+8（所有时间字段按此格式返回）

---

## 端点列表

### 1. 获取单个节点详情
**URL**: `/api/v1/nodes?id={node_id}`  
**方法**: GET  
**权限**: 无需认证  

#### 请求参数
| 参数名 | 类型   | 是否必填 | 描述                  |
|--------|--------|----------|-----------------------|
| node_id| integer| 是       | 目标节点的唯一标识符  |

#### 响应示例
```json
{
  "success": true,
  "data": {
    "server_id": 1,
    "name": "Web_Server_01",
    "status": "active",
    "health_counters": {
      "total": [100, 95, 88],
      "healthy": [98, 94, 85]
    },
    "tags": ["production", "web"],
    "host": "192.168.1.10",
    "port": 6379,
    "protocol": "redis",
    "description": "主站Redis服务",
    "created_at": "2025-11-15 14:30:00",
    "updated_at": "2025-11-16 10:00:00",
    "last_check_time": "2025-11-16 12:45:00"
  },
  "meta": {
    "latest_table_update": "2025-11-16 12:50:00"
  }
}
```

### 2. 分页查询节点列表
**URL**: `/api/v1/nodes`  
**方法**: GET  
**权限**: 无需认证  

#### 请求参数
| 参数名      | 类型   | 是否必填 | 默认值 | 描述                          |
|-------------|--------|----------|--------|-------------------------------|
| page        | int    | 否       | 1      | 当前页码                      |
| per_page    | int    | 否       | 20     | 每页数量（10-100）            |
| search      | string | 否       | -      | 模糊搜索关键词（支持名称/标签/主机/IP/描述/ID）|

#### 响应示例
```json
{
  "success": true,
  "data": [
    {
      "server_id": 1,
      "name": "DB_Master",
      "status": "active",
      "health_percentage_24h": 99.2,
      "current_connections": 150,
      "max_connections": 500,
      "ring_granularity": 900
    },
    {
      "server_id": 2,
      "name": "Cache_Node_02",
      "status": "inactive",
      "health_percentage_24h": 95.5,
      "current_connections": 80,
      "max_connections": 200,
      "ring_granularity": 300
    }
  ],
  "meta": {
    "pagination": {
      "total": 25,
      "page": 1,
      "per_page": 20,
      "total_pages": 2
    },
    "latest_table_update": "2025-11-16 12:50:00"
  }
}
```

---

## 字段说明

### 公共字段
| 字段名               | 类型    | 描述                                                                 |
|----------------------|---------|----------------------------------------------------------------------|
| server_id            | integer | 节点唯一标识符                                                       |
| name                 | string  | 节点名称                                                             |
| status               | string  | 节点状态（active/inactive）                                          |
| health_counters      | object  | 健康检查计数器                                                       |
| tags                 | array   | 关联标签（JSON数组格式）                                             |
| host                 | string  | 主机地址                                                             |
| port                 | integer | 服务端口                                                             |
| protocol             | string  | 通信协议（redis/mysql等）                                            |
| description          | string  | 节点描述                                                             |
| created_at           | string  | 创建时间（格式：Y-m-d H:i:s）                                        |
| updated_at           | string  | 最后更新时间                                                         |
| last_check_time      | string  | 最近健康检查时间                                                     |

### 健康检查相关字段
| 字段名                   | 类型    | 描述                                                                 |
|--------------------------|---------|----------------------------------------------------------------------|
| health_record_total_counter_ring | array | 健康检查总次数时间序列                                               |
| health_record_healthy_counter_ring | array | 健康检查成功次数时间序列                                             |
| health_percentage_24h    | float   | 过去24小时平均健康率（%）                                            |

---

## 错误代码

| 状态码 | 错误类型               | 描述                                                                 |
|--------|------------------------|----------------------------------------------------------------------|
| 400    | Invalid Parameters     | 参数缺失或格式错误（如非数字字符出现在数值字段）                       |
| 404    | Node Not Found         | 请求的节点ID不存在                                                   |
| 500    | Internal Server Error  | 数据库连接失败、查询超时或内部程序错误                               |

---

## 实现细节
1. **分表处理**：自动合并`monitor_YYYYMM`格式的分表数据，按更新时间排序
2. **数据清洗**：
   - 自动修复非标准JSON格式的tags字段
   - 提取健康检查字段中的数字序列
   - 标准化时间格式为UTC+8
3. **安全措施**：
   - 防止SQL注入（使用预编译语句）
   - 输出过滤（XSS防护）
   - CORS支持（Access-Control-Allow-Origin: *）

建议配合以下工具使用：
- 测试工具：Postman / Insomnia
- 监控工具：Prometheus + Grafana（通过health_percentage_24h字段）
- 文档生成：Swagger/OpenAPI 规范转换

注：实际部署时请修改数据库配置并添加身份验证机制。
