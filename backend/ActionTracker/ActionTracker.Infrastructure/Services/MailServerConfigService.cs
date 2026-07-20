using ActionTracker.Application.Common;
using ActionTracker.Application.Common.Interfaces;
using ActionTracker.Application.Features.MailServerConfig;
using ActionTracker.Application.Features.MailServerConfig.DTOs;
using ActionTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ActionTracker.Infrastructure.Services;

public class MailServerConfigService : IMailServerConfigService
{
    private readonly IAppDbContext _db;
    private readonly ISecretProtector _protector;
    private readonly ILogger<MailServerConfigService> _logger;

    public MailServerConfigService(
        IAppDbContext db,
        ISecretProtector protector,
        ILogger<MailServerConfigService> logger)
    {
        _db        = db;
        _protector = protector;
        _logger    = logger;
    }

    public async Task<MailServerConfigDto?> GetAsync()
    {
        var entity = await GetActiveAsync();
        return entity is null ? null : ToDto(entity);
    }

    public async Task<MailServerConfigDto> UpsertAsync(UpdateMailServerConfigDto dto, string? userId)
    {
        var now = DateTime.UtcNow;
        var entity = await GetActiveAsync();

        if (entity is null)
        {
            entity = new MailServerConfig
            {
                Id                = Guid.NewGuid(),
                IsActive          = true,
                CreatedAt         = now,
                CreatedBy         = userId,
            };
            _db.MailServerConfigs.Add(entity);
        }

        entity.Host      = dto.Host?.Trim()      ?? string.Empty;
        entity.Port      = dto.Port;
        entity.UseSsl    = dto.UseSsl;
        entity.FromEmail = dto.FromEmail?.Trim() ?? string.Empty;
        entity.FromName  = dto.FromName?.Trim()  ?? string.Empty;
        entity.Username  = dto.Username?.Trim()  ?? string.Empty;

        // Password: null/empty = keep existing
        if (!string.IsNullOrEmpty(dto.Password))
        {
            entity.EncryptedPassword = _protector.Protect(dto.Password);
        }

        entity.UpdatedAt = now;
        entity.UpdatedBy = userId;

        await _db.SaveChangesAsync();

        _logger.LogInformation("Mail server configuration saved by {UserId}. Host={Host}, From={From}",
            userId ?? "(unknown)", entity.Host, entity.FromEmail);

        return ToDto(entity);
    }

    public async Task<SmtpSettings?> GetActiveSmtpSettingsAsync()
    {
        var entity = await GetActiveAsync();
        if (entity is null) return null;

        string password = string.Empty;
        if (!string.IsNullOrEmpty(entity.EncryptedPassword))
        {
            try
            {
                password = _protector.Unprotect(entity.EncryptedPassword);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to decrypt mail server password. Falling back to empty.");
                password = string.Empty;
            }
        }

        return new SmtpSettings
        {
            Host      = entity.Host,
            Port      = entity.Port,
            UseSsl    = entity.UseSsl,
            FromEmail = entity.FromEmail,
            FromName  = entity.FromName,
            Username  = entity.Username,
            Password  = password,
        };
    }

    private Task<MailServerConfig?> GetActiveAsync()
        => _db.MailServerConfigs
            .OrderByDescending(m => m.IsActive)
            .ThenByDescending(m => m.UpdatedAt ?? m.CreatedAt)
            .FirstOrDefaultAsync(m => m.IsActive);

    private static MailServerConfigDto ToDto(MailServerConfig m) => new()
    {
        Id          = m.Id,
        Host        = m.Host,
        Port        = m.Port,
        UseSsl      = m.UseSsl,
        FromEmail   = m.FromEmail,
        FromName    = m.FromName,
        Username    = m.Username,
        HasPassword = !string.IsNullOrEmpty(m.EncryptedPassword),
        IsActive    = m.IsActive,
        CreatedAt   = m.CreatedAt,
        CreatedBy   = m.CreatedBy,
        UpdatedAt   = m.UpdatedAt,
        UpdatedBy   = m.UpdatedBy,
    };
}
