# Prompt: CR-001-F — KPI Target Actuals + Evidence per Period

> **CR reference:** `CR-001-Strategic-Plans-and-KPI-Actuals.docx`, section CR-001-F
> **Phase:** 3 (Actuals & evidence)
> **Depends on:** Phase 2 complete (in particular CR-001-D for the workflow
>                 helper patterns and CR-001-H for approver resolution)
> **Blocks:** nothing — this is the final piece

---

## 0. Read first

This prompt builds on **two existing patterns**:

- The strategy approval workflow you just built (CR-001-D).
- The polymorphic document attachment system (`status.md` §9 / `Document` entity).

Read in order:

1. `CLAUDE.md`
2. `BACKEND_OVERVIEW.md` — `Document` polymorphic table, per-entity caps,
   `DocumentService` scope-aware authorization
3. `backend/ActionTracker/ActionTracker.Domain/Entities/Document.cs`
4. `backend/ActionTracker/ActionTracker.Infrastructure/Services/DocumentService.cs`
   — pay attention to `MaxDocumentsPerEntity` lookup and the `KpiTarget`
   scope check
5. The whole `IStrategicPlanWorkflowService` you built in CR-001-D
6. The frontend KPI target inline grid (the 12-row monthly editor described
   in `status.md` §6)

---

## 1. Open questions — defaults applied here

| # | Default applied |
|---|---|
| Q6 | Evidence attached to **`KpiActual`** (per period), not `KpiTarget`. |
| Q7 | Cannot submit an actual for a period whose end date is still in the future (server-side reject; client also disables the submit button). |
| Q10 | Migrate any existing `KpiTarget.actual` value into `KpiActual` rows, then drop the column in a follow-up migration. |
| Q11 / Q12 | Reviewer = same level-3 head used for plan approval (per CR-001-H). PMO Heads receive in-app cc. |

---

## 2. Scope

Add a first-class `KpiActual` entity that:

