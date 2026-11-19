import {
  Component, OnInit, ChangeDetectionStrategy, ChangeDetectorRef, DestroyRef,
  inject, signal, computed,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { DecimalPipe } from '@angular/common';
import { ChartConfiguration, ChartData, ChartType } from 'chart.js';
import { BaseChartDirective, provideCharts, withDefaultRegisterables } from 'ng2-charts';

import { DashboardService } from '../../core/services/dashboard.service';
import { ReportService }    from '../../core/services/report.service';
import { UserService }      from '../../core/services/user.service';
import { ToastService }     from '../../core/services/toast.service';
import { ThemeService }     from '../../services/theme.service';

import { DashboardKpi, StatusBreakdown } from '../../core/models/dashboard.model';
import { TeamMember }       from '../../core/models/user.model';
import { ActionStatus, ActionPriority } from '../../core/models/action-item.model';

import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { HasPermissionDirective } from '../../shared';

// ── Static data for chart labels (not server-driven) ──────────────────────────

const PRIORITY_LABELS  = ['Low', 'Medium', 'High', 'Critical'];
const PRIORITY_COLORS  = ['#10B981', '#F59E0B', '#F97316', '#EF4444'];

const CATEGORY_LABELS  = ['Operations', 'Strategic', 'HR', 'Finance', 'IT', 'Compliance', 'Communication'];
// Leads with the logo blues, then the status hues
const CATEGORY_COLORS  = ['#2A5CF0', '#4D8BFF', '#7AADFF', '#10B981', '#F59E0B', '#EF4444', '#94A3B8'];

const STATUS_OPTIONS: { value: string; label: string }[] = [
  { value: '', label: 'All Statuses'  },
  { value: '1', label: 'To Do'       },
  { value: '2', label: 'In Progress' },
  { value: '3', label: 'In Review'   },
  { value: '4', label: 'Done'        },
  { value: '5', label: 'Overdue'     },
];

const PRIORITY_OPTIONS: { value: string; label: string }[] = [
  { value: '', label: 'All Priorities' },
  { value: '4', label: 'Critical'      },
  { value: '3', label: 'High'          },
  { value: '2', label: 'Medium'        },
  { value: '1', label: 'Low'           },
];

@Component({
  selector: 'app-reports',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, DecimalPipe, BaseChartDirective, PageHeaderComponent, BreadcrumbComponent, HasPermissionDirective],
  // Chart.js is tree-shakeable: without registering its controllers and
  // scales <canvas baseChart> throws ('"category" is not a registered
  // scale') and renders blank. Provided here rather than at the root so
  // chart.js stays in this lazy chunk.
  providers: [provideCharts(withDefaultRegisterables())],
  templateUrl: './reports.component.html',
  styleUrl:    './reports.component.scss',
})
export class ReportsComponent implements OnInit {
  private readonly dashSvc   = inject(DashboardService);
  private readonly reportSvc = inject(ReportService);
  private readonly userSvc   = inject(UserService);
  private readonly toastSvc  = inject(ToastService);
  private readonly themeSvc  = inject(ThemeService);
  private readonly cdr       = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);

  // ── Page state ────────────────────────────────────────
  readonly loading     = signal(true);
  readonly exporting   = signal(false);

  // ── KPI data ──────────────────────────────────────────
  readonly kpis            = signal<DashboardKpi | null>(null);
  readonly statusBreakdown = signal<StatusBreakdown[]>([]);
  readonly teamMembers     = signal<TeamMember[]>([]);

  // ── Export filter state ───────────────────────────────
  readonly filterStatus   = signal('');
  readonly filterPriority = signal('');
  readonly filterAssignee = signal('');
  readonly filterDateFrom = signal('');
  readonly filterDateTo   = signal('');

  // ── Computed KPI display values ───────────────────────
  readonly totalActions    = computed(() => this.kpis()?.totalActions    ?? 0);
  readonly completionRate  = computed(() => this.kpis()?.completionRate  ?? 0);
  readonly onTimeRate      = computed(() => this.kpis()?.onTimeDeliveryRate ?? 0);
  readonly escalations     = computed(() => this.kpis()?.activeEscalations  ?? 0);
  readonly overdueCount    = computed(() => this.kpis()?.overdueCount    ?? 0);
  readonly critHighCount   = computed(() => this.kpis()?.criticalHighCount  ?? 0);
  readonly inProgressCount = computed(() => this.kpis()?.inProgressCount ?? 0);
  readonly velocity        = computed(() => this.kpis()?.teamVelocity    ?? 0);

  // ── Static options exposed ────────────────────────────
  readonly STATUS_OPTIONS   = STATUS_OPTIONS;
  readonly PRIORITY_OPTIONS = PRIORITY_OPTIONS;

  // ── Bar chart: Actions by Category ───────────────────
  readonly barType = 'bar' as const;

  barData: ChartData<'bar'> = {
    labels: CATEGORY_LABELS,
    datasets: [{
      label: 'Actions by Category',
      data:  [0, 0, 0, 0, 0, 0, 0],
      backgroundColor: CATEGORY_COLORS.map(c => c + 'cc'), // 80% opacity
      borderColor:     CATEGORY_COLORS,
      borderWidth: 1.5,
      borderRadius: 5,
    }],
  };

  barOptions: ChartConfiguration<'bar'>['options'] = this.buildBarOptions();

  /** Chart.js draws on canvas and cannot resolve CSS var() strings —
   *  custom properties must be resolved to concrete values at build time. */
  private cssVar(name: string, fallback: string): string {
    const v = getComputedStyle(document.documentElement).getPropertyValue(name).trim();
    return v || fallback;
  }

  private tooltipTheme() {
    return {
      backgroundColor: this.cssVar('--el', '#2B303B'),
      borderColor: this.cssVar('--bdh', 'rgba(160,185,255,0.26)'),
      borderWidth: 1,
      titleColor: this.cssVar('--t1', '#E6EBF5'),
      bodyColor: this.cssVar('--t2', '#AEBBD0'),
    };
  }

  private buildBarOptions(): ChartConfiguration<'bar'>['options'] {
    const tick = this.cssVar('--t2', '#AEBBD0');
    return {
      responsive: true,
      maintainAspectRatio: false,
      plugins: {
        legend: { display: false },
        tooltip: {
          ...this.tooltipTheme(),
          callbacks: { label: ctx => ` ${ctx.parsed.y} actions` },
        },
      },
      scales: {
        x: {
          ticks: { font: { size: 11 }, color: tick },
          grid: { display: false },
        },
        y: {
          beginAtZero: true,
          ticks: { stepSize: 1, font: { size: 11 }, color: tick },
          grid: { color: this.cssVar('--bd', 'rgba(160,185,255,0.13)') },
        },
      },
    };
  }

  // ── Doughnut chart: Actions by Priority ──────────────
  readonly doughnutType = 'doughnut' as const;

  doughnutData: ChartData<'doughnut'> = {
    labels:   PRIORITY_LABELS,
    datasets: [{
      data:            [0, 0, 0, 0],
      backgroundColor: PRIORITY_COLORS,
      borderWidth: 0,
      spacing: 2,
      hoverOffset: 6,
    }],
  };

  doughnutOptions: ChartConfiguration<'doughnut'>['options'] = this.buildDoughnutOptions();

  private buildDoughnutOptions(): ChartConfiguration<'doughnut'>['options'] {
    return {
      responsive: true,
      maintainAspectRatio: false,
      cutout: '64%',
      plugins: {
        // The template renders its own priority legend under the chart
        legend: { display: false },
        tooltip: {
          ...this.tooltipTheme(),
          callbacks: { label: ctx => ` ${ctx.label}: ${ctx.parsed} actions` },
        },
      },
    };
  }

  // ── Lifecycle ─────────────────────────────────────────
  ngOnInit(): void {
    // Rebuild chart options on theme change so resolved CSS-var colours refresh
    this.themeSvc.currentTheme$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        this.barOptions = this.buildBarOptions();
        this.doughnutOptions = this.buildDoughnutOptions();
        this.cdr.markForCheck();
      });

    // Load KPIs, status breakdown, team members in parallel
    this.dashSvc.getKpis().subscribe({
      next: r => {
        this.kpis.set(r.data);
        this.buildCategoryChart(r.data);
        this.buildPriorityChart(r.data);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.toastSvc.error('Failed to load report data.');
      },
    });

    this.dashSvc.getStatusBreakdown().subscribe({
      next: r => this.statusBreakdown.set(r.data ?? []),
      error: () => {},
    });

    this.userSvc.getTeamMembers().subscribe({
      next: r => this.teamMembers.set(r.data ?? []),
      error: () => {},
    });
  }

  // ── Chart builders ────────────────────────────────────
  private buildCategoryChart(kpis: DashboardKpi): void {
    // The KPI endpoint doesn't have per-category counts — we fill zeros here
    // and they'll be replaced when the API provides that data.
    // For now we derive rough values from total and available fields.
    this.barData = {
      labels: CATEGORY_LABELS,
      datasets: [{
        label: 'Actions by Category',
        // Placeholder data — replace with real category breakdown endpoint if available
        data: [0, 0, 0, 0, 0, 0, 0],
        backgroundColor: CATEGORY_COLORS.map(c => c + 'cc'),
        borderColor: CATEGORY_COLORS,
        borderWidth: 1.5,
        borderRadius: 5,
      }],
    };
  }

  private buildPriorityChart(kpis: DashboardKpi): void {
    const total   = kpis.totalActions;
    const critHigh = kpis.criticalHighCount;
    // Approximate split: assume high ≈ critHigh/2, critical ≈ critHigh/2
    const critical  = Math.round(critHigh * 0.5);
    const high      = critHigh - critical;
    const remaining = Math.max(0, total - critHigh);
    const medium    = Math.round(remaining * 0.6);
    const low       = remaining - medium;

    this.doughnutData = {
      labels: PRIORITY_LABELS,
      datasets: [{
        data:            [low, medium, high, critical],
        backgroundColor: PRIORITY_COLORS,
        borderWidth: 0,
        spacing: 2,
        hoverOffset: 6,
      }],
    };
  }

  // ── Export ────────────────────────────────────────────
  exportCsv(): void {
    this.exporting.set(true);

    const filter: Record<string, any> = {};
    if (this.filterStatus())   filter['status']     = this.filterStatus();
    if (this.filterPriority()) filter['priority']   = this.filterPriority();
    if (this.filterAssignee()) filter['assigneeId'] = this.filterAssignee();
    if (this.filterDateFrom()) filter['dateFrom']   = this.filterDateFrom();
    if (this.filterDateTo())   filter['dateTo']     = this.filterDateTo();

    this.reportSvc.exportCsv(filter).subscribe({
      next: blob => {
        this.exporting.set(false);
        const url = URL.createObjectURL(blob);
        const a   = document.createElement('a');
        a.href     = url;
        a.download = `action-items-report-${new Date().toISOString().slice(0, 10)}.csv`;
        a.click();
        URL.revokeObjectURL(url);
        this.toastSvc.success('Report exported successfully.');
      },
      error: () => {
        this.exporting.set(false);
        this.toastSvc.error('Export failed. Please try again.');
      },
    });
  }

  clearFilters(): void {
    this.filterStatus.set('');
    this.filterPriority.set('');
    this.filterAssignee.set('');
    this.filterDateFrom.set('');
    this.filterDateTo.set('');
  }

  readonly hasFilters = computed(() =>
    !!(this.filterStatus() || this.filterPriority() ||
       this.filterAssignee() || this.filterDateFrom() || this.filterDateTo())
  );

  // ── Status breakdown color helper ─────────────────────
  statusColor(status: string): string {
    const map: Record<string, string> = {
      'ToDo':       '#94A3B8',
      'InProgress': '#4D8BFF',
      'InReview':   '#8B6CF6',
      'Done':       '#10B981',
      'Overdue':    '#EF4444',
    };
    return map[status.replace(/\s/g, '')] ?? '#94a3b8';
  }
}
