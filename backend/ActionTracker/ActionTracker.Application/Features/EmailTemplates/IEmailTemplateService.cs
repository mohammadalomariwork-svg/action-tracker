using ActionTracker.Application.Features.EmailTemplates.DTOs;
using ActionTracker.Application.Helpers;

namespace ActionTracker.Application.Features.EmailTemplates;

public interface IEmailTemplateService
{
    Task<List<EmailTemplateListDto>> GetAllAsync();
    Task<EmailTemplateDto?> GetByIdAsync(Guid id);
    Task<EmailTemplateDto?> GetByKeyAsync(string templateKey);
    Task<EmailTemplateDto> UpdateAsync(Guid id, UpdateEmailTemplateDto dto);
    Task<PagedResult<EmailLogDto>> GetLogsAsync(int page, int pageSize, string? templateKey, string? status);
}
