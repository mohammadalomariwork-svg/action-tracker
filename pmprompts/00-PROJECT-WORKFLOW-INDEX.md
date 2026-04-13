# Project Approval Workflow — Prompt Series Index

> **Feature:** Project creation notifications, draft-phase editing, start-approval workflow, and date freeze enforcement
> **Created:** 2026-04-13
> **Convention:** `B-PW-XX` = Backend, `F-PW-XX` = Frontend

---

## Execution Order (strict dependency chain)

| Step | Prompt File | Description | Depends On |
|------|-------------|-------------|------------|
| 1 | `B-PW-01` | New entity: `ProjectApprovalRequest` + migration | — |
| 2 | `B-PW-02` | New enum: `ProjectApprovalStatus` + extend `ProjectStatus` with `PendingApproval` | B-PW-01 |
| 3 | `B-PW-03` | Email templates seeder for project workflow notifications | B-PW-02 |
| 4 | `B-PW-04` | `IProjectWorkflowService` + implementation — creation notifications, submit for approval, review, date freeze enforcement | B-PW-03 |
| 5 | `B-PW-05` | `ProjectWorkflowController` — API endpoints | B-PW-04 |
| 6 | `B-PW-06` | Enforce date freeze in existing `ProjectService`, `MilestoneService`, `ActionItemService` | B-PW-05 |
| 7 | `B-PW-07` | Permission catalog seed: add `Projects.Approve` mapping if missing | B-PW-06 |
| 8 | `F-PW-01` | TypeScript models, enums, and `ProjectWorkflowService` | B-PW-07 |
| 9 | `F-PW-02` | "Create New Project" button on My Projects page (permission-gated) | F-PW-01 |
| 10 | `F-PW-03` | Project detail — "Submit for Approval" button and approval request panel | F-PW-02 |
| 11 | `F-PW-04` | Project Approvals tab in My Approvals page | F-PW-03 |
| 12 | `F-PW-05` | Date freeze UI — lock date inputs when project is approved/active | F-PW-04 |

---

## Business Rules Summary

1. **On project creation:** Send email + in-app notification to the project creator's direct line manager (from `KuEmployeeInfo`) and all assigned project sponsors.
2. **Draft phase:** The project manager (and users with `Projects.Edit`) can add/edit milestones and action items while the project status is `Draft`.
3. **Submit for approval:** The project manager submits a start-approval request targeting all sponsors + their direct line manager. Project status transitions from `Draft` → `PendingApproval`.
4. **Approval gate:** Any single sponsor OR the direct line manager can approve. On approval the project status transitions from `PendingApproval` → `Active`, `ActualStartDate` is set to today, and `IsBaselined` is set to `true` (freezing all baseline dates). Any rejection returns the project to `Draft` with a comment.
5. **Date freeze (post-approval):** Once a project is `Active` (or `Suspended`/`Closed`), the following dates become read-only and cannot be changed through the API:
   - Project: `PlannedStartDate`, `PlannedEndDate`
   - Milestones: `PlannedStartDate`, `PlannedDueDate`
   - Action items (linked to this project): `StartDate`, `DueDate`
6. **Notifications on approval/rejection:** Email + in-app notification to the project manager with the reviewer's decision and comment.
