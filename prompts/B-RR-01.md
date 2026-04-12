# B-RR-01 — ProjectRisk Entity, Enum, EF Configuration, Migration

## Context
We are adding a PMI-compliant Risk Register to the Action Tracker. Each project can have zero or more risks. This prompt creates the domain entity, enum, and database migration only.

## Requirements

### 1. Create enum `RiskRating` in `ActionTracker.Domain/Enums/`
Values: `Critical = 0`, `High = 1`, `Medium = 2`, `Low = 3`

### 2. Create enum `RiskStatus` in `ActionTracker.Domain/Enums/`
Values: `Open = 0`, `Mitigating = 1`, `Accepted = 2`, `Transferred = 3`, `Closed = 4`

### 3. Create entity `ProjectRisk` in `ActionTracker.Domain/Entities/`

| Field | Type | Notes |
|-------|------|-------|
| Id | Guid | PK, default `Guid.NewGuid()` |
| RiskCode | string (20) | Auto-generated per project: `RISK-001`, `RISK-002` |
| ProjectId | Guid | FK to Project (required) |
| Title | string (300) | Risk title |
| Description | string (2000) | Detailed description |
| Category | string (100) | Risk category (e.g. Technical, Schedule, Resource, Budget, External, Quality) |
| ProbabilityScore | int | 1–5 scale |
| ImpactScore | int | 1–5 scale |
| RiskScore | int | Computed: ProbabilityScore × ImpactScore (1–25) |
| RiskRating | RiskRating enum | Derived from RiskScore: 20–25 = Critical, 12–19 = High, 5–11 = Medium, 1–4 = Low |
| Status | RiskStatus enum | Default `Open` |
| MitigationPlan | string? (2000) | Mitigation strategy |
| ContingencyPlan | string? (2000) | Fallback plan if risk occurs |
| RiskOwnerUserId | string? | User responsible for monitoring (no FK to AspNetUsers — store as plain string) |
| RiskOwnerDisplayName | string? (200) | Denormalized display name |
| IdentifiedDate | DateTime | Date risk was identified, default UTC now |
| DueDate | DateTime? | Date by which mitigation must be applied |
| ClosedDate | DateTime? | Date risk was closed |
| Notes | string? (2000) | Additional notes |
| CreatedByUserId | string? | Creator user ID (no FK) |
| CreatedByDisplayName | string? (200) | Denormalized |
| CreatedAt | DateTime | UTC |
| UpdatedAt | DateTime | UTC |
| IsDeleted | bool | Soft delete, default false |

Navigation property: `public virtual Project Project { get; set; }` (required)

### 4. Add `ICollection<ProjectRisk> Risks` navigation to the existing `Project` entity
This is the ONLY modification to an existing file. Add the collection property to the Project entity class.

### 5. Create EF configuration `ProjectRiskConfiguration` in `ActionTracker.Infrastructure/Data/Configurations/`
- Map to table `ProjectRisks`
- Configure `ProjectId` FK with `DeleteBehavior.Cascade` (when project is soft-deleted, risks cascade)
- `RiskOwnerUserId` — no FK constraint, just a plain string column
- `CreatedByUserId` — no FK constraint, just a plain string column
- Index on `ProjectId` + `IsDeleted`
- Index on `RiskCode` + `ProjectId` (unique, filtered where `IsDeleted = false`)
- `RiskRating` and `Status` stored as int

### 6. Register `DbSet<ProjectRisk>` in the `ApplicationDbContext`

### 7. Create migration
Run: `dotnet ef migrations add AddProjectRisks --project ActionTracker.Infrastructure --startup-project ActionTracker.Api`

## Rules
- GUID primary key
- No FK to AspNetUsers — user references are plain strings with denormalized display names
- Soft delete pattern with `IsDeleted` flag
- UTC for all DateTime fields
- Do NOT create any services, DTOs, or controllers in this prompt
