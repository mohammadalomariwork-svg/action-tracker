namespace ActionTracker.Application.Features.MailServerConfig.DTOs;

/// <summary>
/// Response DTO for the active mail-server configuration.
/// The password is never returned — clients only get a flag indicating
/// whether a password is currently stored.
/// </summary>
public class MailServerConfigDto
{
    public Guid Id { get; set; }

    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public bool UseSsl { get; set; }

    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;
    public bool HasPassword { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}
