using ActionTracker.Application.Features.Dashboard;
using ActionTracker.Application.Common.Interfaces;
using ActionTracker.Application.Features.KpiActuals;
using ActionTracker.Application.Features.KpiActuals.DTOs;
using ActionTracker.Application.Features.KpiActuals.Interfaces;
using ActionTracker.Application.Helpers;
using ActionTracker.Domain.Entities;
using ActionTracker.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ActionTracker.Infrastructure.Services;

/// <summary>
/// CR-001-F — CRUD service for KPI actuals.
///
/// IMPORTANT: actuals deliberately do NOT call <c>IStrategicPlanLockGuard</c>.
/// Per CR-001-E §3 / §11 and CR-001-F §4.5, actuals are the ongoing reporting
/// layer and remain editable even when the parent plan is approved/locked.
/// </summary>
public class KpiActualService : IKpiActualService
{
    private readonly IAppDbContext _db;
    private readonly IStrategicScopeService _scopeService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<KpiActualService> _logger;

    public KpiActualService(
        IAppDbContext db,
        IStrategicScopeService scopeService,
        UserManager<ApplicationUser> userManager,
        ILogger<KpiActualService> logger)
    {
        _db = db;
        _scopeService = scopeService;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<KpiActualDto> CreateAsync(
        CreateKpiActualRequestDto dto, string currentUserId, CancellationToken ct = default)
    {
        var (target, kpi, orgUnitId) = await LoadTargetContextAsync(dto.KpiTargetId, ct);

        await _scopeService.EnsureCanWriteAsync(currentUserId, orgUnitId, ct);
        ValidatePeriodMatchesKpi(kpi, dto.PeriodType, dto.PeriodIndex);

        // Filtered unique on (target, period, year) where Status <> Rejected.
        var conflict = await _db.KpiActuals.AnyAsync(a =>
            a.KpiTargetId == dto.KpiTargetId
            && a.PeriodType == dto.PeriodType
            && a.PeriodIndex == dto.PeriodIndex
            && a.Year == dto.Year
            && a.Status != KpiActualStatus.Rejected, ct);
        if (conflict)
            throw new InvalidOperationException(
                $"An active actual already exists for {KpiPeriodCalendar.GetPeriodLabel(dto.PeriodType, dto.PeriodIndex)} {dto.Year}.");

        var entity = new KpiActual
        {
            Id = Guid.NewGuid(),
            KpiTargetId = dto.KpiTargetId,
            PeriodType = dto.PeriodType,
            PeriodIndex = dto.PeriodIndex,
            Year = dto.Year,
            ActualValue = dto.ActualValue,
            Notes = dto.Notes?.Trim(),
            Status = KpiActualStatus.Draft,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = currentUserId,
        };

        _db.KpiActuals.Add(entity);
        await _db.SaveChangesAsync(ct);

        return await BuildDtoAsync(entity.Id, ct)
               ?? throw new InvalidOperationException("Failed to load newly created actual.");
    }

    public async Task<KpiActualDto> UpdateAsync(
        Guid actualId, UpdateKpiActualRequestDto dto, string currentUserId, CancellationToken ct = default)
    {
        var entity = await _db.KpiActuals.FirstOrDefaultAsync(a => a.Id == actualId, ct)
            ?? throw new KeyNotFoundException($"KPI actual '{actualId}' not found.");

        if (entity.Status != KpiActualStatus.Draft)
            throw new ArgumentException("Only drafts can be edited. Submit a new draft if revisions are required.");

        var (_, _, orgUnitId) = await LoadTargetContextAsync(entity.KpiTargetId, ct);
        await _scopeService.EnsureCanWriteAsync(currentUserId, orgUnitId, ct);

        entity.ActualValue = dto.ActualValue;
        entity.Notes = dto.Notes?.Trim();
        entity.ModifiedAtUtc = DateTime.UtcNow;
        entity.ModifiedByUserId = currentUserId;

        await _db.SaveChangesAsync(ct);

        return await BuildDtoAsync(entity.Id, ct)
               ?? throw new InvalidOperationException("Failed to reload actual after update.");
    }

    public async Task DeleteAsync(Guid actualId, string currentUserId, CancellationToken ct = default)
    {
        var entity = await _db.KpiActuals.FirstOrDefaultAsync(a => a.Id == actualId, ct)
            ?? throw new KeyNotFoundException($"KPI actual '{actualId}' not found.");

        if (entity.Status != KpiActualStatus.Draft)
            throw new ArgumentException("Only drafts can be deleted.");

        var (_, _, orgUnitId) = await LoadTargetContextAsync(entity.KpiTargetId, ct);
        await _scopeService.EnsureCanWriteAsync(currentUserId, orgUnitId, ct);

        entity.IsDeleted = true;
        entity.ModifiedAtUtc = DateTime.UtcNow;
        entity.ModifiedByUserId = currentUserId;

        await _db.SaveChangesAsync(ct);
    }

    public async Task<KpiActualDto?> GetByIdAsync(Guid actualId, string currentUserId, CancellationToken ct = default)
    {
        var entity = await _db.KpiActuals
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == actualId, ct);
        if (entity is null) return null;

        var (_, _, orgUnitId) = await LoadTargetContextAsync(entity.KpiTargetId, ct);
        var visible = await _scopeService.GetVisibleOrgUnitIdsAsync(currentUserId, ct);
        if (visible is not null && !visible.Contains(orgUnitId))
            return null;

        return await BuildDtoAsync(actualId, ct);
    }

