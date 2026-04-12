using ActionTracker.Application.Common.Interfaces;
using ActionTracker.Application.Features.ProjectRisks.DTOs;
using ActionTracker.Application.Features.ProjectRisks.Interfaces;
using ActionTracker.Application.Helpers;
using ActionTracker.Domain.Entities;
using ActionTracker.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ActionTracker.Infrastructure.Services;

public class ProjectRiskService : IProjectRiskService
{
    private readonly IAppDbContext _db;
    private readonly IUserLookupService _userLookup;
    private readonly ILogger<ProjectRiskService> _logger;

    public ProjectRiskService(
        IAppDbContext db,
        IUserLookupService userLookup,
        ILogger<ProjectRiskService> logger)
    {
        _db         = db;
        _userLookup = userLookup;
        _logger     = logger;
    }

    public async Task<PagedResult<ProjectRiskSummaryDto>> GetByProjectAsync(
        Guid projectId, int page, int pageSize,
        string? status, string? rating, string? category,
        CancellationToken ct = default)
    {
        var query = _db.ProjectRisks
            .Where(r => r.ProjectId == projectId)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<RiskStatus>(status, true, out var parsedStatus))
            query = query.Where(r => r.Status == parsedStatus);

        if (!string.IsNullOrWhiteSpace(rating) && Enum.TryParse<RiskRating>(rating, true, out var parsedRating))
            query = query.Where(r => r.RiskRating == parsedRating);

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(r => r.Category == category);

        var projected = query
            .OrderByDescending(r => r.RiskScore)
            .ThenBy(r => r.RiskCode)
            .Select(r => new ProjectRiskSummaryDto
            {
                Id                   = r.Id,
                RiskCode             = r.RiskCode,
                Title                = r.Title,
                Category             = r.Category,
                RiskScore            = r.RiskScore,
                RiskRating           = r.RiskRating.ToString(),
                Status               = r.Status.ToString(),
                RiskOwnerDisplayName = r.RiskOwnerDisplayName,
                IdentifiedDate       = r.IdentifiedDate,
                DueDate              = r.DueDate,
            });

