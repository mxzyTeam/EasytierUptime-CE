using FreeSql.DataAnnotations;

namespace EasytierUptime_Entities.Entities;

[Table(Name = "health_aggregates")]
public class HealthAggregate
{
    [Column(IsIdentity = true, IsPrimary = true, Name = "id")]
    public int Id { get; set; }

    [Column(Name = "node_id")]
    public int NodeId { get; set; }

    // Bucket start time in UTC
    [Column(Name = "bucket_start")]
    public DateTime BucketStart { get; set; }

    // Aggregation period label: "1m", "15m", "30m", "1h", "2h", "6h", "12h", "24h"
    [Column(StringLength = 8, Name = "period")]
    public string Period { get; set; } = "1m";

    // Number of underlying samples
    [Column(Name = "sample_count")]
    public int SampleCount { get; set; }

    // Healthy samples count
    [Column(Name = "healthy_count")]
    public int HealthyCount { get; set; }

    // Unhealthy samples count
    [Column(Name = "unhealthy_count")]
    public int UnhealthyCount { get; set; }

    // Avg response time (us)
    [Column(Name = "avg_response_time_us")]
    public double AvgResponseTimeUs { get; set; }

    // Avg connection count
    [Column(Name = "avg_connection_count")]
    public double AvgConnectionCount { get; set; }
}
