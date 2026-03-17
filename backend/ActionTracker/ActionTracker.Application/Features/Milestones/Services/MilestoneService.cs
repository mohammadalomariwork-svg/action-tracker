using ActionTracker.Application.Common.Interfaces;
using ActionTracker.Application.Features.Milestones.DTOs;
using ActionTracker.Application.Features.Milestones.Interfaces;
using ActionTracker.Domain.Entities;
using ActionTracker.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ActionTracker.Application.Features.Milestones.Services;

public class MilestoneService : IMilestoneService
{
    private readonly IAppDbContext _db;
    private readonly ILogger<MilestoneService> _logger;

    public MilestoneService(IAppDbContext db, ILogger<MilestoneService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<List<MilestoneResponseDto>> GetByProjectAsync(Guid projectId, CancellationToken ct)
    {
        var milestones = await _db.Milestones
            .Where(m => m.ProjectId == projectId)
            .Include(m => m.Approver)
            .OrderBy(m => m.SequenceOrder)
            .ThenBy(m => m.PlannedStartDate)
            .ToListAsync(ct);

        return milestones.Select(MapToDto).ToList();
    }

    public async Task<MilestoneResponseDto?> GetByIdAsync(Guid milestoneId, CancellationToken ct)
    {
        var milestone = await _db.Milestones
            .Include(m => m.Approver)
            .FirstOrDefaultAsync(m => m.Id == milestoneId, ct);

        return milestone is null ? null : MapToDto(milestone);
    }

    public async Task<MilestoneResponseDto> CreateAsync(Guid projectId, MilestoneCreateDto dto, CancellationToken ct)
    {
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct)
            ?? throw new KeyNotFoundException($"Project {projectId} not found.");

        if (dto.PlannedDueDate < dto.PlannedStartDate)
            throw new ArgumentException("Planned due date must be on or after the planned start date.");

        // Generate a globally unique milestone code for the current year.
        // We look at ALL milestones (across every project) that share the
        // "MS-YYYY-" prefix, find the highest sequence already used, and
        // increment it — this prevents collisions when multiple projects
        // each receive their first milestone in the same year.
        var year   = DateTime.UtcNow.Year;
        var prefix = $"MS-{year}-";
        var existingSequences = await _db.Milestones
            .IgnoreQueryFilters()
            .Where(m => m.MilestoneCode.StartsWith(prefix))
            .Select(m => m.MilestoneCode)
            .ToListAsync(ct);

        var nextSeq = existingSequences
            .Select(code => int.TryParse(code[prefix.Length..], out var n) ? n : 0)
            .DefaultIfEmpty(0)
            .Max() + 1;

        var milestoneCode = $"{prefix}{nextSeq:D3}";

        var milestone = new Milestone
        {
            Id = Guid.NewGuid(),
            MilestoneCode = milestoneCode,
            Name = dto.Name.Trim(),
            Description = dto.Description?.Trim(),
            ProjectId = projectId,
            SequenceOrder = dto.SequenceOrder,
            PlannedStartDate = dto.PlannedStartDate,
            PlannedDueDate = dto.PlannedDueDate,
            IsDeadlineFixed = dto.IsDeadlineFixed,
            Status = MilestoneStatus.NotStarted,
            CompletionPercentage = dto.CompletionPercentage,
            ApproverUserId = dto.ApproverUserId,
            CreatedAt = DateTime.UtcNow,
        };

        _db.Milestones.Add(milestone);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Milestone {Code} created for project {ProjectId}", milestoneCode, projectId);

        // Re-fetch with includes
        return (await GetByIdAsync(milestone.Id, ct))!;
    }

    public async Task<MilestoneResponseDto> UpdateAsync(Guid projectId, Guid milestoneId, MilestoneUpdateDto dto, CancellationToken ct)
    {
        var milestone = await _db.Milestones
            .FirstOrDefaultAsync(m => m.Id == milestoneId && m.ProjectId == projectId, ct)
            ?? throw new KeyNotFoundException($"Milestone {milestoneId} not found.");

        // Check if project is baselined — if so, block date changes
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct);
        if (project?.IsBaselined == true)
        {
            if (milestone.PlannedStartDate != dto.PlannedStartDate ||
                milestone.PlannedDueDate != dto.PlannedDueDate)
            {
                throw new ArgumentException("Cannot modify milestone dates on a baselined project.");
            }
        }

        if (dto.PlannedDueDate < dto.PlannedStartDate)
            throw new ArgumentException("Planned due date must be on or after the planned start date.");

