# Prompt: CR-001-D — Strategic Plan Approval Workflow

> **CR reference:** `CR-001-Strategic-Plans-and-KPI-Actuals.docx`, section CR-001-D
> **Phase:** 2 (Approval gates)
> **Depends on:** CR-001-A (entity), CR-001-H (approver resolver)
> **Blocks:** CR-001-E (lockdown rules need this workflow's status transitions),
>            CR-001-F (KPI actuals reuse the same pattern)

---

## 0. Read first — this prompt mirrors an existing pattern

The Project Approval Workflow (`status.md` §16) is the template you are
**copying**. Read these files end-to-end before writing any code:

1. `CLAUDE.md`
2. `BACKEND_OVERVIEW.md` — especially `ProjectWorkflowController`,
   `IWorkflowNotificationHelper`
3. `backend/ActionTracker/ActionTracker.Domain/Entities/ProjectApprovalRequest.cs`
4. `backend/ActionTracker/ActionTracker.Application/Projects/Workflow/`
5. `backend/ActionTracker/ActionTracker.API/Controllers/ProjectWorkflowController.cs`
6. `frontend/action-tracker-ui/src/app/features/approvals/my-approvals/`
7. `frontend/action-tracker-ui/src/app/features/projects/` — find the
   "Submit for approval" button + justification modal, the approval banner,
   the approval history table

**Your job is to clone this pattern for Strategic Plans.** If you find
yourself inventing something the project pattern doesn't already have,
stop and ask.

---

## 1. Open questions — defaults applied here

| # | Default applied |
|---|---|
| Q3 | Reviewer = head of level-3 ancestor (resolved by `IApprovalAuthorityResolver` from CR-001-H). |
| Q11 | PMO Head is **not** a fallback approver. |
| Q12 | Recipients on submit: submitter (cc), level-3 head (action), all PMO Heads (cc). On approve/reject: same set. |

---

## 2. Scope

Add the workflow that takes a Strategic Plan from `Draft` →
`PendingApproval` → `Approved` (or back to `Draft` on rejection). Mirror
`ProjectApprovalRequest` exactly.

Edit lockdown rules are **out of scope** here — that's CR-001-E. This
prompt only manages the status transitions and notifications.

---

## 3. Domain layer

### 3.1 New entity `Entities/StrategicPlanApprovalRequest.cs`

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` PK | |
| `StrategicPlanId` | `Guid` FK | Cascade delete with the plan |
| `RequestType` | `StrategicPlanApprovalRequestType` enum | `Initial`, `Revision` (Revision used by CR-001-E) |
| `RequestedByUserId` | `Guid` FK | Submitter |
| `RequestedAtUtc` | `DateTime` | |
| `Justification` | `string` | Required, max 2000 |
| `Status` | `StrategicPlanApprovalStatus` enum | `Pending`, `Approved`, `Rejected` |
| `ReviewedByUserId` | `Guid?` FK | Resolved level-3 head at submit time, captured at review time (could be a different user if the head changes mid-flight — record the actual reviewer) |
| `AssignedReviewerUserId` | `Guid?` FK | The user who *should* review (resolved at submit). Drives the My Approvals list. |
| `ReviewedAtUtc` | `DateTime?` | |
| `ReviewerComment` | `string?` | Required on reject |
| Audit | match `ProjectApprovalRequest` exactly | |

### 3.2 New enums

```csharp
public enum StrategicPlanApprovalStatus { Pending, Approved, Rejected }
public enum StrategicPlanApprovalRequestType { Initial, Revision }
```

### 3.3 Existing `StrategicPlanStatus` already includes `PendingApproval`
and `Approved` — no change needed (CR-001-A pre-allocated them).

### 3.4 Notification action types

Add to the existing notification action enum:

- `StrategicPlanApprovalRequested`
- `StrategicPlanApprovalApproved`
- `StrategicPlanApprovalRejected`

Add `StrategicPlan` to `NotificationEntityType` if not already there
(CR-001-C added it).

---

## 4. Application layer

### 4.1 Service interface `IStrategicPlanWorkflowService`

Mirror `IProjectWorkflowService`:

```csharp
public interface IStrategicPlanWorkflowService
{
    Task<StrategicPlanApprovalRequestDto> SubmitAsync(
        Guid planId, string justification, Guid currentUserId, CancellationToken ct);

    Task<StrategicPlanApprovalRequestDto> ReviewAsync(
        Guid requestId, ApprovalDecision decision, string? comment,
        Guid currentUserId, CancellationToken ct);

    Task<List<StrategicPlanApprovalRequestDto>> GetForPlanAsync(Guid planId, CancellationToken ct);
    Task<PagedResult<StrategicPlanApprovalRequestDto>> GetPendingReviewsAsync(
        Guid currentUserId, PageRequest page, CancellationToken ct);
    Task<PagedResult<StrategicPlanApprovalRequestDto>> GetMyRequestsAsync(
        Guid currentUserId, PageRequest page, CancellationToken ct);
    Task<int> GetPendingSummaryAsync(Guid currentUserId, CancellationToken ct);
    Task<bool> CanReviewAsync(Guid planId, Guid currentUserId, CancellationToken ct);
}
```

`ApprovalDecision` is the existing enum — reuse it.

### 4.2 SubmitAsync rules

1. Plan must exist, not be soft-deleted, and have `Status = Draft`
   (or `Approved` for `Revision` — but CR-001-E owns the revision flow;
   for now, accept only `Draft` and `RequestType = Initial`).
2. Submitter must have `StrategicPlans.Edit` and be in scope per
   `IStrategicScopeService`.
3. Plan must have at least one Strategic Objective with at least one KPI
   (mirror the project activation validation in `status.md` §4 —
   "blocks if milestones or action items are missing"). If the requestor
   wants different gating, stop and ask.
4. Resolve the reviewer via `IApprovalAuthorityResolver`. If null, throw
   a `BusinessRuleException` with a clear message — submission cannot
   proceed without an approver.
5. Persist the request; transition the plan to `PendingApproval`.
6. Fan-out notifications:
   - In-app + email to `AssignedReviewerUserId` (action type
     `StrategicPlanApprovalRequested`).
   - In-app to all PMO Heads (cc; do not duplicate the email).
   - In-app to the submitter (confirmation).
7. SignalR push to header badges of the reviewer and PMO Heads.

### 4.3 ReviewAsync rules

1. Request must be `Pending`.
2. Reviewer must be the `AssignedReviewerUserId` **or** an Admin (escape
   hatch — match the project workflow's pattern). PMO Head **cannot**
   approve unless they happen to be the assigned reviewer (per Q11 default).
3. On approve:
   - Set request `Status = Approved`, capture reviewer + timestamp.
   - Plan → `Status = Approved`, `ApprovedAtUtc`, `ApprovedByUserId`.
   - Fire `StrategicPlanApprovalApproved` notification to submitter + PMO.
4. On reject:
   - Comment required (validate ≥ 1 char).
   - Set request `Status = Rejected`.
   - Plan → `Status = Draft`. The plan is editable again.
   - Fire `StrategicPlanApprovalRejected` notification to submitter.
5. Race protection: if the plan status is no longer `PendingApproval` at
   review time (e.g. another reviewer raced ahead), throw a 409.

### 4.4 Validators

Use FluentValidation throughout. Match the existing project workflow's
validator style.

### 4.5 Email templates

Add via `EmailTemplateSeeder`:

- `StrategicPlan.Submitted` — "Strategic plan {{PlanName}} awaiting your
  approval"
- `StrategicPlan.Reviewed` — "Strategic plan {{PlanName}} {{Decision}}"

Mirror the HTML structure of `ProjectApproval.Requested` and `.Reviewed`.

---

## 5. Infrastructure layer

### 5.1 EF configuration

`StrategicPlanApprovalRequestConfiguration`:

- Indexes: `(StrategicPlanId, Status)` and `(AssignedReviewerUserId, Status)`.
- Relationships: cascade delete from plan; restrict on user FKs.
- Match the column naming and types of `ProjectApprovalRequestConfiguration`.

### 5.2 Migration

```bash
dotnet ef migrations add Add_StrategicPlanApprovalRequest --project ActionTracker.Infrastructure --startup-project ActionTracker.API
```

### 5.3 Service implementation

`Infrastructure/Services/StrategicPlanWorkflowService.cs`. Single class. All
EF queries respect `IStrategicScopeService` for read paths.

---

## 6. API layer

### 6.1 New `Controllers/StrategicPlanWorkflowController.cs`

Route: `/api/strategic-plans/workflow`. Mirror `ProjectWorkflowController`.

| Method | Endpoint | Permission |
|---|---|---|
| POST | `/submit` | `StrategicPlans.Edit` (submitter must own scope; service-level check) |
| PUT | `/requests/{id}/review` | `StrategicPlans.Approve` (also enforced at service level — must match `AssignedReviewerUserId` or be Admin) |
| GET | `/plan/{planId}` | `StrategicPlans.View` |
| GET | `/pending-reviews` (paged) | `StrategicPlans.View` |
| GET | `/my-requests` (paged) | `StrategicPlans.View` |
| GET | `/pending-summary` | `StrategicPlans.View` |
| GET | `/can-review/{planId}` | `StrategicPlans.View` |

### 6.2 Permission seeder

Grant `StrategicPlans.Approve` to:

- Admin (✓)
- *Nobody else by default.* The `AssignedReviewerUserId` check in the
  service is the actual authority — the permission is just a safety net.
  This matches how `Projects.Approve` is granted today (verify and copy).

---

## 7. Frontend

### 7.1 Models

Add types for `StrategicPlanApprovalRequest`, `StrategicPlanApprovalStatus`,
`StrategicPlanApprovalRequestType`. Match the field names from DTOs.

### 7.2 Service

`strategic-plan-workflow.service.ts` — typed HTTP client mirroring the
project workflow service.

### 7.3 Plan header card — Submit button

On the Strategy page (CR-001-B), the plan header card gains:

- **"Submit for Approval"** button — visible when `Status == Draft` and the
  user has `StrategicPlans.Edit` and is the plan's org-unit scope owner.
  Clicking opens a justification modal (re-use the project workflow's modal
  if it's reusable; otherwise replicate it — single text area, minimum 1
  char, max 2000).
- **Pending Approval banner** — when `Status == PendingApproval`, show an
  amber banner: "This plan is awaiting approval from {ReviewerDisplayName}."
  Editing on the page is disabled (CR-001-E will formalize this; for now,
  hide edit buttons via `[disabled]`).
- **Approve / Reject inline buttons** — when the current user is the
  `AssignedReviewerUserId` of the latest pending request, show the same
  approve/reject controls used on the project page. Reuse the project's
  `WorkflowReviewDialog` component (per `status.md` §15).

### 7.4 Approval History section

Below the plan content, add a collapsible "Approval History" section showing
all `StrategicPlanApprovalRequest` rows for the plan: requester, requested at,
status badge, reviewer, reviewed at, comment. Mirror the project approval
history component — extract a shared component if it isn't already shared.

### 7.5 My Approvals page — new tab

Per `status.md` §15 / §16, the My Approvals page has tabs for action items
and project approvals. Add a third tab: **"Strategy Approvals"**.

Within the tab:

- "Pending Reviews" sub-tab — list pending requests where the current user
  is the assigned reviewer.
- "My Requests" sub-tab — list requests submitted by the current user.
- Reuse the existing list/card pattern from the other tabs.

### 7.6 Header badge

The header pending-approval badge currently aggregates action item + project
counts. Extend the `WorkflowStateService` (or equivalent) to include the
strategic plan pending count from `GET /pending-summary`.

### 7.7 Notification toasts

Add toast handlers for the three new notification action types so they pop
the right colour and route to the right page on click (mirror the existing
toast handlers in `core/services/notification-toast.service.ts` or wherever).

---

## 8. Tests

- Service tests covering each rule in §4.2 and §4.3.
- Integration tests for each controller endpoint.
- Frontend tests for the new tab, the submit modal, the banner, and the
  badge count.
- A scenario test: Strategy Editor submits → reviewer approves → plan ends
  up `Approved` with all timestamps populated.
- A scenario test: rejection returns the plan to `Draft` and clears
  `ApprovedAtUtc`.

---

## 9. Acceptance criteria

1. A Strategy Editor can submit a `Draft` plan; the plan transitions to
   `PendingApproval` and a request row exists with `Status = Pending`.
2. The level-3 head receives an in-app + email notification.
3. PMO Heads receive an in-app notification (no email).
4. The header badge increments for the reviewer.
5. The reviewer sees the request in My Approvals → Strategy Approvals →
   Pending Reviews and can approve/reject.
6. Approve transitions plan to `Approved`; submitter is notified.
7. Reject transitions plan back to `Draft`; submitter is notified with the
   reviewer comment.
8. A second submit on an already-pending plan returns 409.
9. A non-reviewer attempting to review returns 403.
10. Concurrent reviews of the same request — the second one returns 409.
11. The plan with no objectives or no KPIs cannot be submitted (analogous
    to project activation validation).

---

## 10. Documentation updates

- `status.md` — new section after §17 titled **"Strategic Plan Approval
  Workflow"**, describing the entity, the flow, the notifications, the
  My Approvals tab, and the header badge inclusion. Bump `Last updated:`.
- `BACKEND_OVERVIEW.md` — add `StrategicPlanWorkflowController` rows;
  add `StrategicPlanApprovalRequest`, `StrategicPlanApprovalStatus`,
  `StrategicPlanApprovalRequestType` to the entities/enums tables;
  add `IStrategicPlanWorkflowService`. Bump `Last updated:`.
- `FRONTEND_OVERVIEW.md` — note the new tab on My Approvals, the submit/
  approve dialogs on the Strategy page, the workflow service. Bump
  `Last updated:`.

---

## 11. Stop conditions

Stop and ask if:

- `WorkflowReviewDialog` and the project-style modal aren't reusable — you
  may need to extract a generic version first.
- The "minimum content for submit" rule (objectives + KPIs) doesn't match
  what the requestor wants. The current default mirrors the project
  activation rule but the requestor may want stricter or looser checks.
- The `ApprovalDecision` enum doesn't exist — verify before assuming.
