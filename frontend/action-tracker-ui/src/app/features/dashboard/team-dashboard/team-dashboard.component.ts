import {
  Component, OnInit, OnDestroy, ChangeDetectionStrategy, ChangeDetectorRef,
  inject, signal, computed, effect, untracked, HostListener,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { DatePipe, DecimalPipe } from '@angular/common';
import { ChartConfiguration, ChartData, ChartType } from 'chart.js';
import { BaseChartDirective } from 'ng2-charts';
import { Subject, takeUntil } from 'rxjs';

import { ActionItemService } from '../../../core/services/action-item.service';
import { DashboardService }  from '../../../core/services/dashboard.service';
import { AuthService }       from '../../../core/services/auth.service';
import { ToastService }      from '../../../core/services/toast.service';
import { WorkspaceService }  from '../../workspaces/services/workspace.service';

import {
  ActionItem, ActionItemCreate, ActionItemFilter,
  ActionStatus, ActionPriority, AssignableUser,
} from '../../../core/models/action-item.model';
import { DashboardKpi, StatusBreakdown } from '../../../core/models/dashboard.model';
import { AuthUser }          from '../../../core/models/auth.models';
import { WorkspaceList, WorkspaceSummary } from '../../workspaces/models/workspace.model';

import { StatusBadgeComponent }   from '../../../shared/components/status-badge/status-badge.component';
import { PriorityBadgeComponent } from '../../../shared/components/priority-badge/priority-badge.component';
import { BreadcrumbComponent }    from '../../../shared/components/breadcrumb/breadcrumb.component';
import { PageHeaderComponent }    from '../../../shared/components/page-header/page-header.component';
import { HasPermissionDirective } from '../../../shared';

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

const STATUS_COLORS: Record<string, string> = {
  'ToDo':       '#94a3b8',
  'InProgress': '#0284c7',
  'InReview':   '#7c3aed',
  'Done':       '#059669',
  'Overdue':    '#dc2626',
};

@Component({
  selector: 'app-team-dashboard',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterLink, FormsModule, DatePipe, DecimalPipe, BaseChartDirective,
    StatusBadgeComponent, PriorityBadgeComponent, BreadcrumbComponent, PageHeaderComponent,
    HasPermissionDirective,
  ],
  templateUrl: './team-dashboard.component.html',
  styleUrl:    './team-dashboard.component.scss',
})
export class TeamDashboardComponent implements OnInit, OnDestroy {
  private readonly actionSvc     = inject(ActionItemService);
  private readonly dashSvc       = inject(DashboardService);
  private readonly authSvc       = inject(AuthService);
  private readonly toastSvc      = inject(ToastService);
  private readonly workspaceSvc  = inject(WorkspaceService);
  private readonly cdr           = inject(ChangeDetectorRef);
  private readonly destroy$      = new Subject<void>();
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

  // ── Loading ───────────────────────────────────────────
  readonly loadingMine              = signal(true);
  readonly loadingRecent            = signal(true);
  readonly loadingKpis              = signal(true);
  readonly loadingWorkspaceSummary  = signal(false);

  // ── Data ──────────────────────────────────────────────
  readonly myActions        = signal<ActionItem[]>([]);
  readonly recentActions    = signal<ActionItem[]>([]);
  readonly kpis             = signal<DashboardKpi | null>(null);
  readonly statusBreakdown  = signal<StatusBreakdown[]>([]);
  readonly workspaceSummary = signal<WorkspaceSummary | null>(null);

  // ── Global stats (derived from KPIs) ─────────────────
  readonly totalActions    = computed(() => this.kpis()?.totalActions    ?? 0);
  readonly completionRate  = computed(() => this.kpis()?.completionRate  ?? 0);
  readonly onTimeRate      = computed(() => this.kpis()?.onTimeDeliveryRate ?? 0);
  readonly escalations     = computed(() => this.kpis()?.activeEscalations  ?? 0);
  readonly overdueCount    = computed(() => this.kpis()?.overdueCount    ?? 0);

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

