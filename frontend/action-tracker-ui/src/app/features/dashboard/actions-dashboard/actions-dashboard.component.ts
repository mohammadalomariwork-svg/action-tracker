import { ChangeDetectionStrategy, Component, NgZone, OnInit, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ChartData } from 'chart.js';
import { BaseChartDirective, provideCharts, withDefaultRegisterables } from 'ng2-charts';

import { DashboardAnalyticsService } from '../../../core/services/dashboard-analytics.service';
import { ActionItemsDashboard, ChartSlice } from '../../../core/models/dashboard-analytics.model';
import { ActionStatus } from '../../../core/models/action-item.model';
import { ThemeService } from '../../../services/theme.service';

import { BreadcrumbComponent } from '../../../shared/components/breadcrumb/breadcrumb.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { DashTabsComponent } from '../shared/dash-tabs.component';
import { DashStatComponent } from '../shared/dash-stat.component';
import { DashPanelComponent } from '../shared/dash-panel.component';
import { DashLegendComponent, LegendItem } from '../shared/dash-legend.component';
import { RankedBarsComponent, RankedItem } from '../shared/ranked-bars.component';
import { DrilldownPanelComponent } from '../shared/drilldown-panel.component';
import { DrilldownRequest } from '../shared/drilldown.model';
import {
  AGE_COLOR, BRAND, DUE_COLOR, PRIORITY_COLOR, STATUS_COLOR, TONE,
  barOptions, doughnutOptions, lineOptions, pct,
} from '../shared/chart-theme';

/**
 * Action-item performance across everything the user can see: how much is
 * open, late or escalated, how it is trending, and where the load sits.
 * Every figure and chart segment opens the list of items behind it.
 */
@Component({
  selector: 'app-actions-dashboard',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    BaseChartDirective, BreadcrumbComponent, PageHeaderComponent, DashTabsComponent, DashStatComponent,
    DashPanelComponent, DashLegendComponent, RankedBarsComponent, DrilldownPanelComponent,
  ],
  // Chart.js is tree-shakeable: without registering its controllers and
  // scales <canvas baseChart> throws and renders blank.
  providers: [provideCharts(withDefaultRegisterables())],
  templateUrl: './actions-dashboard.component.html',
  styleUrl: './actions-dashboard.component.scss',
})
export class ActionsDashboardComponent implements OnInit {
  private readonly analytics = inject(DashboardAnalyticsService);
  private readonly zone      = inject(NgZone);
  private readonly theme     = toSignal(inject(ThemeService).currentTheme$, { initialValue: 'dark' as const });

  readonly data    = signal<ActionItemsDashboard | null>(null);
  readonly loading = signal(true);
  readonly failed  = signal(false);
  readonly drill   = signal<DrilldownRequest | null>(null);

