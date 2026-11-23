import {
  Component, OnInit, OnDestroy, ChangeDetectionStrategy, ChangeDetectorRef, NgZone,
  inject, signal, computed, effect, untracked, HostListener,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { ChartData } from 'chart.js';
import { BaseChartDirective, provideCharts, withDefaultRegisterables } from 'ng2-charts';
import { Subject, takeUntil } from 'rxjs';

import { ActionItemService } from '../../../core/services/action-item.service';
import { DashboardAnalyticsService } from '../../../core/services/dashboard-analytics.service';
import { AuthService }       from '../../../core/services/auth.service';
import { ToastService }      from '../../../core/services/toast.service';
import { injectWorkspaceCalendar } from '../../../services/workspace-calendar.service';
import { WorkingDayHintComponent } from '../../../shared/components/working-day-hint/working-day-hint.component';
import { WorkingDaysComponent } from '../../../shared/components/working-days/working-days.component';
import { nonWorkingDatesMessage } from '../../../shared/validators/working-day.validator';
import { WorkspaceService }  from '../../workspaces/services/workspace.service';
import { ThemeService }      from '../../../services/theme.service';

import {
  ActionItem, ActionItemCreate, ActionItemFilter,
  ActionStatus, ActionPriority, AssignableUser,
} from '../../../core/models/action-item.model';
import { ActionItemsDashboard, ChartSlice, MyActionsDashboard } from '../../../core/models/dashboard-analytics.model';
import { AuthUser }          from '../../../core/models/auth.models';
import { WorkspaceList, WorkspaceSummary } from '../../workspaces/models/workspace.model';

import { StatusBadgeComponent }   from '../../../shared/components/status-badge/status-badge.component';
import { PriorityBadgeComponent } from '../../../shared/components/priority-badge/priority-badge.component';
import { BreadcrumbComponent }    from '../../../shared/components/breadcrumb/breadcrumb.component';
import { HasPermissionDirective } from '../../../shared';

import { DashTabsComponent } from '../shared/dash-tabs.component';
import { DashStatComponent } from '../shared/dash-stat.component';
import { DashPanelComponent } from '../shared/dash-panel.component';
import { DashLegendComponent, LegendItem } from '../shared/dash-legend.component';
import { RankedBarsComponent, RankedItem } from '../shared/ranked-bars.component';
import { DrilldownPanelComponent } from '../shared/drilldown-panel.component';
import { DrilldownRequest } from '../shared/drilldown.model';
import {
  DUE_COLOR, PRIORITY_COLOR, STATUS_COLOR, TONE, barOptions, doughnutOptions, pct,
} from '../shared/chart-theme';

const STATUS_OPTIONS: { value: ActionStatus; label: string }[] = [
  { value: ActionStatus.ToDo,       label: 'To Do'       },
  { value: ActionStatus.InProgress, label: 'In Progress' },
  { value: ActionStatus.InReview,   label: 'In Review'   },
  { value: ActionStatus.Done,       label: 'Done'        },
  { value: ActionStatus.Overdue,    label: 'Overdue'     },
];

const PRIORITY_OPTIONS: { value: ActionPriority; label: string }[] = [
  { value: ActionPriority.Critical, label: 'Critical' },
  { value: ActionPriority.High,     label: 'High'     },
  { value: ActionPriority.Medium,   label: 'Medium'   },
  { value: ActionPriority.Low,      label: 'Low'      },
];

interface ActionItemFormData {
  workspaceId: string;
  title: string;
  description: string;
  assigneeIds: string[];
  priority: ActionPriority;
  status: ActionStatus;
  startDate: string;
  dueDate: string;
  progress: number;
}

function greeting(): string {
  const h = new Date().getHours();
  if (h < 12) return 'Good morning';
  if (h < 17) return 'Good afternoon';
  return 'Good evening';
}

/**
 * Dashboard overview. Everything on it leads somewhere: stat cards and chart
 * segments open the list of items behind them (or the deeper dashboard for
 * that area), and every listed item links to its own page.
 */
@Component({
  selector: 'app-team-dashboard',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterLink, FormsModule, DatePipe, BaseChartDirective,
    StatusBadgeComponent, PriorityBadgeComponent, BreadcrumbComponent,
    HasPermissionDirective,
    DashTabsComponent, DashStatComponent, DashPanelComponent, DashLegendComponent,
    RankedBarsComponent, DrilldownPanelComponent,
    WorkingDayHintComponent, WorkingDaysComponent,
  ],
  // Chart.js is tree-shakeable: without registering its controllers and
  // scales <canvas baseChart> throws ('"category" is not a registered
  // scale') and renders blank. Provided here rather than at the root so
  // chart.js stays in this lazy chunk.
  providers: [provideCharts(withDefaultRegisterables())],
  templateUrl: './team-dashboard.component.html',
  styleUrl:    './team-dashboard.component.scss',
})
export class TeamDashboardComponent implements OnInit, OnDestroy {
  private readonly actionSvc     = inject(ActionItemService);
  private readonly analytics     = inject(DashboardAnalyticsService);
  private readonly authSvc       = inject(AuthService);
  private readonly toastSvc      = inject(ToastService);
  private readonly workspaceSvc  = inject(WorkspaceService);
  private readonly cdr           = inject(ChangeDetectorRef);
  private readonly zone          = inject(NgZone);
  private readonly destroy$      = new Subject<void>();
  private readonly theme         = toSignal(inject(ThemeService).currentTheme$, { initialValue: 'dark' as const });
  readonly router                = inject(Router);

