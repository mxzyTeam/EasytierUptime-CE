using FreeSql.DataAnnotations;

namespace EasytierUptime_Entities.Entities;

[Table(Name = "node_tags")]
public class NodeTag
{
    [Column(IsIdentity = true, IsPrimary = true, Name = "id")]
    public int Id { get; set; }
    [Column(Name = "node_id")]
    public int NodeId { get; set; }
    [Column(StringLength = 64, IsNullable = false, Name = "tag")]
    public string Tag { get; set; } = string.Empty;
    [Column(Name = "created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
