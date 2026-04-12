using ActionTracker.Application.Common.Interfaces;
using ActionTracker.Application.Features.EmailTemplates;
using ActionTracker.Application.Features.EmailTemplates.DTOs;
using ActionTracker.Application.Helpers;
using Microsoft.EntityFrameworkCore;

namespace ActionTracker.Infrastructure.Services;

public class EmailTemplateService : IEmailTemplateService
{
    private readonly IAppDbContext _db;

    public EmailTemplateService(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<List<EmailTemplateListDto>> GetAllAsync()
    {
        return await _db.EmailTemplates
            .OrderBy(t => t.Name)
            .Select(t => new EmailTemplateListDto
            {
                Id          = t.Id,
                TemplateKey = t.TemplateKey,
                Name        = t.Name,
                Subject     = t.Subject.Length > 100 ? t.Subject.Substring(0, 100) : t.Subject,
                IsActive    = t.IsActive,
                Description = t.Description,
            })
            .ToListAsync();
    }

    public async Task<EmailTemplateDto?> GetByIdAsync(Guid id)
    {
        return await _db.EmailTemplates
            .Where(t => t.Id == id)
            .Select(t => new EmailTemplateDto
            {
                Id          = t.Id,
                TemplateKey = t.TemplateKey,
                Name        = t.Name,
                Subject     = t.Subject,
                HtmlBody    = t.HtmlBody,
                IsActive    = t.IsActive,
                Description = t.Description,
                CreatedAt   = t.CreatedAt,
                UpdatedAt   = t.UpdatedAt,
            })
            .FirstOrDefaultAsync();
    }

    public async Task<EmailTemplateDto?> GetByKeyAsync(string templateKey)
    {
        return await _db.EmailTemplates
            .Where(t => t.TemplateKey == templateKey)
            .Select(t => new EmailTemplateDto
            {
                Id          = t.Id,
                TemplateKey = t.TemplateKey,
                Name        = t.Name,
                Subject     = t.Subject,
                HtmlBody    = t.HtmlBody,
                IsActive    = t.IsActive,
                Description = t.Description,
                CreatedAt   = t.CreatedAt,
                UpdatedAt   = t.UpdatedAt,
            })
            .FirstOrDefaultAsync();
    }

    public async Task<EmailTemplateDto> UpdateAsync(Guid id, UpdateEmailTemplateDto dto)
    {
        var template = await _db.EmailTemplates.FindAsync(id)
            ?? throw new KeyNotFoundException($"Email template with ID '{id}' not found.");

        template.Subject  = dto.Subject;
        template.HtmlBody = dto.HtmlBody;
        template.IsActive = dto.IsActive;

        await _db.SaveChangesAsync();

        return new EmailTemplateDto
        {
            Id          = template.Id,
            TemplateKey = template.TemplateKey,
            Name        = template.Name,
            Subject     = template.Subject,
            HtmlBody    = template.HtmlBody,
            IsActive    = template.IsActive,
            Description = template.Description,
            CreatedAt   = template.CreatedAt,
            UpdatedAt   = template.UpdatedAt,
        };
    }

    public async Task<PagedResult<EmailLogDto>> GetLogsAsync(
        int page, int pageSize, string? templateKey, string? status)
    {
        var query = _db.EmailLogs.AsQueryable();

        if (!string.IsNullOrWhiteSpace(templateKey))
            query = query.Where(l => l.TemplateKey == templateKey);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(l => l.Status == status);

        var projected = query
            .OrderByDescending(l => l.SentAt)
            .Select(l => new EmailLogDto
            {
                Id                = l.Id,
                TemplateKey       = l.TemplateKey,
                ToEmail           = l.ToEmail,
                Subject           = l.Subject,
                SentAt            = l.SentAt,
                Status            = l.Status,
                ErrorMessage      = l.ErrorMessage,
                RelatedEntityType = l.RelatedEntityType,
                RelatedEntityId   = l.RelatedEntityId,
            });

        return await PagedResult<EmailLogDto>.CreateAsync(projected, page, pageSize);
    }
}
