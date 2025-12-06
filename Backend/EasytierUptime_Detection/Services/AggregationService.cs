using EasytierUptime_Detection.Data;
using EasytierUptime_Entities.Entities;
using NLog;

namespace EasytierUptime.Services;

public class AggregationService
{
    private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

    bool isRunning = false;
    public void RunAggregationAndArchival()
    {
        if (isRunning)
        {
            Logger.Info("【汇总】任务已在运行，跳过本次调用。");
            return;
        }
        isRunning = true;
        try
        {
            var now = DateTime.UtcNow;
            var from = now.AddHours(-24);

            var nodeIds = FreeSqlDb.Orm.Select<HealthRecord>()
                .Where(r => r.CheckedAt >= from && r.CheckedAt <= now)
                .ToList(r => r.NodeId)
                .Distinct()
                .ToList();

            int totalAggRows = 0;
            foreach (var nodeId in nodeIds)
            {
                Logger.Info("【汇总】当前节点：{nodeId}", nodeId);
                var records = FreeSqlDb.Orm.Select<HealthRecord>()
                    .Where(r => r.NodeId == nodeId && r.CheckedAt >= from && r.CheckedAt <= now)
                    .OrderBy(r => r.CheckedAt)
                    .ToList();

                if (records.Count == 0)
                    continue;

                // Update denormalized latest health summary on shared_nodes
                var latest = records.Last();
                var healthy24h = records.Count(r => string.Equals(r.Status, "Healthy", StringComparison.OrdinalIgnoreCase));
                var healthPercent24h = records.Count == 0 ? 0d : Math.Round((double)healthy24h * 100.0 / records.Count, 2, MidpointRounding.AwayFromZero);

                FreeSqlDb.Orm.Update<SharedNode>(nodeId)
                    .Set(n => n.LastCheckedAt, latest.CheckedAt)
                    .Set(n => n.LastResponseTimeUs, latest.ResponseTime)
                    .Set(n => n.CurrentHealthStatus, latest.Status)
                    .Set(n => n.CurrentConnectionCount, latest.ConnectionCount)
                    .Set(n => n.HealthPercentage24h, healthPercent24h)
                    .Set(n => n.UpdatedAt, now)
                    .ExecuteAffrows();

                // existing aggregations
                AggregatePeriod1m(nodeId, records, ref totalAggRows, from);
                AggregatePeriod15m(nodeId, records, ref totalAggRows);
                AggregatePeriod30m(nodeId, records, ref totalAggRows);
                AggregatePeriod1h(nodeId, records, ref totalAggRows);
                AggregatePeriod2h(nodeId, records, ref totalAggRows);
                AggregatePeriod6h(nodeId, records, ref totalAggRows);
                AggregatePeriod12h(nodeId, records, ref totalAggRows);
            }

            var coldCutoff = now.AddHours(-24);
            int batchSize = 5000;
            int archived = 0;
            while (true)
            {
                var batch = FreeSqlDb.Orm.Select<HealthRecord>()
                    .Where(r => r.CheckedAt < coldCutoff)
                    .OrderBy(r => r.CheckedAt)
                    .Limit(batchSize)
                    .ToList();

                if (batch.Count == 0) break;

                var archives = batch.Select(r => new HealthRecordArchive
                {
                    NodeId = r.NodeId,
                    Status = r.Status,
                    ResponseTime = r.ResponseTime,
                    ErrorMessage = r.ErrorMessage,
                    ConnectionCount = r.ConnectionCount,
                    CheckedAt = r.CheckedAt,
                    ArchivedAt = now,
                }).ToList();

                FreeSqlDb.Orm.Insert(archives).ExecuteAffrows();

                var ids = batch.Select(b => b.Id).ToArray();
                FreeSqlDb.Orm.Delete<HealthRecord>().Where(r => ids.Contains(r.Id)).ExecuteAffrows();

                archived += batch.Count;
                Logger.Info("【归档】已处理批次：数量={count}，累计归档={archived}", batch.Count, archived);
            }

            Logger.Info("【汇总】完成。新增聚合行数={totalAggRows}，归档数量={archived}", totalAggRows, archived);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "【汇总】执行失败，发生异常");
        }
        finally
        {
            isRunning = false;
        }
    }

    private static void AggregatePeriod1m(int nodeId, List<HealthRecord> records, ref int totalAggRows, DateTime from)
    {
        var span = TimeSpan.FromMinutes(1);
        var latestAgg = FreeSqlDb.Orm.Select<HealthAggregate1m>()
            .Where(h => h.NodeId == nodeId && h.BucketStart >= from)
            .OrderByDescending(h => h.BucketStart)
            .Limit(1)
            .ToOne();

        var firstBucket = FloorToBucket(records.First().CheckedAt, span);
        var lastBucket = FloorToBucket(records.Last().CheckedAt, span);
        var currentBucket = latestAgg != null ? latestAgg.BucketStart : firstBucket;

        while (currentBucket <= lastBucket)
        {
            var bucketEnd = currentBucket.Add(span);
            var bucketRecords = records.Where(r => r.CheckedAt >= currentBucket && r.CheckedAt < bucketEnd).ToList();

            var avgResp = bucketRecords.Count == 0 ? 0 : bucketRecords.Average(h => h.ResponseTime);
            var avgConn = bucketRecords.Count == 0 ? 0 : bucketRecords.Average(h => h.ConnectionCount);
            var healthy = bucketRecords.Count == 0 ? 0 : bucketRecords.Count(e => string.Equals(e.Status, "Healthy", StringComparison.OrdinalIgnoreCase));
            var unhealthy = bucketRecords.Count == 0 ? 0 : bucketRecords.Count(e => string.Equals(e.Status, "Unhealthy", StringComparison.OrdinalIgnoreCase));

            if (latestAgg != null && currentBucket == latestAgg.BucketStart)
            {
                FreeSqlDb.Orm.Update<HealthAggregate1m>()
                    .Where(r => r.Id == latestAgg.Id)
                    .Set(r => r.AvgConnectionCount, avgConn)
                    .Set(r => r.AvgResponseTimeUs, avgResp)
                    .Set(r => r.HealthyCount, healthy)
                    .Set(r => r.UnhealthyCount, unhealthy)
                    .Set(r => r.SampleCount, bucketRecords.Count)
                    .ExecuteAffrows();

                Logger.Info("【汇总】更新 1 分钟聚合：节点={nodeId}，桶起点={bucket}，样本={samples}，健康={healthy}，不健康={unhealthy}，平均响应(us)={avgResp}，平均连接数={avgConn}", nodeId, currentBucket.ToString("o"), bucketRecords.Count, healthy, unhealthy, avgResp, avgConn);
            }
            else
            {
                FreeSqlDb.Orm.Insert(new HealthAggregate1m
                {
                    NodeId = nodeId,
                    BucketStart = currentBucket,
                    AvgResponseTimeUs = avgResp,
                    AvgConnectionCount = avgConn,
                    HealthyCount = healthy,
                    UnhealthyCount = unhealthy,
                    SampleCount = bucketRecords.Count,
                }).ExecuteAffrows();
                Logger.Info("【汇总】新增 1 分钟聚合：节点={nodeId}，桶起点={bucket}，样本={samples}，健康={healthy}，不健康={unhealthy}，平均响应(us)={avgResp}，平均连接数={avgConn}", nodeId, currentBucket.ToString("o"), bucketRecords.Count, healthy, unhealthy, avgResp, avgConn);
                totalAggRows++;
            }

            currentBucket = bucketEnd;
        }
    }

    private static void AggregatePeriod15m(int nodeId, List<HealthRecord> records, ref int totalAggRows)
    {
        var span = TimeSpan.FromMinutes(15);
        var firstBucket = FloorToBucket(records.First().CheckedAt, span);
        var lastBucket = FloorToBucket(records.Last().CheckedAt, span);

        var latestAgg = FreeSqlDb.Orm.Select<HealthAggregate15m>()
            .Where(h => h.NodeId == nodeId)
            .OrderByDescending(h => h.BucketStart)
            .Limit(1)
            .ToOne();
        var currentBucket = latestAgg != null ? latestAgg.BucketStart : firstBucket;

        while (currentBucket <= lastBucket)
        {
            var bucketEnd = currentBucket.Add(span);
            var bucketRecords = records.Where(r => r.CheckedAt >= currentBucket && r.CheckedAt < bucketEnd).ToList();

            var avgResp = bucketRecords.Count == 0 ? 0 : bucketRecords.Average(h => h.ResponseTime);
            var avgConn = bucketRecords.Count == 0 ? 0 : bucketRecords.Average(h => h.ConnectionCount);
            var healthy = bucketRecords.Count == 0 ? 0 : bucketRecords.Count(e => string.Equals(e.Status, "Healthy", StringComparison.OrdinalIgnoreCase));
            var unhealthy = bucketRecords.Count == 0 ? 0 : bucketRecords.Count(e => string.Equals(e.Status, "Unhealthy", StringComparison.OrdinalIgnoreCase));

            if (latestAgg != null && currentBucket == latestAgg.BucketStart)
            {
                FreeSqlDb.Orm.Update<HealthAggregate15m>()
                    .Where(r => r.Id == latestAgg.Id)
                    .Set(r => r.AvgConnectionCount, avgConn)
                    .Set(r => r.AvgResponseTimeUs, avgResp)
                    .Set(r => r.HealthyCount, healthy)
                    .Set(r => r.UnhealthyCount, unhealthy)
                    .Set(r => r.SampleCount, bucketRecords.Count)
                    .ExecuteAffrows();

                Logger.Info("【汇总】更新 15 分钟聚合：节点={nodeId}，桶起点={bucket}，样本={samples}，健康={healthy}，不健康={unhealthy}，平均响应(us)={avgResp}，平均连接数={avgConn}", nodeId, currentBucket.ToString("o"), bucketRecords.Count, healthy, unhealthy, avgResp, avgConn);
            }
            else
            {
                FreeSqlDb.Orm.Insert(new HealthAggregate15m
                {
                    NodeId = nodeId,
                    BucketStart = currentBucket,
                    AvgResponseTimeUs = avgResp,
                    AvgConnectionCount = avgConn,
                    HealthyCount = healthy,
                    UnhealthyCount = unhealthy,
                    SampleCount = bucketRecords.Count,
                }).ExecuteAffrows();
                Logger.Info("【汇总】新增 15 分钟聚合：节点={nodeId}，桶起点={bucket}，样本={samples}，健康={healthy}，不健康={unhealthy}，平均响应(us)={avgResp}，平均连接数={avgConn}", nodeId, currentBucket.ToString("o"), bucketRecords.Count, healthy, unhealthy, avgResp, avgConn);
                totalAggRows++;
            }

            currentBucket = bucketEnd;
        }
    }

    private static void AggregatePeriod30m(int nodeId, List<HealthRecord> records, ref int totalAggRows)
    {
        var span = TimeSpan.FromMinutes(30);
        var firstBucket = FloorToBucket(records.First().CheckedAt, span);
        var lastBucket = FloorToBucket(records.Last().CheckedAt, span);

        var latestAgg = FreeSqlDb.Orm.Select<HealthAggregate30m>()
            .Where(h => h.NodeId == nodeId)
            .OrderByDescending(h => h.BucketStart)
            .Limit(1)
            .ToOne();
        var currentBucket = latestAgg != null ? latestAgg.BucketStart : firstBucket;

        while (currentBucket <= lastBucket)
        {
            var bucketEnd = currentBucket.Add(span);
            var bucketRecords = records.Where(r => r.CheckedAt >= currentBucket && r.CheckedAt < bucketEnd).ToList();

            var avgResp = bucketRecords.Count == 0 ? 0 : bucketRecords.Average(h => h.ResponseTime);
            var avgConn = bucketRecords.Count == 0 ? 0 : bucketRecords.Average(h => h.ConnectionCount);
            var healthy = bucketRecords.Count == 0 ? 0 : bucketRecords.Count(e => string.Equals(e.Status, "Healthy", StringComparison.OrdinalIgnoreCase));
            var unhealthy = bucketRecords.Count == 0 ? 0 : bucketRecords.Count(e => string.Equals(e.Status, "Unhealthy", StringComparison.OrdinalIgnoreCase));

            if (latestAgg != null && currentBucket == latestAgg.BucketStart)
            {
                FreeSqlDb.Orm.Update<HealthAggregate30m>()
                    .Where(r => r.Id == latestAgg.Id)
                    .Set(r => r.AvgConnectionCount, avgConn)
                    .Set(r => r.AvgResponseTimeUs, avgResp)
                    .Set(r => r.HealthyCount, healthy)
                    .Set(r => r.UnhealthyCount, unhealthy)
                    .Set(r => r.SampleCount, bucketRecords.Count)
                    .ExecuteAffrows();

                Logger.Info("【汇总】更新 30 分钟聚合：节点={nodeId}，桶起点={bucket}，样本={samples}，健康={healthy}，不健康={unhealthy}，平均响应(us)={avgResp}，平均连接数={avgConn}", nodeId, currentBucket.ToString("o"), bucketRecords.Count, healthy, unhealthy, avgResp, avgConn);
            }
            else
            {
                FreeSqlDb.Orm.Insert(new HealthAggregate30m
                {
                    NodeId = nodeId,
                    BucketStart = currentBucket,
                    AvgResponseTimeUs = avgResp,
                    AvgConnectionCount = avgConn,
                    HealthyCount = healthy,
                    UnhealthyCount = unhealthy,
                    SampleCount = bucketRecords.Count,
                }).ExecuteAffrows();
                Logger.Info("【汇总】新增 30 分钟聚合：节点={nodeId}，桶起点={bucket}，样本={samples}，健康={healthy}，不健康={unhealthy}，平均响应(us)={avgResp}，平均连接数={avgConn}", nodeId, currentBucket.ToString("o"), bucketRecords.Count, healthy, unhealthy, avgResp, avgConn);
                totalAggRows++;
            }

            currentBucket = bucketEnd;
        }
    }

    private static void AggregatePeriod1h(int nodeId, List<HealthRecord> records, ref int totalAggRows)
    {
        var span = TimeSpan.FromHours(1);
        var firstBucket = FloorToBucket(records.First().CheckedAt, span);
        var lastBucket = FloorToBucket(records.Last().CheckedAt, span);

        var latestAgg = FreeSqlDb.Orm.Select<HealthAggregate1h>()
            .Where(h => h.NodeId == nodeId)
            .OrderByDescending(h => h.BucketStart)
            .Limit(1)
            .ToOne();
        var currentBucket = latestAgg != null ? latestAgg.BucketStart : firstBucket;

        while (currentBucket <= lastBucket)
        {
            var bucketEnd = currentBucket.Add(span);
            var bucketRecords = records.Where(r => r.CheckedAt >= currentBucket && r.CheckedAt < bucketEnd).ToList();

            var avgResp = bucketRecords.Count == 0 ? 0 : bucketRecords.Average(h => h.ResponseTime);
            var avgConn = bucketRecords.Count == 0 ? 0 : bucketRecords.Average(h => h.ConnectionCount);
            var healthy = bucketRecords.Count == 0 ? 0 : bucketRecords.Count(e => string.Equals(e.Status, "Healthy", StringComparison.OrdinalIgnoreCase));
            var unhealthy = bucketRecords.Count == 0 ? 0 : bucketRecords.Count(e => string.Equals(e.Status, "Unhealthy", StringComparison.OrdinalIgnoreCase));

            if (latestAgg != null && currentBucket == latestAgg.BucketStart)
            {
                FreeSqlDb.Orm.Update<HealthAggregate1h>()
                    .Where(r => r.Id == latestAgg.Id)
                    .Set(r => r.AvgConnectionCount, avgConn)
                    .Set(r => r.AvgResponseTimeUs, avgResp)
                    .Set(r => r.HealthyCount, healthy)
                    .Set(r => r.UnhealthyCount, unhealthy)
                    .Set(r => r.SampleCount, bucketRecords.Count)
                    .ExecuteAffrows();

                Logger.Info("【汇总】更新 1 小时聚合：节点={nodeId}，桶起点={bucket}，样本={samples}，健康={healthy}，不健康={unhealthy}，平均响应(us)={avgResp}，平均连接数={avgConn}", nodeId, currentBucket.ToString("o"), bucketRecords.Count, healthy, unhealthy, avgResp, avgConn);
            }
            else
            {
                FreeSqlDb.Orm.Insert(new HealthAggregate1h
                {
                    NodeId = nodeId,
                    BucketStart = currentBucket,
                    AvgResponseTimeUs = avgResp,
                    AvgConnectionCount = avgConn,
                    HealthyCount = healthy,
                    UnhealthyCount = unhealthy,
                    SampleCount = bucketRecords.Count,
                }).ExecuteAffrows();
                Logger.Info("【汇总】新增 1 小时聚合：节点={nodeId}，桶起点={bucket}，样本={samples}，健康={healthy}，不健康={unhealthy}，平均响应(us)={avgResp}，平均连接数={avgConn}", nodeId, currentBucket.ToString("o"), bucketRecords.Count, healthy, unhealthy, avgResp, avgConn);
                totalAggRows++;
            }

            currentBucket = bucketEnd;
        }
    }

    private static void AggregatePeriod2h(int nodeId, List<HealthRecord> records, ref int totalAggRows)
    {
        var span = TimeSpan.FromHours(2);
        var firstBucket = FloorToBucket(records.First().CheckedAt, span);
        var lastBucket = FloorToBucket(records.Last().CheckedAt, span);

        var latestAgg = FreeSqlDb.Orm.Select<HealthAggregate2h>()
            .Where(h => h.NodeId == nodeId)
            .OrderByDescending(h => h.BucketStart)
            .Limit(1)
            .ToOne();
        var currentBucket = latestAgg != null ? latestAgg.BucketStart : firstBucket;

        while (currentBucket <= lastBucket)
        {
            var bucketEnd = currentBucket.Add(span);
            var bucketRecords = records.Where(r => r.CheckedAt >= currentBucket && r.CheckedAt < bucketEnd).ToList();

            var avgResp = bucketRecords.Count == 0 ? 0 : bucketRecords.Average(h => h.ResponseTime);
            var avgConn = bucketRecords.Count == 0 ? 0 : bucketRecords.Average(h => h.ConnectionCount);
            var healthy = bucketRecords.Count == 0 ? 0 : bucketRecords.Count(e => string.Equals(e.Status, "Healthy", StringComparison.OrdinalIgnoreCase));
            var unhealthy = bucketRecords.Count == 0 ? 0 : bucketRecords.Count(e => string.Equals(e.Status, "Unhealthy", StringComparison.OrdinalIgnoreCase));

            if (latestAgg != null && currentBucket == latestAgg.BucketStart)
            {
                FreeSqlDb.Orm.Update<HealthAggregate2h>()
                    .Where(r => r.Id == latestAgg.Id)
                    .Set(r => r.AvgConnectionCount, avgConn)
                    .Set(r => r.AvgResponseTimeUs, avgResp)
                    .Set(r => r.HealthyCount, healthy)
                    .Set(r => r.UnhealthyCount, unhealthy)
                    .Set(r => r.SampleCount, bucketRecords.Count)
                    .ExecuteAffrows();

                Logger.Info("【汇总】更新 2 小时聚合：节点={nodeId}，桶起点={bucket}，样本={samples}，健康={healthy}，不健康={unhealthy}，平均响应(us)={avgResp}，平均连接数={avgConn}", nodeId, currentBucket.ToString("o"), bucketRecords.Count, healthy, unhealthy, avgResp, avgConn);
            }
            else
            {
                FreeSqlDb.Orm.Insert(new HealthAggregate2h
                {
                    NodeId = nodeId,
                    BucketStart = currentBucket,
                    AvgResponseTimeUs = avgResp,
                    AvgConnectionCount = avgConn,
                    HealthyCount = healthy,
                    UnhealthyCount = unhealthy,
                    SampleCount = bucketRecords.Count,
                }).ExecuteAffrows();
                Logger.Info("【汇总】新增 2 小时聚合：节点={nodeId}，桶起点={bucket}，样本={samples}，健康={healthy}，不健康={unhealthy}，平均响应(us)={avgResp}，平均连接数={avgConn}", nodeId, currentBucket.ToString("o"), bucketRecords.Count, healthy, unhealthy, avgResp, avgConn);
                totalAggRows++;
            }

            currentBucket = bucketEnd;
        }
    }

    private static void AggregatePeriod6h(int nodeId, List<HealthRecord> records, ref int totalAggRows)
    {
        var span = TimeSpan.FromHours(6);
        var firstBucket = FloorToBucket(records.First().CheckedAt, span);
        var lastBucket = FloorToBucket(records.Last().CheckedAt, span);

        var latestAgg = FreeSqlDb.Orm.Select<HealthAggregate6h>()
            .Where(h => h.NodeId == nodeId)
            .OrderByDescending(h => h.BucketStart)
            .Limit(1)
            .ToOne();
        var currentBucket = latestAgg != null ? latestAgg.BucketStart : firstBucket;

        while (currentBucket <= lastBucket)
        {
            var bucketEnd = currentBucket.Add(span);
            var bucketRecords = records.Where(r => r.CheckedAt >= currentBucket && r.CheckedAt < bucketEnd).ToList();

            var avgResp = bucketRecords.Count == 0 ? 0 : bucketRecords.Average(h => h.ResponseTime);
            var avgConn = bucketRecords.Count == 0 ? 0 : bucketRecords.Average(h => h.ConnectionCount);
            var healthy = bucketRecords.Count == 0 ? 0 : bucketRecords.Count(e => string.Equals(e.Status, "Healthy", StringComparison.OrdinalIgnoreCase));
            var unhealthy = bucketRecords.Count == 0 ? 0 : bucketRecords.Count(e => string.Equals(e.Status, "Unhealthy", StringComparison.OrdinalIgnoreCase));

            if (latestAgg != null && currentBucket == latestAgg.BucketStart)
            {
                FreeSqlDb.Orm.Update<HealthAggregate6h>()
                    .Where(r => r.Id == latestAgg.Id)
                    .Set(r => r.AvgConnectionCount, avgConn)
                    .Set(r => r.AvgResponseTimeUs, avgResp)
                    .Set(r => r.HealthyCount, healthy)
                    .Set(r => r.UnhealthyCount, unhealthy)
                    .Set(r => r.SampleCount, bucketRecords.Count)
                    .ExecuteAffrows();

                Logger.Info("【汇总】更新 6 小时聚合：节点={nodeId}，桶起点={bucket}，样本={samples}，健康={healthy}，不健康={unhealthy}，平均响应(us)={avgResp}，平均连接数={avgConn}", nodeId, currentBucket.ToString("o"), bucketRecords.Count, healthy, unhealthy, avgResp, avgConn);
            }
            else
            {
                FreeSqlDb.Orm.Insert(new HealthAggregate6h
                {
                    NodeId = nodeId,
                    BucketStart = currentBucket,
                    AvgResponseTimeUs = avgResp,
                    AvgConnectionCount = avgConn,
                    HealthyCount = healthy,
                    UnhealthyCount = unhealthy,
                    SampleCount = bucketRecords.Count,
                }).ExecuteAffrows();
                Logger.Info("【汇总】新增 6 小时聚合：节点={nodeId}，桶起点={bucket}，样本={samples}，健康={healthy}，不健康={unhealthy}，平均响应(us)={avgResp}，平均连接数={avgConn}", nodeId, currentBucket.ToString("o"), bucketRecords.Count, healthy, unhealthy, avgResp, avgConn);
                totalAggRows++;
            }

            currentBucket = bucketEnd;
        }
    }

    private static void AggregatePeriod12h(int nodeId, List<HealthRecord> records, ref int totalAggRows)
    {
        var span = TimeSpan.FromHours(12);
        var firstBucket = FloorToBucket(records.First().CheckedAt, span);
        var lastBucket = FloorToBucket(records.Last().CheckedAt, span);

        var latestAgg = FreeSqlDb.Orm.Select<HealthAggregate12h>()
            .Where(h => h.NodeId == nodeId)
            .OrderByDescending(h => h.BucketStart)
            .Limit(1)
            .ToOne();
        var currentBucket = latestAgg != null ? latestAgg.BucketStart : firstBucket;

        while (currentBucket <= lastBucket)
        {
            var bucketEnd = currentBucket.Add(span);
            var bucketRecords = records.Where(r => r.CheckedAt >= currentBucket && r.CheckedAt < bucketEnd).ToList();

            var avgResp = bucketRecords.Count == 0 ? 0 : bucketRecords.Average(h => h.ResponseTime);
            var avgConn = bucketRecords.Count == 0 ? 0 : bucketRecords.Average(h => h.ConnectionCount);
            var healthy = bucketRecords.Count == 0 ? 0 : bucketRecords.Count(e => string.Equals(e.Status, "Healthy", StringComparison.OrdinalIgnoreCase));
            var unhealthy = bucketRecords.Count == 0 ? 0 : bucketRecords.Count(e => string.Equals(e.Status, "Unhealthy", StringComparison.OrdinalIgnoreCase));

            if (latestAgg != null && currentBucket == latestAgg.BucketStart)
            {
                FreeSqlDb.Orm.Update<HealthAggregate12h>()
                    .Where(r => r.Id == latestAgg.Id)
                    .Set(r => r.AvgConnectionCount, avgConn)
                    .Set(r => r.AvgResponseTimeUs, avgResp)
                    .Set(r => r.HealthyCount, healthy)
                    .Set(r => r.UnhealthyCount, unhealthy)
                    .Set(r => r.SampleCount, bucketRecords.Count)
                    .ExecuteAffrows();

                Logger.Info("【汇总】更新 12 小时聚合：节点={nodeId}，桶起点={bucket}，样本={samples}，健康={healthy}，不健康={unhealthy}，平均响应(us)={avgResp}，平均连接数={avgConn}", nodeId, currentBucket.ToString("o"), bucketRecords.Count, healthy, unhealthy, avgResp, avgConn);
            }
            else
            {
                FreeSqlDb.Orm.Insert(new HealthAggregate12h
                {
                    NodeId = nodeId,
                    BucketStart = currentBucket,
                    AvgResponseTimeUs = avgResp,
                    AvgConnectionCount = avgConn,
                    HealthyCount = healthy,
                    UnhealthyCount = unhealthy,
                    SampleCount = bucketRecords.Count,
                }).ExecuteAffrows();
                Logger.Info("【汇总】新增 12 小时聚合：节点={nodeId}，桶起点={bucket}，样本={samples}，健康={healthy}，不健康={unhealthy}，平均响应(us)={avgResp}，平均连接数={avgConn}", nodeId, currentBucket.ToString("o"), bucketRecords.Count, healthy, unhealthy, avgResp, avgConn);
                totalAggRows++;
            }

            currentBucket = bucketEnd;
        }
    }

    private static DateTime FloorToBucket(DateTime dt, TimeSpan period)
    {
        var ticks = (dt.Ticks / period.Ticks) * period.Ticks;
        return new DateTime(ticks, DateTimeKind.Utc);
    }
}
