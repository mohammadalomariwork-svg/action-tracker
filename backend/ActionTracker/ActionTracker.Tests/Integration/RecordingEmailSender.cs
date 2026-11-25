using System.Collections.Concurrent;
using ActionTracker.Application.Common;

namespace ActionTracker.Tests.Integration;

/// <summary>
/// The integration host's <see cref="IEmailSender"/> (WO-2026-11 A-3, fix 2):
/// it never opens an SMTP connection or reads the mail-server settings, and
/// records every e-mail the app asks it to send so a test can assert on it.
/// Registered as a singleton by <see cref="ActionTrackerWebApplicationFactory"/>,
/// so e-mails sent from background tasks (their own DI scope) land here too.
/// </summary>
public sealed class RecordingEmailSender : IEmailSender
{
    public sealed record SentEmail(
        string TemplateKey,
        IReadOnlyDictionary<string, string> Placeholders,
        IReadOnlyList<string> Recipients,
        string? RelatedEntityType,
        Guid? RelatedEntityId,
        string? TriggeredByUserId);

    private readonly ConcurrentQueue<SentEmail> _sent = new();

    public IReadOnlyList<SentEmail> Sent => _sent.ToArray();

    public Task SendEmailAsync(
        string templateKey,
        Dictionary<string, string> placeholders,
        List<string> recipientEmails,
        string? relatedEntityType = null,
        Guid? relatedEntityId = null,
        string? triggeredByUserId = null)
    {
        _sent.Enqueue(new SentEmail(
            templateKey,
            new Dictionary<string, string>(placeholders),
            recipientEmails.ToList(),
            relatedEntityType,
            relatedEntityId,
            triggeredByUserId));
        return Task.CompletedTask;
    }

    /// <summary>
    /// Waits (up to <paramref name="timeout"/>, default 10 s) for e-mails that
    /// match — notifications are sent fire-and-forget after the response.
    /// Returns what matched when the wait ends, possibly nothing.
    /// </summary>
    public async Task<IReadOnlyList<SentEmail>> WaitForAsync(
        Func<SentEmail, bool> match, int count = 1, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(10));
        while (true)
        {
            var found = Sent.Where(match).ToList();
            if (found.Count >= count || DateTime.UtcNow >= deadline)
                return found;
            await Task.Delay(50);
        }
    }
}
