using ActionTracker.Application.Common;
using ActionTracker.Application.Features.MailServerConfig.DTOs;

namespace ActionTracker.Application.Features.MailServerConfig;

public interface IMailServerConfigService
{
    /// <summary>Returns the single active configuration, or null if none has been saved.</summary>
    Task<MailServerConfigDto?> GetAsync();

    /// <summary>Creates the active config or updates the existing one.</summary>
    Task<MailServerConfigDto> UpsertAsync(UpdateMailServerConfigDto dto, string? userId);

    /// <summary>
    /// Returns the active configuration as fully-resolved SMTP settings (password decrypted).
    /// Used by EmailSender at send time. Returns null when no DB config exists.
    /// </summary>
    Task<SmtpSettings?> GetActiveSmtpSettingsAsync();
}
