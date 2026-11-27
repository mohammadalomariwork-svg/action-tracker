# WO-2026-12 — Executive Office dashboards and Outlook add-in: prompt series

| Field | Value |
|---|---|
| Application | KUmmand Center (KU Action Tracker) |
| Source | `VP_Office_Dashboard_Mockups_with_data_mapping_v1.2.pptx` (5 screens + architecture) and the owner's request of 27 November 2026 |
| Starts after | WO-2026-11 is finished: P-2b step 2, P-3, P-4, P-5 and **D-1 to D-5** |
| Written against | commit `a45873b` (2026-11-26) plus the uncommitted P-2b step 1 work |
| Executor | Claude Code, **one prompt per session** |

## 1. How to run

Give Claude Code one file at a time, in the order of §3, with this instruction:

> Read `Docs/WO-2026-12-VP-Dashboards-Outlook-Addin/prompts/00-README.md` and `Docs/WO-2026-12-VP-Dashboards-Outlook-Addin/prompts/<file>`. Implement that package only and stop.

Commit after each package so it can be reviewed or reverted on its own.

## 2. The idea in one paragraph

The mockup's five screens are **not** built as five pages. Every block on them becomes a **widget in the catalogue** created by WO-2026-11 D-2, and the five screens become **system dashboard templates** assembled from those widgets (package V-S1). An administrator can assign a template, or a clone of it, to a role, a user or a group, limit it to workspaces, and show or hide each widget per audience, exactly as for every other dashboard. The names "VP Office", "Data", "AI" and "Managed Services" are sample data: nothing in the code may name a specific office or department.

## 3. Execution order

| # | File | Package | Size |
|---|---|---|---|
| 1 | `01-V-F1-schedule-baseline-forecast-gates.md` | V-F1 — Schedule data: project baseline, forecast, gates, issues | M |
| 2 | `02-V-F2-project-status-updates.md` | V-F2 — Project status updates and the stale rule | M |
| 3 | `03-V-F3-project-decisions.md` | V-F3 — Decisions needed on a project | M |
| 4 | `04-V-F4-dependencies-planning-periods.md` | V-F4 — Cross-project dependencies and planning periods (freeze bands) | M |
| 5 | `05-V-F5-action-source-office-scope.md` | V-F5 — Action item source, office scope and closure measures | M |
| 6 | `06-V-F6-portfolio-calculation-engine.md` | V-F6 — Portfolio calculation engine and scope | L |
| 7 | `07-V-F7-weekly-snapshots.md` | V-F7 — Weekly snapshots, history and reminders job | M |
| 8 | `08-V-F8-dashboard-context-filters-print.md` | V-F8 — Dashboard context: filter bar, parameters, cross-dashboard drill, print | L |
| 9 | `09-V-W01-portfolio-stats.md` | V-W01 — Widget `portfolio.stats` — Portfolio KPI tiles | M |
| 10 | `10-V-W02-portfolio-department-health.md` | V-W02 — Widget `portfolio.department-health` — Department health matrix | M |
| 11 | `11-V-W03-portfolio-attention.md` | V-W03 — Widget `portfolio.attention` — Projects needing attention | M |
| 12 | `12-V-W04-office-actions-by-department.md` | V-W04 — Widget `office-actions.by-department` — Office actions by department with ageing | M |
| 13 | `13-V-W05-portfolio-type-split.md` | V-W05 — Widget `portfolio.type-split` — Strategic vs operational table | S |
| 14 | `14-V-W06-portfolio-upcoming-gates.md` | V-W06 — Widget `portfolio.upcoming-gates` — Gates and key milestones, next 30 days | S |
| 15 | `15-V-W07-portfolio-project-table.md` | V-W07 — Widget `portfolio.project-table` — Projects table | M |
| 16 | `16-V-W08-portfolio-baseline-vs-forecast.md` | V-W08 — Widget `portfolio.baseline-vs-forecast` — Baseline vs forecast mini-roadmap | M |
| 17 | `17-V-W09-office-actions-list.md` | V-W09 — Widget `office-actions.list` — Office actions list (compact) | S |
| 18 | `18-V-W10-project-header.md` | V-W10 — Widget `project.header` — Project header and actions | M |
| 19 | `19-V-W11-project-stats.md` | V-W11 — Widget `project.stats` — Project KPI tiles | S |
| 20 | `20-V-W12-project-milestones.md` | V-W12 — Widget `project.milestones` — Project milestones with variance | S |
| 21 | `21-V-W13-project-progress-history.md` | V-W13 — Widget `project.progress-history` — RAG and % complete history | M |
| 22 | `22-V-W14-project-decisions.md` | V-W14 — Widget `project.decisions` — Decisions needed | S |
| 23 | `23-V-W15-project-top-risks.md` | V-W15 — Widget `project.top-risks` — Top risks and issues | S |
| 24 | `24-V-W16-project-latest-status.md` | V-W16 — Widget `project.latest-status` — Latest status update | S |
| 25 | `25-V-W17-office-actions-stats.md` | V-W17 — Widget `office-actions.stats` — Office action KPI tiles | S |
| 26 | `26-V-W18-office-actions-tracker.md` | V-W18 — Widget `office-actions.tracker` — Office action tracker (table, board, by owner) | L |
| 27 | `27-V-W19-office-actions-by-owner.md` | V-W19 — Widget `office-actions.by-owner` — Open office actions by owner | S |
| 28 | `28-V-W20-office-actions-by-source.md` | V-W20 — Widget `office-actions.by-source` — Open office actions by source | S |
| 29 | `29-V-W21-office-actions-closed-per-week.md` | V-W21 — Widget `office-actions.closed-per-week` — Office actions closed per week | S |
| 30 | `30-V-W22-roadmap-gantt.md` | V-W22 — Widget `roadmap.gantt` — Portfolio roadmap (Gantt) | L |
| 31 | `31-V-W23-roadmap-load-by-month.md` | V-W23 — Widget `roadmap.load-by-month` — Milestone and gate load by month | S |
| 32 | `32-V-W24-roadmap-watch-list.md` | V-W24 — Widget `roadmap.watch-list` — Planning watch-list | M |
| 33 | `33-V-S1-system-dashboard-templates.md` | V-S1 — Five system dashboard templates and navigation | M |
| 34 | `34-V-S2-roadmap-scenario-mode.md` | V-S2 — Roadmap scenario mode (what-if) — optional, last | L |
| 35 | `35-O-1-outlook-backend-context-auth.md` | O-1 — Add-in API: permission, context, targets, e-mail links | M |
| 36 | `36-O-2-outlook-backend-convert.md` | O-2 — Add-in API: convert an e-mail into an action item or a project | M |
| 37 | `37-O-3-outlook-addin-shell-manifest-signin.md` | O-3 — Add-in shell, manifest and sign-in | L |
| 38 | `38-O-4-outlook-addin-home-mail-reader.md` | O-4 — Task pane home and reading the e-mail | M |
| 39 | `39-O-5-outlook-addin-convert-forms.md` | O-5 — Convert forms: action item, project, add to existing project | L |
| 40 | `40-O-6-outlook-addin-hardening-deployment.md` | O-6 — Add-in hardening, tests and deployment pack | M |

