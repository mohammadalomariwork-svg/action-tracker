# Prompt: CR-001-E — Post-Approval Edit Lockdown

> **CR reference:** `CR-001-Strategic-Plans-and-KPI-Actuals.docx`, section CR-001-E
> **Phase:** 2 (Approval gates)
> **Depends on:** CR-001-D (the workflow status transitions must already exist)
> **Blocks:** nothing (Phase 3 is independent)

---

## 0. Read first — another existing pattern to mirror

The Project date-freeze (`status.md` §16, "Date Freeze (Post-Approval)") is
your template. Read:

1. `CLAUDE.md`
2. `BACKEND_OVERVIEW.md` — `IsBaselined`, `BaselinePlannedStartDate`, etc.
3. The project service code that enforces date locking
4. The frontend project detail page where date inputs are disabled and
   show a 🔒 icon
5. `status.md` §16 in full

You are copying that whole pattern for the strategy module.

---

## 1. Open questions — defaults applied here

| # | Default applied |
|---|---|
| Q4 | Objective `Description` (and any other free-text fields) is **frozen** post-approval. Only the revision cycle reopens it. |
| Q5 | Use a **revision cycle** (option b): the plan transitions back to a `Draft`-equivalent state via a `Revision` request. Reuse `StrategicPlanApprovalRequest` with `RequestType = Revision`. Do **not** add a separate entity. |

---

## 2. Scope

Once a plan reaches `Approved`, every descendant entity becomes read-only
unless a Revision is in flight. Edits flow through a re-approval loop.

This prompt:

1. Adds the lockdown checks (backend service guards + frontend disabled
   inputs).
2. Adds the Revision request flow.
3. Adds visual indicators (🔒 icons, banners).

---

## 3. Lockdown rules — backend

For every command/handler that touches a plan, objective, KPI, or KPI target,
add a precondition check **before any mutation**:

```
Resolve the parent StrategicPlan (walk up if needed):
  KpiTarget → Kpi → StrategicObjective → StrategicPlan
  StrategicObjective → StrategicPlan

If StrategicPlan.Status == Approved AND no in-flight Revision request exists:
   Throw new BusinessRuleException("Plan is approved and locked. Submit a revision request to make changes.")
Else:
   Proceed.
```

Apply to:

| Handler | Behaviour when locked |
|---|---|
| `UpdateStrategicPlanCommand` | Block updates to `Name`, `StartYear`, `DurationYears`. |
| `DeleteStrategicPlanCommand` | Block delete; offer "Archive" instead (allowed). |
| `CreateStrategicObjectiveCommand` | Block adding new objectives to a locked plan. |
| `UpdateStrategicObjectiveCommand` | Block any update. |
| `DeleteStrategicObjectiveCommand` | Block. |
| `CreateKpiCommand` | Block. |
| `UpdateKpiCommand` | Block. |
| `DeleteKpiCommand` | Block. |
| `BulkUpsertKpiTargetsCommand` (target value, baseline, etc.) | Block changes to `TargetValue`, `Baseline`, `DataSource`, `CalculationNotes`, `Direction`. **Permit** any actuals/evidence work (CR-001-F). |

Centralize the precondition into a single helper:

```csharp
public interface IStrategicPlanLockGuard
{
    Task EnsureNotLockedAsync(Guid strategicPlanId, CancellationToken ct);
    Task<bool> IsLockedAsync(Guid strategicPlanId, CancellationToken ct);
}
```

So you can sprinkle one call per handler rather than re-implementing the
check.

`IsLockedAsync` returns `true` when:
- Plan status is `Approved`, **and**
- There is no pending `Revision`-type approval request for the plan.

(`Draft` and `PendingApproval` plans are not "locked" in this sense —
PendingApproval just means a workflow is in flight; that's CR-001-D's job
to disallow edits during pending. CR-001-E's lockdown is specifically the
Approved state.)

---

## 4. Revision flow

### 4.1 Request a revision

New service method on `IStrategicPlanWorkflowService` (extending CR-001-D):

```csharp
Task<StrategicPlanApprovalRequestDto> RequestRevisionAsync(
    Guid planId, string justification, Guid currentUserId, CancellationToken ct);
```

Behaviour:

1. Plan must be `Approved`. (Cannot request a revision on a draft.)
2. Submitter must have `StrategicPlans.Edit` and be in scope.
3. There must be no other pending `Revision` request for this plan
   (uniqueness — only one revision in flight at a time).
4. Create a new `StrategicPlanApprovalRequest` with `RequestType = Revision`,
   `Status = Pending`, justification, assigned reviewer = same level-3 head
   resolved by `IApprovalAuthorityResolver`.
5. **Do not** change the plan's `Status` — it remains `Approved` while the
   revision is pending. The lock guard checks for the pending revision and
   unlocks edits when one exists.
6. Notifications: `StrategicPlanRevisionRequested` — reviewer + PMO Heads.

### 4.2 Reviewing a revision

The existing `ReviewAsync` handles both `Initial` and `Revision` types
because the data shape is identical. Differences:

- On approve a `Revision`: plan stays at `Approved`. Snapshot any new
  `Approved` audit fields if you wish (e.g. `LastRevisionApprovedAtUtc`,
  `LastRevisionApprovedByUserId`) — add these as optional columns now if
  the requestor wants the audit trail. **[CONFIRM with requestor before
  adding]** — defaulting to *not* adding extra columns; the request entity
  itself is the audit trail.
- On reject a `Revision`: plan stays at `Approved`. The request is closed
  with `Status = Rejected`. Edits remain locked.

### 4.3 Lock guard interaction

`IsLockedAsync(planId)`:

