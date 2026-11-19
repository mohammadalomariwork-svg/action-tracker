import { ChangeDetectionStrategy, Component, NgZone, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { ChartData } from 'chart.js';
import { BaseChartDirective, provideCharts, withDefaultRegisterables } from 'ng2-charts';

import { DashboardAnalyticsService } from '../../../core/services/dashboard-analytics.service';
import {
  StrategyDashboard, StrategyKpiRow, StrategyObjectiveRow, StrategyPlanRow,
} from '../../../core/models/dashboard-analytics.model';
import { ThemeService } from '../../../services/theme.service';

import { BreadcrumbComponent } from '../../../shared/components/breadcrumb/breadcrumb.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { DashTabsComponent } from '../shared/dash-tabs.component';
import { DashStatComponent } from '../shared/dash-stat.component';
import { DashPanelComponent } from '../shared/dash-panel.component';
import { DashLegendComponent, LegendItem } from '../shared/dash-legend.component';
import { RankedBarsComponent, RankedItem } from '../shared/ranked-bars.component';
import { DrilldownPanelComponent } from '../shared/drilldown-panel.component';
import { DrillRow, DrillTone, DrilldownRequest } from '../shared/drilldown.model';
import { HEALTH_COLOR, TONE, BRAND, doughnutOptions, pct } from '../shared/chart-theme';
import { healthTone, projectDrillRow } from '../projects-dashboard/projects-dashboard.component';

const KPI_HEALTH_ORDER = ['OnTrack', 'AtRisk', 'OffTrack', 'NoData'];
const KPI_HEALTH_LABEL: Record<string, string> = {
  OnTrack: 'On track (90%+)', AtRisk: 'At risk (60–89%)', OffTrack: 'Off track (under 60%)', NoData: 'No data yet',
};
/** Keyed by StrategicPlanStatus value. */
const PLAN_STATUS_COLOR: Record<string, string> = { '0': TONE.neutral, '1': TONE.warn, '2': TONE.good };

function kpiTone(health: string): DrillTone {
  switch (health) {
    case 'OnTrack':  return 'good';
    case 'AtRisk':   return 'warn';
    case 'OffTrack': return 'bad';
    default:         return 'neutral';
  }
}

/** Health band for an achievement figure — same thresholds as the server. */
function bandOf(achievement: number | null): string {
  if (achievement == null) return 'NoData';
  return achievement >= 90 ? 'OnTrack' : achievement >= 60 ? 'AtRisk' : 'OffTrack';
}

/**
 * Strategy execution for a chosen year: how KPIs are performing against their
 * targets, rolled up to objectives and plans, alongside the strategic
 * projects that deliver them.
 */
@Component({
  selector: 'app-strategy-dashboard',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    BaseChartDirective, FormsModule, RouterLink, BreadcrumbComponent, PageHeaderComponent, DashTabsComponent,
    DashStatComponent, DashPanelComponent, DashLegendComponent, RankedBarsComponent, DrilldownPanelComponent,
  ],
  // Chart.js is tree-shakeable: without registering its controllers and
  // scales <canvas baseChart> throws and renders blank.
  providers: [provideCharts(withDefaultRegisterables())],
  templateUrl: './strategy-dashboard.component.html',
  styleUrl: './strategy-dashboard.component.scss',
})
export class StrategyDashboardComponent implements OnInit {
  private readonly analytics = inject(DashboardAnalyticsService);
  private readonly zone      = inject(NgZone);
  private readonly router    = inject(Router);
  private readonly theme     = toSignal(inject(ThemeService).currentTheme$, { initialValue: 'dark' as const });

  private readonly thisYear = new Date().getFullYear();
  readonly years = [this.thisYear - 2, this.thisYear - 1, this.thisYear, this.thisYear + 1];
  readonly year  = signal(this.thisYear);

  readonly data    = signal<StrategyDashboard | null>(null);
  readonly loading = signal(true);
  readonly failed  = signal(false);
  readonly drill   = signal<DrilldownRequest | null>(null);

