#!/usr/bin/env python3
"""
API监控脚本 - 修复版本：处理缺失的description列
"""

import requests
import pymysql
from datetime import datetime, timedelta
import time
import schedule
import logging
import json
import sys
from requests.exceptions import RequestException, Timeout
from dataclasses import dataclass
from typing import List, Dict, Any, Optional
import threading

# 配置类
@dataclass
class DBConfig:
    host: str = 'localhost'
    user: str = 'EasyTier'
    password: str = 'yTzEWfHKrpfBSxDr'
    database: str = 'easytier'
    charset: str = 'utf8mb4'
    port: int = 3306

@dataclass
class APIConfig:
    endpoints: List[str] = None
    timeout: int = 120
    retry_times: int = 3
    retry_delay: int = 5
    health_check_interval: int = 30
    
    def __post_init__(self):
        if self.endpoints is None:
            self.endpoints = [
                'https://uptime.easytier.cn/api/nodes?page=1&per_page=1000',
                'https://uptime.lisfox.top/api/nodes?page=1&per_page=1000'
            ]

@dataclass
class APIMetrics:
    success_count: int = 0
    fail_count: int = 0
    total_response_time: float = 0
    last_success: Optional[datetime] = None
    last_fail: Optional[datetime] = None
    last_response_time: float = 0
    consecutive_fails: int = 0

