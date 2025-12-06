using FreeSql.DataAnnotations;

namespace EasytierUptime_Entities.Entities;

[Table(Name = "health_agg_6h")]
public class HealthAggregate6h
{
    [Column(IsIdentity = true, IsPrimary = true, Name = "id")]
    public int Id { get; set; }
    [Column(Name = "node_id")] public int NodeId { get; set; }
    [Column(Name = "bucket_start")] public DateTime BucketStart { get; set; }
    [Column(Name = "sample_count")] public int SampleCount { get; set; }
    [Column(Name = "healthy_count")] public int HealthyCount { get; set; }
    [Column(Name = "unhealthy_count")] public int UnhealthyCount { get; set; }
    [Column(Name = "avg_response_time_us")] public double AvgResponseTimeUs { get; set; }
    [Column(Name = "avg_connection_count")] public double AvgConnectionCount { get; set; }
}
