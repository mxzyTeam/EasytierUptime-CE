import pymysql
import os
from dotenv import load_dotenv

def setup_mysql():
    """设置MySQL用户和数据库"""
    load_dotenv()
    
    # 使用root用户连接
    try:
        connection = pymysql.connect(
            host='localhost',
            user='root',
            password='2200220',  # 你刚才输入的密码
            port=3306
        )
        
        print("✅ Root连接成功")
        
        with connection.cursor() as cursor:
            # 1. 创建数据库
            db_name = 'easytier'
            cursor.execute(f"CREATE DATABASE IF NOT EXISTS {db_name} CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci")
            print(f"✅ 数据库 '{db_name}' 创建成功")
            
            # 2. 创建用户
            username = 'EasyTier'
            password = 'yTzEWfHKrpfBSxDr'  # 使用.env文件中的密码
            
            # 删除已存在的用户（如果存在）
            cursor.execute(f"DROP USER IF EXISTS '{username}'@'localhost'")
            
            # 创建新用户
            cursor.execute(f"CREATE USER '{username}'@'localhost' IDENTIFIED BY '{password}'")
            print(f"✅ 用户 '{username}' 创建成功")
            
            # 3. 授予权限
            cursor.execute(f"GRANT ALL PRIVILEGES ON {db_name}.* TO '{username}'@'localhost'")
            cursor.execute("FLUSH PRIVILEGES")
            print("✅ 权限授予成功")
            
            # 4. 显示创建结果
            cursor.execute("SELECT user, host FROM mysql.user WHERE user = %s", (username,))
            users = cursor.fetchall()
            print(f"📊 用户列表: {users}")
            
            cursor.execute("SHOW DATABASES")
            databases = [db[0] for db in cursor.fetchall()]
            print(f"📊 数据库列表: {databases}")
            
        connection.commit()
        connection.close()
        
        print("\n🎉 MySQL设置完成！")
        return True
        
    except pymysql.Error as e:
        print(f"❌ 设置失败: {e}")
        return False

def test_new_connection():
    """测试新的连接配置"""
    print("\n🔍 测试新的连接配置...")
    
    load_dotenv()
    
    config = {
        'host': os.getenv('DB_HOST'),
        'user': os.getenv('DB_USER'),
        'password': os.getenv('DB_PASSWORD'),
        'database': os.getenv('DB_DATABASE'),
        'charset': os.getenv('DB_CHARSET'),
        'port': int(os.getenv('DB_PORT', 3306))
    }
    
    try:
        connection = pymysql.connect(**config)
        print("✅ 新的连接配置测试成功！")
        
        with connection.cursor() as cursor:
            cursor.execute("SELECT DATABASE()")
            current_db = cursor.fetchone()[0]
            print(f"📊 当前数据库: {current_db}")
            
        connection.close()
        return True
        
    except pymysql.Error as e:
        print(f"❌ 新的连接配置测试失败: {e}")
        return False

if __name__ == "__main__":
    print("开始设置MySQL用户和数据库...\n")
    
    if setup_mysql():
        print("\n正在测试新的连接...")
        if test_new_connection():
            print("\n🎊 所有设置完成！现在可以正常运行EasyTier Uptime CE了。")
        else:
            print("\n⚠️ 连接测试失败，请检查设置。")
    else:
        print("\n❌ MySQL设置失败，请检查root密码是否正确。")