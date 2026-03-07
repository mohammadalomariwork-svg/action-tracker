using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ActionTracker.Application.Common.Interfaces;
using ActionTracker.Application.Features.Projects.DTOs;
using ActionTracker.Application.Features.Projects.Interfaces;
using ActionTracker.Application.Features.Projects.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ActionTracker.Application.Features.Projects.Services;

/// <summary>
/// Application service for managing projects within a workspace.
/// Handles full project lifecycle including creation, updates, baselining, and
/// retrieval with nested data.
/// </summary>
public class ProjectService : IProjectService
{
    private readonly IAppDbContext _db;
    private readonly ILogger<ProjectService> _logger;

    /// <summary>Initialises the service with its required dependencies.</summary>
    public ProjectService(IAppDbContext db, ILogger<ProjectService> logger)
    {
        _db     = db;
        _logger = logger;
    }

    // ── Queries ───────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    /// <remarks>
    /// Loads milestone count and action-item count per project to compute
    /// <see cref="ProjectListDto.CompletionPercentage"/> without fetching full
    /// entity graphs.
    /// </remarks>
    public async Task<IEnumerable<ProjectListDto>> GetByWorkspaceAsync(Guid workspaceId)
    {
        try
        {
            var rows = await _db.Projects
                .Where(p => p.WorkspaceId == workspaceId && p.IsActive)
                .Select(p => new
                {
                    Project         = p,
                    MilestoneCount  = p.Milestones.Count(m => m.IsActive),
                    ActionItemCount = p.ActionItems.Count(a => a.IsActive)
                                    + p.Milestones
                                       .Where(m => m.IsActive)
                                       .SelectMany(m => m.ActionItems)
                                       .Count(a => a.IsActive),
                    AvgCompletion   = p.ActionItems
                                       .Where(a => a.IsActive)
                                       .Select(a => (int?)a.CompletionPercentage)
                                       .Concat(
                                           p.Milestones
                                            .Where(m => m.IsActive)
                                            .SelectMany(m => m.ActionItems)
                                            .Where(a => a.IsActive)
                                            .Select(a => (int?)a.CompletionPercentage))
                                       .Average()
                })
                .OrderBy(r => r.Project.PlannedStartDate)
                .ToListAsync();

            return rows.Select(r => MapToListDto(r.Project, r.MilestoneCount, r.ActionItemCount,
                                                 (int)Math.Round(r.AvgCompletion ?? 0)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving projects for workspace {WorkspaceId}.", workspaceId);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<ProjectDetailDto?> GetByIdAsync(Guid id)
    {
        try
        {
            var p = await _db.Projects
                .Include(p => p.StrategicObjective)
                .Include(p => p.Budget)
                .FirstOrDefaultAsync(p => p.Id == id && p.IsActive);

            if (p is null) return null;

            int milestoneCount  = await _db.Milestones.CountAsync(m => m.ProjectId == id && m.IsActive);
            int actionItemCount = await _db.ProjectActionItems.CountAsync(a => a.ProjectId == id && a.IsActive);

            return MapToDetailDto(p, milestoneCount, actionItemCount, p.Budget is not null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving project {Id}.", id);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<ProjectDetailDto?> GetProjectWithFullDetailsAsync(Guid id)
    {
        try
        {
            var p = await _db.Projects
                .Include(p => p.StrategicObjective)
                .Include(p => p.Budget)
                .Include(p => p.Contracts.Where(c => c.IsActive))
                .Include(p => p.ChangeRequests)
                .Include(p => p.Milestones.Where(m => m.IsActive))
                    .ThenInclude(m => m.ActionItems.Where(a => a.IsActive))
                .Include(p => p.ActionItems.Where(a => a.IsActive))
                .FirstOrDefaultAsync(p => p.Id == id && p.IsActive);

            if (p is null) return null;

            int milestoneCount  = p.Milestones.Count;
            int actionItemCount = p.ActionItems.Count
                                 + p.Milestones.Sum(m => m.ActionItems.Count);

            var dto = MapToDetailDto(p, milestoneCount, actionItemCount, p.Budget is not null);
            return dto;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving full details for project {Id}.", id);
            throw;
        }
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">
    /// Thrown when <see cref="CreateProjectDto.ProjectType"/> is
    /// <see cref="ProjectType.Strategic"/> but
    /// <see cref="CreateProjectDto.StrategicObjectiveId"/> is <c>null</c>.
    /// </exception>
    public async Task<ProjectDetailDto> CreateAsync(CreateProjectDto dto)
    {
        if (dto.ProjectType == ProjectType.Strategic && dto.StrategicObjectiveId is null)
            throw new ArgumentException(
                "A strategic project must be linked to a StrategicObjectiveId.",
                nameof(dto.StrategicObjectiveId));

        try
        {
            var project = new Project
            {
                WorkspaceId            = dto.WorkspaceId,
                Title                  = dto.Title,
                Description            = dto.Description,
                ProjectType            = dto.ProjectType,
                StrategicObjectiveId   = dto.StrategicObjectiveId,
                SponsorUserId          = dto.SponsorUserId,
                SponsorUserName        = dto.SponsorUserName,
                ProjectManagerUserId   = dto.ProjectManagerUserId,
                ProjectManagerUserName = dto.ProjectManagerUserName,
                PlannedStartDate       = dto.PlannedStartDate,
                PlannedEndDate         = dto.PlannedEndDate,
                Status                 = ProjectStatus.Draft,
                IsBaselined            = false,
                IsActive               = true,
                CreatedAt              = DateTime.UtcNow,
                CreatedByUserId        = dto.CreatedByUserId
            };

            _db.Projects.Add(project);
            await _db.SaveChangesAsync();

            _logger.LogInformation("Created project {Id} '{Title}' in workspace {WorkspaceId}.",
                project.Id, project.Title, project.WorkspaceId);

            return MapToDetailDto(project, 0, 0, false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating project '{Title}'.", dto.Title);
            throw;
        }
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the project is baselined and the caller attempts to modify
    /// <see cref="Project.PlannedStartDate"/> or <see cref="Project.PlannedEndDate"/>.
    /// </exception>
    public async Task<ProjectDetailDto?> UpdateAsync(Guid id, UpdateProjectDto dto)
    {
        try
        {
            var project = await _db.Projects
                .Include(p => p.StrategicObjective)
                .Include(p => p.Budget)
                .FirstOrDefaultAsync(p => p.Id == id && p.IsActive);

            if (project is null) return null;

            // Guard: baselined projects cannot have their schedule altered.
            if (project.IsBaselined &&
                (dto.PlannedStartDate is not null || dto.PlannedEndDate is not null))
            {
                throw new InvalidOperationException(
                    "Project is baselined. Submit a change request to modify the schedule.");
            }

            // Apply permitted updates.
            if (dto.Title                  is not null) project.Title                  = dto.Title;
            if (dto.Description            is not null) project.Description            = dto.Description;
            if (dto.Status                 is not null) project.Status                 = dto.Status.Value;
            if (dto.SponsorUserId          is not null) project.SponsorUserId          = dto.SponsorUserId;
            if (dto.SponsorUserName        is not null) project.SponsorUserName        = dto.SponsorUserName;
            if (dto.ProjectManagerUserId   is not null) project.ProjectManagerUserId   = dto.ProjectManagerUserId;
            if (dto.ProjectManagerUserName is not null) project.ProjectManagerUserName = dto.ProjectManagerUserName;
            if (dto.ActualStartDate        is not null) project.ActualStartDate        = dto.ActualStartDate;
            if (dto.ActualEndDate          is not null) project.ActualEndDate          = dto.ActualEndDate;

            // Schedule fields — only reachable when IsBaselined == false (guard above).
            if (dto.PlannedStartDate is not null) project.PlannedStartDate = dto.PlannedStartDate.Value;
            if (dto.PlannedEndDate   is not null) project.PlannedEndDate   = dto.PlannedEndDate.Value;

            // Strategic objective re-alignment.
            if (dto.ProjectType is not null) project.ProjectType = dto.ProjectType.Value;
            if (dto.StrategicObjectiveId is not null) project.StrategicObjectiveId = dto.StrategicObjectiveId;

            project.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            _logger.LogInformation("Updated project {Id}.", id);

            int milestoneCount  = await _db.Milestones.CountAsync(m => m.ProjectId == id && m.IsActive);
            int actionItemCount = await _db.ProjectActionItems.CountAsync(a => a.ProjectId == id && a.IsActive);

            return MapToDetailDto(project, milestoneCount, actionItemCount, project.Budget is not null);
        }
        catch (InvalidOperationException)
        {
            throw; // Re-throw business-rule exceptions without wrapping.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating project {Id}.", id);
            throw;
        }
    }

    /// <inheritdoc/>
    /// <remarks>Performs a soft delete by setting <c>IsActive = false</c>.</remarks>
    public async Task<bool> DeleteAsync(Guid id)
    {
        try
        {
            var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == id && p.IsActive);
            if (project is null) return false;

            project.IsActive  = false;
            project.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            _logger.LogInformation("Soft-deleted project {Id}.", id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting project {Id}.", id);
            throw;
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// If the project already has a baseline record it is overwritten.
    /// A JSON snapshot of all active milestone and action-item dates is
    /// captured using <see cref="System.Text.Json"/>.
    /// </remarks>
    public async Task<ProjectBaselineDto> BaselineProjectAsync(
        Guid projectId, string baselinedByUserId, string baselinedByUserName)
    {
        try
        {
            var project = await _db.Projects
                .Include(p => p.Milestones.Where(m => m.IsActive))
                    .ThenInclude(m => m.ActionItems.Where(a => a.IsActive))
                .Include(p => p.ActionItems.Where(a => a.IsActive))
                .FirstOrDefaultAsync(p => p.Id == projectId && p.IsActive)
                ?? throw new InvalidOperationException($"Project {projectId} not found.");

            var now = DateTime.UtcNow;

            // Build snapshot.
            var snapshot = new
            {
                CapturedAt   = now,
                Milestones   = project.Milestones.Select(m => new
                {
                    m.Id,
                    m.Title,
                    m.PlannedStartDate,
                    m.PlannedEndDate,
                    ActionItems = m.ActionItems.Select(a => new
                    {
                        a.Id,
                        a.Title,
                        a.PlannedStartDate,
                        a.DueDate
                    })
                }),
                ProjectActionItems = project.ActionItems.Select(a => new
                {
                    a.Id,
                    a.Title,
                    a.PlannedStartDate,
                    a.DueDate
                })
            };

            string snapshotJson = JsonSerializer.Serialize(snapshot);

            // Update or create the ProjectBaseline record.
            var baseline = await _db.ProjectBaselines
                .FirstOrDefaultAsync(b => b.ProjectId == projectId);

            if (baseline is null)
            {
                baseline = new ProjectBaseline
                {
                    ProjectId                = projectId,
                    BaselinedAt              = now,
                    BaselinedByUserId        = baselinedByUserId,
                    BaselinedByUserName      = baselinedByUserName,
                    BaselinePlannedStartDate = project.PlannedStartDate,
                    BaselinePlannedEndDate   = project.PlannedEndDate,
                    BaselineSnapshotJson     = snapshotJson
                };
                _db.ProjectBaselines.Add(baseline);
            }
            else
            {
                baseline.BaselinedAt              = now;
                baseline.BaselinedByUserId        = baselinedByUserId;
                baseline.BaselinedByUserName      = baselinedByUserName;
                baseline.BaselinePlannedStartDate = project.PlannedStartDate;
                baseline.BaselinePlannedEndDate   = project.PlannedEndDate;
                baseline.BaselineSnapshotJson     = snapshotJson;
            }

            // Mark the project as baselined.
            project.IsBaselined      = true;
            project.BaselinedAt      = now;
            project.BaselinedByUserId = baselinedByUserId;
            project.UpdatedAt        = now;

            await _db.SaveChangesAsync();

            _logger.LogInformation("Project {Id} baselined by user {UserId}.", projectId, baselinedByUserId);

            return new ProjectBaselineDto
            {
                Id                       = baseline.Id,
                ProjectId                = baseline.ProjectId,
                BaselinedAt              = baseline.BaselinedAt,
                BaselinedByUserName      = baseline.BaselinedByUserName,
                BaselinePlannedStartDate = baseline.BaselinePlannedStartDate,
                BaselinePlannedEndDate   = baseline.BaselinePlannedEndDate,
                BaselineSnapshotJson     = baseline.BaselineSnapshotJson
            };
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error baselining project {Id}.", projectId);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> UnfreezeProjectAsync(Guid projectId)
    {
        try
        {
            var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId && p.IsActive);
            if (project is null) return false;

            project.IsBaselined = false;
            project.UpdatedAt   = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            _logger.LogInformation("Project {Id} unfrozen (IsBaselined reset to false).", projectId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unfreezing project {Id}.", projectId);
            throw;
        }
    }

    // ── Private mapping ───────────────────────────────────────────────────────

    /// <summary>Maps a <see cref="Project"/> to a <see cref="ProjectListDto"/>.</summary>
    private static ProjectListDto MapToListDto(
        Project p, int milestoneCount, int actionItemCount, int completionPercentage) => new()
    {
        Id                     = p.Id,
        WorkspaceId            = p.WorkspaceId,
        Title                  = p.Title,
        ProjectType            = p.ProjectType,
        Status                 = p.Status,
        ProjectManagerUserName = p.ProjectManagerUserName,
        SponsorUserName        = p.SponsorUserName,
        PlannedStartDate       = p.PlannedStartDate,
        PlannedEndDate         = p.PlannedEndDate,
        IsBaselined            = p.IsBaselined,
        CompletionPercentage   = completionPercentage
    };

    /// <summary>Maps a <see cref="Project"/> to a <see cref="ProjectDetailDto"/>.</summary>
    private static ProjectDetailDto MapToDetailDto(
        Project p, int milestoneCount, int actionItemCount, bool hasBudget) => new()
    {
        // Inherited from ProjectListDto.
        Id                     = p.Id,
        WorkspaceId            = p.WorkspaceId,
        Title                  = p.Title,
        ProjectType            = p.ProjectType,
        Status                 = p.Status,
        ProjectManagerUserName = p.ProjectManagerUserName,
        SponsorUserName        = p.SponsorUserName,
        PlannedStartDate       = p.PlannedStartDate,
        PlannedEndDate         = p.PlannedEndDate,
        IsBaselined            = p.IsBaselined,
        CompletionPercentage   = 0, // Caller may override after projection.

        // Detail-only fields.
        Description              = p.Description,
        StrategicObjectiveId     = p.StrategicObjectiveId,
        StrategicObjectiveTitle  = p.StrategicObjective?.Title,
        SponsorUserId            = p.SponsorUserId,
        ProjectManagerUserId     = p.ProjectManagerUserId,
        ActualStartDate          = p.ActualStartDate,
        ActualEndDate            = p.ActualEndDate,
        BaselinedAt              = p.BaselinedAt,
        CreatedAt                = p.CreatedAt,
        UpdatedAt                = p.UpdatedAt,
        CreatedByUserId          = p.CreatedByUserId,
        MilestoneCount           = milestoneCount,
        ActionItemCount          = actionItemCount,
        HasBudget                = hasBudget
    };
}