  // ── Status breakdown derived stats ───────────────────
  readonly doneCount = computed(() =>
    this.statusBreakdown().find(s => s.status.replace(/\s/g, '') === 'Done')?.count ?? 0);
  readonly dueThisWeek = computed(() =>
    this.myActions().filter(a => {
      if (!a.dueDate) return false;
      const diff = (new Date(a.dueDate).getTime() - Date.now()) / 86_400_000;
      return diff >= 0 && diff <= 7;
    }).length
  );

  // ── Enums ─────────────────────────────────────────────
  readonly ActionStatus   = ActionStatus;
  readonly ActionPriority = ActionPriority;
  readonly STATUS_OPTIONS   = STATUS_OPTIONS;
  readonly PRIORITY_OPTIONS = PRIORITY_OPTIONS;

  // ── Offcanvas form state ──────────────────────────────
  showActionForm = false;
  actionSaving   = false;
  actionForm: ActionItemFormData = this.emptyActionForm();
  assigneeDropdownOpen = false;
  assigneeSearchTerm   = '';
  allUsers:      AssignableUser[] = [];
  allWorkspaces: WorkspaceList[]  = [];

  // ── My actions link with assignee filter ──────────────
  readonly myActionsLink = computed(() => ['/actions']);
  readonly myActionsQueryParams = computed(() => ({}));

  // ── Compact horizontal bar chart ──────────────────────
  readonly hBarType = 'bar' as const;

  hBarData: ChartData<'bar'> = { labels: [], datasets: [] };

  hBarOptions: ChartConfiguration<'bar'>['options'] = {
    responsive: true,
    maintainAspectRatio: false,
    indexAxis: 'y',
    plugins: {
      legend: { display: false },
      tooltip: {
        backgroundColor: 'var(--el, #1e293b)',
        borderColor: 'var(--bd, rgba(255,255,255,0.06))',
        borderWidth: 1,
        titleColor: 'var(--t1, #f1f5f9)',
        bodyColor: 'var(--t1, #f1f5f9)',
        callbacks: { label: ctx => ` ${ctx.parsed.x} actions` },
      },
    },
    scales: {
      x: {
        beginAtZero: true,
        ticks: { stepSize: 1, font: { size: 10 }, color: '#94a3b8' },
        grid: { color: 'rgba(0,229,255,0.06)' },
      },
      y: {
        ticks: { font: { size: 11 }, color: '#94a3b8' },
        grid: { display: false },
      },
    },
  };

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
    this.loadMyActions();
    this.loadRecentActions();
    this.loadKpis();
    this.loadStatusBreakdown();
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

  private loadMyActions(): void {
    const filter: ActionItemFilter = {
      pageNumber: 1, pageSize: 10,
      sortBy: 'dueDate', sortDescending: false,
    };

    this.actionSvc.getAll(filter).subscribe({
      next: r => {
        this.myActions.set(r.data?.items ?? []);
        this.loadingMine.set(false);
      },
      error: () => this.loadingMine.set(false),
    });
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

  private loadKpis(): void {
    this.dashSvc.getKpis().subscribe({
      next: r => { this.kpis.set(r.data); this.loadingKpis.set(false); },
      error: () => this.loadingKpis.set(false),
    });
  }

  private loadStatusBreakdown(): void {
    this.dashSvc.getStatusBreakdown().subscribe({
      next: r => {
        const data = r.data ?? [];
        this.statusBreakdown.set(data);
        this.buildChart(data);
      },
      error: () => {},
    });
  }

  // ── Chart builder ─────────────────────────────────────
  private buildChart(breakdown: StatusBreakdown[]): void {
    this.hBarData = {
      labels: breakdown.map(s => s.status),
      datasets: [{
        data:            breakdown.map(s => s.count),
        backgroundColor: breakdown.map(s =>
          STATUS_COLORS[s.status.replace(/\s/g, '')] ?? '#94a3b8'),
        borderRadius: 4,
        barThickness: 14,
      }],
    };
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
        this.loadMyActions();
        this.loadRecentActions();
        this.loadKpis();
        this.loadStatusBreakdown();
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
