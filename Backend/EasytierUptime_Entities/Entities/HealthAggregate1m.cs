using System;
using FreeSql.DataAnnotations;

namespace EasytierUptime_Entities.Entities;

/// <summary>
/// 表示节点的 1 分钟健康数据聚合实体。
/// </summary>
[Table(Name = "health_agg_1m")]
public class HealthAggregate1m
{
    /// <summary>主键 ID。</summary>
    [Column(IsIdentity = true, IsPrimary = true, Name = "id")]
    public int Id { get; set; }

    /// <summary>节点 ID。</summary>
    [Column(Name = "node_id")]
    public int NodeId { get; set; }

    /// <summary>时间桶起始时间（UTC）。</summary>
    [Column(Name = "bucket_start")]
    public DateTime BucketStart { get; set; }

    /// <summary>采样总数。</summary>
    [Column(Name = "sample_count")]
    public int SampleCount { get; set; }

    /// <summary>健康样本数量。</summary>
    [Column(Name = "healthy_count")]
    public int HealthyCount { get; set; }

    /// <summary>不健康样本数量。</summary>
    [Column(Name = "unhealthy_count")]
    public int UnhealthyCount { get; set; }

    /// <summary>平均响应时间（微秒）。</summary>
    [Column(Name = "avg_response_time_us")]
    public double AvgResponseTimeUs { get; set; }

    /// <summary>平均连接数。</summary>
    [Column(Name = "avg_connection_count")]
    public double AvgConnectionCount { get; set; }
}