    public async Task<KpiActualGridResponseDto> GetGridAsync(
        Guid kpiTargetId, int year, string currentUserId, CancellationToken ct = default)
    {
        var (target, kpi, orgUnitId) = await LoadTargetContextAsync(kpiTargetId, ct);

        var visible = await _scopeService.GetVisibleOrgUnitIdsAsync(currentUserId, ct);
        if (visible is not null && !visible.Contains(orgUnitId))
            throw new UnauthorizedAccessException("You do not have access to this KPI target.");

        var periodType = KpiPeriodCalendar.FromKpiPeriod(kpi.Period);
        var slotCount = KpiPeriodCalendar.SlotCount(periodType);

        var actuals = await _db.KpiActuals
            .AsNoTracking()
            .Where(a => a.KpiTargetId == kpiTargetId && a.Year == year)
            .OrderByDescending(a => a.CreatedAtUtc)
            .ToListAsync(ct);

        var orgUnitName = await _db.OrgUnits.AsNoTracking()
            .Where(o => o.Id == orgUnitId)
            .Select(o => o.Name)
            .FirstOrDefaultAsync(ct) ?? string.Empty;

        var rows = new List<KpiActualGridRowDto>();
        for (int idx = 1; idx <= slotCount; idx++)
        {
            var current = actuals
                .Where(a => a.PeriodType == periodType
                    && a.PeriodIndex == idx
                    && a.Status != KpiActualStatus.Rejected)
                .OrderByDescending(a => a.CreatedAtUtc)
                .FirstOrDefault();

            var endUtc = KpiPeriodCalendar.GetPeriodEndUtc(periodType, idx, year);
            var row = new KpiActualGridRowDto
            {
                PeriodType = periodType,
                PeriodIndex = idx,
                Year = year,
                PeriodLabel = KpiPeriodCalendar.GetPeriodLabel(periodType, idx),
                TargetValue = target.Target,
                Direction = target.Direction,
                PeriodEndUtc = endUtc,
                PeriodHasEnded = DateTime.UtcNow >= endUtc,
                Current = current is null ? null : MapToDto(current, target, kpi, orgUnitId, orgUnitName, 0),
            };
            rows.Add(row);
        }

        // Backfill document counts for the rows that have a current actual.
        var actualIds = rows.Where(r => r.Current is not null).Select(r => r.Current!.Id).ToList();
        if (actualIds.Count > 0)
        {
            var counts = await _db.Documents
                .AsNoTracking()
                .Where(d => d.RelatedEntityType == "KpiActual" && actualIds.Contains(d.RelatedEntityId))
                .GroupBy(d => d.RelatedEntityId)
                .Select(g => new { Id = g.Key, Count = g.Count() })
                .ToListAsync(ct);
            var lookup = counts.ToDictionary(c => c.Id, c => c.Count);
            foreach (var row in rows.Where(r => r.Current is not null))
                row.Current!.DocumentCount = lookup.GetValueOrDefault(row.Current.Id, 0);
        }

        return new KpiActualGridResponseDto
        {
            KpiId = kpi.Id,
            KpiTargetId = target.Id,
            KpiName = kpi.Name,
            PeriodType = periodType,
            Year = year,
            Rows = rows,
        };
    }

