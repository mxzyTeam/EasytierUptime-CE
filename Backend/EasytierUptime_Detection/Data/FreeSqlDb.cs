using FreeSql;
using EasytierUptime_Detection.Config;

namespace EasytierUptime_Detection.Data;

public static class FreeSqlDb
{
    public static IFreeSql Orm { get; private set; } = null!;
    private static bool _inited;

    public static void Initialize(DatabaseOptions opt)
    {
        if (_inited) return;
        Orm = Build(opt);
        _inited = true;
    }

    private static IFreeSql Build(DatabaseOptions opt)
    {
        if (string.Equals(opt.Provider, "MySql", StringComparison.OrdinalIgnoreCase))
        {
            var conn = opt.ConnectionString ?? "server=localhost;port=3306;database=easytier_uptime;user=root;password=123456;Charset=utf8mb4;SslMode=None;";
            return new FreeSqlBuilder()
                .UseConnectionString(DataType.MySql, conn)
                .UseAutoSyncStructure(true)
                .UseNoneCommandParameter(true)
                .Build();
        }
        // default Sqlite
        var connStr = opt.ConnectionString;
        if (string.IsNullOrWhiteSpace(connStr))
        {
            var baseDir = AppContext.BaseDirectory;
            var dataDir = Path.Combine(baseDir, "data");
            Directory.CreateDirectory(dataDir);
            var dbPath = Path.Combine(dataDir, "detection.db");
            connStr = $"Data Source={dbPath};Pooling=true;Max Pool Size=10";
        }
        return new FreeSqlBuilder()
            .UseConnectionString(DataType.Sqlite, connStr)
            .UseAutoSyncStructure(true)
            .UseNoneCommandParameter(true)
            .Build();
    }
}
