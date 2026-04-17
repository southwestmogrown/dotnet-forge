using Core.Interfaces;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace Infrastructure.Services.Notifications;

public class EmailNotificationChannel : INotificationChannel
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailNotificationChannel> _logger;

    public string ChannelType => "email";

    public EmailNotificationChannel(IConfiguration config, ILogger<EmailNotificationChannel> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task SendAsync(AlertEvent alert, CancellationToken ct = default)
    {
        var smtpHost = _config["Notifications:Email:SmtpHost"];
        var smtpPort = int.TryParse(_config["Notifications:Email:SmtpPort"], out var p) ? p : 587;
        var username = _config["Notifications:Email:Username"];
        var password = _config["Notifications:Email:Password"];
        var from = _config["Notifications:Email:From"];
        var to = _config["Notifications:Email:To"];

        if (string.IsNullOrEmpty(smtpHost) || string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to))
        {
            _logger.LogWarning("Email notification channel is not configured — skipping.");
            return;
        }

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(from));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = $"Alert: {alert.TagAddress} triggered";
        message.Body = new TextPart("plain") { Text = alert.Message };

        using var client = new SmtpClient();
        await client.ConnectAsync(smtpHost, smtpPort, MailKit.Security.SecureSocketOptions.StartTls, ct);

        if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            await client.AuthenticateAsync(username, password, ct);

        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);
    }
}