    public async Task<List<KpiActualDto>> ListByKpiAsync(
        Guid kpiId, int year, string currentUserId, CancellationToken ct = default)
    {
        var kpi = await _db.Kpis
            .AsNoTracking()
            .Include(k => k.StrategicObjective)
            .FirstOrDefaultAsync(k => k.Id == kpiId, ct)
            ?? throw new KeyNotFoundException($"KPI '{kpiId}' not found.");

        var orgUnitId = kpi.StrategicObjective.OrgUnitId;
        var visible = await _scopeService.GetVisibleOrgUnitIdsAsync(currentUserId, ct);
        if (visible is not null && !visible.Contains(orgUnitId))
            throw new UnauthorizedAccessException("You do not have access to this KPI.");

        var targetIds = await _db.KpiTargets
            .AsNoTracking()
            .Where(t => t.KpiId == kpiId)
            .Select(t => t.Id)
            .ToListAsync(ct);

        var rows = await _db.KpiActuals
            .AsNoTracking()
            .Include(a => a.KpiTarget)
            .Where(a => targetIds.Contains(a.KpiTargetId) && a.Year == year)
            .OrderBy(a => a.PeriodType).ThenBy(a => a.PeriodIndex)
            .ToListAsync(ct);

        var orgUnitName = await _db.OrgUnits.AsNoTracking()
            .Where(o => o.Id == orgUnitId)
            .Select(o => o.Name)
            .FirstOrDefaultAsync(ct) ?? string.Empty;

        var docCounts = await _db.Documents
            .AsNoTracking()
            .Where(d => d.RelatedEntityType == "KpiActual"
                        && rows.Select(r => r.Id).Contains(d.RelatedEntityId))
            .GroupBy(d => d.RelatedEntityId)
            .Select(g => new { Id = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var lookup = docCounts.ToDictionary(c => c.Id, c => c.Count);

        return rows
            .Select(a => MapToDto(a, a.KpiTarget!, kpi, orgUnitId, orgUnitName, lookup.GetValueOrDefault(a.Id, 0)))
            .ToList();
    }

    public async Task<List<KpiActualDto>> GetHistoryAsync(
        Guid kpiTargetId, int periodType, int periodIndex, int year, string currentUserId, CancellationToken ct = default)
    {
        var (target, kpi, orgUnitId) = await LoadTargetContextAsync(kpiTargetId, ct);

        var visible = await _scopeService.GetVisibleOrgUnitIdsAsync(currentUserId, ct);
        if (visible is not null && !visible.Contains(orgUnitId))
            throw new UnauthorizedAccessException("You do not have access to this KPI target.");

        var pt = (KpiActualPeriodType)periodType;
        var rows = await _db.KpiActuals
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(a => a.KpiTargetId == kpiTargetId
                        && a.PeriodType == pt
                        && a.PeriodIndex == periodIndex
                        && a.Year == year)
            .OrderByDescending(a => a.CreatedAtUtc)
            .ToListAsync(ct);

        var orgUnitName = await _db.OrgUnits.AsNoTracking()
            .Where(o => o.Id == orgUnitId)
            .Select(o => o.Name)
            .FirstOrDefaultAsync(ct) ?? string.Empty;

        var docCounts = await _db.Documents
            .AsNoTracking()
            .Where(d => d.RelatedEntityType == "KpiActual"
                        && rows.Select(r => r.Id).Contains(d.RelatedEntityId))
            .GroupBy(d => d.RelatedEntityId)
            .Select(g => new { Id = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var lookup = docCounts.ToDictionary(c => c.Id, c => c.Count);

        return rows
            .Select(a => MapToDto(a, target, kpi, orgUnitId, orgUnitName, lookup.GetValueOrDefault(a.Id, 0)))
            .ToList();
    }

    // ─────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────

    private async Task<(KpiTarget Target, Kpi Kpi, Guid OrgUnitId)> LoadTargetContextAsync(
        Guid kpiTargetId, CancellationToken ct)
    {
        var target = await _db.KpiTargets
            .Include(t => t.Kpi)
                .ThenInclude(k => k.StrategicObjective)
            .FirstOrDefaultAsync(t => t.Id == kpiTargetId, ct)
            ?? throw new KeyNotFoundException($"KPI target '{kpiTargetId}' not found.");

        return (target, target.Kpi, target.Kpi.StrategicObjective.OrgUnitId);
    }

    private static void ValidatePeriodMatchesKpi(Kpi kpi, KpiActualPeriodType periodType, int periodIndex)
    {
        var expected = KpiPeriodCalendar.FromKpiPeriod(kpi.Period);
        if (expected != periodType)
            throw new ArgumentException(
                $"Period type {periodType} is not compatible with KPI measurement period {kpi.Period}. Expected {expected}.");

        var max = KpiPeriodCalendar.SlotCount(periodType);
        if (periodIndex < 1 || periodIndex > max)
            throw new ArgumentException(
                $"Period index {periodIndex} is out of range for {periodType} (1..{max}).");
    }

    internal async Task<KpiActualDto?> BuildDtoAsync(Guid actualId, CancellationToken ct)
    {
        var entity = await _db.KpiActuals
            .AsNoTracking()
            .Include(a => a.KpiTarget)
                .ThenInclude(t => t.Kpi)
                    .ThenInclude(k => k.StrategicObjective)
            .FirstOrDefaultAsync(a => a.Id == actualId, ct);
        if (entity is null) return null;

        var orgUnitId = entity.KpiTarget.Kpi.StrategicObjective.OrgUnitId;
        var orgUnitName = await _db.OrgUnits.AsNoTracking()
            .Where(o => o.Id == orgUnitId)
            .Select(o => o.Name)
            .FirstOrDefaultAsync(ct) ?? string.Empty;

        var docCount = await _db.Documents.AsNoTracking()
            .CountAsync(d => d.RelatedEntityType == "KpiActual" && d.RelatedEntityId == actualId, ct);

        return MapToDto(entity, entity.KpiTarget, entity.KpiTarget.Kpi, orgUnitId, orgUnitName, docCount);
    }

    internal static KpiActualDto MapToDto(
        KpiActual a, KpiTarget target, Kpi kpi, Guid orgUnitId, string orgUnitName, int documentCount)
    {
        return new KpiActualDto
        {
            Id = a.Id,
            KpiTargetId = a.KpiTargetId,
            KpiId = kpi.Id,
            KpiName = kpi.Name,
            StrategicObjectiveId = kpi.StrategicObjectiveId,
            OrgUnitId = orgUnitId,
            OrgUnitName = orgUnitName,
            PeriodType = a.PeriodType,
            PeriodIndex = a.PeriodIndex,
            Year = a.Year,
            PeriodLabel = KpiPeriodCalendar.GetPeriodLabel(a.PeriodType, a.PeriodIndex),
            ActualValue = a.ActualValue,
            TargetValue = target.Target,
            Direction = target.Direction,
            AchievementPercent = ComputeAchievementPercent(a, target),
            Notes = a.Notes,
            Status = a.Status,
            SubmittedByUserId = a.SubmittedByUserId,
            SubmittedByDisplayName = a.SubmittedByDisplayName,
            SubmittedAtUtc = a.SubmittedAtUtc,
            AssignedReviewerUserId = a.AssignedReviewerUserId,
            AssignedReviewerDisplayName = a.AssignedReviewerDisplayName,
            ReviewedByUserId = a.ReviewedByUserId,
            ReviewedByDisplayName = a.ReviewedByDisplayName,
            ReviewedAtUtc = a.ReviewedAtUtc,
            ReviewerComment = a.ReviewerComment,
            CreatedAtUtc = a.CreatedAtUtc,
            ModifiedAtUtc = a.ModifiedAtUtc,
            DocumentCount = documentCount,
        };
    }

    /// <summary>
    /// Direction-aware achievement percent. Only meaningful when the actual is
    /// Approved (the UI hides the column otherwise).
    /// </summary>
    private static decimal? ComputeAchievementPercent(KpiActual a, KpiTarget target) =>
        KpiAchievement.Compute(a.ActualValue, target.Target, target.Direction);
}