- If plan status `≠ Approved` → return `false` (CR-001-D handles
  PendingApproval; Draft is editable; Archived is read-only via a
  different rule, see §5).
- Else if any `Revision` request exists with `Status = Pending` → return
  `false` (revision is in flight; edits temporarily allowed).
- Else → return `true`.

---

## 5. Archived plans

`Archived` plans are also read-only — no revision flow allows edits. This
is the "lifecycle close" state. Add a separate guard or extend
`IsLockedAsync` to return `true` for both `Approved` and `Archived` (with
no revision exception).

How a plan reaches `Archived` is for a future CR (the notes don't cover
that lifecycle yet). For now, only Admin can move a plan to `Archived`
through a direct UPDATE — the existing `UpdateStrategicPlanCommand` should
allow `Status = Archived` only when the user is Admin and the current
status is `Approved`.

---

## 6. API layer

### 6.1 New endpoint

`POST /api/strategic-plans/workflow/request-revision` — body
`{ planId, justification }`. Permission: `StrategicPlans.Edit`.

### 6.2 Endpoint behaviour propagation

Every existing endpoint that mutates the strategy graph should now return
`409 Conflict` with a `ProblemDetails` payload like:

```json
{
  "title": "Plan is locked",
  "detail": "The plan is approved. Submit a revision request to make changes.",
  "type": "/errors/plan-locked",
  "status": 409
}
```

Wire the `BusinessRuleException` from §3 to this response shape via the
global exception middleware. Add a `BusinessRuleException` type if one
doesn't exist (mirror the existing convention).

### 6.3 New read endpoint

`GET /api/strategic-plans/{id}/lock-state` → returns:

```json
{
  "planId": "...",
  "status": "Approved",
  "isLocked": true,
  "pendingRevisionId": null
}
```

This lets the frontend cache lock state without inferring it from status
alone (the revision-in-flight case is non-obvious).

---

## 7. Frontend

### 7.1 Disabled inputs + 🔒 icons

On the Strategy page (CR-001-B) and any deep-linked KPI / target editors,
when the plan is locked:

- All form inputs in the plan header card become `[disabled]`.
- All "Add objective", "Add KPI", "Add target detail" buttons are hidden.
- Each affected input gets a small 🔒 icon adjacent to its label, with
  tooltip "Plan is approved. Request a revision to edit."

Reuse the existing 🔒 styling from project date inputs.

### 7.2 Banner at top of Strategy page when locked

Slate-coloured banner:

> 🔒 This plan is approved and locked. To edit, request a revision.
> [Request Revision] (button — only visible if user has
> `*hasPermission="'StrategicPlans.Edit'"` and is in scope)

When a revision is pending, swap the banner:

> 🟡 Revision pending review by {ReviewerDisplayName}. Edits are temporarily
> enabled.

### 7.3 Request Revision modal

Reuse the same justification modal used for Submit (CR-001-D), with the
title swapped to "Request Revision".

### 7.4 Approval History section

Update the history component (added in CR-001-D) so each request row
shows `RequestType` (Initial / Revision) as a badge.

### 7.5 KPI target details panel

When the plan is locked, the new "details" panel (CR-001-G) becomes
read-only. Actuals (CR-001-F) remain editable.

### 7.6 Optimistic UX consideration

After clicking "Approve" on a Revision, the page must refetch the lock
state — the inputs need to re-disable instantly. Use the existing pattern
for optimistic-then-confirm refresh.

---

## 8. Tests

- Unit tests for `StrategicPlanLockGuard`:
  - Approved + no revision → locked.
  - Approved + pending revision → unlocked.
  - Approved + rejected revision → locked.
  - Approved + approved revision → locked again (closing the loop).
  - Draft / PendingApproval / Archived behaviour.
- Service tests: every mutating handler throws when locked.
- Integration test for the full revision flow: approve plan → lock → request
  revision → edit → approve revision → lock again.
- Frontend test: lock banner renders with the correct copy in each state.

---

## 9. Acceptance criteria

1. After approving a plan, no edit endpoint accepts mutations (all return
   409 with the standard payload).
2. Requesting a revision unlocks edits without changing the plan's `Status`.
3. Approving the revision re-locks the plan.
4. Rejecting the revision keeps the plan locked and closes the request.
5. The 🔒 icon and lock banner render correctly in dark and light themes.
6. Archived plans cannot be edited or revised.
7. Existing approved plans (none yet, but for forward-compat) are also
   locked correctly when the migration runs in production.

---

## 10. Documentation updates

- `status.md` — extend the "Strategic Plan Approval Workflow" section
  added by CR-001-D with two new subsections: "Edit Lockdown" and
  "Revision Cycle". Mirror the structure used for the project workflow.
  Bump `Last updated:`.
- `BACKEND_OVERVIEW.md` — add `IStrategicPlanLockGuard` to the services
  table; add the new endpoint; document the 409 lock response. Bump
  `Last updated:`.
- `FRONTEND_OVERVIEW.md` — note the lock banner, the request-revision
  modal, and disabled-input behaviour. Bump `Last updated:`.

---

## 11. Stop conditions

Stop and ask if:

- The requestor wants additional audit fields on the plan
  (`LastRevisionApprovedAtUtc`, etc.) — defaulting to "no" but worth
  confirming.
- An existing handler outside the strategy module also needs lock
  protection (e.g. polymorphic document upload to a `KpiTarget` belonging
  to a locked plan — confirm with the requestor whether evidence on
  approved-plan targets should also be frozen; **default**: evidence
  **is** frozen because target value is frozen, but actuals + their
  evidence (CR-001-F) are *not* frozen).
- A handler you find doesn't have a unit test today — add one as part of
  this work, do not skip.
