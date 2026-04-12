using ActionTracker.Application.Common;
using ActionTracker.Application.Common.Interfaces;
using ActionTracker.Application.Features.Milestones.DTOs;
using ActionTracker.Application.Features.Milestones.Interfaces;
using ActionTracker.Application.Features.Notifications;
using ActionTracker.Application.Features.Notifications.DTOs;
using ActionTracker.Domain.Entities;
using ActionTracker.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ActionTracker.Application.Features.Milestones.Services;

public class MilestoneService : IMilestoneService
{
    private readonly IAppDbContext _db;
    private readonly ILogger<MilestoneService> _logger;
    private readonly IEmailSender _emailSender;
    private readonly INotificationService _notificationService;
    private readonly AppSettings _appSettings;
    private readonly IServiceScopeFactory _scopeFactory;

    public MilestoneService(
        IAppDbContext db,
        ILogger<MilestoneService> logger,
        IEmailSender emailSender,
        INotificationService notificationService,
        IOptions<AppSettings> appSettings,
        IServiceScopeFactory scopeFactory)
    {
        _db                  = db;
        _logger              = logger;
        _emailSender         = emailSender;
        _notificationService = notificationService;
        _appSettings         = appSettings.Value;
        _scopeFactory        = scopeFactory;
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

        // Fire-and-forget email notification
        var capturedMilestone = milestone;
        var capturedProject = project;
        _ = Task.Run(async () =>
        {
            using var scope = _scopeFactory.CreateScope();
            var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
            var notifService = scope.ServiceProvider.GetRequiredService<INotificationService>();
            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

            try
            {
                var placeholders = BuildMilestonePlaceholders(capturedMilestone, capturedProject);
                var pmEmail = await GetUserEmailAsync(db, capturedProject.ProjectManagerUserId);
                if (pmEmail is not null)
                {
                    await emailSender.SendEmailAsync("Milestone.Created", placeholders,
                        [pmEmail], "Milestone", capturedMilestone.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending email for Milestone.Created {Code}", capturedMilestone.MilestoneCode);
            }

            // In-app notification — notify PM (no actor exclusion needed, milestone create has no userId context)
            try
            {
                var url = $"{_appSettings.FrontendBaseUrl}/projects/{capturedProject.Id}/milestones/{capturedMilestone.Id}";
                await notifService.CreateAsync(new CreateNotificationDto
                {
                    UserId               = capturedProject.ProjectManagerUserId,
                    Title                = "New Milestone",
                    Message              = $"{capturedMilestone.Name} added to {capturedProject.Name}",
                    Type                 = "Milestone",
                    ActionType           = "Created",
                    RelatedEntityType    = "Milestone",
                    RelatedEntityId      = capturedMilestone.Id,
                    RelatedEntityCode    = capturedMilestone.MilestoneCode,
                    Url                  = url,
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating notification for Milestone.Created {Code}", capturedMilestone.MilestoneCode);
            }
        });

        // Re-fetch with includes
        return (await GetByIdAsync(milestone.Id, ct))!;
    }

    public async Task<MilestoneResponseDto> UpdateAsync(Guid projectId, Guid milestoneId, MilestoneUpdateDto dto, CancellationToken ct)
    {
        var milestone = await _db.Milestones
            .FirstOrDefaultAsync(m => m.Id == milestoneId && m.ProjectId == projectId, ct)
            ?? throw new KeyNotFoundException($"Milestone {milestoneId} not found.");

        var previousStatus = milestone.Status;

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

        // Fire-and-forget email when status changes to Completed
        if (dto.Status == MilestoneStatus.Completed && previousStatus != MilestoneStatus.Completed)
        {
            var capturedMilestone = milestone;
            _ = Task.Run(async () =>
            {
                using var scope = _scopeFactory.CreateScope();
                var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
                var notifService = scope.ServiceProvider.GetRequiredService<INotificationService>();
                var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

                try
                {
                    var proj = await db.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
                    if (proj is null) return;

                    var placeholders = BuildMilestonePlaceholders(capturedMilestone, proj);
                    var recipients = new List<string>();

                    var pmEmail = await GetUserEmailAsync(db, proj.ProjectManagerUserId);
                    if (pmEmail is not null) recipients.Add(pmEmail);

                    if (!string.IsNullOrWhiteSpace(capturedMilestone.ApproverUserId))
                    {
                        var approverEmail = await GetUserEmailAsync(db, capturedMilestone.ApproverUserId);
                        if (approverEmail is not null) recipients.Add(approverEmail);
                    }

                    recipients = recipients.Distinct().ToList();
                    if (recipients.Count > 0)
                    {
                        await emailSender.SendEmailAsync("Milestone.Completed", placeholders,
                            recipients, "Milestone", capturedMilestone.Id);
                    }
                    // In-app notifications
                    var notifRecipients = new HashSet<string> { proj.ProjectManagerUserId };
                    if (!string.IsNullOrWhiteSpace(capturedMilestone.ApproverUserId))
                        notifRecipients.Add(capturedMilestone.ApproverUserId);

                    var url = $"{_appSettings.FrontendBaseUrl}/projects/{proj.Id}/milestones/{capturedMilestone.Id}";
                    var notifications = notifRecipients.Select(uid => new CreateNotificationDto
                    {
                        UserId               = uid,
                        Title                = "Milestone Completed",
                        Message              = $"{capturedMilestone.Name} ({capturedMilestone.MilestoneCode}) has been completed",
                        Type                 = "Milestone",
                        ActionType           = "Completed",
                        RelatedEntityType    = "Milestone",
                        RelatedEntityId      = capturedMilestone.Id,
                        RelatedEntityCode    = capturedMilestone.MilestoneCode,
                        Url                  = url,
                    }).ToList();
                    if (notifications.Count > 0)
                        await notifService.CreateBulkAsync(notifications);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending notifications for Milestone.Completed {Id}", capturedMilestone.Id);
                }
            });
        }

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

    // ── Email helpers ──────────────────────────────────────────

    private Dictionary<string, string> BuildMilestonePlaceholders(Milestone m, Project p) => new()
    {
        ["MilestoneCode"]        = m.MilestoneCode,
        ["MilestoneName"]        = m.Name,
        ["ProjectName"]          = p.Name,
        ["ProjectCode"]          = p.ProjectCode,
        ["Status"]               = m.Status.ToString(),
        ["PlannedDueDate"]       = m.PlannedDueDate.ToString("yyyy-MM-dd"),
        ["CompletionPercentage"] = m.CompletionPercentage.ToString(),
        ["ItemUrl"]              = $"{_appSettings.FrontendBaseUrl}/projects/{p.Id}/milestones/{m.Id}",
    };

    private async Task<string?> GetUserEmailAsync(string? userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return null;
        return await _db.Users
            .Where(u => u.Id == userId && u.IsActive)
            .Select(u => u.Email)
            .FirstOrDefaultAsync();
    }

    private static async Task<string?> GetUserEmailAsync(IAppDbContext db, string? userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return null;
        return await db.Users
            .Where(u => u.Id == userId && u.IsActive)
            .Select(u => u.Email)
            .FirstOrDefaultAsync();
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