        milestone.Name = dto.Name.Trim();
        milestone.Description = dto.Description?.Trim();
        milestone.SequenceOrder = dto.SequenceOrder;
        milestone.PlannedStartDate = dto.PlannedStartDate;
        milestone.PlannedDueDate = dto.PlannedDueDate;
        milestone.ActualCompletionDate = dto.ActualCompletionDate;
        milestone.IsDeadlineFixed = dto.IsDeadlineFixed;
        milestone.Status = dto.Status;
        milestone.CompletionPercentage = dto.CompletionPercentage;
        milestone.ApproverUserId = dto.ApproverUserId;

        // Auto-set ActualCompletionDate when marked Completed
        if (dto.Status == MilestoneStatus.Completed && milestone.ActualCompletionDate is null)
            milestone.ActualCompletionDate = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return (await GetByIdAsync(milestoneId, ct))!;
    }

    public async Task DeleteAsync(Guid projectId, Guid milestoneId, CancellationToken ct)
    {
        var milestone = await _db.Milestones
            .FirstOrDefaultAsync(m => m.Id == milestoneId && m.ProjectId == projectId, ct)
            ?? throw new KeyNotFoundException($"Milestone {milestoneId} not found.");

        milestone.IsDeleted = true;
        await _db.SaveChangesAsync(ct);
    }

    public async Task BaselineMilestonesAsync(Guid projectId, CancellationToken ct)
    {
        var milestones = await _db.Milestones
            .Where(m => m.ProjectId == projectId)
            .ToListAsync(ct);

        foreach (var m in milestones)
        {
            m.BaselinePlannedStartDate = m.PlannedStartDate;
            m.BaselinePlannedDueDate = m.PlannedDueDate;
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Baselined {Count} milestones for project {ProjectId}", milestones.Count, projectId);
    }

    public async Task<MilestoneStatsDto> GetMilestoneStatsAsync(Guid milestoneId, CancellationToken ct)
    {
        var actionItems = await _db.ActionItems
            .Where(a => a.MilestoneId == milestoneId)
            .Select(a => new
            {
                a.Status,
                a.IsEscalated,
                a.DueDate,
                a.UpdatedAt,
            })
            .ToListAsync(ct);

        var total = actionItems.Count;
        var doneCount = actionItems.Count(a => a.Status == ActionStatus.Done);
        var completionRate = total > 0
            ? Math.Round((decimal)doneCount / total * 100, 1)
            : 0m;

        var doneOnTime = actionItems.Count(a =>
            a.Status == ActionStatus.Done &&
            a.UpdatedAt <= a.DueDate);
        var onTimeRate = doneCount > 0
            ? Math.Round((decimal)doneOnTime / doneCount * 100, 1)
            : 0m;

        var escalated = actionItems.Count(a => a.IsEscalated);

        return new MilestoneStatsDto
        {
            TotalActionItems = total,
            CompletionRate = completionRate,
            OnTimeDeliveryRate = onTimeRate,
            EscalatedActionItems = escalated,
        };
    }

    private static MilestoneResponseDto MapToDto(Milestone m)
    {
        int? varianceDays = null;
        if (m.BaselinePlannedDueDate.HasValue)
        {
            var compareDate = m.ActualCompletionDate ?? m.PlannedDueDate;
            varianceDays = (int)(compareDate - m.BaselinePlannedDueDate.Value).TotalDays;
        }

        return new MilestoneResponseDto
        {
            Id = m.Id,
            MilestoneCode = m.MilestoneCode,
            Name = m.Name,
            Description = m.Description,
            ProjectId = m.ProjectId,
            SequenceOrder = m.SequenceOrder,
            PlannedStartDate = m.PlannedStartDate,
            PlannedDueDate = m.PlannedDueDate,
            ActualCompletionDate = m.ActualCompletionDate,
            IsDeadlineFixed = m.IsDeadlineFixed,
            Status = m.Status,
            CompletionPercentage = m.CompletionPercentage,
            ApproverUserId = m.ApproverUserId,
            ApproverName = m.Approver != null
                ? $"{m.Approver.FirstName} {m.Approver.LastName}".Trim()
                : null,
            BaselinePlannedStartDate = m.BaselinePlannedStartDate,
            BaselinePlannedDueDate = m.BaselinePlannedDueDate,
            ScheduleVarianceDays = varianceDays,
            CreatedAt = m.CreatedAt,
            UpdatedAt = m.UpdatedAt,
        };
    }
}
