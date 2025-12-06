using FreeSql.DataAnnotations;

namespace EasytierUptime_Entities.Entities;

[Table(Name = "shared_nodes")]
public class SharedNode
{
    [Column(IsIdentity = true, IsPrimary = true, Name = "id")]
    public int Id { get; set; }
    [Column(StringLength = 128, IsNullable = false, Name = "name")]
    public string Name { get; set; } = string.Empty;
    [Column(StringLength = 256, IsNullable = false, Name = "host")]
    public string Host { get; set; } = string.Empty;
    [Column(Name = "port")]
    public int Port { get; set; }
    [Column(StringLength = 64, IsNullable = false, Name = "protocol")]
    public string Protocol { get; set; } = string.Empty;
    [Column(StringLength = 64, IsNullable = false, Name = "version")]
    public string Version { get; set; } = string.Empty;
    [Column(Name = "allow_relay")]
    public bool AllowRelay { get; set; }
    [Column(StringLength = 128, IsNullable = false, Name = "network_name")]
    public string NetworkName { get; set; } = string.Empty;
    [Column(StringLength = 256, IsNullable = false, Name = "network_secret")]
    public string NetworkSecret { get; set; } = string.Empty;
    [Column(StringLength = -1, Name = "description")]
    public string Description { get; set; } = string.Empty;
    [Column(Name = "max_connections")]
    public int MaxConnections { get; set; }
    [Column(Name = "current_connections")]
    public int CurrentConnections { get; set; }
    [Column(Name = "is_active")]
    public bool IsActive { get; set; }
    [Column(Name = "is_approved")]
    public bool IsApproved { get; set; }
    [Column(StringLength = 64, Name = "qq_number")]
    public string QqNumber { get; set; } = string.Empty;
    [Column(StringLength = 64, Name = "wechat")]
    public string Wechat { get; set; } = string.Empty;
    [Column(StringLength = 128, Name = "mail")]
    public string Mail { get; set; } = string.Empty;
    [Column(Name = "created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [Column(Name = "updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    [Column(StringLength = 128, Name = "owner")]
    public string Owner { get; set; } = string.Empty;
    [Column(Name = "is_public")]
    public bool IsPublic { get; set; }

    // NEW: denormalized latest health summary for fast reads
    [Column(Name = "last_checked_at", IsNullable = true)]
    public DateTime? LastCheckedAt { get; set; }
    [Column(Name = "last_response_time_us")]
    public long LastResponseTimeUs { get; set; }
    [Column(StringLength = 32, Name = "current_health_status")]
    public string CurrentHealthStatus { get; set; } = string.Empty;
    [Column(Name = "current_connection_count")]
    public int CurrentConnectionCount { get; set; }
    [Column(Name = "health_percentage_24h")]
    public double HealthPercentage24h { get; set; }
}