  // ── Auth state ────────────────────────────────────────
  readonly currentUser  = toSignal(this.authSvc.currentUser$, { initialValue: null as AuthUser | null });
  readonly greeting     = signal(greeting());
  readonly firstName    = computed(() => {
    const name = this.currentUser()?.displayName;
    return name ? name.split(' ')[0] : 'there';
  });
  readonly isPrivileged = computed(() => {
    const roles = this.currentUser()?.roles ?? [];
    return roles.includes('Admin') || roles.includes('Manager');
  });

  /** Shown in the greeting eyebrow. */
  readonly today = new Date();
  readonly pct   = pct;

  // ── Loading ───────────────────────────────────────────
  readonly loadingMine              = signal(true);
  readonly loadingRecent            = signal(true);
  readonly loadingActions           = signal(true);
  readonly loadingWorkspaceSummary  = signal(false);
  /** Set when a request fails, so a failure is not shown as "nothing to do". */
  readonly mineFailed               = signal(false);
  readonly actionsFailed            = signal(false);

  // ── Data ──────────────────────────────────────────────
  /** The signed-in user's own assignments, pre-aggregated for the charts. */
  readonly mine             = signal<MyActionsDashboard | null>(null);
  /** Everything the user can see: headline figures and team activity. */
  readonly actions          = signal<ActionItemsDashboard | null>(null);
  readonly recentActions    = signal<ActionItem[]>([]);
  readonly workspaceSummary = signal<WorkspaceSummary | null>(null);

  /** What the drill-down drawer is showing; null = closed. */
  readonly drill = signal<DrilldownRequest | null>(null);

  // ── Workspace / project stats ─────────────────────────
  readonly totalWorkspaces          = computed(() => this.workspaceSummary()?.totalWorkspaces   ?? 0);
  readonly totalProjects            = computed(() =>
    (this.workspaceSummary()?.strategicProjects   ?? 0) +
    (this.workspaceSummary()?.operationalProjects ?? 0)
  );
  readonly strategicProjectsCount   = computed(() => this.workspaceSummary()?.strategicProjects   ?? 0);
  readonly operationalProjectsCount = computed(() => this.workspaceSummary()?.operationalProjects ?? 0);
  readonly projectCompletionPct     = computed(() => Math.round(this.workspaceSummary()?.projectCompletionRate     ?? 0));
  readonly projectDeliveryPct       = computed(() => Math.round(this.workspaceSummary()?.projectOnTimeDeliveryRate ?? 0));

  // ── My Actions charts ─────────────────────────────────
  private legend(slices: ChartSlice[] | undefined, colors: Record<string, string>): LegendItem[] {
    return (slices ?? []).map(s => ({ ...s, color: colors[s.key] ?? TONE.neutral }));
  }

  readonly myStatusLegend   = computed(() => this.legend(this.mine()?.byStatus, STATUS_COLOR));
  readonly myPriorityLegend = computed(() => this.legend(this.mine()?.byPriority, PRIORITY_COLOR));
  readonly myDueLegend      = computed(() => this.legend(this.mine()?.byDue, DUE_COLOR));

  readonly myStatusChart = computed<ChartData<'doughnut'>>(() => {
    const items = this.myStatusLegend();
    return {
      labels: items.map(i => i.label),
      datasets: [{ data: items.map(i => i.count), backgroundColor: items.map(i => i.color), borderWidth: 0, spacing: 2, hoverOffset: 6 }],
    };
  });

