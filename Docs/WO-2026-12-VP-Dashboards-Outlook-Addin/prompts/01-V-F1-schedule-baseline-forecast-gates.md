# V-F1 — Schedule data: project baseline, forecast, gates, issues

> **Work order:** WO-2026-12 — Executive Office dashboards and Outlook add-in
> **Package:** V-F1 · **Size:** M · **Depends on:** WO-2026-11 P-3, P-4
> **Before anything else read:** `Docs/WO-2026-12-VP-Dashboards-Outlook-Addin/prompts/00-README.md` (rules, definitions, stop conditions), then `CLAUDE.md`, `status.md`, `BACKEND_OVERVIEW.md`, `FRONTEND_OVERVIEW.md`.
> Implement **this package only**. Do not start the next one.

## Why
The mockups show baseline against forecast, variance in working days, gates and "risks / issues". Today a project has planned and actual dates and an `IsBaselined` flag; only milestones store baseline dates; nothing stores a forecast; a milestone cannot be marked as a gate; `ProjectRisk` has no issue type.

## Read first
`Project.cs`, `Milestone.cs`, `ProjectRisk.cs`, `ProjectService`, `MilestoneService` (find where the baseline is locked today and where `IsBaselined` is set), `ProjectWorkflowService` (date freeze), `IWorkCalendarService`, `IProjectAccessService`.

## Data (one additive migration `Add_ScheduleBaselineForecast`)
- `Project`: `BaselineStartDate`, `BaselineEndDate`, `BaselineSetAt` (`DateTime?`), `BaselineNumber` (`int`, default 0), `ForecastEndDate` (`DateTime?`).
- `Milestone`: `ForecastDueDate` (`DateTime?`), `IsGate` (`bool`, default false), `IsKeyMilestone` (`bool`, default false).
- `ProjectRisk`: `Kind` (new enum `RiskKind { Risk = 0, Issue = 1 }`, default Risk).
- Backfill inside the migration: for projects with `IsBaselined = 1`, `BaselineStartDate = PlannedStartDate`, `BaselineEndDate = PlannedEndDate`, `BaselineSetAt = COALESCE(UpdatedAt, CreatedAt)`, `BaselineNumber = 1`. Nothing else is touched. Report the row count before applying.

## Rules
- Wherever the baseline is locked today, also write the project's baseline columns and raise `BaselineNumber`. Do not add a second baselining mechanism.
- **Forecast dates are not frozen**, like actual dates: they stay editable on an active project. Who may edit: whoever may manage the project (`ProjectAccess.CanManage`). A forecast date must be a working day in the workspace calendar (same check and message as C-3); a milestone forecast may lie outside the project's planned dates (that is the point of a forecast).
- `ForecastEndDate` empty means "derived" (see README §4). The API returns both the stored value and the effective forecast finish.
- `IsGate` and `IsKeyMilestone` are plain flags set by project managers; a gate is drawn as a diamond.
- `Kind` appears on the risk form ("Risk" / "Issue"), the register list, filters, stats and the Excel export. Existing rows are risks.

## API and UI
- Extend the existing project, milestone and risk DTOs and validators; no new controller.
- Project page: a "Schedule" summary block (baseline start and finish with BLn, forecast finish, variance in working days). Milestone form and grid: forecast due date, gate and key-milestone switches. Gantt (P-3): baseline as a thin grey line, forecast as the bar end, diamonds for gates.

## Acceptance
- Baselining a project stores the project baseline and keeps doing everything it did before.
- A frozen active project accepts forecast edits from its managers and refuses them from viewers (403 with the reason).
- A forecast on a non-working day is refused by API and form.
- The four existing dashboards show the same numbers as before.

## Done when
- The acceptance points above hold, and the common acceptance criteria in `00-README.md` §5 hold.
- `dotnet build`, `dotnet test`, `ng build` pass with no new warnings.
- `status.md`, `BACKEND_OVERVIEW.md`, `FRONTEND_OVERVIEW.md` (and the entity / feature lists in `CLAUDE.md`) describe what was actually built.
- You finish with a short report: what was built, what differs from this prompt and why, any open point for the owner.