  readonly pct = pct;

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.failed.set(false);
    this.analytics.getActionItems().subscribe({
      next: d => { this.data.set(d); this.loading.set(false); },
      error: () => { this.loading.set(false); this.failed.set(true); },
    });
  }

  // ── Legends (also the keyboard route into each chart) ──────────────────────
  private legend(slices: ChartSlice[] | undefined, colors: Record<string, string>): LegendItem[] {
    return (slices ?? []).map(s => ({ ...s, color: colors[s.key] ?? TONE.neutral }));
  }

  readonly statusLegend   = computed(() => this.legend(this.data()?.byStatus, STATUS_COLOR));
  readonly priorityLegend = computed(() => this.legend(this.data()?.byPriority, PRIORITY_COLOR));
  readonly dueLegend      = computed(() => this.legend(this.data()?.byDue, DUE_COLOR));
  readonly ageLegend      = computed(() => this.legend(this.data()?.byAge, AGE_COLOR));

  // ── Chart data ─────────────────────────────────────────────────────────────
  readonly statusChart = computed<ChartData<'doughnut'>>(() => {
    const items = this.statusLegend();
    return {
      labels: items.map(i => i.label),
      datasets: [{ data: items.map(i => i.count), backgroundColor: items.map(i => i.color), borderWidth: 0, spacing: 2, hoverOffset: 6 }],
    };
  });

  private bars(items: LegendItem[]): ChartData<'bar'> {
    return {
      labels: items.map(i => i.label),
      datasets: [{ data: items.map(i => i.count), backgroundColor: items.map(i => i.color), borderRadius: 6, maxBarThickness: 46 }],
    };
  }

  readonly priorityChart = computed(() => this.bars(this.priorityLegend()));
  readonly dueChart      = computed(() => this.bars(this.dueLegend()));
  readonly ageChart      = computed(() => this.bars(this.ageLegend()));

  readonly trendChart = computed<ChartData<'line'>>(() => {
    const trend = this.data()?.trend ?? [];
    return {
      labels: trend.map(t => t.label),
      datasets: [
        { label: 'Created', data: trend.map(t => t.created), borderColor: BRAND.blue, backgroundColor: 'rgba(77,139,255,0.14)',
          fill: true, tension: 0.35, pointRadius: 3, pointBackgroundColor: BRAND.blue },
        { label: 'Completed', data: trend.map(t => t.completed), borderColor: TONE.good, backgroundColor: 'rgba(16,185,129,0.10)',
          fill: true, tension: 0.35, pointRadius: 3, pointBackgroundColor: TONE.good },
      ],
    };
  });

  // ── Chart options (rebuilt on theme change so resolved colours refresh) ────
  /** Chart.js fires callbacks outside Angular's zone. */
  private inZone(fn: () => void): void {
    this.zone.run(fn);
  }

  readonly statusOptions   = computed(() => { this.theme(); return doughnutOptions(i => this.inZone(() => this.openStatus(this.statusLegend()[i]?.key))); });
  readonly priorityOptions = computed(() => { this.theme(); return barOptions(i => this.inZone(() => this.openPriority(this.priorityLegend()[i]?.key)), { horizontal: true }); });
  readonly dueOptions      = computed(() => { this.theme(); return barOptions(i => this.inZone(() => this.openDue(this.dueLegend()[i]?.key))); });
  readonly ageOptions      = computed(() => { this.theme(); return barOptions(i => this.inZone(() => this.openAge(this.ageLegend()[i]?.key))); });
  readonly trendOptions    = computed(() => { this.theme(); return lineOptions(); });

  // ── Ranked lists ───────────────────────────────────────────────────────────
  private ranked(rows: { id: string; label: string; total: number; open: number; completed: number; overdue: number }[]): RankedItem[] {
    return rows.map(r => ({
      id: r.id,
      label: r.label,
      total: r.total,
      segments: [
        { value: r.completed, tone: 'good', label: 'Completed' },
        // "open" includes the overdue ones — show them separately
        { value: Math.max(0, r.open - r.overdue), tone: 'brand', label: 'Open' },
        { value: r.overdue, tone: 'bad', label: 'Overdue' },
      ],
    }));
  }

  readonly teamItems = computed(() =>
    this.ranked((this.data()?.team ?? []).map(m => ({ id: m.userId, label: m.userName, ...m }))));

  readonly workspaceItems = computed(() =>
    this.ranked((this.data()?.byWorkspace ?? []).map(w => ({ id: w.workspaceId, label: w.title, ...w }))));

  // ── Drill-downs ────────────────────────────────────────────────────────────
  private labelOf(items: LegendItem[], key: string): string {
    return items.find(i => i.key === key)?.label ?? key;
  }

  openAll(): void {
    this.drill.set({ kind: 'actions', scope: 'all', title: 'All action items', filter: {} });
  }

  openOpen(): void {
    this.drill.set({ kind: 'actions', scope: 'all', title: 'Open action items',
      subtitle: 'To do, in progress, in review or overdue', filter: { openOnly: true } });
  }

  openCompleted(): void {
    this.drill.set({ kind: 'actions', scope: 'all', title: 'Completed action items', filter: { status: ActionStatus.Done } });
  }

  openOverdue(): void {
    this.drill.set({ kind: 'actions', scope: 'all', title: 'Overdue action items',
      subtitle: 'Open and past their due date', filter: { overdueOnly: true } });
  }

  openEscalated(): void {
    this.drill.set({ kind: 'actions', scope: 'all', title: 'Escalated action items', filter: { isEscalated: true } });
  }

  openStatus(key: string | undefined): void {
    if (!key) return;
    this.drill.set({ kind: 'actions', scope: 'all', title: `${this.labelOf(this.statusLegend(), key)} action items`,
      filter: { status: +key as ActionStatus } });
  }

  openPriority(key: string | undefined): void {
    if (!key) return;
    this.drill.set({ kind: 'actions', scope: 'all', title: `${this.labelOf(this.priorityLegend(), key)} priority`,
      subtitle: 'Open items only', filter: { priority: +key, openOnly: true } });
  }

  openDue(key: string | undefined): void {
    if (!key) return;
    this.drill.set({ kind: 'actions', scope: 'all', title: this.labelOf(this.dueLegend(), key),
      subtitle: 'Open items by due date', filter: { dueBucket: key } });
  }

  openAge(key: string | undefined): void {
    if (!key) return;
    this.drill.set({ kind: 'actions', scope: 'all', title: `Open for ${this.labelOf(this.ageLegend(), key).toLowerCase()}`,
      subtitle: 'Open items by time since they were created', filter: { ageBucket: key } });
  }

  openMember(item: RankedItem): void {
    this.drill.set({ kind: 'actions', scope: 'all', title: item.label,
      subtitle: `${item.total} action items assigned`, filter: { assigneeId: item.id } });
  }

  openWorkspace(item: RankedItem): void {
    this.drill.set({ kind: 'actions', scope: 'all', title: item.label,
      subtitle: `${item.total} action items in this workspace`, filter: { workspaceId: item.id } });
  }
}
