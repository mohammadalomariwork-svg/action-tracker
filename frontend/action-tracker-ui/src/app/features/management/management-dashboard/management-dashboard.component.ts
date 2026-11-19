import {
  Component, OnInit, OnDestroy, ChangeDetectionStrategy, ChangeDetectorRef,
  inject, signal, computed,
} from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { DatePipe } from '@angular/common';
import { interval, Subject, switchMap, takeUntil } from 'rxjs';
import { ChartConfiguration, ChartData, ChartType } from 'chart.js';
import { BaseChartDirective, provideCharts, withDefaultRegisterables } from 'ng2-charts';

import { DashboardService }       from '../../../core/services/dashboard.service';
import { WorkspaceService }       from '../../workspaces/services/workspace.service';
import { ThemeService }           from '../../../services/theme.service';
import { ManagementDashboard, AtRiskItem, TeamWorkload } from '../../../core/models/dashboard.model';
import { ActionItem, ActionStatus, ActionPriority }      from '../../../core/models/action-item.model';
import { WorkspaceSummary }       from '../../workspaces/models/workspace.model';

import { KpiCardComponent }       from '../../../shared/components/kpi-card/kpi-card.component';
import { StatusBadgeComponent }   from '../../../shared/components/status-badge/status-badge.component';
import { PriorityBadgeComponent } from '../../../shared/components/priority-badge/priority-badge.component';
import { PageHeaderComponent }    from '../../../shared/components/page-header/page-header.component';
import { BreadcrumbComponent }    from '../../../shared/components/breadcrumb/breadcrumb.component';

const REFRESH_INTERVAL_MS = 30_000;

// Status doughnut colours aligned with StatusBadge
const STATUS_COLORS: Record<string, string> = {
  'ToDo':       '#94A3B8',
  'InProgress': '#4D8BFF',
  'InReview':   '#8B6CF6',
  'Done':       '#10B981',
  'Overdue':    '#EF4444',
};

@Component({
  selector: 'app-management-dashboard',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterLink, DatePipe,
    BaseChartDirective,
    KpiCardComponent, StatusBadgeComponent, PriorityBadgeComponent, PageHeaderComponent, BreadcrumbComponent,
  ],
  // Chart.js is tree-shakeable: without registering its controllers and
  // scales <canvas baseChart> throws ('"category" is not a registered
  // scale') and renders blank. Provided here rather than at the root so
  // chart.js stays in this lazy chunk.
  providers: [provideCharts(withDefaultRegisterables())],
  templateUrl: './management-dashboard.component.html',
  styleUrl:    './management-dashboard.component.scss',
})
export class ManagementDashboardComponent implements OnInit, OnDestroy {
  private readonly dashSvc      = inject(DashboardService);
  private readonly workspaceSvc = inject(WorkspaceService);
  private readonly themeSvc     = inject(ThemeService);
  private readonly cdr          = inject(ChangeDetectorRef);
  readonly router               = inject(Router);
  private readonly destroy$     = new Subject<void>();
  private readonly refresh$     = new Subject<void>();

  // ── State ─────────────────────────────────────────────
  readonly dashboard        = signal<ManagementDashboard | null>(null);
  readonly loading          = signal(true);
  readonly lastUpdated      = signal<Date | null>(null);
  readonly workspaceSummary = signal<WorkspaceSummary | null>(null);

  // ── Computed KPIs ─────────────────────────────────────
  readonly kpis = computed(() => this.dashboard()?.kpis);

  readonly completionRate   = computed(() => this.kpis()?.completionRate   ?? 0);
  readonly onTimeRate       = computed(() => this.kpis()?.onTimeDeliveryRate ?? 0);
  readonly escalations      = computed(() => this.kpis()?.activeEscalations  ?? 0);
  readonly velocity         = computed(() => this.kpis()?.teamVelocity       ?? 0);

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

  // ── Enums exposed ─────────────────────────────────────
  readonly ActionStatus   = ActionStatus;
  readonly ActionPriority = ActionPriority;

  // ── Doughnut chart ────────────────────────────────────
  readonly doughnutType = 'doughnut' as const;

  doughnutData: ChartData<'doughnut'> = {
    labels:   [],
    datasets: [{ data: [], backgroundColor: [], borderWidth: 0, spacing: 2, hoverOffset: 6 }],
  };

  doughnutOptions: ChartConfiguration<'doughnut'>['options'] = this.buildDoughnutOptions();

  /** Chart.js draws on canvas and cannot resolve CSS var() strings —
   *  custom properties must be resolved to concrete values at build time. */
  private cssVar(name: string, fallback: string): string {
    const v = getComputedStyle(document.documentElement).getPropertyValue(name).trim();
    return v || fallback;
  }

  private buildDoughnutOptions(): ChartConfiguration<'doughnut'>['options'] {
    return {
      responsive: true,
      maintainAspectRatio: false,
      cutout: '68%',
      plugins: {
        legend: {
          position: 'bottom',
          labels: { padding: 14, font: { size: 12 }, color: this.cssVar('--t2', '#AEBBD0') },
        },
        tooltip: {
          backgroundColor: this.cssVar('--el', '#2B303B'),
          borderColor: this.cssVar('--bdh', 'rgba(160,185,255,0.26)'),
          borderWidth: 1,
          titleColor: this.cssVar('--t1', '#f1f5f9'),
          bodyColor: this.cssVar('--t2', '#94a3b8'),
          callbacks: {
            label: ctx => ` ${ctx.label}: ${ctx.parsed} items`,
          },
        },
      },
    };
  }

