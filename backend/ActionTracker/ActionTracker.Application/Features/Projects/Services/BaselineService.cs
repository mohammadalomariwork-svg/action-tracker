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
/// Application service for managing project baselines and the change-request
/// approval workflow.
/// Workflow: PM creates baseline → PM submits change request → Sponsor
/// approves or rejects → PM implements approved change.
/// </summary>
public class BaselineService : IBaselineService
{
    private readonly IAppDbContext _db;
    private readonly ILogger<BaselineService> _logger;

    /// <summary>Initialises the service with its required dependencies.</summary>
    public BaselineService(IAppDbContext db, ILogger<BaselineService> logger)
    {
        _db     = db;
        _logger = logger;
    }

    // ── Baseline queries ──────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<ProjectBaselineDto?> GetBaselineByProjectAsync(int projectId)
    {
        try
        {
            var baseline = await _db.ProjectBaselines
                .Include(b => b.Project)
                .FirstOrDefaultAsync(b => b.ProjectId == projectId);

            return baseline is null ? null : MapBaselineToDto(baseline);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error retrieving baseline for project {ProjectId}.", projectId);
            throw;
        }
    }

    // ── Baseline creation ─────────────────────────────────────────────────────

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">
    /// Thrown when the project does not exist or has no active milestones.
    /// </exception>
    public async Task<ProjectBaselineDto> CreateBaselineAsync(
        int projectId, string userId, string userName)
    {
        try
        {
            var project = await _db.Projects
                .Include(p => p.Milestones.Where(m => m.IsActive))
                    .ThenInclude(m => m.ActionItems.Where(a => a.IsActive))
                .FirstOrDefaultAsync(p => p.Id == projectId && p.IsActive)
                ?? throw new ArgumentException(
                       $"Project {projectId} not found.", nameof(projectId));

            if (!project.Milestones.Any())
                throw new ArgumentException(
                    $"Project {projectId} has no active milestones. Add at least one milestone before baselining.",
                    nameof(projectId));

            var now = DateTime.UtcNow;

            // Build the immutable schedule snapshot.
            var snapshot = new
            {
                ProjectPlannedStart = project.PlannedStartDate,
                ProjectPlannedEnd   = project.PlannedEndDate,
                Milestones          = project.Milestones.Select(m => new
                {
                    m.Id,
                    m.Title,
                    m.PlannedStartDate,
                    m.PlannedEndDate,
                    Actions = m.ActionItems.Select(a => new
                    {
                        a.Id,
                        a.Title,
                        a.PlannedStartDate,
                        a.DueDate
                    })
                })
            };

            string snapshotJson = JsonSerializer.Serialize(snapshot);

            // Upsert the ProjectBaseline record.
            var baseline = await _db.ProjectBaselines
                .FirstOrDefaultAsync(b => b.ProjectId == projectId);

            if (baseline is null)
            {
                baseline = new ProjectBaseline
                {
                    ProjectId                = projectId,
                    BaselinedAt              = now,
                    BaselinedByUserId        = userId,
                    BaselinedByUserName      = userName,
                    BaselinePlannedStartDate = project.PlannedStartDate,
                    BaselinePlannedEndDate   = project.PlannedEndDate,
                    BaselineSnapshotJson     = snapshotJson
                };
                _db.ProjectBaselines.Add(baseline);
            }
            else
            {
                baseline.BaselinedAt              = now;
                baseline.BaselinedByUserId        = userId;
                baseline.BaselinedByUserName      = userName;
                baseline.BaselinePlannedStartDate = project.PlannedStartDate;
                baseline.BaselinePlannedEndDate   = project.PlannedEndDate;
                baseline.BaselineSnapshotJson     = snapshotJson;
            }

            // Freeze the project.
            project.IsBaselined       = true;
            project.BaselinedAt       = now;
            project.BaselinedByUserId = userId;
            project.UpdatedAt         = now;

            await _db.SaveChangesAsync();

            _logger.LogInformation(
                "Project {ProjectId} baselined by user {UserId}. Snapshot captured {MilestoneCount} milestones.",
                projectId, userId, project.Milestones.Count);

            return MapBaselineToDto(baseline);
        }
        catch (ArgumentException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating baseline for project {ProjectId}.", projectId);
            throw;
        }
    }

