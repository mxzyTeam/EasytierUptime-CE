using System;
using FreeSql.DataAnnotations;

namespace EasytierUptime_Entities.Entities;

/// <summary>
/// 表示单次健康检查的原始记录实体。
/// </summary>
[Table(Name = "health_records")]
public class HealthRecord
{
    /// <summary>记录的唯一标识。</summary>
    [Column(IsIdentity = true, IsPrimary = true, Name = "id")]
    public int Id { get; set; }

    /// <summary>归属的节点标识。</summary>
    [Column(Name = "node_id")]
    public int NodeId { get; set; }

    /// <summary>检查结果状态。</summary>
    [Column(StringLength = 32, IsNullable = false, Name = "status")]
    public string Status { get; set; } = "Unknown";

    /// <summary>响应时间（毫秒）。</summary>
    [Column(Name = "response_time")]
    public int ResponseTime { get; set; }

    /// <summary>错误信息。</summary>
    [Column(StringLength = -1, Name = "error_message")]
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>连接数量。</summary>
    [Column(Name = "connection_count")]
    public int ConnectionCount { get; set; }

    /// <summary>检查发生时间（UTC）。</summary>
    [Column(Name = "checked_at")]
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
}
