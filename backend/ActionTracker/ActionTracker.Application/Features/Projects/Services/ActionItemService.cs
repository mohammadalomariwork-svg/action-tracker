using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ActionTracker.Application.Common.Interfaces;
using ActionTracker.Application.Features.Projects.DTOs;
using ActionTracker.Application.Features.Projects.Interfaces;
using ActionTracker.Application.Features.Projects.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ActionTracker.Application.Features.Projects.Services;

/// <summary>
/// Application service for managing action items across workspaces, projects,
/// and milestones.
/// Handles assignee validation, cascading soft-delete to child documents, and
/// manual DTO mapping without AutoMapper.
/// </summary>
public class ActionItemService : IActionItemService
{
    private readonly IAppDbContext _db;
    private readonly ILogger<ActionItemService> _logger;

    /// <summary>Initialises the service with its required dependencies.</summary>
    public ActionItemService(IAppDbContext db, ILogger<ActionItemService> logger)
    {
        _db     = db;
        _logger = logger;
    }

    // ── Queries ───────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    /// <remarks>
    /// Returns only standalone actions — those where <c>ProjectId IS NULL</c>.
    /// Project-scoped and milestone-scoped actions are excluded.
    /// </remarks>
    public async Task<IEnumerable<ActionItemListDto>> GetByWorkspaceAsync(Guid workspaceId)
    {
        try
        {
            var items = await _db.ProjectActionItems
                .Where(a => a.WorkspaceId == workspaceId
                         && a.ProjectId == null
                         && a.IsActive)
                .OrderBy(a => a.DueDate)
                .ToListAsync();

            return items.Select(MapToListDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error retrieving standalone action items for workspace {WorkspaceId}.", workspaceId);
            throw;
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Returns all active action items belonging to the project — both
    /// project-level actions and those nested under milestones.
    /// The <see cref="ActionItemListDto.MilestoneId"/> field gives callers
    /// the context of which milestone each item belongs to.
    /// </remarks>
    public async Task<IEnumerable<ActionItemListDto>> GetByProjectAsync(Guid projectId)
    {
        try
        {
            var items = await _db.ProjectActionItems
                .Where(a => a.ProjectId == projectId && a.IsActive)
                .OrderBy(a => a.MilestoneId)
                .ThenBy(a => a.DueDate)
                .ToListAsync();

            return items.Select(MapToListDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error retrieving action items for project {ProjectId}.", projectId);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<ActionItemListDto>> GetByMilestoneAsync(Guid milestoneId)
    {
        try
        {
            var items = await _db.ProjectActionItems
                .Where(a => a.MilestoneId == milestoneId && a.IsActive)
                .OrderBy(a => a.DueDate)
                .ToListAsync();

            return items.Select(MapToListDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error retrieving action items for milestone {MilestoneId}.", milestoneId);
            throw;
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Loads the action item together with its attached documents and comments.
    /// </remarks>
    public async Task<ActionItemDetailDto?> GetByIdAsync(Guid id)
    {
        try
        {
            var item = await _db.ProjectActionItems
                .Include(a => a.Documents.Where(d => d.IsActive))
                .Include(a => a.Comments.Where(c => c.IsActive))
                .FirstOrDefaultAsync(a => a.Id == id && a.IsActive);

            return item is null ? null : MapToDetailDto(item);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving action item {Id}.", id);
            throw;
        }
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">
    /// Thrown when <c>IsExternalAssignee == true</c> but
    /// <c>AssignedToExternalName</c> is not provided, or when
    /// <c>IsExternalAssignee == true</c> but <c>AssignedToUserId</c> is
    /// also set.
    /// </exception>
    public async Task<ActionItemDetailDto> CreateAsync(CreateActionItemDto dto)
    {
        // Assignee validation.
        if (dto.IsExternalAssignee)
        {
            if (string.IsNullOrWhiteSpace(dto.AssignedToExternalName))
                throw new ArgumentException(
                    "AssignedToExternalName is required when IsExternalAssignee is true.",
                    nameof(dto.AssignedToExternalName));

            if (!string.IsNullOrWhiteSpace(dto.AssignedToUserId))
                throw new ArgumentException(
                    "AssignedToUserId must be null when IsExternalAssignee is true.",
                    nameof(dto.AssignedToUserId));
        }
        else
        {
            // Internal assignment — userId may legitimately be null (unassigned).
            // Nothing further to enforce here beyond what the DTO annotations provide.
        }

        try
        {
            var item = new ActionItem
            {
                WorkspaceId              = dto.WorkspaceId,
                ProjectId                = dto.ProjectId,
                MilestoneId              = dto.MilestoneId,
                Title                    = dto.Title,
                Description              = dto.Description,
                Status                   = dto.Status,
                Priority                 = dto.Priority,
                PlannedStartDate         = dto.PlannedStartDate,
                DueDate                  = dto.DueDate,
                AssignedToUserId         = dto.IsExternalAssignee ? null : dto.AssignedToUserId,
                AssignedToUserName       = dto.IsExternalAssignee ? null : dto.AssignedToUserName,
                AssignedToExternalName   = dto.AssignedToExternalName,
                AssignedToExternalEmail  = dto.AssignedToExternalEmail,
                IsExternalAssignee       = dto.IsExternalAssignee,
                CompletionPercentage     = 0,
                CreatedByUserId          = dto.CreatedByUserId,
                IsActive                 = true,
                CreatedAt                = DateTime.UtcNow
            };

            _db.ProjectActionItems.Add(item);
            await _db.SaveChangesAsync();

            _logger.LogInformation("Created action item {Id} '{Title}'.", item.Id, item.Title);
            return MapToDetailDto(item);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating action item '{Title}'.", dto.Title);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<ActionItemDetailDto?> UpdateAsync(Guid id, UpdateActionItemDto dto)
    {
        try
        {
            var item = await _db.ProjectActionItems
                .Include(a => a.Documents.Where(d => d.IsActive))
                .Include(a => a.Comments.Where(c => c.IsActive))
                .FirstOrDefaultAsync(a => a.Id == id && a.IsActive);

            if (item is null) return null;

            if (dto.Title                   is not null) item.Title                   = dto.Title;
            if (dto.Description             is not null) item.Description             = dto.Description;
            if (dto.Status                  is not null) item.Status                  = dto.Status.Value;
            if (dto.Priority                is not null) item.Priority                = dto.Priority.Value;
            if (dto.PlannedStartDate        is not null) item.PlannedStartDate        = dto.PlannedStartDate.Value;
            if (dto.DueDate                 is not null) item.DueDate                 = dto.DueDate.Value;
            if (dto.ActualCompletionDate    is not null) item.ActualCompletionDate    = dto.ActualCompletionDate;
            if (dto.AssignedToUserId        is not null) item.AssignedToUserId        = dto.AssignedToUserId;
            if (dto.AssignedToUserName      is not null) item.AssignedToUserName      = dto.AssignedToUserName;
            if (dto.AssignedToExternalName  is not null) item.AssignedToExternalName  = dto.AssignedToExternalName;
            if (dto.AssignedToExternalEmail is not null) item.AssignedToExternalEmail = dto.AssignedToExternalEmail;
            if (dto.IsExternalAssignee      is not null) item.IsExternalAssignee      = dto.IsExternalAssignee.Value;
            if (dto.CompletionPercentage    is not null) item.CompletionPercentage    = dto.CompletionPercentage.Value;

            item.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            _logger.LogInformation("Updated action item {Id}.", id);
            return MapToDetailDto(item);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating action item {Id}.", id);
            throw;
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Soft-deletes the action item (<c>IsActive = false</c>) and cascades the
    /// same soft-delete to all attached <c>ActionDocument</c> records in a
    /// single <c>SaveChangesAsync</c> call.
    /// </remarks>
    public async Task<bool> DeleteAsync(Guid id)
    {
        try
        {
            var item = await _db.ProjectActionItems
                .Include(a => a.Documents)
                .FirstOrDefaultAsync(a => a.Id == id && a.IsActive);

            if (item is null) return false;

            var now = DateTime.UtcNow;

            // Cascade soft-delete to attached documents.
            foreach (var doc in item.Documents.Where(d => d.IsActive))
                doc.IsActive = false;

            item.IsActive  = false;
            item.UpdatedAt = now;

            await _db.SaveChangesAsync();

            _logger.LogInformation(
                "Soft-deleted action item {Id} and {Count} child documents.",
                id, item.Documents.Count(d => !d.IsActive));

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting action item {Id}.", id);
            throw;
        }
    }

    // ── Private mapping ───────────────────────────────────────────────────────

    /// <summary>Maps an <see cref="ActionItem"/> to an <see cref="ActionItemListDto"/>.</summary>
    private static ActionItemListDto MapToListDto(ActionItem a) => new()
    {
        Id                     = a.Id,
        WorkspaceId            = a.WorkspaceId,
        ProjectId              = a.ProjectId,
        MilestoneId            = a.MilestoneId,
        Title                  = a.Title,
        Status                 = a.Status,
        Priority               = a.Priority,
        PlannedStartDate       = a.PlannedStartDate,
        DueDate                = a.DueDate,
        ActualCompletionDate   = a.ActualCompletionDate,
        AssignedToUserName     = a.AssignedToUserName,
        AssignedToExternalName = a.AssignedToExternalName,
        IsExternalAssignee     = a.IsExternalAssignee,
        CompletionPercentage   = a.CompletionPercentage
    };

    /// <summary>
    /// Maps an <see cref="ActionItem"/> (with loaded navigations) to an
    /// <see cref="ActionItemDetailDto"/>.
    /// </summary>
    private static ActionItemDetailDto MapToDetailDto(ActionItem a) => new()
    {
        // Inherited list fields.
        Id                     = a.Id,
        WorkspaceId            = a.WorkspaceId,
        ProjectId              = a.ProjectId,
        MilestoneId            = a.MilestoneId,
        Title                  = a.Title,
        Status                 = a.Status,
        Priority               = a.Priority,
        PlannedStartDate       = a.PlannedStartDate,
        DueDate                = a.DueDate,
        ActualCompletionDate   = a.ActualCompletionDate,
        AssignedToUserName     = a.AssignedToUserName,
        AssignedToExternalName = a.AssignedToExternalName,
        IsExternalAssignee     = a.IsExternalAssignee,
        CompletionPercentage   = a.CompletionPercentage,

        // Detail-only fields.
        Description             = a.Description,
        AssignedToUserId        = a.AssignedToUserId,
        AssignedToExternalEmail = a.AssignedToExternalEmail,
        CreatedByUserId         = a.CreatedByUserId,
        CreatedAt               = a.CreatedAt,
        UpdatedAt               = a.UpdatedAt,
        Documents = a.Documents
            .Where(d => d.IsActive)
            .Select(d => new DocumentDto
            {
                Id                  = d.Id,
                Title               = d.Title,
                FileName            = d.FileName,
                ContentType         = d.ContentType,
                FileSizeBytes       = d.FileSizeBytes,
                UploadedByUserName  = d.UploadedByUserName,
                UploadedAt          = d.UploadedAt
            })
            .ToList(),
        Comments = a.Comments
            .Where(c => c.IsActive)
            .Select(c => new CommentDto
            {
                Id             = c.Id,
                Content        = c.Content,
                AuthorUserId   = c.AuthorUserId,
                AuthorUserName = c.AuthorUserName,
                ActionItemId   = c.ActionItemId,
                MilestoneId    = c.MilestoneId,
                ProjectId      = c.ProjectId,
                CreatedAt      = c.CreatedAt,
                UpdatedAt      = c.UpdatedAt,
                IsEdited       = c.IsEdited
            })
            .ToList()
    };
}
