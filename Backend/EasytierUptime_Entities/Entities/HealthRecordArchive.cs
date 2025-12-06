using FreeSql.DataAnnotations;

namespace EasytierUptime_Entities.Entities;

[Table(Name = "health_records_archive")]
public class HealthRecordArchive
{
    [Column(IsIdentity = true, IsPrimary = true, Name = "id")]
    public int Id { get; set; }

    [Column(Name = "node_id")]
    public int NodeId { get; set; }

    [Column(StringLength = 32, IsNullable = false, Name = "status")]
    public string Status { get; set; } = "Unknown";

    [Column(Name = "response_time")]
    public int ResponseTime { get; set; }

    [Column(StringLength = -1, Name = "error_message")]
    public string ErrorMessage { get; set; } = string.Empty;

    [Column(Name = "connection_count")]
    public int ConnectionCount { get; set; }

    [Column(Name = "checked_at")]
    public DateTime CheckedAt { get; set; }

    [Column(Name = "archived_at")]
    public DateTime ArchivedAt { get; set; } = DateTime.UtcNow;
}