  // ── Bar chart ─────────────────────────────────────────
  readonly barType = 'bar' as const;

  barData: ChartData<'bar'> = {
    labels:   [],
    datasets: [
      {
        label: 'Assigned',
        data: [],
        backgroundColor: 'rgba(77, 139, 255, 0.85)',
        borderRadius: 4,
      },
      {
        label: 'Completed',
        data: [],
        backgroundColor: 'rgba(16, 185, 129, 0.85)',
        borderRadius: 4,
      },
    ],
  };

  barOptions: ChartConfiguration<'bar'>['options'] = this.buildBarOptions();

  private buildBarOptions(): ChartConfiguration<'bar'>['options'] {
    const gridColor = this.cssVar('--bd', 'rgba(160,185,255,0.13)');
    return {
      responsive: true,
      maintainAspectRatio: false,
      indexAxis: 'y',
      scales: {
        x: {
          beginAtZero: true,
          ticks: { stepSize: 1, font: { size: 11 }, color: this.cssVar('--t2', '#94a3b8') },
          grid: { color: gridColor },
        },
        y: {
          ticks: { font: { size: 11 }, color: this.cssVar('--t2', '#94a3b8') },
          grid: { display: false },
        },
      },
      plugins: {
        legend: {
          position: 'bottom',
          labels: { padding: 14, font: { size: 12 }, color: this.cssVar('--t2', '#AEBBD0') },
        },
        tooltip: {
          backgroundColor: this.cssVar('--el', '#2B303B'),
          borderColor: this.cssVar('--bdh', 'rgba(160,185,255,0.26)'),
          borderWidth: 1,
          titleColor: this.cssVar('--t1', '#f1f5f9'),
          bodyColor: this.cssVar('--t2', '#94a3b8'),
          callbacks: {
            label: ctx => ` ${ctx.dataset.label}: ${ctx.parsed.x}`,
          },
        },
      },
      onClick: (_event, elements) => {
        if (elements.length) {
          const idx = elements[0].index;
          const workload = this.dashboard()?.teamWorkload ?? [];
          if (workload[idx]) {
            this.router.navigate(['/actions'], {
              queryParams: { assigneeId: workload[idx].userId },
            });
          }
        }
      },
    };
  }

  // ── Computed view helpers ─────────────────────────────
  readonly atRiskItems = computed(() =>
    (this.dashboard()?.atRiskItems ?? []).slice(0, 5)
  );

  readonly recentActivity = computed(() =>
    (this.dashboard()?.recentActivity ?? []).slice(0, 5)
  );

  readonly criticalActions = computed(() =>
    this.dashboard()?.criticalActions ?? []
  );

  readonly totalStatusCount = computed(() =>
    (this.dashboard()?.statusBreakdown ?? []).reduce((s, b) => s + b.count, 0)
  );

  // ── Lifecycle ─────────────────────────────────────────
  ngOnInit(): void {
    // initial + periodic refresh
    this.refresh$.pipe(
      switchMap(() => this.dashSvc.getManagementDashboard()),
      takeUntil(this.destroy$),
    ).subscribe({
      next: r => {
        this.dashboard.set(r.data);
        this.loading.set(false);
        this.lastUpdated.set(new Date());
        this.buildCharts(r.data);
      },
      error: () => { this.loading.set(false); },
    });

    this.refresh$.next();

    interval(REFRESH_INTERVAL_MS).pipe(takeUntil(this.destroy$))
      .subscribe(() => this.refresh$.next());

    // Rebuild chart options on theme change so resolved CSS-var colors refresh
    this.themeSvc.currentTheme$
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        this.doughnutOptions = this.buildDoughnutOptions();
        this.barOptions      = this.buildBarOptions();
        this.cdr.markForCheck();
      });

    this.loadWorkspaceSummary();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private loadWorkspaceSummary(): void {
    this.workspaceSvc.getSummary().subscribe({
      next: r => {
        this.workspaceSummary.set(r.data);
      },
      error: () => {},
    });
  }

  // ── Chart builders ────────────────────────────────────
  private buildCharts(data: ManagementDashboard): void {
    // Doughnut
    const sb = data.statusBreakdown;
    this.doughnutData = {
      labels: sb.map(s => s.status),
      datasets: [{
        data:            sb.map(s => s.count),
        backgroundColor: sb.map(s => STATUS_COLORS[s.status.replace(/\s/g, '')] ?? s.color),
        borderWidth: 0,
        spacing: 2,
        hoverOffset: 6,
      }],
    };

    // Bar
    const wl = data.teamWorkload;
    this.barData = {
      labels: wl.map(w => w.userName),
      datasets: [
        {
          label: 'Assigned',
          data:  wl.map(w => w.assignedCount),
          backgroundColor: 'rgba(77, 139, 255, 0.85)',
          borderRadius: 4,
        },
        {
          label: 'Completed',
          data:  wl.map(w => w.completedCount),
          backgroundColor: 'rgba(16, 185, 129, 0.85)',
          borderRadius: 4,
        },
      ],
    };
  }

  // ── View helpers ──────────────────────────────────────
  severityClass(item: AtRiskItem): string {
    switch (item.severityLevel?.toLowerCase()) {
      case 'critical': return 'risk--critical';
      case 'high':     return 'risk--high';
      default:         return 'risk--medium';
    }
  }

  criticalRowClass(item: ActionItem): string {
    return item.isOverdue ? 'crit-row--overdue' : '';
  }

  avatarInitial(name: string): string {
    return name?.charAt(0).toUpperCase() ?? '?';
  }

  trackById(_: number, item: { id: string }): string { return item.id; }
}
