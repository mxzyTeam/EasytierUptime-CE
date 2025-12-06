using EasytierUptime.Services;
using EasytierUptime_Detection.Config;
using EasytierUptime_Detection.Data;
using EasytierUptime_Entities.Entities;
using Microsoft.Extensions.Configuration;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace EasytierUptime_Detection
{
    internal class Program
    {
        static void Main(string[] args)
        {
            // 以代码方式配置 NLog，输出到三个文件：系统级、汇总、检测
            var config = new LoggingConfiguration();

            var layout = "${longdate}|${level:uppercase=true}|${logger}|${message}${exception:format=ToString}";

            var consoleTarget = new ConsoleTarget("console") { Layout = layout };

            var logDir = Path.Combine(AppContext.BaseDirectory, "logs");
            Directory.CreateDirectory(logDir);

            // 系统级日志（程序、调度器、数据库初始化、通用信息）
            var systemFile = new FileTarget("file-system")
            {
                FileName = Path.Combine(logDir, "system-${shortdate}.log"),
                ArchiveFileName = Path.Combine(logDir, "archives", "system-${shortdate}.{#}.log"),
                ArchiveNumbering = ArchiveNumberingMode.Rolling,
                ArchiveAboveSize = 10 * 1024 * 1024,
                MaxArchiveFiles = 7,
                Layout = layout
            };

            // 汇总日志
            var aggregationFile = new FileTarget("file-aggregation")
            {
                FileName = Path.Combine(logDir, "aggregation-${shortdate}.log"),
                ArchiveFileName = Path.Combine(logDir, "archives", "aggregation-${shortdate}.{#}.log"),
                ArchiveNumbering = ArchiveNumberingMode.Rolling,
                ArchiveAboveSize = 10 * 1024 * 1024,
                MaxArchiveFiles = 7,
                Layout = layout
            };

            // 检测日志
            var detectionFile = new FileTarget("file-detection")
            {
                FileName = Path.Combine(logDir, "detection-${shortdate}.log"),
                ArchiveFileName = Path.Combine(logDir, "archives", "detection-${shortdate}.{#}.log"),
                ArchiveNumbering = ArchiveNumberingMode.Rolling,
                ArchiveAboveSize = 10 * 1024 * 1024,
                MaxArchiveFiles = 7,
                Layout = layout
            };

            config.AddTarget(consoleTarget);
            config.AddTarget(systemFile);
            config.AddTarget(aggregationFile);
            config.AddTarget(detectionFile);

            // 控制台：输出全部级别
            config.AddRuleForAllLevels(consoleTarget);

            // 按 logger 名称分流到文件
            // 系统级：Program、Scheduler、以及 EasytierUptime_Detection.* 的通用输出
            config.LoggingRules.Add(new LoggingRule("EasytierUptime_Detection.*", NLog.LogLevel.Info, systemFile));
            config.LoggingRules.Add(new LoggingRule("EasytierUptime.Services.Scheduler*", NLog.LogLevel.Info, systemFile));
            config.LoggingRules.Add(new LoggingRule("EasytierUptime_Detection.Program*", NLog.LogLevel.Info, systemFile));

            // AggregationService -> 汇总日志
            config.LoggingRules.Add(new LoggingRule("EasytierUptime.Services.AggregationService*", NLog.LogLevel.Info, aggregationFile));

            // ProbeService -> 检测日志
            config.LoggingRules.Add(new LoggingRule("EasytierUptime.Services.ProbeService*", NLog.LogLevel.Info, detectionFile));

            LogManager.Configuration = config;
            var logger = LogManager.GetCurrentClassLogger();

            try
            {
                // 构建配置（appsettings.json + 环境变量）
                var appConfig = new ConfigurationBuilder()
                    .SetBasePath(AppContext.BaseDirectory)
                    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                    .AddEnvironmentVariables(prefix: "EASYTIER_DETECT_")
                    .Build();

                var dbOptions = appConfig.GetSection("Database").Get<DatabaseOptions>() ?? new DatabaseOptions();
                FreeSqlDb.Initialize(dbOptions);

                logger.Info("FreeSql 初始化完成。Provider={provider} Conn={conn}", dbOptions.Provider, dbOptions.ConnectionString ?? "(auto)");

                // 演示：查询所有 shared_nodes 并打印部分字段
                var nodes = FreeSqlDb.Orm.Select<SharedNode>().ToList();
                if (nodes.Count == 0)
                {
                    logger.Info("未找到 shared_nodes 记录。");
                }
                else
                {
                    logger.Info("找到 {count} 条 shared_nodes：", nodes.Count);
                    foreach (var n in nodes)
                    {
                        logger.Info(
                            "[#{id}] {name} host={host}:{port} protocol={protocol} version={version} relays={relays} conns={curr}/{max} active={active} approved={approved} public={pub} owner={owner} updated={updated}",
                            n.Id, n.Name, n.Host, n.Port, n.Protocol, n.Version, (n.AllowRelay ? 1 : 0), n.CurrentConnections, n.MaxConnections, (n.IsActive ? 1 : 0), (n.IsApproved ? 1 : 0), (n.IsPublic ? 1 : 0), n.Owner, n.UpdatedAt.ToString("O"));
                        if (!string.IsNullOrWhiteSpace(n.Description))
                            logger.Info("  描述: {desc}", n.Description);
                    }
                }

                // 启动调度循环（Ctrl+C 退出）
                var probe = new ProbeService();
                var scheduler = new Scheduler(probe);
                var cts = new CancellationTokenSource();
                Console.CancelKeyPress += (s, e) =>
                {
                    e.Cancel = true; // 防止进程被立即终止
                    cts.Cancel();
                    logger.Info("正在停止调度器...");
                };

                logger.Info("调度器已启动。按 Ctrl+C 结束。");
                try
                {
                    scheduler.RunAsync(TimeSpan.FromSeconds(5), cts.Token).GetAwaiter().GetResult();
                }
                catch (OperationCanceledException)
                {
                    // Ctrl+C 预期取消
                }
                logger.Info("调度器已停止。");
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Program 发生未处理异常");
                throw;
            }
            finally
            {
                LogManager.Shutdown();
            }
        }
    }
}
