import pymysql
import os
from dotenv import load_dotenv

def test_mysql_connection():
    """测试MySQL连接"""
    load_dotenv()
    
    config = {
        'host': os.getenv('DB_HOST'),
        'user': os.getenv('DB_USER'),
        'password': os.getenv('DB_PASSWORD'),
        'database': os.getenv('DB_DATABASE'),
        'charset': os.getenv('DB_CHARSET'),
        'port': int(os.getenv('DB_PORT', 3306))
    }
    
    print("=== MySQL连接诊断 ===")
    print(f"配置信息:")
    print(f"  主机: {config['host']}")
    print(f"  端口: {config['port']}")
    print(f"  用户: {config['user']}")
    print(f"  数据库: {config['database']}")
    print()
    
    # 测试1: 尝试连接（不带数据库名）
    print("1. 测试基础连接（不带数据库）...")
    try:
        test_config = config.copy()
        test_config.pop('database', None)
        connection = pymysql.connect(**test_config)
        print("   ✅ 基础连接成功")
        
        # 检查数据库是否存在
        with connection.cursor() as cursor:
            cursor.execute("SHOW DATABASES")
            databases = [db[0] for db in cursor.fetchall()]
            print(f"   📊 可用数据库: {databases}")
            
            if config['database'] in databases:
                print(f"   ✅ 数据库 '{config['database']}' 存在")
            else:
                print(f"   ❌ 数据库 '{config['database']}' 不存在")
                
        connection.close()
    except pymysql.Error as e:
        print(f"   ❌ 基础连接失败: {e}")
        return False
    
    print()
    
    # 测试2: 尝试连接指定数据库
    print("2. 测试数据库连接...")
    try:
        connection = pymysql.connect(**config)
        print("   ✅ 数据库连接成功")
        
        # 检查表
        with connection.cursor() as cursor:
            cursor.execute("SHOW TABLES")
            tables = [table[0] for table in cursor.fetchall()]
            print(f"   📋 数据库中的表: {tables}")
            
            # 检查用户权限
            cursor.execute("SELECT user, host FROM mysql.user WHERE user = %s", (config['user'],))
            users = cursor.fetchall()
            print(f"   👤 用户权限: {users}")
            
        connection.close()
        return True
        
    except pymysql.Error as e:
        print(f"   ❌ 数据库连接失败: {e}")
        
        # 提供解决方案建议
        error_msg = str(e)
        if "Access denied" in error_msg:
            print("\n💡 解决方案建议:")
            print("   1. 检查MySQL用户 'EasyTier' 是否存在")
            print("   2. 检查用户密码是否正确")
            print("   3. 检查用户是否有访问 'easytier' 数据库的权限")
            print("   4. 尝试使用root用户连接测试")
        elif "Unknown database" in error_msg:
            print("\n💡 解决方案建议:")
            print("   1. 数据库 'easytier' 不存在，需要创建")
            print("   2. 运行数据库初始化脚本")
        
        return False

def test_with_root():
    """使用root用户测试连接"""
    print("\n3. 使用root用户测试连接...")
    
    try:
        # 尝试使用root用户连接（可能需要密码）
        connection = pymysql.connect(
            host='localhost',
            user='root',
            password=input("请输入MySQL root密码（如果没有密码直接回车）: ") or None,
            port=3306
        )
        
        print("   ✅ Root连接成功")
        
        with connection.cursor() as cursor:
            # 检查EasyTier用户是否存在
            cursor.execute("SELECT user, host, authentication_string FROM mysql.user WHERE user = 'EasyTier'")
            user_info = cursor.fetchall()
            
            if user_info:
                print("   ✅ EasyTier用户存在")
                for user in user_info:
                    print(f"      用户: {user[0]}@{user[1]}")
            else:
                print("   ❌ EasyTier用户不存在")
                
            # 检查数据库权限
            cursor.execute("SHOW GRANTS FOR 'EasyTier'@'localhost'")
            grants = cursor.fetchall()
            print("   🔑 用户权限:")
            for grant in grants:
                print(f"      {grant[0]}")
                
        connection.close()
        return True
        
    except pymysql.Error as e:
        print(f"   ❌ Root连接失败: {e}")
        return False

if __name__ == "__main__":
    print("开始MySQL连接诊断...\n")
    
    if test_mysql_connection():
        print("\n🎉 所有连接测试通过！")
    else:
        print("\n⚠️  连接存在问题，尝试使用root用户诊断...")
        test_with_root()
        
    print("\n诊断完成。")