- Captures the *measured* value per reporting period (Quarter / Semi-Annual /
  Annual — driven by the parent KPI's `Period`).
- Holds up to 10 evidence documents per actual.
- Goes through Strategy Editor → level-3 head approval, mirroring CR-001-D.
- Is **editable even when the parent plan is locked** (per CR-001-E §3 and
  §11) — actuals are the ongoing reporting layer, not the plan baseline.

---

## 3. Domain layer

### 3.1 New entity `Entities/KpiActual.cs`

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` PK | |
| `KpiTargetId` | `Guid` FK → `KpiTarget` | |
| `PeriodType` | `KpiActualPeriodType` enum | `Quarter`, `SemiAnnual`, `Annual`. Must match the parent KPI's `Period`. |
| `PeriodIndex` | `int` | Q1=1 … Q4=4; H1=1, H2=2; Annual=1. Validate range against `PeriodType`. |
| `Year` | `int` | |
| `ActualValue` | `decimal(18,4)` | |
| `Notes` | `string?` | Max 2000 |
| `Status` | `KpiActualStatus` enum | `Draft`, `PendingApproval`, `Approved`, `Rejected` |
| `SubmittedByUserId` | `Guid?` FK | Set on submit |
| `SubmittedAtUtc` | `DateTime?` | |
| `AssignedReviewerUserId` | `Guid?` FK | Resolved at submit time via `IApprovalAuthorityResolver` |
| `ReviewedByUserId` | `Guid?` FK | |
| `ReviewedAtUtc` | `DateTime?` | |
| `ReviewerComment` | `string?` | Required on reject |
| `IsDeleted` | `bool` | Soft delete |
| Audit | `CreatedAtUtc/By`, `ModifiedAtUtc/By` | |
| Nav | `KpiTarget`, navigation to documents via the polymorphic table | |

### 3.2 New enums

```csharp
public enum KpiActualStatus { Draft, PendingApproval, Approved, Rejected }
public enum KpiActualPeriodType { Quarter, SemiAnnual, Annual }
```

### 3.3 Uniqueness

A KPI Target should have at most one **non-deleted, non-rejected** actual
per `(PeriodType, PeriodIndex, Year)` tuple. Rejected actuals are kept in
history but a new draft can be created for the same period. Enforce via
filtered unique index on
`(KpiTargetId, PeriodType, PeriodIndex, Year)` where `IsDeleted = 0 AND Status <> 3`
(Rejected = 3 in the enum order above).

### 3.4 Notification action types

Add:

- `KpiActualSubmitted`
- `KpiActualApproved`
- `KpiActualRejected`

Add `KpiActual` to `NotificationEntityType`.

### 3.5 Permissions

Add area `KpiActuals` with actions `View`, `Create`, `Edit`, `Approve`.

---

## 4. Application layer

### 4.1 Service `IKpiActualService`

CRUD + period-aware list operations. Operations:

| Method | Notes |
|---|---|
| `CreateAsync` | Validates period range against parent KPI. |
| `UpdateAsync` | Allowed only when `Status = Draft`. |
| `DeleteAsync` | Soft delete; only when `Status = Draft`. |
| `GetByIdAsync` | Includes evidence document refs. |
| `ListByKpiAsync(kpiId, year)` | Returns one row per period slot (Q1..Q4, etc.) populated where actuals exist. |
| `GetGridAsync(kpiTargetId, year)` | Returns a grid row matching the KPI's `Period` shape, with `Status` and achievement %. |

### 4.2 Service `IKpiActualWorkflowService`

Mirror `IStrategicPlanWorkflowService` from CR-001-D:

| Method | Notes |
|---|---|
| `SubmitAsync(actualId, currentUserId, ct)` | Validates: actual is `Draft`; period has ended (per Q7); evidence has at least one document **(soft rule — confirm with requestor whether mandatory; default: not mandatory but warn in UI)**; resolves reviewer via `IApprovalAuthorityResolver`; transitions `Draft → PendingApproval`; fires `KpiActualSubmitted`. |
| `ReviewAsync(actualId, decision, comment, currentUserId, ct)` | Approve → `Approved`; Reject → `Rejected` (final, not back to Draft — submitter must create a new draft for the same period). Comment required on reject. |
| `GetPendingReviewsAsync` / `GetMyRequestsAsync` / `GetPendingSummaryAsync` | Same shape as plan workflow. |

### 4.3 Period-end validation (Q7)

Helper:

```csharp
public static class KpiPeriodCalendar
{
    public static DateTime GetPeriodEndUtc(KpiActualPeriodType type, int index, int year);
}
```

- Quarter 1 → 31 Mar
- Quarter 2 → 30 Jun
- Quarter 3 → 30 Sep
- Quarter 4 → 31 Dec
- SemiAnnual H1 → 30 Jun
- SemiAnnual H2 → 31 Dec
- Annual → 31 Dec

Submit fails if `now < end of period`.

### 4.4 Document scope-aware authorization

`DocumentService` already extends to `KpiTarget` for scope (`status.md` §9).
Extend it to `entityType = KpiActual`:

1. Walk `KpiActual → KpiTarget → Kpi → StrategicObjective → OrgUnitId`.
2. Call `IStrategicScopeService.EnsureCanWriteAsync(userId, orgUnitId)`.
3. Apply the existing 10-doc cap (add `KpiActual` to the
   `MaxDocumentsPerEntity` lookup with value `10`).

### 4.5 Lock-guard interaction (CR-001-E)

Per CR-001-E §11 default, actuals **bypass** the plan lock — Strategy
Editors can submit and revise actuals against approved/locked plans. The
`IStrategicPlanLockGuard` is therefore **not** called from any actuals
handler. Document this in code comments (it's the kind of thing future
maintainers will second-guess).

### 4.6 Backfill from existing `KpiTarget.actual`

If the existing schema has an `actual` column on `KpiTarget` (verify by
inspecting the migration history), the migration in §5.2 should:

1. Insert `KpiActual` rows for every `KpiTarget` with a non-null `actual`,
   defaulting `Status = Approved`, `Year = KpiTarget.Year`,
   `PeriodType` and `PeriodIndex` derived from the KPI's `Period` and the
   target row's `Month`.
2. Drop `KpiTarget.actual` in a **separate follow-up migration** so the
   release can roll back if needed.

If no such column exists, skip the backfill.

---

## 5. Infrastructure layer

### 5.1 EF configuration

`KpiActualConfiguration`:

- Indexes: `(KpiTargetId, Year)`, `(AssignedReviewerUserId, Status)`,
  filtered unique per §3.3.
- FK to `KpiTarget` with `OnDelete(DeleteBehavior.Cascade)` (deleting a
  target removes its actuals).

### 5.2 Migration

```bash
dotnet ef migrations add Add_KpiActual --project ActionTracker.Infrastructure --startup-project ActionTracker.API
```

Then a follow-up migration once the data is verified in production:

```bash
dotnet ef migrations add Drop_KpiTarget_actual_column --project ActionTracker.Infrastructure --startup-project ActionTracker.API
```

(If the actual column doesn't exist today, the second migration is unnecessary.)

### 5.3 Seeders

Permission seeder for `KpiActuals.{View,Create,Edit,Approve}`:

| Role | View | Create | Edit | Approve |
|---|:-:|:-:|:-:|:-:|
| Admin | ✓ | ✓ | ✓ | ✓ |
| PMO Head | ✓ | – | – | – |
| PMO Analyst | ✓ | – | – | – |
| Project Manager | ✓ (RO) | – | – | – |
| Strategy Editor | ✓ (scope) | ✓ | ✓ | – |
| Workspace Admin | ✓ (scope) | – | – | – |

The `Approve` permission is again a safety net — actual authority is the
`AssignedReviewerUserId` check inside the service.

### 5.4 Email templates

- `KpiActual.Submitted` — "KPI actual for {{PeriodLabel}} {{Year}}
  awaiting your approval"
- `KpiActual.Reviewed` — "KPI actual {{Decision}}"

`PeriodLabel` = "Q1" / "H1" / "Annual" formatted by the helper.

---

## 6. API layer

### 6.1 New `Controllers/KpiActualsController.cs` at `/api/kpis/actuals`

| Method | Endpoint | Permission |
|---|---|---|
| GET | `/by-kpi/{kpiId}?year=` | `KpiActuals.View` |
| GET | `/{id}` | `KpiActuals.View` |
| POST | `/` (create draft) | `KpiActuals.Create` |
| PUT | `/{id}` (edit draft) | `KpiActuals.Edit` |
| DELETE | `/{id}` | `KpiActuals.Edit` |
| POST | `/{id}/documents` | `KpiActuals.Edit` (delegates to `DocumentService` with `entityType=KpiActual`) |
| GET | `/{id}/documents` | `KpiActuals.View` |
| DELETE | `/{id}/documents/{documentId}` | `KpiActuals.Edit` |

### 6.2 New `Controllers/KpiActualsWorkflowController.cs` at `/api/kpis/actuals/workflow`

Mirror the strategic plan workflow controller:

| Method | Endpoint |
|---|---|
| POST | `/submit` |
| PUT | `/requests/{actualId}/review` |
| GET | `/pending-reviews` (paged) |
| GET | `/my-requests` (paged) |
| GET | `/pending-summary` |

(The "request" identifier in this case is the `KpiActual.Id` itself, not a
separate request entity — the actual *is* the workflow record.)

---

## 7. Frontend

### 7.1 Period-aware actuals grid

Add a new component `kpi-actuals-grid` reachable from the KPI detail page.
The grid shape depends on the KPI's `Period`:

| KPI Period | Grid rows |
|---|---|
| Quarterly | Q1, Q2, Q3, Q4 |
| Semi-Annual | H1, H2 |
| Annual | Annual |

Each row shows columns:

- Period label
- Target value (read-only — comes from `KpiTarget`)
- Actual value (input, disabled unless `Status = Draft` or empty)
- Notes (input, same gate)
- Evidence (chip count + "Manage" → opens a side panel reusing the existing
  document upload component)
- Status badge (`Draft` / `PendingApproval` / `Approved` / `Rejected`)
- Achievement % (only when `Status = Approved`; computed using the
  `Direction` field from CR-001-G — green if on/above target, amber if
  partial, red if missed)
- Actions:
  - "Save Draft"
  - "Submit for Approval" (disabled when period not ended — see §4.3)
  - For reviewers: "Approve" / "Reject" via the shared review dialog
  - "View History" → opens a side panel showing all rows (including
    rejected) for that slot

### 7.2 My Approvals — fourth tab

Add a "KPI Actuals" tab. Two sub-tabs (Pending Reviews / My Requests),
matching the strategy-approvals tab structure.

### 7.3 Header badge

Extend the `WorkflowStateService` again to include
`KpiActualsPendingCount` in the total. By Phase 3 the badge total is:
action items + projects + strategic plans + KPI actuals.

### 7.4 Notifications & toasts

Wire the three new notification action types to toast handlers and
deep-links into the KPI Actuals tab / KPI detail page.

### 7.5 Period-end client guard

The "Submit for Approval" button must be disabled with a tooltip
("Available after {PeriodLabel} ends on {Date}") when the period has not
yet ended. Server still enforces — client guard is just UX.

### 7.6 Theme

Verify dark + light. The grid should preserve sticky headers when scrolling
horizontally on mobile.

---

## 8. Tests

- Service tests for every state transition.
- Period-calendar helper tests.
- Document upload scope test: a Strategy Editor outside scope cannot upload
  evidence to an actual.
- Backfill migration test on a seeded dev DB if the `actual` column
  existed pre-migration.
- Frontend tests for the grid in each period mode (Quarterly / Semi /
  Annual) and for the achievement % colouring with each `KpiDirection`.

---

## 9. Acceptance criteria

1. A Strategy Editor can create, edit, attach evidence to, and submit a
   draft actual for a period that has ended.
2. The reviewer (level-3 head) sees the actual in My Approvals → KPI
   Actuals → Pending Reviews and can approve/reject.
3. Approve / reject notifications fire correctly.
4. Submitting an actual for a period that has not ended returns 400.
5. Uploading an 11th evidence document returns 409 with the existing
   "max documents reached" error shape.
6. An actual can be created against a `KpiTarget` whose parent plan is
   `Approved` (locked) — the plan lock does not block actuals work.
7. After approval, achievement % renders correctly for each `KpiDirection`.
8. All Phase 1 and Phase 2 features still work — no regressions.

---

## 10. Documentation updates

- `status.md` — new section after the Strategic Plan Approval Workflow,
  titled **"KPI Actuals & Evidence"**, describing the entity, the period
  grid, the approval flow, and the My Approvals tab. Bump the
  "Not Yet Implemented" list (remove anything this CR delivers). Bump
  `Last updated:`.
- `BACKEND_OVERVIEW.md` — add `KpiActualsController`,
  `KpiActualsWorkflowController` rows; add `KpiActual`, `KpiActualStatus`,
  `KpiActualPeriodType` to entities/enums; add `IKpiActualService`,
  `IKpiActualWorkflowService` to services; document the per-entity
  document cap addition (`KpiActual` → 10). Bump `Last updated:`.
- `FRONTEND_OVERVIEW.md` — note the new grid component, the My Approvals
  fourth tab, the period-end client guard. Bump `Last updated:`.
- `CLAUDE.md` — Implemented Features section: tick off the items this
  CR delivered. Update the Key Entities list with `StrategicPlan`,
  `StrategicPlanApprovalRequest`, `KpiActual`. Bump `Last updated:`.

---

## 11. Stop conditions

Stop and ask if:

- The requestor wants evidence to be **mandatory** on submit (default: no,
  but recommended once data quality is a concern).
- The schema actually has a `KpiTarget.actual` field that this prompt's
  default expected — check before writing the backfill.
- Rejected actuals should *replace* the previous draft rather than coexist
  in history (the default keeps history; some teams prefer overwrite).
- The achievement % formula for `OnTarget` direction needs a tolerance
  band (e.g. ±5% counts as on-target). Default: exact comparison; surface
  band logic only if requested.
