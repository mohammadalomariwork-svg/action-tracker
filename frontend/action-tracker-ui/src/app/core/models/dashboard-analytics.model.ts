// Shapes returned by the analytics endpoints under /api/dashboard
// (DashboardAnalyticsDtos.cs on the backend).

/** One chart segment. `key` is sent back as a filter when drilling down. */
export interface ChartSlice {
  key: string;
  label: string;
  count: number;
}

export interface TeamMemberActivity {
  userId: string;
  userName: string;
  total: number;
  open: number;
  inProgress: number;
  completed: number;
  overdue: number;
  completionRate: number;
}

// ── My actions ──────────────────────────────────────────────────────────────
export interface MyActionsDashboard {
  total: number;
  open: number;
  completed: number;
  overdue: number;
  dueThisWeek: number;
  completionRate: number;
  /** Every item, keyed by ActionStatus value. */
  byStatus: ChartSlice[];
  /** Open items only, keyed by ActionPriority value. */
  byPriority: ChartSlice[];
  /** Open items only, keyed by due window (overdue | next7 | next30 | later). */
  byDue: ChartSlice[];
}

// ── Action items ────────────────────────────────────────────────────────────
export interface ActionTrendPoint {
  weekStart: string;
  label: string;
  created: number;
  completed: number;
}

export interface WorkspaceActions {
  workspaceId: string;
  title: string;
  total: number;
  open: number;
  completed: number;
  overdue: number;
}

export interface ActionItemsDashboard {
  total: number;
  open: number;
  completed: number;
  overdue: number;
  escalated: number;
  standalone: number;
  projectLinked: number;
  completionRate: number;
  onTimeRate: number;
  byStatus: ChartSlice[];
  byPriority: ChartSlice[];
  byDue: ChartSlice[];
  /** Open items by days since creation (0-7 | 8-30 | 31-90 | 90+). */
  byAge: ChartSlice[];
  trend: ActionTrendPoint[];
  byWorkspace: WorkspaceActions[];
  team: TeamMemberActivity[];
}

// ── Projects ────────────────────────────────────────────────────────────────
export type ProjectHealth =
  | 'OnTrack' | 'AtRisk' | 'Delayed' | 'Completed' | 'NotStarted' | 'OnHold' | 'Cancelled';

export interface ProjectRow {
  id: string;
  code: string;
  name: string;
  workspaceId: string;
  workspaceTitle: string;
  status: number;
  statusLabel: string;
  projectType: number;
  typeLabel: string;
  priority: number;
  priorityLabel: string;
  projectManagerName: string;
  strategicObjectiveId: string | null;
  plannedStartDate: string;
  plannedEndDate: string;
  /** 0–100 */
  progress: number;
  actionsTotal: number;
  actionsDone: number;
  actionsOverdue: number;
  milestonesTotal: number;
  milestonesDone: number;
  milestonesDelayed: number;
  health: ProjectHealth;
  healthLabel: string;
  /** Negative when past the planned end date. */
  daysToEnd: number;
}

export interface MilestoneRow {
  id: string;
  code: string;
  name: string;
  projectId: string;
  projectName: string;
  status: number;
  statusLabel: string;
  plannedDueDate: string;
  completion: number;
  isLate: boolean;
  daysToDue: number;
}

export interface ProjectsDashboard {
  total: number;
  active: number;
  completed: number;
  onTrack: number;
  atRisk: number;
  delayed: number;
  avgProgress: number;
  milestonesTotal: number;
  milestonesCompleted: number;
  milestonesDelayed: number;
  milestonesDueSoon: number;
  projects: ProjectRow[];
  milestones: MilestoneRow[];
}

// ── Strategy ────────────────────────────────────────────────────────────────
export type KpiHealth = 'OnTrack' | 'AtRisk' | 'OffTrack' | 'NoData';

export interface StrategyPlanRow {
  id: string;
  name: string;
  orgUnitId: string;
  orgUnitName: string;
  status: number;
  statusLabel: string;
  startYear: number;
  endYear: number;
  objectiveCount: number;
  kpiCount: number;
  avgAchievement: number | null;
}

export interface StrategyObjectiveRow {
  id: string;
  code: string;
  statement: string;
  planId: string;
  planName: string;
  orgUnitId: string;
  orgUnitName: string;
  kpiCount: number;
  kpisOnTrack: number;
  kpisAtRisk: number;
  kpisOffTrack: number;
  kpisNoData: number;
  avgAchievement: number | null;
  projectCount: number;
  projectsCompleted: number;
  avgProjectProgress: number;
}

export interface StrategyKpiRow {
  id: string;
  kpiNumber: number;
  name: string;
  unit: string | null;
  periodLabel: string;
  objectiveId: string;
  objectiveCode: string;
  planId: string;
  orgUnitId: string;
  orgUnitName: string;
  targetCount: number;
  actualCount: number;
  achievement: number | null;
  health: KpiHealth;
  healthLabel: string;
}

export interface StrategyDashboard {
  year: number;
  plans: number;
  objectives: number;
  kpis: number;
  kpisWithData: number;
  strategicProjects: number;
  strategicProjectsCompleted: number;
  actualsPendingApproval: number;
  avgAchievement: number | null;
  actualsByStatus: ChartSlice[];
  planRows: StrategyPlanRow[];
  objectiveRows: StrategyObjectiveRow[];
  kpiRows: StrategyKpiRow[];
  projectRows: ProjectRow[];
}