    // ── Change request queries ────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<IEnumerable<BaselineChangeRequestDto>> GetChangeRequestsByProjectAsync(
        int projectId)
    {
        try
        {
            var requests = await _db.BaselineChangeRequests
                .Where(r => r.ProjectId == projectId)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            return requests.Select(MapChangeRequestToDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error retrieving change requests for project {ProjectId}.", projectId);
            throw;
        }
    }

    // ── Change request workflow ───────────────────────────────────────────────

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the project has not been baselined, or when a
    /// <see cref="ChangeRequestStatus.Pending"/> request already exists.
    /// </exception>
    public async Task<BaselineChangeRequestDto> SubmitChangeRequestAsync(
        CreateBaselineChangeRequestDto dto)
    {
        try
        {
            var project = await _db.Projects
                .FirstOrDefaultAsync(p => p.Id == dto.ProjectId && p.IsActive)
                ?? throw new InvalidOperationException(
                       $"Project {dto.ProjectId} not found.");

            if (!project.IsBaselined)
                throw new InvalidOperationException(
                    $"Project {dto.ProjectId} has not been baselined. " +
                    "Baseline the project before submitting a change request.");

            bool hasPending = await _db.BaselineChangeRequests
                .AnyAsync(r => r.ProjectId == dto.ProjectId
                            && r.Status == ChangeRequestStatus.Pending);

            if (hasPending)
                throw new InvalidOperationException(
                    $"Project {dto.ProjectId} already has a pending change request. " +
                    "Resolve the existing request before submitting a new one.");

            var request = new BaselineChangeRequest
            {
                ProjectId            = dto.ProjectId,
                RequestedByUserId    = dto.RequestedByUserId,
                RequestedByUserName  = dto.RequestedByUserName,
                ChangeJustification  = dto.ChangeJustification,
                ProposedChangesJson  = dto.ProposedChangesJson,
                Status               = ChangeRequestStatus.Pending,
                CreatedAt            = DateTime.UtcNow
            };

            _db.BaselineChangeRequests.Add(request);
            await _db.SaveChangesAsync();

            _logger.LogInformation(
                "Change request {Id} submitted for project {ProjectId} by user {UserId}.",
                request.Id, dto.ProjectId, dto.RequestedByUserId);

            return MapChangeRequestToDto(request);
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error submitting change request for project {ProjectId}.", dto.ProjectId);
            throw;
        }
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the change request is not found or is not in
    /// <see cref="ChangeRequestStatus.Pending"/> status.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the supplied <see cref="ReviewChangeRequestDto.Status"/> is
    /// not <see cref="ChangeRequestStatus.ApprovedBySponsor"/> or
    /// <see cref="ChangeRequestStatus.Rejected"/>.
    /// </exception>
    public async Task<BaselineChangeRequestDto?> ReviewChangeRequestAsync(
        ReviewChangeRequestDto dto)
    {
        try
        {
            // Validate the supplied decision status.
            if (dto.Status != ChangeRequestStatus.ApprovedBySponsor &&
                dto.Status != ChangeRequestStatus.Rejected)
            {
                throw new ArgumentException(
                    "Status must be ApprovedBySponsor or Rejected.",
                    nameof(dto.Status));
            }

            var request = await _db.BaselineChangeRequests
                .FirstOrDefaultAsync(r => r.Id == dto.ChangeRequestId);

            if (request is null) return null;

            if (request.Status != ChangeRequestStatus.Pending)
                throw new InvalidOperationException(
                    $"Change request {dto.ChangeRequestId} is not in Pending status " +
                    $"(current: {request.Status}). Only pending requests can be reviewed.");

            request.Status             = dto.Status;
            request.ReviewedByUserId   = dto.ReviewedByUserId;
            request.ReviewedByUserName = dto.ReviewedByUserName;
            request.ReviewedAt         = DateTime.UtcNow;
            request.ReviewNotes        = dto.ReviewNotes;
            request.UpdatedAt          = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            _logger.LogInformation(
                "Change request {Id} reviewed by {UserId}: {Status}.",
                dto.ChangeRequestId, dto.ReviewedByUserId, dto.Status);

            return MapChangeRequestToDto(request);
        }
        catch (ArgumentException)
        {
            throw;
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error reviewing change request {ChangeRequestId}.", dto.ChangeRequestId);
            throw;
        }
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the change request is not found or its status is not
    /// <see cref="ChangeRequestStatus.ApprovedBySponsor"/>.
    /// </exception>
    public async Task<bool> ImplementApprovedChangeAsync(
        int changeRequestId, string implementedByUserId)
    {
        try
        {
            var request = await _db.BaselineChangeRequests
                .FirstOrDefaultAsync(r => r.Id == changeRequestId);

            if (request is null) return false;

            if (request.Status != ChangeRequestStatus.ApprovedBySponsor)
                throw new InvalidOperationException(
                    $"Change request {changeRequestId} is not in ApprovedBySponsor status " +
                    $"(current: {request.Status}). Only approved requests can be implemented.");

            var now = DateTime.UtcNow;

            // Mark the change request as implemented.
            request.Status    = ChangeRequestStatus.Implemented;
            request.UpdatedAt = now;

            // Unfreeze the project so the PM can edit schedule dates.
            var project = await _db.Projects
                .FirstOrDefaultAsync(p => p.Id == request.ProjectId && p.IsActive);

            if (project is not null)
            {
                project.IsBaselined = false;
                project.UpdatedAt   = now;
            }

            await _db.SaveChangesAsync();

            _logger.LogInformation(
                "Change request {Id} implemented by user {UserId}. Project {ProjectId} unfrozen.",
                changeRequestId, implementedByUserId, request.ProjectId);

            return true;
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error implementing change request {ChangeRequestId}.", changeRequestId);
            throw;
        }
    }

    // ── Private mapping ───────────────────────────────────────────────────────

    /// <summary>Maps a <see cref="ProjectBaseline"/> entity to its DTO.</summary>
    private static ProjectBaselineDto MapBaselineToDto(ProjectBaseline b) => new()
    {
        Id                       = b.Id,
        ProjectId                = b.ProjectId,
        BaselinedAt              = b.BaselinedAt,
        BaselinedByUserName      = b.BaselinedByUserName,
        BaselinePlannedStartDate = b.BaselinePlannedStartDate,
        BaselinePlannedEndDate   = b.BaselinePlannedEndDate,
        BaselineSnapshotJson     = b.BaselineSnapshotJson
    };

    /// <summary>Maps a <see cref="BaselineChangeRequest"/> entity to its DTO.</summary>
    private static BaselineChangeRequestDto MapChangeRequestToDto(BaselineChangeRequest r) => new()
    {
        Id                   = r.Id,
        ProjectId            = r.ProjectId,
        RequestedByUserName  = r.RequestedByUserName,
        ChangeJustification  = r.ChangeJustification,
        ProposedChangesJson  = r.ProposedChangesJson,
        Status               = r.Status,
        ReviewedByUserName   = r.ReviewedByUserName,
        ReviewedAt           = r.ReviewedAt,
        ReviewNotes          = r.ReviewNotes,
        CreatedAt            = r.CreatedAt
    };
}
