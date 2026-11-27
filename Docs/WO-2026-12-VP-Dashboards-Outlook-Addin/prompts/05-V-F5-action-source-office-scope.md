# V-F5 — Action item source, office scope and closure measures

> **Work order:** WO-2026-12 — Executive Office dashboards and Outlook add-in
> **Package:** V-F5 · **Size:** M · **Depends on:** WO-2026-11 D-1 (user groups), A-1
> **Before anything else read:** `Docs/WO-2026-12-VP-Dashboards-Outlook-Addin/prompts/00-README.md` (rules, definitions, stop conditions), then `CLAUDE.md`, `status.md`, `BACKEND_OVERVIEW.md`, `FRONTEND_OVERVIEW.md`.
> Implement **this package only**. Do not start the next one.

## Why
The action tracker screen shows where each action came from (meeting minutes, e-mail, manual, project milestone), counts "actions assigned by the office", on-time closure, average days to close, ageing and extension requests. `ActionItem` has no source, and "assigned by the office" has no definition in the data.

## Read first
`ActionItem.cs`, `ActionItemService`, `ActionItemFilterDto`, `ActionItemRules` in `DashboardRules.cs`, `ActionItemWorkflowRequest` (date-change requests = extension requests), `UserGroup` / `UserGroupMember` from D-1, the action form and list components, the Excel export.

## Data (migration `Add_ActionItemSource`)
- `ActionItem.Source` (new enum `ActionSource { Manual = 0, Meeting = 1, Email = 2, ProjectMilestone = 3 }`, default Manual), `SourceReference` (`string?` ≤ 200, e.g. "MoM 03-Nov" or the sender and date of an e-mail), `SourceDate` (`DateTime?`).
- No backfill beyond the column default.

## Rules
- Source is chosen on create and may be corrected by whoever may work the item. `ProjectMilestone` is set automatically when an item is created inside a milestone from the project Gantt, unless the user chose another value.
- **Office scope**: add `CreatedByGroupId` (`Guid?`) to `ActionItemFilterDto`; it keeps items whose `CreatedByUserId` is a member of that group **now**. It is a filter on top of the normal visibility, never a grant.
- Add to `ActionItemRules` (new members only; do not change existing ones): `IsClosedOnTime`, `DaysToClose` (calendar days from `CreatedAt` to `ActualEndDate`), `DaysOverdue`, office ageing buckets `0-7`, `8-30`, `31-60`, `60+` (the existing age buckets `31-90` / `90+` stay for the existing widgets), each with its database predicate, as the existing rules do.
- **Extension request** = a pending `DateChangeRequest` whose `RequestedNewDueDate` is later than `CurrentDueDate`.
- "Last update" of an action = the latest submitted action update, else `UpdatedAt`.

## API and UI
- DTOs, validators, list filter (`source`, `createdByGroupId`), sort by days overdue; no new controller.
- Action form and detail: Source dropdown + reference; list column and filter; Excel export columns "Source", "Source reference".

## Acceptance
- Existing items read as Manual; nothing else changes for them.
- `createdByGroupId` never returns an item the caller could not already see.
- Unit tests for each new rule and its predicate (same value in memory and in SQL).

## Done when
- The acceptance points above hold, and the common acceptance criteria in `00-README.md` §5 hold.
- `dotnet build`, `dotnet test`, `ng build` pass with no new warnings.
- `status.md`, `BACKEND_OVERVIEW.md`, `FRONTEND_OVERVIEW.md` (and the entity / feature lists in `CLAUDE.md`) describe what was actually built.
- You finish with a short report: what was built, what differs from this prompt and why, any open point for the owner.
