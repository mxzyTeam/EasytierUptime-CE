using EasytierUptime.Config;
using EasytierUptime.DTOs;
using EasytierUptime_Entities.Entities;
using EasytierUptime.Data;
using EasytierUptime.Services;
using Lazy.Captcha.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace EasytierUptime.Controllers;

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    // 邮箱格式校验的正则表达式（忽略大小写）
    private static readonly Regex EmailRegex = new("^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly IOptions<JwtOptions> _jwtOptions; // JWT 配置
    private readonly ICaptcha _captcha;                // 图形验证码服务
    private readonly IEmailSender _emailSender;        // 邮件发送服务

    // 通过依赖注入获取所需服务
    public AuthController(IOptions<JwtOptions> jwtOptions, ICaptcha captcha, IEmailSender emailSender)
    {
        _jwtOptions = jwtOptions;
        _captcha = captcha;
        _emailSender = emailSender;
    }

    [HttpGet("/health")] // 保持原始绝对路径 /health
    public IActionResult Health() => Ok(new { ok = true });

    [HttpGet("captcha")] // GET /auth/captcha
    public IActionResult GetCaptcha()
    {
        // 生成图形验证码，并尽量兼容不同库的返回属性名
        var captchaId = Guid.NewGuid().ToString("N");
        var data = _captcha.Generate(captchaId);
        string imgBase64 = string.Empty;
        var base64PropNames = new[] { "ImageBase64", "ImageBase64Data", "Base64", "ImgBase64" };
        foreach (var name in base64PropNames)
        {
            var p = data.GetType().GetProperty(name);
            if (p != null)
            {
                var val = p.GetValue(data)?.ToString();
                if (!string.IsNullOrEmpty(val)) { imgBase64 = val; break; }
            }
        }
        if (string.IsNullOrEmpty(imgBase64))
        {
            var bytesProp = data.GetType().GetProperty("Bytes") ?? data.GetType().GetProperty("ImageBytes");
            if (bytesProp != null && bytesProp.GetValue(data) is byte[] bytes && bytes.Length > 0)
            {
                imgBase64 = "data:image/png;base64," + Convert.ToBase64String(bytes);
            }
        }
        return Ok(new CaptchaCreateResponse(captchaId, imgBase64));
    }

    [HttpPost("register")] // POST /auth/register（禁用）
    public IActionResult RegisterDisabled() => StatusCode(403);

    [HttpPost("register_email")] // POST /auth/register_email（禁用）
    public IActionResult RegisterEmailDisabled() => StatusCode(403);

    [HttpPost("resend_verification")] // POST /auth/resend_verification（禁用）
    public IActionResult ResendVerificationDisabled() => StatusCode(403);

    [HttpPost("send_code")] // POST /auth/send_code：发送邮箱验证码
    public async Task<IActionResult> SendCode([FromBody] SendCodeRequest req)
    {
        // 基本校验：邮箱、图形验证码
        if (string.IsNullOrWhiteSpace(req.Email) || !EmailRegex.IsMatch(req.Email))
            return BadRequest(new { message = "邮箱格式不正确" });
        if (string.IsNullOrWhiteSpace(req.CaptchaId) || string.IsNullOrWhiteSpace(req.CaptchaCode))
            return BadRequest(new { message = "缺少验证码参数" });
        if (!_captcha.Validate(req.CaptchaId, req.CaptchaCode))
            return BadRequest(new { message = "图形验证码错误" });

        // 邮箱是否已被注册
        var existsUser = await FreeSqlDb.Orm.Select<AppUser>().Where(x => x.Email == req.Email).AnyAsync();
        if (existsUser) return BadRequest(new { message = "邮箱已被使用" });

        // 生成 6 位验证码，有效期 10 分钟，并发送邮件
        var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        var rec = new EmailVerificationCode { Email = req.Email, Code = code, ExpiresAt = DateTime.UtcNow.AddMinutes(10) };
        await FreeSqlDb.Orm.Insert(rec).ExecuteAffrowsAsync();
        try
        {
            await _emailSender.SendAsync(req.Email, "验证码", $"<p>你的注册验证码是：<b>{code}</b></p><p>10 分钟内有效。</p>");
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        return Ok(new { ok = true });
    }

    [HttpPost("register_with_code")] // POST /auth/register_with_code：使用邮箱验证码注册
    public async Task<IActionResult> RegisterWithCode([FromBody] RegisterWithCodeRequest req)
    {
        // 基本参数校验
        if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password) || string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Code))
            return BadRequest(new { message = "用户名/密码/邮箱/验证码必填" });
        if (!EmailRegex.IsMatch(req.Email)) return BadRequest(new { message = "邮箱格式不正确" });

        // 用户名或邮箱是否已存在
        var exists = await FreeSqlDb.Orm.Select<AppUser>().Where(x => x.Username == req.Username || x.Email == req.Email).AnyAsync();
        if (exists) return BadRequest(new { message = "用户名或邮箱已存在" });

        // 验证码是否有效
        var now = DateTime.UtcNow;
        var codeRec = await FreeSqlDb.Orm.Select<EmailVerificationCode>()
            .Where(x => x.Email == req.Email && x.ExpiresAt >= now)
            .OrderByDescending(x => x.CreatedAt)
            .FirstAsync();
        if (codeRec is null || !string.Equals(codeRec.Code, req.Code, StringComparison.Ordinal))
            return BadRequest(new { message = "验证码无效或已过期" });

        // 创建用户，密码哈希存储，标记邮箱已验证，清理验证码记录
        var user = new AppUser
        {
            Username = req.Username,
            PasswordHash = PasswordHasher.Hash(req.Password),
            Role = "user",
            Email = req.Email,
            EmailVerified = true,
            EmailVerifyToken = null,
            EmailVerifyExpires = null
        };
        await FreeSqlDb.Orm.Insert(user).ExecuteAffrowsAsync();
        await FreeSqlDb.Orm.Delete<EmailVerificationCode>().Where(x => x.Email == req.Email).ExecuteAffrowsAsync();

        return Ok(new { ok = true });
    }

    [HttpGet("verify")] // GET /auth/verify?token=...&u=...：邮箱验证链接
    public async Task<IActionResult> Verify([FromQuery] string token, [FromQuery] string u)
    {
        var user = await FreeSqlDb.Orm.Select<AppUser>().Where(x => x.Username == u).FirstAsync();
        if (user is null) return NotFound();
        if (user.EmailVerified) return Ok(new { ok = true, message = "已验证" });
        if (user.EmailVerifyToken != token || user.EmailVerifyExpires < DateTime.UtcNow)
            return BadRequest(new { message = "链接无效或已过期" });
        user.EmailVerified = true; user.EmailVerifyToken = null; user.EmailVerifyExpires = null;
        await FreeSqlDb.Orm.Update<AppUser>().SetSource(user).ExecuteAffrowsAsync();
        return Ok(new { ok = true });
    }

    [HttpPost("login")] // POST /auth/login：登录并返回 JWT
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        // 图形验证码校验
        if (string.IsNullOrWhiteSpace(req.CaptchaId) || string.IsNullOrWhiteSpace(req.CaptchaCode) || !_captcha.Validate(req.CaptchaId, req.CaptchaCode))
            return BadRequest(new { message = "图形验证码错误" });

        // 用户名密码校验
        var user = await FreeSqlDb.Orm.Select<AppUser>().Where(x => x.Username == req.Username).FirstAsync();
        if (user is null || !PasswordHasher.Verify(req.Password, user.PasswordHash)) return Unauthorized();

        // 生成 JWT 并返回
        var jwtv = _jwtOptions.Value;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtv.Key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddMinutes(jwtv.ExpireMinutes);
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.Role)
        };
        if (!string.IsNullOrWhiteSpace(user.Email)) claims.Add(new Claim(ClaimTypes.Email, user.Email));
        if (user.EmailVerified) claims.Add(new Claim("email_verified", "true"));

        var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
            issuer: jwtv.Issuer,
            audience: jwtv.Audience,
            claims: claims,
            expires: expires,
            signingCredentials: creds
        );
        var tokenStr = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token);
        return Ok(new { access_token = tokenStr, expires_at = expires, role = user.Role });
    }
}
