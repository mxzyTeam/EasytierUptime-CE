namespace EasytierUptime.Config;

/// <summary>
/// Database provider options. Provider: Sqlite | MySql
/// </summary>
public sealed class DatabaseOptions
{
    public string Provider { get; set; } = "Sqlite";
    public string? ConnectionString { get; set; }
}
