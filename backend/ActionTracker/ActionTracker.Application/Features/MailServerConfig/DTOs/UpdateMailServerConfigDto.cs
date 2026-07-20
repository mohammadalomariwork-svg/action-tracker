namespace ActionTracker.Application.Features.MailServerConfig.DTOs;

/// <summary>
/// Request DTO for upserting the active mail-server configuration.
/// Password is optional — null/empty means "keep the existing stored password".
/// </summary>
public class UpdateMailServerConfigDto
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool UseSsl { get; set; } = true;

    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    /// <summary>Null or empty leaves the existing stored password unchanged.</summary>
    public string? Password { get; set; }
}
