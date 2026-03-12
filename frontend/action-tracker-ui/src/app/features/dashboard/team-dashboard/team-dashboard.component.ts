import {
  Component, OnInit, ChangeDetectionStrategy, ChangeDetectorRef,
  inject, signal, computed, effect, untracked,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
import { DatePipe, DecimalPipe } from '@angular/common';
import { ChartConfiguration, ChartData, ChartType } from 'chart.js';
import { BaseChartDirective } from 'ng2-charts';

import { ActionItemService } from '../../../core/services/action-item.service';
import { DashboardService }  from '../../../core/services/dashboard.service';
import { AuthService }       from '../../../core/services/auth.service';
import { WorkspaceService }  from '../../workspaces/services/workspace.service';

import {
  ActionItem, ActionItemFilter,
  ActionStatus, ActionPriority,
} from '../../../core/models/action-item.model';
import { DashboardKpi, StatusBreakdown } from '../../../core/models/dashboard.model';
import { AuthUser }          from '../../../core/models/auth.models';
import { WorkspaceSummary }  from '../../workspaces/models/workspace.model';

import { StatusBadgeComponent }   from '../../../shared/components/status-badge/status-badge.component';
import { PriorityBadgeComponent } from '../../../shared/components/priority-badge/priority-badge.component';
import { BreadcrumbComponent }    from '../../../shared/components/breadcrumb/breadcrumb.component';
import { PageHeaderComponent }    from '../../../shared/components/page-header/page-header.component';

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
  imports: [RouterLink, DatePipe, DecimalPipe, BaseChartDirective, StatusBadgeComponent, PriorityBadgeComponent, BreadcrumbComponent, PageHeaderComponent],
  templateUrl: './team-dashboard.component.html',
  styleUrl:    './team-dashboard.component.scss',
})
export class TeamDashboardComponent implements OnInit {
  private readonly actionSvc     = inject(ActionItemService);
  private readonly dashSvc       = inject(DashboardService);
  private readonly authSvc       = inject(AuthService);
  private readonly workspaceSvc  = inject(WorkspaceService);
  private readonly cdr           = inject(ChangeDetectorRef);
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

  // ── Project pie charts ────────────────────────────────
  readonly doughnutType = 'doughnut' as const;
  projectCompletionPieData: ChartData<'doughnut'> = {
    labels: ['Completed', 'Remaining'],
    datasets: [{ data: [0, 100], backgroundColor: ['#059669', '#e2e8f0'], borderWidth: 0, hoverOffset: 4 }],
  };
  projectDeliveryPieData: ChartData<'doughnut'> = {
    labels: ['On-Time', 'Delayed'],
    datasets: [{ data: [0, 100], backgroundColor: ['#0284c7', '#e2e8f0'], borderWidth: 0, hoverOffset: 4 }],
  };

  readonly pieOptions: ChartConfiguration<'doughnut'>['options'] = {
    responsive: false,   // fixed canvas size via width/height attrs; avoids 0-dim on OnPush init
    cutout: '68%',
    plugins: {
      legend: { display: false },
      tooltip: { callbacks: { label: ctx => ` ${ctx.label}: ${ctx.parsed}%` } },
    },
  };

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
        callbacks: { label: ctx => ` ${ctx.parsed.x} actions` },
      },
    },
    scales: {
      x: {
        beginAtZero: true,
        ticks: { stepSize: 1, font: { size: 10 } },
        grid: { color: 'rgba(0,0,0,0.04)' },
      },
      y: {
        ticks: { font: { size: 11 } },
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
  }

  // ── Data loaders ──────────────────────────────────────
  private loadWorkspaceSummary(): void {
    this.loadingWorkspaceSummary.set(true);
    this.workspaceSvc.getSummary().subscribe({
      next: r => {
        this.workspaceSummary.set(r.data);
        this.buildProjectPieCharts(r.data);
        this.loadingWorkspaceSummary.set(false);
      },
      error: () => this.loadingWorkspaceSummary.set(false),
    });
  }

  private buildProjectPieCharts(s: WorkspaceSummary): void {
    const comp  = Math.round(s.projectCompletionRate     ?? 0);
    const deliv = Math.round(s.projectOnTimeDeliveryRate ?? 0);
    this.projectCompletionPieData = {
      labels: ['Completed', 'Remaining'],
      datasets: [{ data: [comp, 100 - comp], backgroundColor: ['#059669', '#e2e8f0'], borderWidth: 0, hoverOffset: 4 }],
    };
    this.projectDeliveryPieData = {
      labels: ['On-Time', 'Delayed'],
      datasets: [{ data: [deliv, 100 - deliv], backgroundColor: ['#0284c7', '#e2e8f0'], borderWidth: 0, hoverOffset: 4 }],
    };
    this.cdr.markForCheck();
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
}
