#!/usr/bin/env python3
"""
EasyTier 日志清理脚本
自动清理旧的日志文件，释放磁盘空间
"""

import os
import glob
from datetime import datetime, timedelta
import shutil

def analyze_log_files():
    """分析日志文件"""
    log_files = []
    total_size = 0
    
    # 查找所有日志文件
    patterns = [
        'api_monitor_*.log',
        '*.log',
        'logs/*.log',
        '__pycache__/*',
        '.pytest_cache/*'
    ]
    
    for pattern in patterns:
        for filepath in glob.glob(pattern, recursive=True):
            if os.path.isfile(filepath):
                size = os.path.getsize(filepath)
                mtime = datetime.fromtimestamp(os.path.getmtime(filepath))
                age_days = (datetime.now() - mtime).days
                
                log_files.append({
                    'path': filepath,
                    'size': size,
                    'mtime': mtime,
                    'age_days': age_days
                })
                total_size += size
    
    # 按修改时间排序
    log_files.sort(key=lambda x: x['mtime'])
    
    return log_files, total_size

def cleanup_old_logs(days_threshold=30, size_threshold_mb=100):
    """清理旧的日志文件"""
    log_files, total_size = analyze_log_files()
    
    print("=" * 60)
    print("EasyTier 日志清理工具")
    print("=" * 60)
    
    print(f"\n当前日志文件统计:")
    print(f"总文件数: {len(log_files)}")
    print(f"总大小: {total_size / 1024 / 1024:.2f} MB")
    
    if not log_files:
        print("\n未找到需要清理的日志文件")
        return
    
    # 显示日志文件列表
    print("\n日志文件列表:")
    print("-" * 80)
    print(f"{'文件路径':<40} {'大小':<10} {'修改时间':<20} {'天数'}")
    print("-" * 80)
    
    for log in log_files:
        size_mb = log['size'] / 1024 / 1024
        print(f"{log['path']:<40} {size_mb:.2f}MB {log['mtime'].strftime('%Y-%m-%d %H:%M'):<20} {log['age_days']}天")
    
    # 识别需要清理的文件
    files_to_clean = []
    total_clean_size = 0
    
    for log in log_files:
        if log['age_days'] > days_threshold or log['size'] > size_threshold_mb * 1024 * 1024:
            files_to_clean.append(log)
            total_clean_size += log['size']
    
    if not files_to_clean:
        print(f"\n没有找到超过{days_threshold}天或大于{size_threshold_mb}MB的日志文件")
        return
    
    print(f"\n需要清理的文件 ({len(files_to_clean)}个, 总大小: {total_clean_size / 1024 / 1024:.2f}MB):")
    for log in files_to_clean:
        reason = []
        if log['age_days'] > days_threshold:
            reason.append(f"超过{days_threshold}天")
        if log['size'] > size_threshold_mb * 1024 * 1024:
            reason.append(f"大于{size_threshold_mb}MB")
        
        print(f"  - {log['path']} ({', '.join(reason)})")
    
    # 确认清理
    confirm = input(f"\n确认删除以上 {len(files_to_clean)} 个文件? (y/N): ").strip().lower()
    
    if confirm not in ['y', 'yes']:
        print("取消清理操作")
        return
    
    # 执行清理
    deleted_count = 0
    deleted_size = 0
    
    for log in files_to_clean:
        try:
            os.remove(log['path'])
            print(f"✓ 删除: {log['path']}")
            deleted_count += 1
            deleted_size += log['size']
        except Exception as e:
            print(f"✗ 删除失败 {log['path']}: {e}")
    
    # 清理缓存目录
    cache_dirs = ['__pycache__', '.pytest_cache']
    for cache_dir in cache_dirs:
        if os.path.exists(cache_dir) and os.path.isdir(cache_dir):
            try:
                shutil.rmtree(cache_dir)
                print(f"✓ 清理缓存目录: {cache_dir}")
            except Exception as e:
                print(f"✗ 清理缓存目录失败 {cache_dir}: {e}")
    
    print(f"\n清理完成:")
    print(f"删除文件数: {deleted_count}")
    print(f"释放空间: {deleted_size / 1024 / 1024:.2f} MB")
    
    # 显示剩余空间
    remaining_files, remaining_size = analyze_log_files()
    print(f"剩余文件数: {len(remaining_files)}")
    print(f"剩余空间: {remaining_size / 1024 / 1024:.2f} MB")

def create_log_rotation_plan():
    """创建日志轮转计划"""
    print("\n" + "=" * 60)
    print("日志轮转建议")
    print("=" * 60)
    
    suggestions = [
        "1. 定期清理: 建议每周清理一次超过30天的日志",
        "2. 大小限制: 设置单个日志文件最大100MB",
        "3. 备份重要日志: 重要的错误日志建议备份",
        "4. 监控磁盘空间: 定期检查项目目录磁盘使用情况",
        "5. 使用日志轮转: 考虑使用logrotate工具自动管理"
    ]
    
    for suggestion in suggestions:
        print(f"  {suggestion}")

def main():
    print("=" * 60)
    print("EasyTier 日志管理工具")
    print("=" * 60)
    
    while True:
        print("\n选择操作:")
        print("1. 分析日志文件")
        print("2. 清理旧日志文件")
        print("3. 查看日志轮转建议")
        print("4. 退出")
        
        choice = input("\n请输入选择 (1-4): ").strip()
        
        if choice == '1':
            log_files, total_size = analyze_log_files()
            print(f"\n日志分析结果:")
            print(f"文件数量: {len(log_files)}")
            print(f"总大小: {total_size / 1024 / 1024:.2f} MB")
            
            if log_files:
                print("\n文件详情:")
                for log in log_files[:10]:  # 显示前10个文件
                    print(f"  {log['path']} ({log['size'] / 1024:.1f}KB, {log['age_days']}天前)")
                
                if len(log_files) > 10:
                    print(f"  ... 还有 {len(log_files) - 10} 个文件")
        
        elif choice == '2':
            try:
                days = int(input("清理多少天前的日志? (默认30天): ") or "30")
                size_mb = int(input("清理大于多少MB的日志? (默认100MB): ") or "100")
                cleanup_old_logs(days, size_mb)
            except ValueError:
                print("输入无效，使用默认值")
                cleanup_old_logs()
        
        elif choice == '3':
            create_log_rotation_plan()
        
        elif choice == '4':
            print("退出日志管理工具")
            break
        
        else:
            print("无效选择，请重新输入")

if __name__ == "__main__":
    main()