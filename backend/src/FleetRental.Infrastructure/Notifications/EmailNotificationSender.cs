using System.Net;
using System.Net.Mail;
using FleetRental.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FleetRental.Infrastructure.Notifications;

/// <summary>
/// Email channel for booking decisions (feature 6).
/// </summary>
/// <remarks>
/// Falls back to logging when <see cref="EmailOptions.Enabled"/> is false, so the
/// approve/reject flow is observable without SMTP credentials configured. That
/// keeps a fresh clone runnable rather than failing at the last step of the demo.
/// </remarks>
public class EmailNotificationSender(
    IOptions<EmailOptions> options,
    ILogger<EmailNotificationSender> logger) : INotificationSender
{
    private readonly EmailOptions _options = options.Value;

    public string Channel => "Email";

    public async Task SendAsync(NotificationMessage message, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation(
                "[Email disabled] Would send to {Recipient}\nSubject: {Subject}\n{Body}",
                message.RecipientEmail, message.Subject, message.Body);
            return;
        }

        using var client = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.UseStartTls,
            Credentials = new NetworkCredential(_options.Username, _options.Password),
        };

        using var mail = new MailMessage
        {
            From = new MailAddress(_options.FromAddress, _options.FromName),
            Subject = message.Subject,
            Body = message.Body,
            IsBodyHtml = false,
        };

        mail.To.Add(message.RecipientEmail);

        await client.SendMailAsync(mail, cancellationToken);
    }
}