  readonly pct = pct;
  readonly kpiTone = kpiTone;
  readonly healthTone = healthTone;
  readonly tableLimit = 8;

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.failed.set(false);
    this.analytics.getStrategy(this.year()).subscribe({
      next: d => { this.data.set(d); this.loading.set(false); },
      error: () => { this.loading.set(false); this.failed.set(true); },
    });
  }

  onYearChange(value: number | string): void {
    this.year.set(+value);
    this.load();
  }

  readonly kpis       = computed(() => this.data()?.kpiRows ?? []);
  readonly objectives = computed(() => this.data()?.objectiveRows ?? []);
  readonly plans      = computed(() => this.data()?.planRows ?? []);
  readonly projects   = computed(() => this.data()?.projectRows ?? []);

  // ── KPI health ─────────────────────────────────────────────────────────────
  readonly kpiHealthLegend = computed<LegendItem[]>(() =>
    KPI_HEALTH_ORDER.map(key => ({
      key, label: KPI_HEALTH_LABEL[key], color: HEALTH_COLOR[key],
      count: this.kpis().filter(k => k.health === key).length,
    })));

  readonly kpiHealthChart = computed<ChartData<'doughnut'>>(() => {
    const items = this.kpiHealthLegend();
    return {
      labels: items.map(i => i.label),
      datasets: [{ data: items.map(i => i.count), backgroundColor: items.map(i => i.color), borderWidth: 0, spacing: 2, hoverOffset: 6 }],
    };
  });

  readonly kpiHealthOptions = computed(() => {
    this.theme();
    return doughnutOptions(i => this.zone.run(() => this.openKpiHealth(this.kpiHealthLegend()[i]?.key)));
  });

  // ── Plans by status ────────────────────────────────────────────────────────
  readonly planLegend = computed<LegendItem[]>(() => {
    const map = new Map<string, LegendItem>();
    for (const p of this.plans()) {
      const key = String(p.status);
      const item = map.get(key) ?? { key, label: p.statusLabel, count: 0, color: PLAN_STATUS_COLOR[key] ?? BRAND.blue };
      item.count++;
      map.set(key, item);
    }
    return [...map.values()].sort((a, b) => +b.key - +a.key);
  });

  // ── Objectives: KPI health per objective ───────────────────────────────────
  readonly objectiveItems = computed<RankedItem[]>(() =>
    this.objectives().map(o => ({
      id: o.id,
      label: `${o.code} · ${o.statement}`,
      sublabel: o.orgUnitName,
      total: Math.max(o.kpiCount, 1),   // keeps a bar track for objectives with no KPIs yet
      segments: [
        { value: o.kpisOnTrack,  tone: 'good',    label: 'On track' },
        { value: o.kpisAtRisk,   tone: 'warn',    label: 'At risk' },
        { value: o.kpisOffTrack, tone: 'bad',     label: 'Off track' },
        { value: o.kpisNoData,   tone: 'neutral', label: 'No data yet' },
      ],
      trailing: pct(o.avgAchievement),
    })));

  // ── Drill-down rows ────────────────────────────────────────────────────────
  private kpiRow(k: StrategyKpiRow): DrillRow {
    return {
      code: `${k.objectiveCode} · KPI ${k.kpiNumber}`,
      title: k.name,
      subtitle: [k.orgUnitName, k.periodLabel, k.unit].filter(Boolean).join(' · '),
      badge: k.healthLabel,
      badgeTone: kpiTone(k.health),
      meta: k.achievement == null
        ? (k.targetCount ? 'No approved actuals' : 'No targets set')
        : `${pct(k.achievement)} of target`,
      metaTone: kpiTone(k.health),
      progress: k.achievement == null ? null : Math.min(100, Math.max(0, k.achievement)),
      link: ['/strategy', 'objectives', k.objectiveId],
    };
  }

  private objectiveRow(o: StrategyObjectiveRow): DrillRow {
    const band = bandOf(o.avgAchievement);
    return {
      code: o.code,
      title: o.statement,
      subtitle: `${o.planName} · ${o.orgUnitName} · ${o.kpiCount} KPIs · ${o.projectCount} projects`,
      badge: o.avgAchievement == null ? 'No data yet' : pct(o.avgAchievement),
      badgeTone: kpiTone(band),
      progress: o.avgAchievement == null ? null : Math.min(100, Math.max(0, o.avgAchievement)),
      link: ['/strategy', 'objectives', o.id],
    };
  }

  private planRow(p: StrategyPlanRow): DrillRow {
    return {
      title: p.name,
      subtitle: `${p.orgUnitName} · ${p.startYear}–${p.endYear}`,
      badge: p.statusLabel,
      badgeTone: this.planTone(p.status),
      meta: `${p.objectiveCount} objectives · ${p.kpiCount} KPIs`,
      link: ['/strategy', 'org-units', p.orgUnitId],
    };
  }

  planTone(status: number): DrillTone {
    return status === 2 ? 'good' : status === 1 ? 'warn' : 'neutral';
  }

  // ── Drill-downs ────────────────────────────────────────────────────────────
  openPlans(key?: string): void {
    const rows = key == null ? this.plans() : this.plans().filter(p => String(p.status) === key);
    const label = key == null ? 'Strategic plans' : `${rows[0]?.statusLabel ?? ''} plans`;
    this.drill.set({ kind: 'rows', title: label, rows: rows.map(p => this.planRow(p)), emptyText: 'No plans match.' });
  }

  openObjectives(): void {
    this.drill.set({ kind: 'rows', title: 'Strategic objectives', subtitle: `KPI achievement in ${this.year()}`,
      rows: this.objectives().map(o => this.objectiveRow(o)), emptyText: 'No objectives yet.' });
  }

  openKpis(): void {
    this.drill.set({ kind: 'rows', title: 'KPIs', subtitle: `Weakest first · ${this.year()}`,
      rows: this.kpis().map(k => this.kpiRow(k)), emptyText: 'No KPIs yet.' });
  }

  openKpisWithData(): void {
    this.drill.set({ kind: 'rows', title: 'KPIs with approved actuals', subtitle: `Weakest first · ${this.year()}`,
      rows: this.kpis().filter(k => k.achievement != null).map(k => this.kpiRow(k)),
      emptyText: 'No KPI has approved actuals for this year yet.' });
  }

  openKpiHealth(key: string | undefined): void {
    if (!key) return;
    this.drill.set({ kind: 'rows', title: KPI_HEALTH_LABEL[key] ?? key, subtitle: `KPIs · ${this.year()}`,
      rows: this.kpis().filter(k => k.health === key).map(k => this.kpiRow(k)), emptyText: 'No KPIs in this band.' });
  }

  openObjective(item: RankedItem): void {
    const objective = this.objectives().find(o => o.id === item.id);
    if (!objective) return;
    const rows = this.kpis().filter(k => k.objectiveId === objective.id).map(k => this.kpiRow(k));
    // Lead with the objective itself so its page is one click away
    this.drill.set({ kind: 'rows', title: objective.code, subtitle: objective.statement,
      rows: [{ ...this.objectiveRow(objective), title: 'Open this objective', subtitle: objective.statement }, ...rows] });
  }

  openProjects(): void {
    this.drill.set({ kind: 'rows', title: 'Strategic projects', subtitle: 'Linked to the objectives in scope',
      rows: this.projects().map(projectDrillRow), emptyText: 'No strategic projects are linked yet.' });
  }

  goToApprovals(): void {
    this.router.navigate(['/approvals']);
  }

  goToObjective(k: StrategyKpiRow): void {
    this.router.navigate(['/strategy', 'objectives', k.objectiveId]);
  }

  goToPlan(p: StrategyPlanRow): void {
    this.router.navigate(['/strategy', 'org-units', p.orgUnitId]);
  }

  goToProject(id: string): void {
    this.router.navigate(['/projects', id]);
  }

  barWidth(achievement: number | null): number {
    return achievement == null ? 0 : Math.min(100, Math.max(0, achievement));
  }
}
