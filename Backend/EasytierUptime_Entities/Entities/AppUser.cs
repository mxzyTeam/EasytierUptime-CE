using FreeSql.DataAnnotations;

namespace EasytierUptime_Entities.Entities;

[Table(Name = "app_users")]
[Index("uk_username", nameof(Username), true)]
[Index("uk_email", nameof(Email), true)]
public class AppUser
{
    [Column(IsIdentity = true, IsPrimary = true, Name = "id")]
    public int Id { get; set; }
    [Column(StringLength = 64, IsNullable = false, Name = "username")]
    public string Username { get; set; } = string.Empty;
    [Column(StringLength = 512, IsNullable = false, Name = "password_hash")]
    public string PasswordHash { get; set; } = string.Empty;
    [Column(StringLength = 32, IsNullable = false, Name = "role")]
    public string Role { get; set; } = "user";
    [Column(StringLength = 256, IsNullable = true, Name = "email")]
    public string? Email { get; set; }
    [Column(Name = "email_verified")]
    public bool EmailVerified { get; set; }
    [Column(StringLength = 256, IsNullable = true, Name = "email_verify_token")]
    public string? EmailVerifyToken { get; set; }
    [Column(IsNullable = true, Name = "email_verify_expires")]
    public DateTime? EmailVerifyExpires { get; set; }
    [Column(Name = "created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
