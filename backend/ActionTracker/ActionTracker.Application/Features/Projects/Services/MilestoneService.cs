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
/// Application service for managing milestones (work packages) within a project.
/// Handles CRUD operations, ordered-list management, and cascading soft-delete
/// to child action items.
/// </summary>
public class MilestoneService : IMilestoneService
{
    private readonly IAppDbContext _db;
    private readonly ILogger<MilestoneService> _logger;

    /// <summary>Initialises the service with its required dependencies.</summary>
    public MilestoneService(IAppDbContext db, ILogger<MilestoneService> logger)
    {
        _db     = db;
        _logger = logger;
    }

    // ── Queries ───────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    /// <remarks>Results are ordered ascending by <see cref="Milestone.SequenceOrder"/>.</remarks>
    public async Task<IEnumerable<MilestoneListDto>> GetByProjectAsync(Guid projectId)
    {
        try
        {
            var milestones = await _db.Milestones
                .Where(m => m.ProjectId == projectId && m.IsActive)
                .OrderBy(m => m.SequenceOrder)
                .Select(m => new
                {
                    Milestone       = m,
                    ActionItemCount = m.ActionItems.Count(a => a.IsActive)
                })
                .ToListAsync();

            return milestones.Select(r => MapToListDto(r.Milestone, r.ActionItemCount));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving milestones for project {ProjectId}.", projectId);
            throw;
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Loads the milestone together with its active action items and comments.
    /// </remarks>
    public async Task<MilestoneDetailDto?> GetByIdAsync(Guid id)
    {
        try
        {
            var milestone = await _db.Milestones
                .Include(m => m.ActionItems.Where(a => a.IsActive))
                .Include(m => m.Comments.Where(c => c.IsActive))
                .FirstOrDefaultAsync(m => m.Id == id && m.IsActive);

            if (milestone is null) return null;

            return MapToDetailDto(milestone);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving milestone {Id}.", id);
            throw;
        }
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<MilestoneDetailDto> CreateAsync(CreateMilestoneDto dto)
    {
        try
        {
            var milestone = new Milestone
            {
                ProjectId        = dto.ProjectId,
                Title            = dto.Title,
                Description      = dto.Description,
                SequenceOrder    = dto.SequenceOrder,
                Status           = MilestoneStatus.NotStarted,
                PlannedStartDate = dto.PlannedStartDate,
                PlannedEndDate   = dto.PlannedEndDate,
                CompletionPercentage = 0,
                IsActive         = true,
                CreatedAt        = DateTime.UtcNow
            };

            _db.Milestones.Add(milestone);
            await _db.SaveChangesAsync();

            _logger.LogInformation("Created milestone {Id} '{Title}' in project {ProjectId}.",
                milestone.Id, milestone.Title, milestone.ProjectId);

            return MapToDetailDto(milestone);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating milestone '{Title}' in project {ProjectId}.",
                dto.Title, dto.ProjectId);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<MilestoneDetailDto?> UpdateAsync(Guid id, UpdateMilestoneDto dto)
    {
        try
        {
            var milestone = await _db.Milestones
                .Include(m => m.ActionItems.Where(a => a.IsActive))
                .Include(m => m.Comments.Where(c => c.IsActive))
                .FirstOrDefaultAsync(m => m.Id == id && m.IsActive);

            if (milestone is null) return null;

            if (dto.Title                is not null) milestone.Title                = dto.Title;
            if (dto.Description          is not null) milestone.Description          = dto.Description;
            if (dto.SequenceOrder        is not null) milestone.SequenceOrder        = dto.SequenceOrder.Value;
            if (dto.Status               is not null) milestone.Status               = dto.Status.Value;
            if (dto.PlannedStartDate     is not null) milestone.PlannedStartDate     = dto.PlannedStartDate.Value;
            if (dto.PlannedEndDate       is not null) milestone.PlannedEndDate       = dto.PlannedEndDate.Value;
            if (dto.ActualStartDate      is not null) milestone.ActualStartDate      = dto.ActualStartDate;
            if (dto.ActualEndDate        is not null) milestone.ActualEndDate        = dto.ActualEndDate;
            if (dto.CompletionPercentage is not null) milestone.CompletionPercentage = dto.CompletionPercentage.Value;

            milestone.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            _logger.LogInformation("Updated milestone {Id}.", id);
            return MapToDetailDto(milestone);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating milestone {Id}.", id);
            throw;
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Soft-deletes the milestone (<c>IsActive = false</c>) and cascades the
    /// same soft-delete to all child action items in a single transaction.
    /// </remarks>
    public async Task<bool> DeleteAsync(Guid id)
    {
        try
        {
            var milestone = await _db.Milestones
                .Include(m => m.ActionItems)
                .FirstOrDefaultAsync(m => m.Id == id && m.IsActive);

            if (milestone is null) return false;

            var now = DateTime.UtcNow;

            // Cascade soft-delete to child action items.
            foreach (var item in milestone.ActionItems.Where(a => a.IsActive))
            {
                item.IsActive   = false;
                item.UpdatedAt  = now;
            }

            milestone.IsActive  = false;
            milestone.UpdatedAt = now;

            await _db.SaveChangesAsync();

            _logger.LogInformation(
                "Soft-deleted milestone {Id} and {Count} child action items.",
                id, milestone.ActionItems.Count(a => !a.IsActive));

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting milestone {Id}.", id);
            throw;
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Reassigns <see cref="Milestone.SequenceOrder"/> for every milestone in
    /// the project using the position of each ID within
    /// <paramref name="orderedMilestoneIds"/> (1-based).  All updates are
    /// committed in a single <c>SaveChangesAsync</c> call.
    /// </remarks>
    public async Task<bool> ReorderMilestonesAsync(Guid projectId, List<Guid> orderedMilestoneIds)
    {
        try
        {
            var milestones = await _db.Milestones
                .Where(m => m.ProjectId == projectId && m.IsActive)
                .ToListAsync();

            if (!milestones.Any()) return false;

            var lookup = milestones.ToDictionary(m => m.Id);

            // Validate that every supplied ID belongs to the project.
            if (orderedMilestoneIds.Any(id => !lookup.ContainsKey(id)))
            {
                _logger.LogWarning(
                    "ReorderMilestones: one or more IDs are not milestones of project {ProjectId}.",
                    projectId);
                return false;
            }

            var now = DateTime.UtcNow;
            for (int i = 0; i < orderedMilestoneIds.Count; i++)
            {
                var milestone = lookup[orderedMilestoneIds[i]];
                milestone.SequenceOrder = i + 1; // 1-based
                milestone.UpdatedAt     = now;
            }

            await _db.SaveChangesAsync();

            _logger.LogInformation(
                "Reordered {Count} milestones in project {ProjectId}.",
                orderedMilestoneIds.Count, projectId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reordering milestones for project {ProjectId}.", projectId);
            throw;
        }
    }

    // ── Private mapping ───────────────────────────────────────────────────────

    /// <summary>Maps a <see cref="Milestone"/> to a <see cref="MilestoneListDto"/>.</summary>
    private static MilestoneListDto MapToListDto(Milestone m, int actionItemCount) => new()
    {
        Id                   = m.Id,
        ProjectId            = m.ProjectId,
        Title                = m.Title,
        SequenceOrder        = m.SequenceOrder,
        Status               = m.Status,
        PlannedStartDate     = m.PlannedStartDate,
        PlannedEndDate       = m.PlannedEndDate,
        ActualStartDate      = m.ActualStartDate,
        ActualEndDate        = m.ActualEndDate,
        CompletionPercentage = m.CompletionPercentage,
        ActionItemCount      = actionItemCount
    };

    /// <summary>
    /// Maps a <see cref="Milestone"/> (with loaded navigation collections) to a
    /// <see cref="MilestoneDetailDto"/>.
    /// </summary>
    private static MilestoneDetailDto MapToDetailDto(Milestone m) => new()
    {
        // Inherited list fields.
        Id                   = m.Id,
        ProjectId            = m.ProjectId,
        Title                = m.Title,
        SequenceOrder        = m.SequenceOrder,
        Status               = m.Status,
        PlannedStartDate     = m.PlannedStartDate,
        PlannedEndDate       = m.PlannedEndDate,
        ActualStartDate      = m.ActualStartDate,
        ActualEndDate        = m.ActualEndDate,
        CompletionPercentage = m.CompletionPercentage,
        ActionItemCount      = m.ActionItems.Count(a => a.IsActive),

        // Detail-only fields.
        Description = m.Description,
        UpdatedAt   = m.UpdatedAt,
        ActionItems = m.ActionItems
            .Where(a => a.IsActive)
            .Select(a => new ActionItemListDto
            {
                Id                      = a.Id,
                WorkspaceId             = a.WorkspaceId,
                ProjectId               = a.ProjectId,
                MilestoneId             = a.MilestoneId,
                Title                   = a.Title,
                Status                  = a.Status,
                Priority                = a.Priority,
                PlannedStartDate        = a.PlannedStartDate,
                DueDate                 = a.DueDate,
                ActualCompletionDate    = a.ActualCompletionDate,
                AssignedToUserName      = a.AssignedToUserName,
                AssignedToExternalName  = a.AssignedToExternalName,
                IsExternalAssignee      = a.IsExternalAssignee,
                CompletionPercentage    = a.CompletionPercentage
            })
            .ToList(),
        Comments = m.Comments
            .Where(c => c.IsActive)
            .Select(c => new CommentDto
            {
                Id             = c.Id,
                Content        = c.Content,
                AuthorUserId   = c.AuthorUserId,
                AuthorUserName = c.AuthorUserName,
                MilestoneId    = c.MilestoneId,
                ProjectId      = c.ProjectId,
                ActionItemId   = c.ActionItemId,
                CreatedAt      = c.CreatedAt,
                UpdatedAt      = c.UpdatedAt,
                IsEdited       = c.IsEdited
            })
            .ToList()
    };
}
