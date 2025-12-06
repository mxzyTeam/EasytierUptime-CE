using FreeSql.DataAnnotations;

namespace EasytierUptime_Entities.Entities;

[Table(Name = "connection_instances")]
[Index("uk_instance_id", nameof(InstanceId), true)]
public class ConnectionInstance
{
    [Column(IsIdentity = true, IsPrimary = true, Name = "id")]
    public int Id { get; set; }
    [Column(Name = "node_id")]
    public int NodeId { get; set; }
    [Column(StringLength = 128, IsNullable = false, Name = "instance_id")]
    public string InstanceId { get; set; } = string.Empty;
    [Column(StringLength = 32, IsNullable = false, Name = "status")]
    public string Status { get; set; } = string.Empty;
    [Column(StringLength = -1, Name = "config")]
    public string Config { get; set; } = string.Empty;
    [Column(Name = "started_at")]
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    [Column(Name = "stopped_at")]
    public DateTime StoppedAt { get; set; } = DateTime.UtcNow;
}
