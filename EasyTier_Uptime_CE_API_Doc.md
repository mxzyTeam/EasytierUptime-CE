# EasyTierUptime-CE API 文档

## 概述
- **协议**：HTTPS
- **主机**：uptime.mxzysoa.top
- **基础路径**：/api
- **内容类型**：json
- **时区**：UTC+8（所有时间字段按此格式返回）

---

## 端点列表

### 1. 获取单个节点详情
**URL**: `/api/node?id={node_id}`  
**方法**: GET  
**权限**: 无需认证  

#### 请求参数
| 参数名 | 类型   | 是否必填 | 描述                  |
|--------|--------|----------|-----------------------|
| node_id| integer| 是       | 目标节点的唯一标识符  |

#### 响应示例
```json
{
  "data": {
    "allow_relay": true,
    "created_at": "2025-11-22 18:40:00",
    "current_connections": 122,
    "current_health_status": "healthy",
    "description": "测试节点",
    "health_percentage_24h": 100,
    "health_record_healthy_counter_ring": [],
    "health_record_total_counter_ring": [],
    "host": "****",
    "id": "3",
    "is_active": true,
    "is_approved": true,
    "last_check_time": "2025-11-24 15:14:06",
    "last_response_time": 0,
    "max_connections": 100,
    "name": "测试节点",
    "port": 11010,
    "protocol": "tcp",
    "ring_granularity": 900,
    "server_id": "3",
    "tags": [],
    "updated_at": "2025-11-24 23:44:48",
    "usage_percentage": 122,
    "version": "1.0"
  },
  "latest_update": "2025-11-24 23:45:10",
  "success": true
}
```

### 2. 分页查询节点列表
**URL**: `/api/nodes`  
**方法**: GET  
**权限**: 无需认证  

#### 请求参数
| 参数名      | 类型   | 是否必填 | 默认值 | 描述                          |
|-------------|--------|----------|--------|-------------------------------|
| page        | int    | 否       | 1      | 当前页码                      |
| per_page    | int    | 否       | 20     | 每页数量（10-1000）            |
| search      | string | 否       | -      | 模糊搜索关键词（支持名称/标签/主机/IP/描述/ID）|

#### 响应示例
```json
{
  "data": [
    {
      "@prev_sid := t.server_id": "8",
      "created_at": "2025-11-23 00:50",
      "current_connections": 1,
      "description": "\u652f\u6301tcp\u548cwss\uff0ctcp://42.51.0.142:11011\uff0cwss://42.51.0.142:11010\uff0c10mbps\u5c0f\u6c34\u7ba1\uff0c\u652f\u6301\u4e2d\u8f6c\uff0c\u6682\u4e0d\u9650\u901f",
      "host": "****",
      "is_active": true,
      "load_level": "low",
      "load_text": "\u4f4e\u8d1f\u8f7d(0.0%)",
      "max_connections": 0,
      "name": "\u6d1b\u9633BGP-10M",
      "port": 11011,
      "protocol": "tcp",
      "server_id": "8",
      "tags": [],
      "updated_at": "2025-11-24 23:16:12",
      "usage_percentage": 0.0
    },
    {
      "@prev_sid := t.server_id": "7",
      "created_at": "2025-11-22 21:36",
      "current_connections": 0,
      "description": "",
      "host": "****",
      "is_active": true,
      "load_level": "low",
      "load_text": "\u4f4e\u8d1f\u8f7d(0.0%)",
      "max_connections": 0,
      "name": "test2",
      "port": 11010,
      "protocol": "tcp",
      "server_id": "7",
      "tags": [],
      "updated_at": "2025-11-24 23:16:12",
      "usage_percentage": 0.0
    },
    {
      "@prev_sid := t.server_id": "3",
      "created_at": "2025-11-22 18:40",
      "current_connections": 122,
      "description": "\u6d4b\u8bd5\u8282\u70b9",
      "host": "****",
      "is_active": true,
      "load_level": "high",
      "load_text": "\u9ad8\u8d1f\u8f7d(122.0%)",
      "max_connections": 100,
      "name": "\u6d4b\u8bd5\u8282\u70b9",
      "port": 11010,
      "protocol": "tcp",
      "server_id": "3",
      "tags": [],
      "updated_at": "2025-11-24 23:16:12",
      "usage_percentage": 122.0
    },
    {
      "@prev_sid := t.server_id": "9",
      "created_at": "2025-11-23 08:13",
      "current_connections": 23,
      "description": "",
      "host": "****",
      "is_active": true,
      "load_level": "low",
      "load_text": "\u4f4e\u8d1f\u8f7d(11.5%)",
      "max_connections": 200,
      "name": "\u6e56\u5317\u4e09\u7f51\u8def\u7ebf",
      "port": 22010,
      "protocol": "tcp",
      "server_id": "9",
      "tags": [],
      "updated_at": "2025-11-24 23:16:12",
      "usage_percentage": 11.5
    }
  ],
  "latest_update": "2025-11-24 23:16:36",
  "pagination": {
    "current_page": 1,
    "per_page": 20,
    "total": 4,
    "total_pages": 1
  },
  "success": true
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

