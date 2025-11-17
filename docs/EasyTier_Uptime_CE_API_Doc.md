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
**URL**: `/api/node.php?id={node_id}`  
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
    "id": "7580",
    "server_id": "1",
    "name": "官方公共服务器-湖北浪浪云",
    "host": "public.easytier.cn",
    "port": "11010",
    "protocol": "tcp",
    "version": "2.4.5",
    "max_connections": 100,
    "current_connections": 151,
    "is_active": "1",
    "is_approved": "1",
    "allow_relay": "1",
    "usage_percentage": 151.651163,
    "current_health_status": "healthy",
    "last_check_time": "2025-11-16 06:04:29",
    "last_response_time": 39790,
    "health_percentage_24h": 99.99,
    "health_record_total_counter_ring": [54, 180, 180, 180, 190, 170, 180, 203, 157, 180, 181, 15, 200, 157, 177, 180, 178, 224, 133, 177, 180, 177, 207, 151, 178, 180, 178, 183, 174, 178, 227, 131, 178, 180, 178, 205, 153, 178, 180, 178, 182, 176, 178, 232, 126, 178, 180, 178, 225, 133, 177, 180, 177, 226, 131, 178, 180, 177, 227, 131, 177, 198, 159, 191, 166, 226, 131, 244, 113, 203, 154, 177, 204, 153, 240, 118, 199, 158, 177, 201, 156, 242, 115, 192, 165, 177, 180, 177, 220, 137, 178, 180, 177, 226, 131, 177],
    "health_record_healthy_counter_ring": [54, 180, 180, 180, 190, 170, 180, 203, 157, 180, 181, 13, 200, 157, 177, 180, 178, 224, 133, 177, 180, 177, 207, 151, 178, 180, 178, 183, 174, 178, 227, 131, 178, 180, 178, 205, 153, 178, 180, 178, 182, 176, 178, 232, 126, 178, 180, 178, 225, 133, 177, 180, 177, 226, 131, 178, 180, 177, 227, 131, 177, 198, 159, 191, 166, 226, 131, 244, 113, 203, 154, 177, 204, 153, 240, 118, 199, 158, 177, 201, 156, 242, 115, 192, 165, 177, 180, 177, 220, 137, 178, 180, 177, 226, 131, 177],
    "tags": [
      "国内",
      "官方"
    ],
    "description": "限速 50KB/s。",
    "created_at": "2025-08-18 05:55:40",
    "updated_at": "2025-11-16 14:04:31",
    "ring_granularity": 900
  },
  "latest_update": "2025-11-16 14:04:32"
}
```

### 2. 分页查询节点列表
**URL**: `/api/nodes.php`  
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
      "server_id": "1",
      "name": "官方公共服务器-湖北浪浪云",
      "is_active": 1,
      "usage_percentage": 151.651163,
      "tags": [
        "国内",
        "官方"
      ],
      "host": "public.easytier.cn",
      "port": "11010",
      "protocol": "tcp",
      "description": "限速 50KB/s。",
      "created_at": "2025-08-18 05:55",
      "current_connections": 151,
      "max_connections": 100,
      "updated_at": "2025-11-16 14:04:31",
      "@prev_sid := t.server_id": "1",
      "load_level": "high",
      "load_text": "高负载(151.7%)"
    },
    {
      "server_id": "3",
      "name": "江苏宿迁-京东云-01",
      "is_active": 1,
      "usage_percentage": 91.9,
      "tags": [
        "国内"
      ],
      "host": "turn.js.629957.xyz",
      "port": "11012",
      "protocol": "tcp",
      "description": "电信、联通、移动三线优化，封海外",
      "created_at": "2025-08-18 11:34",
      "current_connections": 91,
      "max_connections": 100,
      "updated_at": "2025-11-16 14:04:31",
      "@prev_sid := t.server_id": "3",
      "load_level": "medium",
      "load_text": "中负载(91.9%)"
    },
    {
      "server_id": "5",
      "name": "西南-成都电信",
      "is_active": 1,
      "usage_percentage": 16.95,
      "tags": [
        "国内"
      ],
      "host": "c.oee.icu",
      "port": "60006",
      "protocol": "tcp",
      "description": "成都电信-西南直连-理论100M_仅限用于学习、研究网络技术，请务必遵守相关法律法规，注重个人网络信息安全",
      "created_at": "2025-08-19 01:25",
      "current_connections": 16,
      "max_connections": 100,
      "updated_at": "2025-11-16 14:04:31",
      "@prev_sid := t.server_id": "5",
      "load_level": "low",
      "load_text": "低负载(17.0%)"
    },
    {
      "server_id": "6",
      "name": "上海-阿里云-冰镇豆浆1",
      "is_active": 0,
      "usage_percentage": 183.2,
      "tags": [
        "即将下线",
        "国内"
      ],
      "host": "et.sh.suhoan.cn",
      "port": "11010",
      "protocol": "tcp",
      "description": "",
      "created_at": "2025-08-19 10:28",
      "current_connections": 183,
      "max_connections": 100,
      "updated_at": "2025-11-16 14:04:31",
      "@prev_sid := t.server_id": "6",
      "load_level": "none",
      "load_text": "--"
    },
    {
      "server_id": "7",
      "name": "上海-阿里云-2",
      "is_active": 1,
      "usage_percentage": 15.3,
      "tags": [
        "国内"
      ],
      "host": "et2.fuis.me",
      "port": "11010",
      "protocol": "tcp",
      "description": "上海，阿里云，3M 小水管",
      "created_at": "2025-08-19 14:26",
      "current_connections": 15,
      "max_connections": 100,
      "updated_at": "2025-11-16 14:04:31",
      "@prev_sid := t.server_id": "7",
      "load_level": "low",
      "load_text": "低负载(15.3%)"
    },
    {
      "server_id": "11",
      "name": "浙江宁波-电信",
      "is_active": 1,
      "usage_percentage": 307,
      "tags": [
        "国内"
      ],
      "host": "et.gbc.moe",
      "port": "11010",
      "protocol": "tcp",
      "description": "物语云 800M",
      "created_at": "2025-08-20 00:07",
      "current_connections": 307,
      "max_connections": 100,
      "updated_at": "2025-11-16 14:04:31",
      "@prev_sid := t.server_id": "11",
      "load_level": "high",
      "load_text": "高负载(307.0%)"
    },
    {
      "server_id": "13",
      "name": "官方公共服务器-雨云",
      "is_active": 1,
      "usage_percentage": 104.5,
      "tags": [
        "国内",
        "官方"
      ],
      "host": "public2.easytier.cn",
      "port": "54321",
      "protocol": "tcp",
      "description": "限速 100K",
      "created_at": "2025-08-21 09:34",
      "current_connections": 104,
      "max_connections": 100,
      "updated_at": "2025-11-16 14:04:31",
      "@prev_sid := t.server_id": "13",
      "load_level": "high",
      "load_text": "高负载(104.5%)"
    },
    {
      "server_id": "18",
      "name": "成都-电信-理论100Mbps",
      "is_active": 1,
      "usage_percentage": 204,
      "tags": [
        "国内"
      ],
      "host": "221.236.27.84",
      "port": "21010",
      "protocol": "tcp",
      "description": "网络路线不算优质，但起码能用。每月流量上限 500 GiB ，目前还有挺多剩余",
      "created_at": "2025-08-26 15:32",
      "current_connections": 204,
      "max_connections": 100,
      "updated_at": "2025-11-16 14:04:31",
      "@prev_sid := t.server_id": "18",
      "load_level": "high",
      "load_text": "高负载(204.0%)"
    },
    {
      "server_id": "19",
      "name": "上海腾讯云ipv4\\v6双栈",
      "is_active": 1,
      "usage_percentage": 70,
      "tags": [
        "国内"
      ],
      "host": "sh.vomiku.com",
      "port": "7910",
      "protocol": "tcp",
      "description": "tcp://sh.vomiku.com:7910\nudp://sh.vomiku.com:7910",
      "created_at": "2025-08-27 02:38",
      "current_connections": 70,
      "max_connections": 100,
      "updated_at": "2025-11-16 14:04:31",
      "@prev_sid := t.server_id": "19",
      "load_level": "medium",
      "load_text": "中负载(70.0%)"
    },
    {
      "server_id": "20",
      "name": "张家口-阿里云-01",
      "is_active": 1,
      "usage_percentage": 50,
      "tags": [
        "国内"
      ],
      "host": "47.92.208.217",
      "port": "11010",
      "protocol": "tcp",
      "description": "阿里云3M的v4小水管，只能打洞。打个广子，欢迎来玩隔壁的邦国崛起服务器~IP：bgjq.simpfun.cn",
      "created_at": "2025-08-27 10:47",
      "current_connections": 50,
      "max_connections": 100,
      "updated_at": "2025-11-16 14:04:31",
      "@prev_sid := t.server_id": "20",
      "load_level": "low",
      "load_text": "低负载(50.0%)"
    },
    {
      "server_id": "31",
      "name": "杭州-阿里云-01",
      "is_active": 0,
      "usage_percentage": 176,
      "tags": [
        "国内"
      ],
      "host": "47.96.28.190",
      "port": "9994",
      "protocol": "tcp",
      "description": "阿里云动态带宽峰值200M，三网BGP，中转限速1Mbps",
      "created_at": "2025-09-03 02:26",
      "current_connections": 176,
      "max_connections": 100,
      "updated_at": "2025-11-16 14:04:31",
      "@prev_sid := t.server_id": "31",
      "load_level": "none",
      "load_text": "--"
    },
    {
      "server_id": "33",
      "name": "广州-腾讯云-Unicorn",
      "is_active": 1,
      "usage_percentage": 108,
      "tags": [
        "国内"
      ],
      "host": "43.139.65.49",
      "port": "11010",
      "protocol": "udp",
      "description": "腾讯云广州节点，仅打洞，有效期至2026年10月22日。",
      "created_at": "2025-09-03 16:50",
      "current_connections": 108,
      "max_connections": 100,
      "updated_at": "2025-11-16 14:04:31",
      "@prev_sid := t.server_id": "33",
      "load_level": "high",
      "load_text": "高负载(108.0%)"
    },
    {
      "server_id": "34",
      "name": "成都-电信-家宽-ipv4\\v6双栈",
      "is_active": 0,
      "usage_percentage": 118,
      "tags": [
        "国内"
      ],
      "host": "space-vector.top",
      "port": "1101",
      "protocol": "tcp",
      "description": "成都电信家宽，限速1MB，仅限用于学习、研究网络技术，请务必遵守相关法律法规，注重个人网络信息安全",
      "created_at": "2025-09-03 19:12",
      "current_connections": 118,
      "max_connections": 100,
      "updated_at": "2025-11-16 14:04:31",
      "@prev_sid := t.server_id": "34",
      "load_level": "none",
      "load_text": "--"
    },
    {
      "server_id": "36",
      "name": "成都-阿里云",
      "is_active": 0,
      "usage_percentage": 34,
      "tags": [
        "国内"
      ],
      "host": "easytier.bh8gcj.top",
      "port": "11010",
      "protocol": "tcp",
      "description": "阿里云成都 禁转发，仅限用于学习、研究网络技术，请务必遵守相关法律法规，注重个人网络信息安全。",
      "created_at": "2025-09-07 10:40",
      "current_connections": 34,
      "max_connections": 100,
      "updated_at": "2025-11-16 14:04:31",
      "@prev_sid := t.server_id": "36",
      "load_level": "none",
      "load_text": "--"
    },
    {
      "server_id": "38",
      "name": "上海四区-腾讯云-1",
      "is_active": 0,
      "usage_percentage": 153,
      "tags": [
        "国内"
      ],
      "host": "101.34.73.50",
      "port": "11010",
      "protocol": "udp",
      "description": "用来做RSShub的机器，还有一些资源闲置就贡献一下. \n小水管流量套餐：4Mbps/300GB/月 （根据消耗情况后面做限速",
      "created_at": "2025-09-09 02:54",
      "current_connections": 153,
      "max_connections": 100,
      "updated_at": "2025-11-16 14:04:31",
      "@prev_sid := t.server_id": "38",
      "load_level": "none",
      "load_text": "--"
    },
    {
      "server_id": "39",
      "name": "北京-腾讯云-01",
      "is_active": 0,
      "usage_percentage": 234,
      "tags": [
        "MC中继",
        "国内"
      ],
      "host": "turn.lzaske.xyz",
      "port": "11010",
      "protocol": "tcp",
      "description": "北京节点，资源闲置",
      "created_at": "2025-09-11 06:56",
      "current_connections": 234,
      "max_connections": 100,
      "updated_at": "2025-11-16 14:04:31",
      "@prev_sid := t.server_id": "39",
      "load_level": "none",
      "load_text": "--"
    },
    {
      "server_id": "41",
      "name": "北京-京东云",
      "is_active": 0,
      "usage_percentage": 26,
      "tags": [
        "国内"
      ],
      "host": "117.72.151.184",
      "port": "11010",
      "protocol": "tcp",
      "description": "",
      "created_at": "2025-09-12 08:31",
      "current_connections": 26,
      "max_connections": 100,
      "updated_at": "2025-11-16 14:04:31",
      "@prev_sid := t.server_id": "41",
      "load_level": "none",
      "load_text": "--"
    },
    {
      "server_id": "44",
      "name": "海南三亚-联通-01",
      "is_active": 1,
      "usage_percentage": 17,
      "tags": [
        "国内"
      ],
      "host": "sy.112c.cc",
      "port": "11090",
      "protocol": "tcp",
      "description": "",
      "created_at": "2025-09-13 14:29",
      "current_connections": 17,
      "max_connections": 100,
      "updated_at": "2025-11-16 14:04:31",
      "@prev_sid := t.server_id": "44",
      "load_level": "low",
      "load_text": "低负载(17.0%)"
    },
    {
      "server_id": "45",
      "name": "北京-腾讯云-02",
      "is_active": 1,
      "usage_percentage": 151,
      "tags": [
        "国内"
      ],
      "host": "211.159.174.121",
      "port": "11010",
      "protocol": "tcp",
      "description": "3Mbps带宽，300G流量",
      "created_at": "2025-09-14 15:05",
      "current_connections": 151,
      "max_connections": 100,
      "updated_at": "2025-11-16 14:04:31",
      "@prev_sid := t.server_id": "45",
      "load_level": "high",
      "load_text": "高负载(151.0%)"
    },
    {
      "server_id": "47",
      "name": "十堰-电信-200m",
      "is_active": 1,
      "usage_percentage": 312,
      "tags": [
        "国内"
      ],
      "host": "59.153.166.186",
      "port": "11010",
      "protocol": "tcp",
      "description": "博客：https://weiai.org.cn\n服务器监控：https://weiai.org.cn/index.php/%e6%9c%8d%e5%8a%a1%e5%99%a8%e7%8a%b6%e6%80%81/\n",
      "created_at": "2025-09-17 15:10",
      "current_connections": 312,
      "max_connections": 100,
      "updated_at": "2025-11-16 14:04:31",
      "@prev_sid := t.server_id": "47",
      "load_level": "high",
      "load_text": "高负载(312.0%)"
    }
  ],
  "pagination": {
    "total": "292",
    "page": 1,
    "per_page": 20,
    "total_pages": 15
  },
  "latest_update": "2025-11-16 14:04:32"
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

