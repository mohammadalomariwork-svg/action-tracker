namespace ActionTracker.Application.Common;

public interface IEmailSender
{
    Task SendEmailAsync(
        string templateKey,
        Dictionary<string, string> placeholders,
        List<string> recipientEmails,
        string? relatedEntityType = null,
        Guid? relatedEntityId = null,
        string? triggeredByUserId = null);
}
