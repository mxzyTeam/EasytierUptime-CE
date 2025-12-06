using EasytierUptime_Detection.Config;
using EasytierUptime_Detection.Data;
using EasytierUptime_Entities.Entities;
using NLog;

namespace EasytierUptime.Services;

/// <summary>
/// 负责按计划调度聚合与归档任务的服务。
/// </summary>
public class Scheduler
{
    private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

    private readonly ProbeService _probe;
    private readonly AggregationService _agg = new AggregationService();

    /// <summary>
    /// 启动调度器并定期触发任务。
    /// </summary>
    public void Start()
    {
        // 原有调度实现保留
        // ...
    }

    public Scheduler(ProbeService probe)
    {
        _probe = probe;
        // 启动时触发一次后台汇总（不阻塞检测）
        _ = Task.Run(() => _agg.RunAggregationAndArchival());
    }

    public async Task RunAsync(TimeSpan interval, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var cycleStart = DateTime.UtcNow;
            Logger.Info("【调度器】===== 周期开始：{start} =====", cycleStart.ToString("O"));
            try
            {
                var nodes = FreeSqlDb.Orm.Select<SharedNode>().Where(x => x.IsActive && x.IsApproved).ToList();
                if (nodes.Count == 0)
                {
                    Logger.Info("【调度器】无可检测的节点（已激活且已审批）。");
                }
                else
                {
                    Logger.Info("【调度器】本次将检测 {count} 个节点。", nodes.Count);
                }

                int okCount = 0, failCount = 0;
                foreach (var n in nodes)
                {
                    Logger.Info("【检测】开始 -> 节点#{id} {name} {host}:{port}（协议：{protocol}）", n.Id, n.Name, n.Host, n.Port, n.Protocol);

                    var (status, rtUs, err, version, connCount) = await _probe.ProbeAsync(n, ct);

                    Logger.Info(
                        "【检测】结束 <- 节点#{id} 状态={status} 响应(us)={rtUs} 版本={version} 连接数={conn} {error}",
                        n.Id, status, rtUs, string.IsNullOrEmpty(version) ? "-" : version, connCount,
                        string.IsNullOrEmpty(err) ? string.Empty : $"错误=\"{err}\""
                    );

                    if (string.Equals(status, "Healthy", StringComparison.OrdinalIgnoreCase)) okCount++; else failCount++;

                    var rec = new HealthRecord
                    {
                        NodeId = n.Id,
                        Status = status,
                        ResponseTime = (int)Math.Clamp(rtUs, 0, int.MaxValue),
                        ErrorMessage = err ?? string.Empty,
                        ConnectionCount = connCount,
                        CheckedAt = DateTime.UtcNow
                    };
                    FreeSqlDb.Orm.Insert(rec).ExecuteAffrows();
                    Logger.Info("【数据库】已写入健康记录：节点#{id} 时间={at}", n.Id, rec.CheckedAt.ToString("O"));

                    // 更新节点聚合信息
                    if (!string.IsNullOrEmpty(version)) n.Version = version;
                    n.CurrentConnections = connCount;
                    n.UpdatedAt = DateTime.UtcNow;
                    FreeSqlDb.Orm.Update<SharedNode>().SetSource(n).ExecuteAffrows();
                    Logger.Info("【数据库】已更新节点#{id} 聚合（连接数={conn}，版本={version}）", n.Id, n.CurrentConnections, n.Version);
                }

                Logger.Info("【调度器】本轮汇总：健康={ok}，不健康={fail}", okCount, failCount);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "【调度器】发生错误");
            }

            // 周期结束后触发一次后台汇总（不阻塞下一次检测）
            _ = Task.Run(() => _agg.RunAggregationAndArchival());

            var cycleEnd = DateTime.UtcNow;
            Logger.Info("【调度器】===== 周期结束：{end}（耗时 {elapsed}s）=====%n", cycleEnd.ToString("O"), (cycleEnd - cycleStart).TotalSeconds.ToString("F1"));

            try { await Task.Delay(interval, ct); } catch { }
        }
    }
}