  private bars(items: LegendItem[]): ChartData<'bar'> {
    return {
      labels: items.map(i => i.label),
      datasets: [{ data: items.map(i => i.count), backgroundColor: items.map(i => i.color), borderRadius: 6, maxBarThickness: 34 }],
    };
  }

  readonly myPriorityChart = computed(() => this.bars(this.myPriorityLegend()));
  readonly myDueChart      = computed(() => this.bars(this.myDueLegend()));

  /** Chart.js fires callbacks outside Angular's zone. */
  private inZone(fn: () => void): void {
    this.zone.run(fn);
  }

  // Rebuilt on theme change so the resolved CSS-variable colours refresh
  readonly myStatusOptions   = computed(() => { this.theme(); return doughnutOptions(i => this.inZone(() => this.openMyStatus(this.myStatusLegend()[i]?.key))); });
  readonly myPriorityOptions = computed(() => { this.theme(); return barOptions(i => this.inZone(() => this.openMyPriority(this.myPriorityLegend()[i]?.key)), { horizontal: true }); });
  readonly myDueOptions      = computed(() => { this.theme(); return barOptions(i => this.inZone(() => this.openMyDue(this.myDueLegend()[i]?.key)), { horizontal: true }); });

  // ── Team activity: one row per person, busiest first ──
  readonly teamItems = computed<RankedItem[]>(() =>
    (this.actions()?.team ?? []).map(m => ({
      id: m.userId,
      label: m.userName,
      total: m.total,
      segments: [
        { value: m.completed, tone: 'good', label: 'Completed' },
        // "open" includes the overdue ones — show them separately
        { value: Math.max(0, m.open - m.overdue), tone: 'brand', label: 'Open' },
        { value: m.overdue, tone: 'bad', label: 'Overdue' },
      ],
    })));

  // ── Drill-downs: my own items ─────────────────────────
  private labelOf(items: LegendItem[], key: string): string {
    return items.find(i => i.key === key)?.label ?? key;
  }

  openMyStatus(key: string | undefined): void {
    if (!key) return;
    this.drill.set({
      kind: 'actions', scope: 'mine', title: `My actions — ${this.labelOf(this.myStatusLegend(), key)}`,
      filter: { status: +key as ActionStatus },
      fullList: { label: 'Open in My Actions', link: ['/actions'], queryParams: { status: +key } },
    });
  }

  openMyPriority(key: string | undefined): void {
    if (!key) return;
    this.drill.set({
      kind: 'actions', scope: 'mine', title: `My open actions — ${this.labelOf(this.myPriorityLegend(), key)} priority`,
      filter: { priority: +key, openOnly: true },
      fullList: { label: 'Open in My Actions', link: ['/actions'], queryParams: { priority: +key } },
    });
  }

  openMyDue(key: string | undefined): void {
    if (!key) return;
    this.drill.set({
      kind: 'actions', scope: 'mine', title: `My open actions — ${this.labelOf(this.myDueLegend(), key).toLowerCase()}`,
      filter: { dueBucket: key },
      fullList: { label: 'Open My Actions', link: ['/actions'] },
    });
  }

  openMyOpen(): void {
    this.drill.set({ kind: 'actions', scope: 'mine', title: 'My open actions', filter: { openOnly: true },
      fullList: { label: 'Open My Actions', link: ['/actions'] } });
  }

  openMyOverdue(): void {
    this.drill.set({ kind: 'actions', scope: 'mine', title: 'My overdue actions', filter: { overdueOnly: true },
      fullList: { label: 'Open My Actions', link: ['/actions'] } });
  }

  // ── Drill-downs: everything visible ───────────────────
  openAllActions(): void {
    this.drill.set({ kind: 'actions', scope: 'all', title: 'All action items', filter: {} });
  }

  openCompleted(): void {
    this.drill.set({ kind: 'actions', scope: 'all', title: 'Completed action items', filter: { status: ActionStatus.Done } });
  }

  openEscalated(): void {
    this.drill.set({ kind: 'actions', scope: 'all', title: 'Escalated action items', filter: { isEscalated: true } });
  }

  openOverdue(): void {
    this.drill.set({ kind: 'actions', scope: 'all', title: 'Overdue action items',
      subtitle: 'Open and past their due date', filter: { overdueOnly: true } });
  }

