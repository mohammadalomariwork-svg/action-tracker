using ActionTracker.API.Models;
using ActionTracker.Application.Features.EmailTemplates;
using ActionTracker.Application.Features.EmailTemplates.DTOs;
using ActionTracker.Application.Helpers;
using ActionTracker.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ActionTracker.API.Controllers;

[ApiController]
[Route("api/email-templates")]
[Authorize(Policy = PermissionPolicies.EmailTemplatesView)]
public class EmailTemplatesController : ControllerBase
{
    private readonly IEmailTemplateService _templateService;
    private readonly ILogger<EmailTemplatesController> _logger;

    public EmailTemplatesController(
        IEmailTemplateService templateService,
        ILogger<EmailTemplatesController> logger)
    {
        _templateService = templateService;
        _logger          = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var result = await _templateService.GetAllAsync();
        return Ok(ApiResponse<List<EmailTemplateListDto>>.Ok(result));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _templateService.GetByIdAsync(id);
        if (result is null)
            return NotFound(ApiResponse<string>.Fail("Email template not found."));

        return Ok(ApiResponse<EmailTemplateDto>.Ok(result));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.EmailTemplatesEdit)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateEmailTemplateDto dto)
    {
        try
        {
            var result = await _templateService.UpdateAsync(id, dto);
            return Ok(ApiResponse<EmailTemplateDto>.Ok(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<string>.Fail(ex.Message));
        }
    }

    [HttpGet("logs")]
    public async Task<IActionResult> GetLogs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? templateKey = null,
        [FromQuery] string? status = null)
    {
        var result = await _templateService.GetLogsAsync(page, pageSize, templateKey, status);
        return Ok(ApiResponse<PagedResult<EmailLogDto>>.Ok(result));
    }
}