class APIMonitor:
    """API监控主类"""
    
    def __init__(self, db_config: DBConfig, api_config: APIConfig):
        self.db_config = db_config
        self.api_config = api_config
        self.api_metrics = {endpoint: APIMetrics() for endpoint in api_config.endpoints}
        self.lock = threading.Lock()
        
        # 设置日志
        self._setup_logging()
        
    def _setup_logging(self):
        """配置日志系统"""
        logging.basicConfig(
            level=logging.INFO,
            format='%(asctime)s - %(levelname)s - %(message)s',
            handlers=[
                logging.StreamHandler(sys.stdout),
                logging.FileHandler(f'api_monitor_{datetime.now().strftime("%Y%m%d")}.log')
            ]
        )
        self.logger = logging.getLogger(__name__)
    
    def get_current_table_name(self) -> str:
        """获取当前日期的分表名"""
        return f"monitor_{datetime.now().strftime('%Y%m%d')}"
    
    def create_table_if_not_exists(self, table_name: str) -> bool:
        """创建分表（如果不存在），并检查表结构"""
        max_retries = 3
        for attempt in range(max_retries):
            try:
                conn = pymysql.connect(**self.db_config.__dict__)
                with conn.cursor() as cursor:
                    cursor.execute(f"SHOW TABLES LIKE '{table_name}'")
                    table_exists = cursor.fetchone()
                    
                    if not table_exists:
                        # 创建新表（包含description字段）
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
                            `last_check_time` DATETIME NOT NULL,
                            `last_response_time` int(11) NOT NULL DEFAULT 0,
                            `health_percentage_24h` decimal(5,2) NOT NULL DEFAULT 0.00,
                            `health_record_total_counter_ring` text NOT NULL,
                            `health_record_healthy_counter_ring` text NOT NULL,
                            `tags` text NOT NULL,
                            `description` text NOT NULL,
                            `created_at` DATETIME NOT NULL,
                            `updated_at` TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                            PRIMARY KEY (`id`),
                            KEY `idx_server_id` (`server_id`)
                        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
                        """
                        cursor.execute(create_sql)
                        
                        # 添加到元信息表
                        now = datetime.now().strftime('%Y-%m-%d %H:%M:%S')
                        cursor.execute("""
                            INSERT INTO `monitor_tables` (`table_name`, `create_time`, `update_time`)
                            VALUES (%s, %s, %s)
                        """, (table_name, now, now))
                        
                        conn.commit()
                        self.logger.info(f"创建新表：{table_name}")
                    else:
                        # 表已存在，检查是否有description列
                        cursor.execute(f"SHOW COLUMNS FROM `{table_name}` LIKE 'description'")
                        has_description = cursor.fetchone()
                        
                        if not has_description:
                            # 添加缺失的description列
                            self.logger.info(f"表 {table_name} 缺少description列，正在添加...")
                            cursor.execute(f"ALTER TABLE `{table_name}` ADD COLUMN `description` TEXT AFTER `tags`")
                            conn.commit()
                            self.logger.info(f"成功为表 {table_name} 添加description列")
                        else:
                            self.logger.info(f"表 {table_name} 结构正常")
                            
                conn.close()
                return True
                
            except Exception as e:
                self.logger.error(f"表操作失败 (尝试 {attempt + 1}/{max_retries}): {str(e)}")
                if attempt < max_retries - 1:
                    time.sleep(2)
                else:
                    return False
        return False
    
    def update_api_metrics(self, endpoint: str, success: bool, response_time: float = 0):
        """更新API指标"""
        with self.lock:
            metrics = self.api_metrics[endpoint]
            if success:
                metrics.success_count += 1
                metrics.last_success = datetime.now()
                metrics.total_response_time += response_time
                metrics.last_response_time = response_time
                metrics.consecutive_fails = 0
            else:
                metrics.fail_count += 1
                metrics.last_fail = datetime.now()
                metrics.consecutive_fails += 1
    
    def test_endpoint_health(self, endpoint: str) -> bool:
        """测试单个端点健康状态"""
        try:
            start_time = time.time()
            response = requests.get(endpoint, timeout=30)
            response_time = (time.time() - start_time) * 1000
            
            if response.status_code == 200:
                self.update_api_metrics(endpoint, True, response_time)
                self.logger.info(f"健康检查通过: {endpoint} ({response_time:.2f}ms)")
                return True
            else:
                self.update_api_metrics(endpoint, False)
                self.logger.warning(f"健康检查失败: {endpoint} - 状态码: {response.status_code}")
                return False
                
        except Exception as e:
            self.update_api_metrics(endpoint, False)
            self.logger.warning(f"健康检查异常: {endpoint} - {str(e)}")
            return False
    
    def health_check(self) -> bool:
        """健康检查：只检查第一个节点"""
        primary_api = self.api_config.endpoints[0]
        self.logger.info(f"健康检查：只检查主API {primary_api}")
        
        is_healthy = self.test_endpoint_health(primary_api)
        
        if is_healthy:
            self.logger.info("主API健康，跳过第二个节点检查")
        else:
            self.logger.warning("主API不健康，将在数据获取时尝试备用API")
        
        return is_healthy
    
    def fetch_api_data(self) -> Optional[Dict[str, Any]]:
        """获取API数据：优先使用第一个API，失败时使用第二个（不检查第二个节点状态）"""
        
        # 优先尝试第一个API
        primary_api = self.api_config.endpoints[0]
        self.logger.info(f"优先尝试主API: {primary_api}")
        
        for retry in range(self.api_config.retry_times):
            try:
                start_time = time.time()
                response = requests.get(primary_api, timeout=self.api_config.timeout)
                response_time = (time.time() - start_time) * 1000
                
                response.raise_for_status()
                data = response.json()
                
                self.update_api_metrics(primary_api, True, response_time)
                self.logger.info(f"主API请求成功: {primary_api} (响应时间: {response_time:.2f}ms)")
                
                return data
                
            except Timeout:
                error_msg = f"请求超时 ({self.api_config.timeout}s)"
                if retry < self.api_config.retry_times - 1:
                    self.logger.warning(f"主API请求超时，第{retry+1}次重试")
                    time.sleep(self.api_config.retry_delay)
                else:
                    self.update_api_metrics(primary_api, False)
                    self.logger.error(f"主API所有重试均失败: {error_msg}")
                    break
                    
            except RequestException as e:
                error_msg = str(e)
                if retry < self.api_config.retry_times - 1:
                    self.logger.warning(f"主API请求失败，第{retry+1}次重试: {error_msg}")
                    time.sleep(self.api_config.retry_delay)
                else:
                    self.update_api_metrics(primary_api, False)
                    self.logger.error(f"主API所有重试均失败: {error_msg}")
                    break
        
        # 如果第一个API失败，直接尝试第二个API（不进行健康检查）
        if len(self.api_config.endpoints) > 1:
            backup_api = self.api_config.endpoints[1]
            self.logger.info(f"主API不可用，直接尝试备用API: {backup_api}")
            
            for retry in range(self.api_config.retry_times):
                try:
                    start_time = time.time()
                    response = requests.get(backup_api, timeout=self.api_config.timeout)
                    response_time = (time.time() - start_time) * 1000
                    
                    response.raise_for_status()
                    data = response.json()
                    
                    self.update_api_metrics(backup_api, True, response_time)
                    self.logger.info(f"备用API请求成功: {backup_api} (响应时间: {response_time:.2f}ms)")
                    
                    return data
                    
                except Timeout:
                    error_msg = f"请求超时 ({self.api_config.timeout}s)"
                    if retry < self.api_config.retry_times - 1:
                        self.logger.warning(f"备用API请求超时，第{retry+1}次重试")
                        time.sleep(self.api_config.retry_delay)
                    else:
                        self.update_api_metrics(backup_api, False)
                        self.logger.error(f"备用API所有重试均失败: {error_msg}")
                        break
                        
                except RequestException as e:
                    error_msg = str(e)
                    if retry < self.api_config.retry_times - 1:
                        self.logger.warning(f"备用API请求失败，第{retry+1}次重试: {error_msg}")
                        time.sleep(self.api_config.retry_delay)
                    else:
                        self.update_api_metrics(backup_api, False)
                        self.logger.error(f"备用API所有重试均失败: {error_msg}")
                        break
        
        self.logger.error("所有API端点均失败，无法获取数据")
        return None
    
    def safe_value(self, value, default=0):
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
    
    def safe_description_value(self, value):
        """安全处理description字段"""
        if value is None or value == '':
            return ''
        # 如果是字符串，直接返回
        if isinstance(value, str):
            return value
        # 如果是其他类型，转换为字符串
        return str(value)
    
    def parse_iso_time(self, iso_time: Optional[str]) -> str:
        """解析ISO时间格式"""
        current_datetime = datetime.now().strftime('%Y-%m-%d %H:%M:%S')
        if not iso_time or iso_time == '':
            return current_datetime
        try:
            # 处理各种时间格式
            iso_time = iso_time.replace('Z', '+00:00')
            if 'T' in iso_time:
                return datetime.fromisoformat(iso_time).strftime('%Y-%m-%d %H:%M:%S')
            else:
                # 如果不是ISO格式，尝试其他解析
                return current_datetime
        except (ValueError, TypeError):
            return current_datetime
    
    def save_nodes_data(self, nodes: List[Dict[str, Any]]) -> bool:
        """保存节点数据到数据库"""
        table_name = self.get_current_table_name()
        
        if not self.create_table_if_not_exists(table_name):
            self.logger.error(f"无法创建或访问表: {table_name}")
            return False
        
        max_retries = 3
        for attempt in range(max_retries):
            try:
                conn = pymysql.connect(**self.db_config.__dict__)
                with conn.cursor() as cursor:
                    success_count = 0
                    
                    for node in nodes:
                        # 处理数据字段
                        server_id = self.safe_value(node.get('id'), 0)
                        if server_id == 0:
                            continue
                            
                        # 删除旧记录
                        cursor.execute(f"DELETE FROM `{table_name}` WHERE `server_id` = %s", (server_id,))
                        
                        # 准备插入数据（添加description字段）
                        insert_data = (
                            server_id,
                            self.safe_value(node.get('name'), ''),
                            self.safe_value(node.get('host'), ''),
                            self.safe_value(node.get('port'), 0),
                            self.safe_value(node.get('protocol'), 'tcp'),
                            self.safe_value(node.get('version'), ''),
                            self.safe_value(node.get('max_connections'), 0),
                            self.safe_value(node.get('current_connections'), 0),
                            int(self.safe_value(node.get('is_active'), False)),
                            int(self.safe_value(node.get('is_approved'), False)),
                            int(self.safe_value(node.get('allow_relay'), False)),
                            self.safe_value(node.get('usage_percentage'), 0.0),
                            self.safe_value(node.get('current_health_status'), 'unknown'),
                            self.parse_iso_time(node.get('last_check_time')),
                            self.safe_value(node.get('last_response_time'), 0),
                            self.safe_value(node.get('health_percentage_24h'), 0.0),
                            self.safe_text_value(node.get('health_record_total_counter_ring')),
                            self.safe_text_value(node.get('health_record_healthy_counter_ring')),
                            self.safe_text_value(node.get('tags')),
                            self.safe_description_value(node.get('description')),  # 新增description字段
                            self.parse_iso_time(node.get('created_at'))
                        )
                        
                        # 插入新记录（更新SQL语句包含description字段）
                        cursor.execute(f"""
                            INSERT INTO `{table_name}` (
                                server_id, name, host, port, protocol, version,
                                max_connections, current_connections, is_active, is_approved, allow_relay,
                                usage_percentage, current_health_status, last_check_time,
                                last_response_time, health_percentage_24h,
                                health_record_total_counter_ring, health_record_healthy_counter_ring,
                                tags, description, created_at
                            ) VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s)
                        """, insert_data)
                        success_count += 1
                    
                    conn.commit()
                    self.logger.info(f"成功更新 {success_count}/{len(nodes)} 条数据到 {table_name}")
                    
                    # 更新元信息表
                    now = datetime.now().strftime('%Y-%m-%d %H:%M:%S')
                    cursor.execute("""
                        UPDATE `monitor_tables` 
                        SET `update_time` = %s 
                        WHERE `table_name` = %s
                    """, (now, table_name))
                    conn.commit()
                    
                conn.close()
                return True
                
            except pymysql.Error as e:
                self.logger.error(f"数据库错误 (尝试 {attempt + 1}/{max_retries}): {str(e)}")
                if attempt < max_retries - 1:
                    time.sleep(2)
                else:
                    return False
            except Exception as e:
                self.logger.error(f"保存数据失败 (尝试 {attempt + 1}/{max_retries}): {str(e)}")
                if attempt < max_retries - 1:
                    time.sleep(2)
                else:
                    return False
        return False
    
    def print_status_report(self):
        """打印状态报告"""
        self.logger.info("=== API状态报告 ===")
        for i, endpoint in enumerate(self.api_config.endpoints):
            metrics = self.api_metrics[endpoint]
            total = metrics.success_count + metrics.fail_count
            success_rate = (metrics.success_count / total * 100) if total > 0 else 0
            
            avg_response_time = metrics.total_response_time / metrics.success_count if metrics.success_count > 0 else 0
            
            last_success = metrics.last_success.strftime('%H:%M:%S') if metrics.last_success else '从未成功'
            last_fail = metrics.last_fail.strftime('%H:%M:%S') if metrics.last_fail else '从未失败'
            
            status = "主API" if i == 0 else "备用API"
            
            self.logger.info(
                f"{status} {endpoint}: 成功率 {success_rate:.1f}% "
                f"平均响应 {avg_response_time:.2f}ms "
                f"最后成功: {last_success} "
                f"连续失败: {metrics.consecutive_fails}"
            )
    
    def run_monitoring_task(self):
        """执行监控任务"""
        self.logger.info(f"===== 开始执行任务: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')} =====")
        
        # 健康检查：只检查第一个节点
        self.health_check()
        
        # 获取数据：优先使用第一个API，失败时使用第二个
        data = self.fetch_api_data()
        if not data or not data.get('success'):
            self.logger.error(f"接口返回错误: {data.get('error') if data else '未知错误'}")
            return
        
        nodes = data.get('data', {}).get('items', [])
        if not nodes:
            self.logger.warning("接口返回空数据")
            return
        
        # 保存数据
        if self.save_nodes_data(nodes):
            self.logger.info("数据保存成功")
        else:
            self.logger.error("数据保存失败")
        
        # 打印报告
        self.print_status_report()
        self.logger.info("===== 任务执行结束 =====")

def main():
    """主函数"""
    # 初始化配置
    db_config = DBConfig()
    api_config = APIConfig()
    
    # 创建监控实例
    monitor = APIMonitor(db_config, api_config)
    
    # 立即执行一次任务
    monitor.run_monitoring_task()
    
    # 设置定时任务
    schedule.every(api_config.health_check_interval).minutes.do(monitor.run_monitoring_task)
    
    monitor.logger.info(f"定时任务已启动，每{api_config.health_check_interval}分钟执行一次...")
    monitor.logger.info(f"主API: {api_config.endpoints[0]}")
    if len(api_config.endpoints) > 1:
        monitor.logger.info(f"备用API: {api_config.endpoints[1]} (仅在主API失败时使用)")
    
    # 主循环
    try:
        while True:
            schedule.run_pending()
            time.sleep(1)
    except KeyboardInterrupt:
        monitor.logger.info("监控任务被用户中断")
    except Exception as e:
        monitor.logger.error(f"监控任务异常退出: {str(e)}")

if __name__ == "__main__":
    main()