  openMember(item: RankedItem): void {
    this.drill.set({ kind: 'actions', scope: 'all', title: item.label,
      subtitle: `${item.total} action items assigned`, filter: { assigneeId: item.id } });
  }

  go(path: string): void {
    this.router.navigate([path]);
  }

  // ── Enums ─────────────────────────────────────────────
  readonly ActionStatus   = ActionStatus;
  readonly ActionPriority = ActionPriority;
  readonly STATUS_OPTIONS   = STATUS_OPTIONS;
  readonly PRIORITY_OPTIONS = PRIORITY_OPTIONS;

  // ── Offcanvas form state ──────────────────────────────
  showActionForm = false;
  actionSaving   = false;
  actionForm: ActionItemFormData = this.emptyActionForm();

  // Working calendar of the drawer's workspace (WO-2026-11 C-3). Create only,
  // so every date entered is checked.
  private readonly formWorkspaceId = signal('');
  readonly calendar = injectWorkspaceCalendar(() => this.formWorkspaceId());
  assigneeDropdownOpen = false;
  assigneeSearchTerm   = '';
  allUsers:      AssignableUser[] = [];
  allWorkspaces: WorkspaceList[]  = [];

  // ── Lifecycle ─────────────────────────────────────────
  constructor() {
    // currentUser is null at ngOnInit time (async auth), so we react to it
    effect(() => {
      if (this.isPrivileged() && !this.workspaceSummary()) {
        untracked(() => this.loadWorkspaceSummary());
      }
    });
  }

