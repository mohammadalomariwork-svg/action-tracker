# Prompt: CR-001-A — Strategic Plan Entity (time-bounded wrapper)

> **CR reference:** `CR-001-Strategic-Plans-and-KPI-Actuals.docx`, section CR-001-A
> **Phase:** 1 (Structure)
> **Depends on:** nothing
> **Blocks:** CR-001-B (Strategy page), CR-001-C (PMO setup), all of Phase 2 & 3

---

## 0. Read first

Before writing any code, read the following files in this order:

1. `CLAUDE.md` (project briefing — already loaded)
2. `BACKEND_OVERVIEW.md` (backend architecture and existing patterns)
3. `FRONTEND_OVERVIEW.md` (frontend layout)
4. `status.md` (what's already implemented)
5. `backend/ActionTracker/ActionTracker.Domain/Entities/StrategicObjective.cs`
6. `backend/ActionTracker/ActionTracker.Domain/Entities/Project.cs` — model your `StrategicPlan` status enum after `ProjectStatus`
7. `backend/ActionTracker/ActionTracker.Application/StrategicObjectives/` — mirror the CQRS shape
8. `backend/ActionTracker/ActionTracker.Infrastructure/Persistence/Configurations/StrategicObjectiveConfiguration.cs`

Do **not** start coding until you have a clear picture of these patterns.

---

## 1. Open questions — defaults if unanswered

| # | Default applied in this prompt |
|---|---|
| Q1 | One active (`Approved` or `PendingApproval`) plan per org unit. Add a unique filtered index. |
| Q2 | Plan soft-delete is **blocked** if any non-deleted objective references it. No cascade. |

If the requestor's answers differ, stop and update this prompt before continuing.

---

## 2. Scope

Add a new domain entity `StrategicPlan` that wraps strategic objectives in a
time-bounded cycle. **No approval workflow yet** (that's CR-001-D). **No edit
lockdown yet** (that's CR-001-E). Just CRUD + the FK from `StrategicObjective`
to `StrategicPlan`.

The status enum already includes `PendingApproval` and `Approved` so Phase 2
can wire the workflow without another migration.

---

## 3. Domain layer (`ActionTracker.Domain`)

### 3.1 New entity `Entities/StrategicPlan.cs`

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` PK | |
| `OrgUnitId` | `Guid` FK → `OrgUnit` | Required |
| `Name` | `string` | Required, max 200 |
| `StartYear` | `int` | 2000–2100 (validate in command, not entity) |
| `DurationYears` | `int` | 1–10 |
| `EndYear` | computed property | `StartYear + DurationYears - 1` |
| `Status` | `StrategicPlanStatus` enum | Defaults to `Draft` |
| `ApprovedAtUtc` | `DateTime?` | |
| `ApprovedByUserId` | `Guid?` FK → `ApplicationUser` | |
| Audit fields | `CreatedAtUtc`, `CreatedByUserId`, `ModifiedAtUtc`, `ModifiedByUserId` | Match the conventions used by `Project` |
| `IsDeleted` | `bool` | Soft delete |
| Nav | `OrgUnit`, `Objectives` (`ICollection<StrategicObjective>`) | |

### 3.2 New enum `Enums/StrategicPlanStatus.cs`

```csharp
public enum StrategicPlanStatus
{
    Draft = 0,
    PendingApproval = 1,
    Approved = 2,
    Archived = 3
}
```

### 3.3 Modify `Entities/StrategicObjective.cs`

- Add `StrategicPlanId` (`Guid?`, **nullable** for the migration window).
- Add nav `StrategicPlan? StrategicPlan`.

### 3.4 Permissions catalogue

In `Domain/Constants/PermissionPolicies.cs` (or wherever permission constants
live — match the existing pattern), add:

- `StrategicPlans.View`
- `StrategicPlans.Create`
- `StrategicPlans.Edit`
- `StrategicPlans.Delete`
- `StrategicPlans.Approve` (placeholder for Phase 2 — define it now so the seeder is one-pass)

---

## 4. Application layer (`ActionTracker.Application`)

Mirror the structure of `Application/StrategicObjectives/`.

### 4.1 DTOs

- `StrategicPlans/Dtos/StrategicPlanDto.cs` — full read DTO including computed `EndYear`, `OrgUnitName`, objective count.
- `StrategicPlans/Dtos/StrategicPlanListItemDto.cs` — slim, for paged list.
- `StrategicPlans/Dtos/CreateStrategicPlanDto.cs`, `UpdateStrategicPlanDto.cs`.

### 4.2 CQRS (MediatR)

Place under `Application/StrategicPlans/Commands/` and `Application/StrategicPlans/Queries/`:

| Command/Query | Notes |
|---|---|
| `CreateStrategicPlanCommand` | Returns `StrategicPlanDto`. Enforces uniqueness rule (Q1 default). |
| `UpdateStrategicPlanCommand` | Cannot change `OrgUnitId` after creation. |
| `DeleteStrategicPlanCommand` | Soft delete. Throws if any non-deleted objective references it (Q2 default). |
| `RestoreStrategicPlanCommand` | |
| `GetStrategicPlanByIdQuery` | |
| `ListStrategicPlansQuery` | Paged, filterable by `OrgUnitId`, `Status`, `Year` (matches start ≤ year ≤ end). |
| `GetActivePlanForOrgUnitQuery` | Returns the single plan whose status is `Approved` or `PendingApproval` for that unit, or null. |

### 4.3 Validators (FluentValidation)

- `Name`: required, ≤ 200 chars.
- `StartYear`: 2000–2100.
- `DurationYears`: 1–10.
- `OrgUnitId`: must exist and not be soft-deleted.

### 4.4 Service interface

`Application/Common/Interfaces/IStrategicPlanService.cs` — wraps the CQRS handlers
in a service abstraction the same way `IStrategicObjectiveService` does. (Match
the existing style — if other features inject the service directly into the
controller, do the same here.)

### 4.5 Scope

`IStrategicScopeService` already exists. Extend it so:

- Reading a `StrategicPlan` follows the same rule as reading a `StrategicObjective`
  in that org unit.
- Writing a `StrategicPlan` calls `EnsureCanWriteAsync(userId, orgUnitId)`.

Add a unit test verifying a Strategy Editor can read/write only within their
level-2 ancestor + descendants.

---

## 5. Infrastructure layer (`ActionTracker.Infrastructure`)

### 5.1 EF Core configuration

`Persistence/Configurations/StrategicPlanConfiguration.cs`:

- Table name: `StrategicPlans`.
- Indexes: non-unique on `OrgUnitId`; **filtered unique** on
  `(OrgUnitId)` where `IsDeleted = 0 AND Status IN (1, 2)` (PendingApproval or
  Approved). Use `HasFilter(...)` and document the filter SQL in a comment.
- FK to `OrgUnit` with `OnDelete(DeleteBehavior.Restrict)`.
- Match the column types and lengths used by `StrategicObjectiveConfiguration`.

Update `StrategicObjectiveConfiguration` to add the optional FK to
`StrategicPlan` (no cascade; `DeleteBehavior.Restrict`).

### 5.2 Migration

Generate with:

```bash
dotnet ef migrations add Add_StrategicPlan --project ActionTracker.Infrastructure --startup-project ActionTracker.API
```

Then **manually edit the migration `Up()` method** to add a data backfill block
*before* the column nullability changes:

1. Insert one `StrategicPlan` per existing `OrgUnitId` referenced by any
   `StrategicObjective`. Set `Name = "{OrgUnitName} — Legacy Strategy"`,
   `StartYear = YEAR(GETUTCDATE())`, `DurationYears = 1`,
   `Status = 2` (Approved), `ApprovedAtUtc = GETUTCDATE()`,
   `CreatedByUserId = NULL` is acceptable — backfill rows are system-generated.
2. Update each `StrategicObjective.StrategicPlanId` to point at the matching
   legacy plan.
3. **Do not** change `StrategicPlanId` to non-nullable in this migration — that
   stays as a follow-up migration once the dust settles. The CR will track it.

`Down()` must reverse cleanly: null out FK, delete rows where the legacy plan
shape can be detected (`Name LIKE '% — Legacy Strategy'` AND
`DurationYears = 1` AND `CreatedByUserId IS NULL`), then drop the table.

### 5.3 Service implementation

`Infrastructure/Services/StrategicPlanService.cs`. All reads/writes go through
`IStrategicScopeService` exactly as `StrategicObjectiveService` does today.

### 5.4 Seeders

In `DefaultRolePermissionsSeeder`, grant the new permissions:

| Role | View | Create | Edit | Delete | Approve |
|---|:-:|:-:|:-:|:-:|:-:|
| Admin | ✓ | ✓ | ✓ | ✓ | ✓ |
| PMO Head | ✓ | – | – | – | – |
| PMO Analyst | ✓ | – | – | – | – |
| Project Manager | ✓ | – | – | – | – |
| Strategy Editor | ✓ | – | ✓ | – | – |
| Workspace Admin | ✓ | – | – | – | – |

(Create / Approve will be expanded by CR-001-C and CR-001-D.)

The seeder is idempotent — only insert missing rows.

---

## 6. API layer (`ActionTracker.API`)

### 6.1 New `Controllers/StrategicPlansController.cs` at `/api/strategic-plans`

| Method | Endpoint | Permission |
|---|---|---|
| GET | `/` (paged, `?orgUnitId=&status=&year=`) | `StrategicPlans.View` |
| GET | `/{id}` | `StrategicPlans.View` |
| GET | `/by-orgunit/{orgUnitId}` | `StrategicPlans.View` |
| GET | `/by-orgunit/{orgUnitId}/active` | `StrategicPlans.View` |
| POST | `/` | `StrategicPlans.Create` (will be tightened in CR-001-C) |
| PUT | `/{id}` | `StrategicPlans.Edit` |
| DELETE | `/{id}` | `StrategicPlans.Delete` |
| PATCH | `/{id}/restore` | `StrategicPlans.Edit` |

Error mapping: `UnauthorizedAccessException` → 403, `KeyNotFoundException` → 404,
`InvalidOperationException` (e.g. uniqueness violation, delete-with-children) →
409 Conflict, `ArgumentException` / FluentValidation failures → 400.

### 6.2 Swagger

The endpoints will appear under their controller automatically. Confirm the
auto-launch works by running `dotnet run` and watching the browser open.

---

## 7. Frontend (`frontend/action-tracker-ui`)

### 7.1 Models

`src/app/models/strategic-plan.ts` — interfaces for `StrategicPlan`,
`StrategicPlanListItem`, `StrategicPlanStatus`, `CreateStrategicPlan`,
`UpdateStrategicPlan`. Match the field names from the DTOs exactly.

### 7.2 Service

`src/app/services/strategic-plan.service.ts` — typed HTTP client mirroring
`strategic-objective.service.ts`. No UI yet — that's CR-001-B.

### 7.3 Permissions

Update `src/app/core/permissions/permission-areas.ts` (or equivalent) to
include the new area `StrategicPlans` with actions
`View, Create, Edit, Delete, Approve`.

---

## 8. Tests

Add to `ActionTracker.Tests`:

- `StrategicPlanServiceTests` — happy path CRUD, soft-delete-blocked-when-children,
  uniqueness rule, scope filtering.
- `StrategicObjectiveScopeTests` — verify the existing tests still pass with
  the new FK.
- One integration test through the API confirming a Strategy Editor outside
  scope receives 403.

Aim for the same coverage style the project already uses. **Do not** modify
existing tests except where the new FK forces a change.

---

## 9. Acceptance criteria

1. `dotnet build` and `dotnet test` pass with no new warnings.
2. The migration applies cleanly to a database that already contains
   `StrategicObjective` rows; every existing objective ends up with a
   non-null `StrategicPlanId` pointing at a legacy plan in its org unit.
3. POST `/api/strategic-plans` creates a plan; the second POST for the
   same org unit while the first is `Approved` returns 409.
4. A Strategy Editor scoped to org unit X cannot read or write a plan
   for org unit Y (403).
5. Swagger lists all 8 new endpoints under `Strategic Plans`.

---

## 10. Documentation updates (mandatory before reporting done)

Per `CLAUDE.md` rules:

- `status.md` — add a new section under "Strategic Objectives" referencing
  the plan entity. Bump `Last updated:`.
- `BACKEND_OVERVIEW.md` — add `StrategicPlansController` rows and the
  `StrategicPlan` / `StrategicPlanStatus` entity tables. Add the new
  permission area to the area count. Bump `Last updated:`.
- `FRONTEND_OVERVIEW.md` — add the model + service. Bump `Last updated:`.

Do not mark any feature as "done" or "approval workflow ready" — that
language belongs to CR-001-D.

---

## 11. Stop conditions

Stop and ask the user before proceeding if you find:

- The existing `StrategicObjective` schema differs materially from
  `BACKEND_OVERVIEW.md`.
- There are already migration files referring to `StrategicPlan` (someone
  else may have started this work).
- The DB contains orphan `StrategicObjective` rows (no matching `OrgUnit`).
