using EasytierUptime.Data;
using EasytierUptime.Services;
using EasytierUptime.Config;
using EasytierUptime_Entities.Entities;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Security.Cryptography;
using Lazy.Captcha.Core;

internal class Program
{
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
        builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Smtp"));
        builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
        builder.Services.AddSingleton<NodeService>();
        // Register ProbeService so controllers can inject it
        builder.Services.AddSingleton<ProbeService>();

        // captcha service (use defaults; can be configured via appsettings 'Captcha' section later)
        builder.Services.AddCaptcha(builder.Configuration);

        // Initialize database (SQLite by default if not specified)
        var dbOptions = builder.Configuration.GetSection("Database").Get<DatabaseOptions>() ?? new DatabaseOptions();
        FreeSqlDb.Initialize(dbOptions);

        // 使用控制器 + 统一 snake_case（输入/输出）
        builder.Services.AddControllers().AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
            options.JsonSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower;
        });

        // 认证/授权
        var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key));
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = key,
                    ClockSkew = TimeSpan.FromMinutes(1)
                };
            });
        builder.Services.AddAuthorization();

        // JSON 输出统一 snake_case（Minimal APIs）
        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
            options.SerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower;
        });

        builder.Services.AddEndpointsApiExplorer();

        var app = builder.Build();

        // 首次启动自动创建默认管理员（用户名：admin，密码：来自环境变量 EASYTIER_ADMIN_PWD 或默认为 admin）
        try { SeedDefaultAdmin(); } catch (Exception se) { Console.Error.WriteLine($"Admin seed error: {se.Message}"); }

        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.UseAuthentication();
        app.UseAuthorization();

        // 控制器：节点管理等
        app.MapControllers();

        app.Run();
    }

    private static void SeedDefaultAdmin()
    {
        // 确保表已创建（触发 FreeSql 初始化）
        _ = FreeSqlDb.Orm;
        var hasUser = FreeSqlDb.Orm.Select<AppUser>().Any();
        if (hasUser) return;

        var pwd = Environment.GetEnvironmentVariable("EASYTIER_ADMIN_PWD") ?? "admin";
        var user = new AppUser
        {
            Username = "admin",
            PasswordHash = HashPassword(pwd),
            Role = "admin",
            Email = null,
            EmailVerified = false
        };
        FreeSqlDb.Orm.Insert(user).ExecuteAffrows();
        Console.WriteLine("Seeded default admin user (username=admin). Change password immediately.");
    }

    private static string HashPassword(string password)
    {
        const int iter = 100_000;
        const int saltSize = 16;
        const int keySize = 32;
        Span<byte> salt = stackalloc byte[saltSize];
        RandomNumberGenerator.Fill(salt);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt.ToArray(), iter, HashAlgorithmName.SHA256, keySize);
        return $"1${iter}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }
}