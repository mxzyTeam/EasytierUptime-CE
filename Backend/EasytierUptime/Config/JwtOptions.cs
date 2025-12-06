namespace EasytierUptime.Config;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = "easytier-uptime";
    public string Audience { get; set; } = "easytier-uptime-clients";
    // Must be at least 32 bytes for HS256; replace in appsettings.json in production
    public string Key { get; set; } = "dev-secret-change-me-0123456789abcdef-0123456789abcdef"; // 56+ chars
    public int ExpireMinutes { get; set; } = 240;
}
