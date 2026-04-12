# B-RR-02 — ProjectRisk DTOs, Validators, Service Interface + Implementation

## Context
B-RR-01 created the `ProjectRisk` entity and migration. This prompt adds the application layer: DTOs, FluentValidation validators, and the service with full CRUD logic.

## Requirements

### 1. Create DTOs in `ActionTracker.Application/Features/ProjectRisks/DTOs/`

**ProjectRiskDto** (response DTO):
- Id (Guid), RiskCode, ProjectId, ProjectName (string), Title, Description, Category
- ProbabilityScore (int), ImpactScore (int), RiskScore (int), RiskRating (string)
- Status (string), MitigationPlan, ContingencyPlan
- RiskOwnerUserId, RiskOwnerDisplayName
- IdentifiedDate, DueDate, ClosedDate, Notes
- CreatedByUserId, CreatedByDisplayName, CreatedAt, UpdatedAt

**CreateProjectRiskDto** (request):
- ProjectId (Guid, required), Title (required), Description (required)
- Category (required), ProbabilityScore (int, required), ImpactScore (int, required)
- Status (string, optional — default "Open")
- MitigationPlan, ContingencyPlan, RiskOwnerUserId, DueDate, Notes

**UpdateProjectRiskDto** (request):
- Title (required), Description (required), Category (required)
- ProbabilityScore (int, required), ImpactScore (int, required)
- Status (string, required)
- MitigationPlan, ContingencyPlan, RiskOwnerUserId, DueDate, ClosedDate, Notes

**ProjectRiskSummaryDto** (lightweight for lists):
- Id, RiskCode, Title, Category, RiskScore, RiskRating (string), Status (string)
- RiskOwnerDisplayName, IdentifiedDate, DueDate

### 2. Create validators in `ActionTracker.Application/Features/ProjectRisks/Validators/`

**CreateProjectRiskDtoValidator:**
- ProjectId must not be empty
- Title required, max 300 chars
- Description required, max 2000 chars
- Category required, max 100 chars
- ProbabilityScore must be between 1 and 5
- ImpactScore must be between 1 and 5
- Status if provided must be one of: Open, Mitigating, Accepted, Transferred, Closed
- MitigationPlan max 2000, ContingencyPlan max 2000, Notes max 2000
- DueDate if provided must be in the future

**UpdateProjectRiskDtoValidator:**
- Same rules as Create except ProjectId not needed, Status is required, DueDate allows past (already set)

### 3. Create `IProjectRiskService` in `ActionTracker.Application/Features/ProjectRisks/`

```
Task<PagedResult<ProjectRiskSummaryDto>> GetByProjectAsync(Guid projectId, int page, int pageSize, string? status, string? rating, string? category);
Task<ProjectRiskDto?> GetByIdAsync(Guid id);
Task<ProjectRiskDto> CreateAsync(CreateProjectRiskDto dto, string userId, string userDisplayName);
Task<ProjectRiskDto> UpdateAsync(Guid id, UpdateProjectRiskDto dto);
Task SoftDeleteAsync(Guid id);
Task RestoreAsync(Guid id);
Task<ProjectRiskStatsDto> GetStatsAsync(Guid projectId);
```

**ProjectRiskStatsDto:**
- TotalRisks (int), OpenRisks (int), CriticalCount (int), HighCount (int), MediumCount (int), LowCount (int), ClosedCount (int), OverdueCount (int)

### 4. Create `ProjectRiskService` in `ActionTracker.Infrastructure/Services/`

Business logic:
- **RiskCode auto-generation:** Query max existing RiskCode for the project, parse the numeric suffix, increment. Format: `RISK-001`, `RISK-002`, etc.
- **RiskScore calculation:** `ProbabilityScore × ImpactScore` (computed on create and update)
- **RiskRating derivation:** 20–25 = Critical, 12–19 = High, 5–11 = Medium, 1–4 = Low
- **Status transitions:** When status changes to `Closed`, auto-set `ClosedDate` to UTC now. When reopened, clear `ClosedDate`.
- **RiskOwnerDisplayName:** If `RiskOwnerUserId` is provided, look up the user's display name from the database and denormalize it
- **Soft delete:** Set `IsDeleted = true`, do NOT hard delete
- **Restore:** Set `IsDeleted = false`
- **Filtering:** Support filter by status, rating, and category. All queries exclude `IsDeleted = true`.
- **Stats:** Count risks grouped by rating and status for the project
- **Overdue:** A risk is overdue if `DueDate < DateTime.UtcNow` and `Status != Closed`

### 5. Register `IProjectRiskService` / `ProjectRiskService` in DI
Add to the service registration in the DI configuration file (this modifies an existing file).

## Rules
- All methods async
- Use existing `PagedResult<T>` wrapper for paginated results
- No FK to AspNetUsers — look up display name via `UserManager<ApplicationUser>` and store as denormalized string
- UTC everywhere
