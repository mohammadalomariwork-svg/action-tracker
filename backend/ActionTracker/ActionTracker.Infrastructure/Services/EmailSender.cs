using System.Text.RegularExpressions;
using ActionTracker.Application.Common;
using ActionTracker.Application.Common.Interfaces;
using ActionTracker.Domain.Entities;
using MailKit.Net.Smtp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace ActionTracker.Infrastructure.Services;

public partial class EmailSender : IEmailSender
{
    private readonly IAppDbContext _db;
    private readonly SmtpSettings _smtp;
    private readonly ILogger<EmailSender> _logger;

    public EmailSender(
        IAppDbContext db,
        IOptions<SmtpSettings> smtp,
        ILogger<EmailSender> logger)
    {
        _db     = db;
        _smtp   = smtp.Value;
        _logger = logger;
    }

    public async Task SendEmailAsync(
        string templateKey,
        Dictionary<string, string> placeholders,
        List<string> recipientEmails,
        string? relatedEntityType = null,
        Guid? relatedEntityId = null,
        string? triggeredByUserId = null)
    {
        var template = await _db.EmailTemplates
            .FirstOrDefaultAsync(t => t.TemplateKey == templateKey);

        if (template is null)
        {
            _logger.LogWarning("Email template '{TemplateKey}' not found. Skipping email.", templateKey);
            return;
        }

        if (!template.IsActive)
        {
            _logger.LogWarning("Email template '{TemplateKey}' is inactive. Skipping email.", templateKey);
            return;
        }

        var resolvedSubject = ResolvePlaceholders(template.Subject, placeholders);
        var resolvedBody    = ResolvePlaceholders(template.HtmlBody, placeholders);
        var wrappedBody     = WrapInLayout(resolvedBody);
        var toEmail         = string.Join(", ", recipientEmails);

        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_smtp.FromName, _smtp.FromEmail));

            foreach (var email in recipientEmails)
                message.To.Add(MailboxAddress.Parse(email));

            message.Subject = resolvedSubject;
            message.Body = new TextPart("html") { Text = wrappedBody };

            using var client = new SmtpClient();
            client.ServerCertificateValidationCallback = (_, _, _, _) => true;
            await client.ConnectAsync(_smtp.Host, _smtp.Port, _smtp.UseSsl
                ? MailKit.Security.SecureSocketOptions.StartTls
                : MailKit.Security.SecureSocketOptions.Auto);

            if (!string.IsNullOrWhiteSpace(_smtp.Username))
                await client.AuthenticateAsync(_smtp.Username, _smtp.Password);

            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            _db.EmailLogs.Add(new EmailLog
            {
                Id                = Guid.NewGuid(),
                TemplateKey       = templateKey,
                ToEmail           = toEmail,
                Subject           = resolvedSubject,
                SentAt            = DateTime.UtcNow,
                Status            = "Sent",
                RelatedEntityType = relatedEntityType,
                RelatedEntityId   = relatedEntityId,
                SentByUserId      = triggeredByUserId,
            });
            await _db.SaveChangesAsync();

            _logger.LogInformation("Email sent via template '{TemplateKey}' to {Recipients}.",
                templateKey, toEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email via template '{TemplateKey}' to {Recipients}.",
                templateKey, toEmail);

            _db.EmailLogs.Add(new EmailLog
            {
                Id                = Guid.NewGuid(),
                TemplateKey       = templateKey,
                ToEmail           = toEmail,
                Subject           = resolvedSubject,
                SentAt            = DateTime.UtcNow,
                Status            = "Failed",
                ErrorMessage      = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message,
                RelatedEntityType = relatedEntityType,
                RelatedEntityId   = relatedEntityId,
                SentByUserId      = triggeredByUserId,
            });

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (Exception logEx)
            {
                _logger.LogError(logEx, "Failed to save email failure log for template '{TemplateKey}'.", templateKey);
            }
        }
    }

    private static string ResolvePlaceholders(string text, Dictionary<string, string> placeholders)
    {
        foreach (var (key, value) in placeholders)
            text = text.Replace($"{{{{{key}}}}}", value);

        // Remove any remaining unreplaced placeholders
        text = PlaceholderRegex().Replace(text, string.Empty);
        return text;
    }

    [GeneratedRegex(@"\{\{[^}]+\}\}")]
    private static partial Regex PlaceholderRegex();

    private static string WrapInLayout(string bodyContent)
    {
        return $"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
                <meta charset="UTF-8" />
                <meta name="viewport" content="width=device-width, initial-scale=1.0" />
                <title>KU Action Tracker</title>
            </head>
            <body style="margin:0; padding:0; background-color:#f4f5f7; font-family: Arial, sans-serif;">
                <table width="100%" cellpadding="0" cellspacing="0" style="background-color:#f4f5f7; padding:20px 0;">
                    <tr>
                        <td align="center">
                            <table width="600" cellpadding="0" cellspacing="0" style="background-color:#ffffff; border-radius:8px; overflow:hidden;">
                                <!-- Header -->
                                <tr>
                                    <td style="background-color:#003366; padding:20px 30px; text-align:center;">
                                        <h1 style="margin:0; color:#ffffff; font-size:22px;">KU Action Tracker</h1>
                                    </td>
                                </tr>
                                <!-- Content -->
                                <tr>
                                    <td style="padding:30px;">
                                        {bodyContent}
                                    </td>
                                </tr>
                                <!-- Footer -->
                                <tr>
                                    <td style="background-color:#f0f0f0; padding:15px 30px; text-align:center; font-size:12px; color:#888888;">
                                        This is an automated message from KU Action Tracker. Please do not reply.
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>
                </table>
            </body>
            </html>
            """;
    }
}