        return await PagedResult<ProjectRiskSummaryDto>.CreateAsync(projected, page, pageSize, ct);
    }

    public async Task<ProjectRiskDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var risk = await _db.ProjectRisks
            .Include(r => r.Project)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

        return risk is null ? null : MapToDto(risk);
    }

    public async Task<ProjectRiskDto> CreateAsync(
        CreateProjectRiskDto dto, string userId, string userDisplayName,
        CancellationToken ct = default)
    {
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == dto.ProjectId, ct)
            ?? throw new KeyNotFoundException($"Project {dto.ProjectId} not found.");

        // Generate RiskCode
        var prefix = "RISK-";
        var existingCodes = await _db.ProjectRisks
            .IgnoreQueryFilters()
            .Where(r => r.ProjectId == dto.ProjectId)
            .Select(r => r.RiskCode)
            .ToListAsync(ct);

        var nextSeq = existingCodes
            .Select(code => int.TryParse(code.Replace(prefix, ""), out var n) ? n : 0)
            .DefaultIfEmpty(0)
            .Max() + 1;

        var riskCode = $"{prefix}{nextSeq:D3}";

        // Compute scores
        var riskScore  = dto.ProbabilityScore * dto.ImpactScore;
        var riskRating = DeriveRating(riskScore);

        // Parse status
        var riskStatus = RiskStatus.Open;
        if (!string.IsNullOrWhiteSpace(dto.Status) && Enum.TryParse<RiskStatus>(dto.Status, true, out var parsed))
            riskStatus = parsed;

        // Resolve owner display name
        string? ownerDisplayName = null;
        if (!string.IsNullOrWhiteSpace(dto.RiskOwnerUserId))
            ownerDisplayName = await _userLookup.GetDisplayNameAsync(dto.RiskOwnerUserId, ct);

        var risk = new ProjectRisk
        {
            Id                    = Guid.NewGuid(),
            RiskCode              = riskCode,
            ProjectId             = dto.ProjectId,
            Title                 = dto.Title.Trim(),
            Description           = dto.Description.Trim(),
            Category              = dto.Category.Trim(),
            ProbabilityScore      = dto.ProbabilityScore,
            ImpactScore           = dto.ImpactScore,
            RiskScore             = riskScore,
            RiskRating            = riskRating,
            Status                = riskStatus,
            MitigationPlan        = dto.MitigationPlan?.Trim(),
            ContingencyPlan       = dto.ContingencyPlan?.Trim(),
            RiskOwnerUserId       = dto.RiskOwnerUserId,
            RiskOwnerDisplayName  = ownerDisplayName,
            IdentifiedDate        = DateTime.UtcNow,
            DueDate               = dto.DueDate,
            Notes                 = dto.Notes?.Trim(),
            CreatedByUserId       = userId,
            CreatedByDisplayName  = userDisplayName,
            CreatedAt             = DateTime.UtcNow,
            UpdatedAt             = DateTime.UtcNow,
        };

        _db.ProjectRisks.Add(risk);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("ProjectRisk {Code} created for project {ProjectId}", riskCode, dto.ProjectId);

        return (await GetByIdAsync(risk.Id, ct))!;
    }

    public async Task<ProjectRiskDto> UpdateAsync(Guid id, UpdateProjectRiskDto dto, CancellationToken ct = default)
    {
        var risk = await _db.ProjectRisks
            .FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new KeyNotFoundException($"ProjectRisk {id} not found.");

        var previousStatus = risk.Status;

        // Parse status
        if (!Enum.TryParse<RiskStatus>(dto.Status, true, out var newStatus))
            throw new ArgumentException($"Invalid status: {dto.Status}");

        // Compute scores
        var riskScore  = dto.ProbabilityScore * dto.ImpactScore;
        var riskRating = DeriveRating(riskScore);

        // Resolve owner display name
        string? ownerDisplayName = null;
        if (!string.IsNullOrWhiteSpace(dto.RiskOwnerUserId))
            ownerDisplayName = await _userLookup.GetDisplayNameAsync(dto.RiskOwnerUserId, ct);

        risk.Title                = dto.Title.Trim();
        risk.Description          = dto.Description.Trim();
        risk.Category             = dto.Category.Trim();
        risk.ProbabilityScore     = dto.ProbabilityScore;
        risk.ImpactScore          = dto.ImpactScore;
        risk.RiskScore            = riskScore;
        risk.RiskRating           = riskRating;
        risk.Status               = newStatus;
        risk.MitigationPlan       = dto.MitigationPlan?.Trim();
        risk.ContingencyPlan      = dto.ContingencyPlan?.Trim();
        risk.RiskOwnerUserId      = dto.RiskOwnerUserId;
        risk.RiskOwnerDisplayName = ownerDisplayName;
        risk.DueDate              = dto.DueDate;
        risk.ClosedDate           = dto.ClosedDate;
        risk.Notes                = dto.Notes?.Trim();

        // Status transitions
        if (newStatus == RiskStatus.Closed && previousStatus != RiskStatus.Closed)
            risk.ClosedDate = DateTime.UtcNow;
        else if (newStatus != RiskStatus.Closed && previousStatus == RiskStatus.Closed)
            risk.ClosedDate = null;

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("ProjectRisk {Id} updated", id);

        return (await GetByIdAsync(id, ct))!;
    }

    public async Task SoftDeleteAsync(Guid id, CancellationToken ct = default)
    {
        var risk = await _db.ProjectRisks
            .FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new KeyNotFoundException($"ProjectRisk {id} not found.");

        risk.IsDeleted = true;
        await _db.SaveChangesAsync(ct);
    }

    public async Task RestoreAsync(Guid id, CancellationToken ct = default)
    {
        var risk = await _db.ProjectRisks
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new KeyNotFoundException($"ProjectRisk {id} not found.");

        risk.IsDeleted = false;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<ProjectRiskStatsDto> GetStatsAsync(Guid projectId, CancellationToken ct = default)
    {
        var risks = await _db.ProjectRisks
            .Where(r => r.ProjectId == projectId)
            .Select(r => new { r.RiskRating, r.Status, r.DueDate })
            .ToListAsync(ct);

        var utcNow = DateTime.UtcNow;

        return new ProjectRiskStatsDto
        {
            TotalRisks    = risks.Count,
            OpenRisks     = risks.Count(r => r.Status == RiskStatus.Open),
            CriticalCount = risks.Count(r => r.RiskRating == RiskRating.Critical),
            HighCount     = risks.Count(r => r.RiskRating == RiskRating.High),
            MediumCount   = risks.Count(r => r.RiskRating == RiskRating.Medium),
            LowCount      = risks.Count(r => r.RiskRating == RiskRating.Low),
            ClosedCount   = risks.Count(r => r.Status == RiskStatus.Closed),
            OverdueCount  = risks.Count(r => r.DueDate < utcNow && r.Status != RiskStatus.Closed),
        };
    }

    // ── Helpers ──────────────────────────────────────────────────

    private static RiskRating DeriveRating(int riskScore) => riskScore switch
    {
        >= 20 => RiskRating.Critical,
        >= 12 => RiskRating.High,
        >= 5  => RiskRating.Medium,
        _     => RiskRating.Low,
    };

    private static ProjectRiskDto MapToDto(ProjectRisk r) => new()
    {
        Id                    = r.Id,
        RiskCode              = r.RiskCode,
        ProjectId             = r.ProjectId,
        ProjectName           = r.Project?.Name ?? string.Empty,
        Title                 = r.Title,
        Description           = r.Description,
        Category              = r.Category,
        ProbabilityScore      = r.ProbabilityScore,
        ImpactScore           = r.ImpactScore,
        RiskScore             = r.RiskScore,
        RiskRating            = r.RiskRating.ToString(),
        Status                = r.Status.ToString(),
        MitigationPlan        = r.MitigationPlan,
        ContingencyPlan       = r.ContingencyPlan,
        RiskOwnerUserId       = r.RiskOwnerUserId,
        RiskOwnerDisplayName  = r.RiskOwnerDisplayName,
        IdentifiedDate        = r.IdentifiedDate,
        DueDate               = r.DueDate,
        ClosedDate            = r.ClosedDate,
        Notes                 = r.Notes,
        CreatedByUserId       = r.CreatedByUserId,
        CreatedByDisplayName  = r.CreatedByDisplayName,
        CreatedAt             = r.CreatedAt,
        UpdatedAt             = r.UpdatedAt,
    };
}
