import { ChangeDetectionStrategy, Component, NgZone, OnInit, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { ChartData } from 'chart.js';
import { BaseChartDirective, provideCharts, withDefaultRegisterables } from 'ng2-charts';

import { DashboardAnalyticsService } from '../../../core/services/dashboard-analytics.service';
import { MilestoneRow, ProjectRow, ProjectsDashboard } from '../../../core/models/dashboard-analytics.model';
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
import {
  BRAND, HEALTH_COLOR, PROJECT_STATUS_COLOR, TONE, barOptions, doughnutOptions, pct,
} from '../shared/chart-theme';

const HEALTH_ORDER = ['OnTrack', 'AtRisk', 'Delayed', 'Completed', 'OnHold', 'NotStarted', 'Cancelled'];
const TYPE_COLOR: Record<string, string> = { '2': BRAND.royal, '1': BRAND.sky };   // Strategic, Operational

const PROGRESS_BANDS = [
  { key: '0-25',   label: '0–25%',   min: 0,     max: 25,    color: TONE.bad },
  { key: '26-50',  label: '26–50%',  min: 25.01, max: 50,    color: TONE.warn },
  { key: '51-75',  label: '51–75%',  min: 50.01, max: 75,    color: BRAND.sky },
  { key: '76-99',  label: '76–99%',  min: 75.01, max: 99.99, color: BRAND.blue },
  { key: '100',    label: '100%',    min: 100,   max: 100,   color: TONE.good },
];

/** How a project's health reads as a colour role. */
export function healthTone(health: string): DrillTone {
  switch (health) {
    case 'OnTrack':   return 'good';
    case 'AtRisk':    return 'warn';
    case 'Delayed':   return 'bad';
    case 'Completed': return 'brand';
    default:          return 'neutral';
  }
}

/** Drill-down row for a project. Shared with the strategy dashboard. */
export function projectDrillRow(p: ProjectRow): DrillRow {
  const end = new Date(p.plannedEndDate).toLocaleDateString(undefined, { day: 'numeric', month: 'short', year: 'numeric' });
  const late = p.health === 'Delayed';
  return {
    code: p.code,
    title: p.name,
    subtitle: [p.workspaceTitle, p.projectManagerName && `PM ${p.projectManagerName}`, p.typeLabel].filter(Boolean).join(' · '),
    badge: p.healthLabel,
    badgeTone: healthTone(p.health),
    meta: late ? `${Math.abs(p.daysToEnd)}d past ${end}` : `Ends ${end}`,
    metaTone: late ? 'bad' : 'neutral',
    progress: p.progress,
    link: ['/projects', p.id],
  };
}

/**
 * Project portfolio: how many projects are on track, at risk or late, how far
 * along they are, and which milestones are slipping. Charts are grouped from
 * the project rows the API returns, so a drill-down lists exactly what a
 * segment counted.
 */
@Component({
  selector: 'app-projects-dashboard',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    BaseChartDirective, DatePipe, RouterLink, BreadcrumbComponent, PageHeaderComponent, DashTabsComponent,
    DashStatComponent, DashPanelComponent, DashLegendComponent, RankedBarsComponent, DrilldownPanelComponent,
  ],
  // Chart.js is tree-shakeable: without registering its controllers and
  // scales <canvas baseChart> throws and renders blank.
  providers: [provideCharts(withDefaultRegisterables())],
  templateUrl: './projects-dashboard.component.html',
  styleUrl: './projects-dashboard.component.scss',
})
export class ProjectsDashboardComponent implements OnInit {
  private readonly analytics = inject(DashboardAnalyticsService);
  private readonly zone      = inject(NgZone);
  private readonly router    = inject(Router);
  private readonly theme     = toSignal(inject(ThemeService).currentTheme$, { initialValue: 'dark' as const });

  readonly data    = signal<ProjectsDashboard | null>(null);
  readonly loading = signal(true);
  readonly failed  = signal(false);
  readonly drill   = signal<DrilldownRequest | null>(null);

  readonly pct = pct;
  readonly healthTone = healthTone;