  ngOnInit(): void {
    this.loadAnalytics();
    this.loadRecentActions();
    this.loadAllWorkspaces();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  // ── Data loaders ──────────────────────────────────────
  private loadWorkspaceSummary(): void {
    this.loadingWorkspaceSummary.set(true);
    this.workspaceSvc.getSummary().subscribe({
      next: r => {
        this.workspaceSummary.set(r.data);
        this.loadingWorkspaceSummary.set(false);
      },
      error: () => this.loadingWorkspaceSummary.set(false),
    });
  }

  /** My-actions charts, headline figures and team activity. */
  loadAnalytics(): void {
    this.mineFailed.set(false);
    this.actionsFailed.set(false);

    this.analytics.getMyActions().pipe(takeUntil(this.destroy$)).subscribe({
      next: d => { this.mine.set(d); this.loadingMine.set(false); },
      error: () => { this.loadingMine.set(false); this.mineFailed.set(true); },
    });

    this.analytics.getActionItems().pipe(takeUntil(this.destroy$)).subscribe({
      next: d => { this.actions.set(d); this.loadingActions.set(false); },
      error: () => { this.loadingActions.set(false); this.actionsFailed.set(true); },
    });
  }

  /** A figure for a stat card, or a dash when the data could not be loaded. */
  stat(value: number | string | undefined): number | string {
    return this.actionsFailed() ? '—' : (value ?? 0);
  }

  private loadRecentActions(): void {
    const filter: ActionItemFilter = {
      pageNumber: 1, pageSize: 5,
      sortBy: 'createdAt', sortDescending: true,
    };

    this.actionSvc.getAll(filter).subscribe({
      next: r => {
        this.recentActions.set(r.data?.items ?? []);
        this.loadingRecent.set(false);
      },
      error: () => this.loadingRecent.set(false),
    });
  }

  // ── View helpers ──────────────────────────────────────
  dueDateClass(item: ActionItem): string {
    if (item.isOverdue || item.status === ActionStatus.Overdue) return 'due--overdue';
    if (item.daysUntilDue <= 3) return 'due--warning';
    return '';
  }

  dueDateLabel(item: ActionItem): string {
    if (item.isOverdue) return `${Math.abs(item.daysUntilDue)}d overdue`;
    if (item.daysUntilDue === 0) return 'Today';
    if (item.daysUntilDue === 1) return 'Tomorrow';
    return `${item.daysUntilDue}d`;
  }

  skeletonRows = Array.from({ length: 4 });

  // ── Offcanvas form ────────────────────────────────────
  private emptyActionForm(): ActionItemFormData {
    return {
      workspaceId: '',
      title: '',
      description: '',
      assigneeIds: [],
      priority: ActionPriority.Medium,
      status: ActionStatus.ToDo,
      startDate: '',
      dueDate: '',
      progress: 0,
    };
  }

  private loadAllWorkspaces(): void {
    this.workspaceSvc.getWorkspaces()
      .pipe(takeUntil(this.destroy$))
      .subscribe({ next: r => { this.allWorkspaces = r.data ?? []; this.cdr.markForCheck(); } });
  }

  openNewActionForm(): void {
    this.actionForm = this.emptyActionForm();
    this.formWorkspaceId.set('');
    this.allUsers = [];
    this.assigneeDropdownOpen = false;
    this.assigneeSearchTerm = '';
    this.showActionForm = true;
    this.cdr.markForCheck();
  }

  cancelActionForm(): void {
    this.showActionForm = false;
    this.assigneeDropdownOpen = false;
    this.assigneeSearchTerm = '';
    this.cdr.markForCheck();
  }

  onWorkspaceChange(workspaceId: string): void {
    this.formWorkspaceId.set(workspaceId ?? '');
    if (!workspaceId) {
      this.allUsers = [];
      this.actionForm.assigneeIds = [];
      this.cdr.markForCheck();
      return;
    }
    this.workspaceSvc.getAssignableUsers(workspaceId)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: r => {
          this.allUsers = r.data ?? [];
          const validIds = new Set(this.allUsers.map(u => u.userId));
          const before = this.actionForm.assigneeIds.length;
          this.actionForm.assigneeIds = this.actionForm.assigneeIds.filter(id => validIds.has(id));
          if (before > 0 && this.actionForm.assigneeIds.length < before) {
            this.toastSvc.info('Assignees were cleared because the workspace changed — please select new assignees.');
          }
          this.cdr.markForCheck();
        },
        error: () => { this.allUsers = []; this.cdr.markForCheck(); },
      });
  }

  onStatusChange(): void {
    if (+this.actionForm.status === ActionStatus.Done) {
      this.actionForm.progress = 100;
    }
  }

  @HostListener('document:click')
  onDocumentClick(): void {
    if (this.assigneeDropdownOpen) {
      this.assigneeDropdownOpen = false;
      this.cdr.markForCheck();
    }
  }

  get filteredUsers(): AssignableUser[] {
    if (!this.assigneeSearchTerm.trim()) return this.allUsers;
    const term = this.assigneeSearchTerm.toLowerCase();
    return this.allUsers.filter(u => u.userName.toLowerCase().includes(term));
  }

  getAssigneeName(userId: string): string {
    return this.allUsers.find(u => u.userId === userId)?.userName ?? userId;
  }

  toggleAssignee(userId: string): void {
    const idx = this.actionForm.assigneeIds.indexOf(userId);
    if (idx >= 0) this.actionForm.assigneeIds.splice(idx, 1);
    else          this.actionForm.assigneeIds.push(userId);
  }

  isAssigneeSelected(userId: string): boolean {
    return this.actionForm.assigneeIds.includes(userId);
  }

  saveAction(): void {
    if (!this.actionForm.title.trim() || !this.actionForm.workspaceId ||
        this.actionForm.assigneeIds.length === 0 || !this.actionForm.dueDate) return;

    // Working-day rule — the server applies the same check and stays the authority.
    const dateProblem = nonWorkingDatesMessage(this.calendar(), [
      { field: 'Start date', value: this.actionForm.startDate },
      { field: 'Due date',   value: this.actionForm.dueDate },
    ]);
    if (dateProblem) {
      this.toastSvc.error(dateProblem);
      return;
    }

    this.actionSaving = true;
    this.cdr.markForCheck();

    const payload: ActionItemCreate = {
      title:       this.actionForm.title.trim(),
      description: this.actionForm.description?.trim() ?? '',
      workspaceId: this.actionForm.workspaceId,
      isStandalone: true,
      assigneeIds: this.actionForm.assigneeIds,
      priority:    +this.actionForm.priority as ActionPriority,
      status:      +this.actionForm.status as ActionStatus,
      startDate:   this.actionForm.startDate || null,
      dueDate:     this.actionForm.dueDate,
      progress:    +this.actionForm.progress,
    };

    this.actionSvc.create(payload).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => {
        this.actionSaving = false;
        this.showActionForm = false;
        this.toastSvc.success('Action item created.');
        this.loadAnalytics();
        this.loadRecentActions();
        this.cdr.markForCheck();
      },
      error: err => {
        this.actionSaving = false;
        this.toastSvc.error(err?.error?.message ?? 'Failed to save action item.');
        this.cdr.markForCheck();
      },
    });
  }
}