Stream A (dashboards) and stream B (Outlook add-in) are independent, except that O-2 needs the `Source` field from V-F5. The widgets V-W01 to V-W24 may be run in any order once V-F1 to V-F8 are done; the listed order follows the screens.

## 4. Definitions used by every package

These are the owner-approved defaults. If the owner changed one, the changed text here wins over the prompt.

| Term | Definition |
|---|---|
| **Portfolio scope** | The projects a dashboard counts: (workspaces linked to the dashboard, else all the viewer's workspaces) ∩ (projects under the dashboard's **root org unit**, when one is set) ∩ what the viewer may see. A dashboard narrows data and never widens it. |
| **Department** | An `OrgUnit` that is a direct child of the dashboard's root org unit. A project belongs to the department that contains `Project.OwnerOrgUnitId`, or `Workspace.OrgUnitId` when the project has no owner org unit. Projects under no child are grouped as "Other". Head = `OrgUnit.HeadUserId`. |
| **Office actions** ("VP actions" in the mockup) | Action items created by a member of the dashboard's **office group** (a `UserGroup` from WO-2026-11 D-1). An action's department comes from its project when it has one, otherwise from its workspace's org unit. |
| **Baseline** | The dates stored when a project is baselined: `Project.BaselineStartDate / BaselineEndDate` and the existing `Milestone.BaselinePlannedStartDate / BaselinePlannedDueDate`. |
| **Forecast finish** | `Project.ForecastEndDate` when set, otherwise the latest milestone forecast (`Milestone.ForecastDueDate ?? PlannedDueDate`), otherwise `PlannedEndDate`. |
| **Variance (WD)** | Working days from baseline finish to forecast finish, counted with the workspace's work calendar (`IWorkCalendarService.WorkingDaysBetween`). Positive = late. |
| **% complete** | The existing `ProjectRules.Progress`. Not redefined. |
| **Planned %** | For a date *d*: the mean over the project's live milestones of how much of each milestone's baseline window has passed on *d* (0 before its baseline start, 100 after its baseline due date, linear in working days in between). No milestones: the same on the project's baseline window. |
| **RAG** | Active and On Hold projects only. **Red**: existing health `Delayed`, or variance above `Portfolio:RedAboveWorkingDays` (default 10). **Amber**: existing health `AtRisk`, or variance of 1 working day or more. **Green**: otherwise. Completed, Draft, Pending Approval and Cancelled projects have no RAG. No manual override. |
| **Stale** | The last *submitted* status update is older than `Portfolio:StaleAfterDays` (default 14), or there is none and the project has been active longer than that. |
| **Decision pending** | A `ProjectDecision` with status Pending. "Old" = raised more than `Portfolio:DecisionOldAfterDays` (default 10) ago. |
| **Gate / key milestone** | A milestone with `IsGate` or `IsKeyMilestone` set. |
| **Milestone forecast status** | Done · Done late (+N wd) · Overdue (+N wd) · Late (+N wd, forecast after baseline by more than the red threshold) · At risk (+N wd, forecast after baseline) · On track · Not started. |
| **Closed on time** | An action item that is Done with `ActualEndDate` (date part) on or before `DueDate` (date part). |
| **Trend** | This week's figure against the previous weekly snapshot: better, worse or same. |