  /** Rows shown in the table before "View all". */
  readonly tableLimit = 8;

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.failed.set(false);
    this.analytics.getProjects().subscribe({
      next: d => { this.data.set(d); this.loading.set(false); },
      error: () => { this.loading.set(false); this.failed.set(true); },
    });
  }

  readonly projects = computed(() => this.data()?.projects ?? []);
  readonly underWay = computed(() => this.projects().filter(p => p.status === 2 || p.status === 3));

  // ── Groupings ──────────────────────────────────────────────────────────────
  private group(
    rows: ProjectRow[], keyOf: (p: ProjectRow) => string, labelOf: (p: ProjectRow) => string,
    colors: Record<string, string>, order?: string[],
  ): LegendItem[] {
    const map = new Map<string, LegendItem>();
    for (const p of rows) {
      const key = keyOf(p);
      const item = map.get(key) ?? { key, label: labelOf(p), count: 0, color: colors[key] ?? TONE.neutral };
      item.count++;
      map.set(key, item);
    }
    const items = [...map.values()];
    return order
      ? items.sort((a, b) => order.indexOf(a.key) - order.indexOf(b.key))
      : items.sort((a, b) => b.count - a.count);
  }

  readonly healthLegend = computed(() =>
    this.group(this.projects(), p => p.health, p => p.healthLabel, HEALTH_COLOR, HEALTH_ORDER));
  readonly statusLegend = computed(() =>
    this.group(this.projects(), p => String(p.status), p => p.statusLabel, PROJECT_STATUS_COLOR));
  readonly typeLegend = computed(() =>
    this.group(this.projects(), p => String(p.projectType), p => p.typeLabel, TYPE_COLOR));

  readonly progressLegend = computed<LegendItem[]>(() => {
    const rows = this.underWay();
    return PROGRESS_BANDS.map(b => ({
      key: b.key, label: b.label, color: b.color,
      count: rows.filter(p => p.progress >= b.min && p.progress <= b.max).length,
    }));
  });

  // ── Chart data ─────────────────────────────────────────────────────────────
  private doughnut(items: LegendItem[]): ChartData<'doughnut'> {
    return {
      labels: items.map(i => i.label),
      datasets: [{ data: items.map(i => i.count), backgroundColor: items.map(i => i.color), borderWidth: 0, spacing: 2, hoverOffset: 6 }],
    };
  }

  private bars(items: LegendItem[]): ChartData<'bar'> {
    return {
      labels: items.map(i => i.label),
      datasets: [{ data: items.map(i => i.count), backgroundColor: items.map(i => i.color), borderRadius: 6, maxBarThickness: 46 }],
    };
  }

  readonly healthChart   = computed(() => this.doughnut(this.healthLegend()));
  readonly typeChart     = computed(() => this.doughnut(this.typeLegend()));
  readonly statusChart   = computed(() => this.bars(this.statusLegend()));
  readonly progressChart = computed(() => this.bars(this.progressLegend()));

  private inZone(fn: () => void): void {
    this.zone.run(fn);
  }

  readonly healthOptions   = computed(() => { this.theme(); return doughnutOptions(i => this.inZone(() => this.openHealth(this.healthLegend()[i]?.key))); });
  readonly typeOptions     = computed(() => { this.theme(); return doughnutOptions(i => this.inZone(() => this.openType(this.typeLegend()[i]?.key))); });
  readonly statusOptions   = computed(() => { this.theme(); return barOptions(i => this.inZone(() => this.openStatus(this.statusLegend()[i]?.key)), { horizontal: true }); });
  readonly progressOptions = computed(() => { this.theme(); return barOptions(i => this.inZone(() => this.openProgress(this.progressLegend()[i]?.key))); });

  // ── By workspace ───────────────────────────────────────────────────────────
  readonly workspaceItems = computed<RankedItem[]>(() => {
    const map = new Map<string, { label: string; rows: ProjectRow[] }>();
    for (const p of this.projects()) {
      const entry = map.get(p.workspaceId) ?? { label: p.workspaceTitle, rows: [] };
      entry.rows.push(p);
      map.set(p.workspaceId, entry);
    }
    return [...map.entries()]
      .map(([id, e]) => {
        const count = (h: string) => e.rows.filter(p => p.health === h).length;
        const done = count('Completed'), risk = count('AtRisk'), late = count('Delayed'), ok = count('OnTrack');
        return {
          id, label: e.label, total: e.rows.length,
          segments: [
            { value: done, tone: 'brand' as const, label: 'Completed' },
            { value: ok,   tone: 'good'  as const, label: 'On track' },
            { value: risk, tone: 'warn'  as const, label: 'At risk' },
            { value: late, tone: 'bad'   as const, label: 'Delayed' },
            { value: e.rows.length - done - ok - risk - late, tone: 'neutral' as const, label: 'Not under way' },
          ],
        };
      })
      .sort((a, b) => b.total - a.total || a.label.localeCompare(b.label));
  });

  // ── Drill-downs ────────────────────────────────────────────────────────────
  private open(title: string, rows: ProjectRow[], subtitle?: string): void {
    this.drill.set({ kind: 'rows', title, subtitle, rows: rows.map(projectDrillRow), emptyText: 'No projects match.' });
  }

  private labelOf(items: LegendItem[], key: string): string {
    return items.find(i => i.key === key)?.label ?? key;
  }

  openAll(): void        { this.open('All projects', this.projects()); }
  openActive(): void     { this.open('Active projects', this.projects().filter(p => p.status === 2)); }
  openCompleted(): void  { this.open('Completed projects', this.projects().filter(p => p.health === 'Completed')); }
  openUnderWay(): void {
    this.open('Projects under way', [...this.underWay()].sort((a, b) => a.progress - b.progress),
      'Active and on-hold projects, least progress first');
  }

  openHealth(key: string | undefined): void {
    if (!key) return;
    const subtitle = key === 'Delayed' ? 'Active and past the planned end date'
      : key === 'AtRisk' ? 'Active with overdue action items or late milestones' : undefined;
    this.open(`${this.labelOf(this.healthLegend(), key)} projects`, this.projects().filter(p => p.health === key), subtitle);
  }

  openStatus(key: string | undefined): void {
    if (!key) return;
    this.open(`${this.labelOf(this.statusLegend(), key)} projects`, this.projects().filter(p => String(p.status) === key));
  }

  openType(key: string | undefined): void {
    if (!key) return;
    this.open(`${this.labelOf(this.typeLegend(), key)} projects`, this.projects().filter(p => String(p.projectType) === key));
  }

  openProgress(key: string | undefined): void {
    const band = PROGRESS_BANDS.find(b => b.key === key);
    if (!band) return;
    this.open(`Progress ${band.label}`, this.underWay().filter(p => p.progress >= band.min && p.progress <= band.max),
      'Projects under way');
  }

  openWorkspace(item: RankedItem): void {
    this.open(item.label, this.projects().filter(p => p.workspaceId === item.id), `${item.total} projects in this workspace`);
  }

  openMilestones(lateOnly: boolean): void {
    const rows = (this.data()?.milestones ?? []).filter(m => !lateOnly || m.isLate);
    this.drill.set({
      kind: 'rows',
      title: lateOnly ? 'Late milestones' : 'Late and upcoming milestones',
      subtitle: lateOnly ? undefined : 'Open milestones that are late or due within 30 days',
      rows: rows.map(m => this.milestoneRow(m)),
      emptyText: 'No milestones need attention.',
    });
  }

  private milestoneRow(m: MilestoneRow): DrillRow {
    const due = new Date(m.plannedDueDate).toLocaleDateString(undefined, { day: 'numeric', month: 'short', year: 'numeric' });
    return {
      code: m.code,
      title: m.name,
      subtitle: m.projectName,
      badge: m.isLate ? 'Late' : m.statusLabel,
      badgeTone: m.isLate ? 'bad' : 'brand',
      meta: m.isLate ? `${Math.abs(m.daysToDue)}d past ${due}` : `Due ${due}`,
      metaTone: m.isLate ? 'bad' : 'neutral',
      progress: m.completion,
      link: ['/projects', m.projectId, 'milestones', m.id],
    };
  }

  goToProject(p: ProjectRow): void {
    this.router.navigate(['/projects', p.id]);
  }

  goToMilestone(m: MilestoneRow): void {
    this.router.navigate(['/projects', m.projectId, 'milestones', m.id]);
  }
}
