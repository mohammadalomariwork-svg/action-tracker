# Claude Code Prompts — CR-001 Strategic Plans & KPI Actuals

This folder contains **9 implementation prompts** for Claude Code, derived from
`CR-001-Strategic-Plans-and-KPI-Actuals.docx`. Each prompt is self-contained
and corresponds to one feature in the change request.

---

## How to use

1. Open the KU Action Tracker repo in Claude Code (it auto-loads `CLAUDE.md`).
2. Confirm the **open questions** at the top of each prompt with the requestor
   *before* asking Claude Code to execute it. Each prompt lists its blocking
   questions and suggests a default if you choose not to wait.
3. Hand Claude Code one prompt at a time, in the order below. Do not run them
   in parallel — later prompts depend on earlier ones.
4. After each prompt finishes, run `dotnet build`, `dotnet test`, and
   `ng build` before moving on.
5. After each prompt, verify the three doc files were updated as instructed
   (`status.md`, `BACKEND_OVERVIEW.md`, `FRONTEND_OVERVIEW.md`) and bump their
   `Last updated:` dates.

---

## Execution order

### Phase 1 — Structure (no approval gates yet)

| # | File | CR | Depends on |
|---|---|---|---|
| 1 | `phase-1-A-strategic-plan-entity.md` | CR-001-A | – |
| 2 | `phase-1-G-kpi-target-details.md` | CR-001-G | – (parallel-safe with 1) |
| 3 | `phase-1-I-pmo-pm-readonly-visibility.md` | CR-001-I | – (parallel-safe) |
| 4 | `phase-1-C-pmo-initiated-setup.md` | CR-001-C | 1 |
| 5 | `phase-1-B-strategy-page.md` | CR-001-B | 1, 4 |

### Phase 2 — Approval gates

| # | File | CR | Depends on |
|---|---|---|---|
| 6 | `phase-2-H-level3-approver-resolution.md` | CR-001-H | Phase 1 done |
| 7 | `phase-2-D-strategy-approval-workflow.md` | CR-001-D | 6 |
| 8 | `phase-2-E-post-approval-edit-lockdown.md` | CR-001-E | 7 |

### Phase 3 — Actuals & evidence

| # | File | CR | Depends on |
|---|---|---|---|
| 9 | `phase-3-F-kpi-actuals-and-evidence.md` | CR-001-F | Phase 2 done |

---

## Conventions every prompt follows

- **Clean Architecture** is non-negotiable. Domain has zero infrastructure deps.
- **CQRS via MediatR** in the Application layer. FluentValidation on every command/query.
- **Soft-delete only** — never hard-delete user data.
- **DTOs only** at the API boundary — never expose entities.
- **Permissions** — every new endpoint gets a `{Area}.{Action}` permission row
  in `DefaultRolePermissionsSeeder`, and every new UI block uses `*hasPermission`.
- **Theme** — every new component must work in dark and light modes.
- **Notifications** — re-use `IWorkflowNotificationHelper` for any in-app + email
  + SignalR event.
- **Migrations** — additive only; new FKs start as nullable, then are tightened
  in a follow-up migration after the data backfill runs.

---

## Open questions referenced across prompts

These questions are repeated where each prompt needs them. The defaults below
are what each prompt will fall back to if the question is unanswered when you
start that prompt.

| # | Question | Default if unanswered |
|---|---|---|
| Q1 | One active plan per org unit, or many? | One active plan (uniqueness on `(OrgUnitId, Status=Approved/PendingApproval)`) |
| Q2 | Soft-delete plan → cascade to objectives? | No cascade; plan delete blocked while it has live objectives |
| Q3 | Who is "Org Unit Head" reviewer? | Add `OrgUnit.HeadUserId` (nullable FK) — option (a) |
| Q4 | Objective `Description` editable post-approval? | Frozen; only via revision cycle |
| Q5 | Edit-after-approval mechanism? | Revision cycle reusing `StrategicPlanApprovalRequest` with `RequestType` enum |
| Q6 | Evidence on `KpiActual` or `KpiTarget`? | On `KpiActual` (per period) |
| Q7 | Submit actuals for not-yet-ended period? | Block; reject server-side |
| Q8 | "Target details" field list? | `Baseline`, `DataSource`, `CalculationNotes`, `Direction` |
| Q9 | "Level 3" — root or leaf? | Level 1 = root, level 3 = ancestor of editor's scope |
| Q10 | Migrate existing `KpiTarget.actual` value? | Yes — backfill into `KpiActual`, then drop column |
| Q11 | PMO Head as fallback approver? | No (Org Unit Head only) |
| Q12 | Notification recipients on submit/approve? | Submitter + Org Unit Head + PMO Head |

If the requestor changes any of these answers, edit the corresponding prompt
*before* handing it to Claude Code.
