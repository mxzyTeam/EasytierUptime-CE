using FreeSql.DataAnnotations;

namespace EasytierUptime_Entities.Entities;

[Table(Name = "email_verification_codes")]
[Index("idx_email_created", nameof(Email), false)]
public class EmailVerificationCode
{
    [Column(IsIdentity = true, IsPrimary = true, Name = "id")]
    public int Id { get; set; }
    [Column(StringLength = 256, IsNullable = false, Name = "email")]
    public string Email { get; set; } = string.Empty;
    [Column(StringLength = 16, IsNullable = false, Name = "code")]
    public string Code { get; set; } = string.Empty;
    [Column(Name = "expires_at")]
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddMinutes(10);
    [Column(Name = "created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
