using EasytierUptime.Config;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;

namespace EasytierUptime.Services;

/// <summary>
/// 电子邮件发送服务接口。
/// </summary>
public interface IEmailSender
{
    /// <summary>
    /// 发送电子邮件。
    /// </summary>
    Task SendAsync(string to, string subject, string body, CancellationToken ct = default);
}

/// <summary>
/// 基于 SMTP 的邮件发送实现。
/// </summary>
public class SmtpEmailSender : IEmailSender
{
    private readonly SmtpOptions _opt;
    public SmtpEmailSender(IOptions<SmtpOptions> opt) => _opt = opt.Value;

    /// <summary>
    /// 发送电子邮件。
    /// </summary>
    public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_opt.Host)) throw new InvalidOperationException("SMTP not configured");
        using var client = new SmtpClient(_opt.Host, _opt.Port)
        {
            EnableSsl = _opt.EnableSsl,
            Credentials = new NetworkCredential(_opt.Username, _opt.Password)
        };
        using var msg = new MailMessage()
        {
            From = new MailAddress(_opt.FromAddress, _opt.FromDisplayName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };
        msg.To.Add(to);
        await client.SendMailAsync(msg, ct);
    }
}
