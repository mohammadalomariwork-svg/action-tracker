# V-F7 — Weekly snapshots, history and reminders job

> **Work order:** WO-2026-12 — Executive Office dashboards and Outlook add-in
> **Package:** V-F7 · **Size:** M · **Depends on:** V-F6
> **Before anything else read:** `Docs/WO-2026-12-VP-Dashboards-Outlook-Addin/prompts/00-README.md` (rules, definitions, stop conditions), then `CLAUDE.md`, `status.md`, `BACKEND_OVERVIEW.md`, `FRONTEND_OVERVIEW.md`.
> Implement **this package only**. Do not start the next one.

## Why
Trend arrows, the eight-week "% complete against plan" chart and "closed per week" need history. Nothing is stored over time today.

## Read first
`Program.cs` (is there any hosted service or scheduler today? there was none at commit `a45873b`; `DataSync` has its own job runs), `DataSyncJob` / `DataSyncJobRun` (pattern for recording runs), V-F6.

## Data
`ProjectSnapshot`: `Id`, `ProjectId`, `SnapshotDate` (date; the last day of the week it describes), `PercentComplete`, `PlannedPercent`, `Rag` (nullable), `ForecastEndDate`, `VarianceWorkingDays`, `OpenActions`, `OverdueActions`, `OpenHighRisks`, `PendingDecisions`, `MilestonesTotal`, `MilestonesDone`, `MilestonesLate`, `CreatedAt`. Unique (`ProjectId`, `SnapshotDate`). Private-workspace query filter through the project.

## Build
- A `BackgroundService` (`PortfolioSnapshotJob`) driven by options `Portfolio:Snapshot` (`Enabled`, `DayOfWeek` default Friday, `TimeOfDay` default 18:00, `TimeZoneId` default the default work calendar's time zone). It computes with the V-F6 engine **without a user scope** (system context — make that an explicit, internal entry point, not a flag a controller can pass) for every live project that is Active or On Hold, and upserts one row per project for that week. Idempotent; safe when the API runs on more than one instance (take an application lock in SQL Server, or stop and ask if that is not acceptable).
- The same job, daily, sends the "decision needed-by date passed" reminders defined in V-F3 (once per decision).
- `POST /api/admin/portfolio/snapshots/run` (Admin only) runs it now; `GET /api/admin/portfolio/snapshots/status` returns the last run, rows written and any error.
- Read side for widgets: `GetHistoryAsync(projectId, weeks)` and `GetPreviousAsync(projectIds)` on the portfolio service, both filtered by what the caller may see.
- No backfill of the past. Until two snapshots exist, trend is "none" and history charts show what there is with a note "History starts <date>".
- "Data as of" on dashboards is the time the row set was calculated (live), not the snapshot time.

## Acceptance
- Running twice in the same week leaves one row per project with the later values.
- A viewer never receives a snapshot of a project they cannot see.
- With the job disabled the API starts normally and widgets still work.

## Done when
- The acceptance points above hold, and the common acceptance criteria in `00-README.md` §5 hold.
- `dotnet build`, `dotnet test`, `ng build` pass with no new warnings.
- `status.md`, `BACKEND_OVERVIEW.md`, `FRONTEND_OVERVIEW.md` (and the entity / feature lists in `CLAUDE.md`) describe what was actually built.
- You finish with a short report: what was built, what differs from this prompt and why, any open point for the owner.