All of these live in **one** place on the server, `PortfolioRules` (V-F6), next to the existing `ActionItemRules` and `ProjectRules` in `Features/Dashboard/DashboardRules.cs`. The existing rules, and therefore the numbers of the four existing dashboards, do not change.

## 5. Rules for every package

1. **WO-2026-11 §6 and §7 apply unchanged** (`Docs/WO-2026-11-Kummand-Center-Enhancements.md`): real code pattern over the briefing (no MediatR, routes `/api/<resource>`, Guid keys, string user ids), additive migrations only, soft delete and private-workspace query filters on every new entity, a policy on every endpoint and the four places for a new permission, DTOs + FluentValidation at the boundary, standalone components, theme tokens, dark and light, keyboard, mobile width, unit tests for rules and one integration test per new controller, the three documentation files updated.
2. **Read what was built, not what was planned.** WO-2026-11 D-1 to D-5 will have produced the dashboard entities, the widget registry on both sides, the renderer, the builder and the multi-level drill-down. Before writing a line, read that code and `status.md` §7 and follow its actual names and contracts. Where this series says "the widget registry", "the widget contract" or "`DrilldownRequest`", it means whatever those are called in the code.
3. **Widgets are catalogue entries.** No widget is hard-wired to a page, an office, a department or a role. Everything that differs between offices is a dashboard setting or a widget setting.
4. **No arithmetic in the browser.** Every figure, colour band and flag is calculated by the server from `PortfolioRules`. The client formats and draws.
5. **A list equals its figure.** A drill-down opened from a number shows exactly that many rows, for the same viewer and the same filters.
6. **Access.** Data scope always comes from the existing resolvers (`IWorkspaceAccessResolver`, `IProjectAccessService`, `IStrategicScopeService`, the private-workspace filters). A user who may not see a project must not learn its name, code, counts or dates through any widget, drill-down, export, dependency arrow or watch-list sentence. Counts are taken over visible rows only.
7. **Writes go through existing services** (`ProjectService`, `MilestoneService`, `ActionItemService`, `DocumentService`, the workflow services) so that the ownership rule (P-2b), the calendar rule (C-3), the date freeze and the approval workflows stay in force. No widget or add-in endpoint writes to a table directly.
8. **No new dependency** without the owner's approval. Allowed: `@angular/cdk` (already approved). The add-in may load Office.js from Microsoft's CDN and use the MSAL packages already installed.
9. **Sample data.** The names and numbers in the mockup are illustrative. Do not seed them into any real database. Tests use the fixture created in V-F6.

## 6. Stop and ask before continuing if

- a WO-2026-11 package this prompt depends on is not in the code, or was built in a way that makes this prompt impossible as written;
- a migration would drop, rename or re-type a column, or a backfill touches more rows than the prompt says;
- the change would alter `ActionItemRules`, `ProjectRules`, an approval workflow, the date freeze, the ownership rule or private-workspace filtering in a way the prompt does not describe;
- a new NuGet or npm package is needed;
- the prompt needs a decision that §4 does not give.

## 7. Out of scope for this series

Meetings and minutes; the "VP inbox"; AI extraction of actions from meetings or e-mail; MS Teams; KUBee, Fusion / ADERP, budget and procurement feeds; the employee performance framework; automatic rescheduling from dependencies or critical path; saving what-if scenarios; a PDF library (exports use the print style sheet); Outlook on mobile; compose-mode add-in commands; event-based add-in activation.
