import {
  Component,
  ChangeDetectionStrategy,
  OnInit,
  inject,
  signal,
  computed,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { forkJoin, of } from 'rxjs';
import { catchError, map, switchMap } from 'rxjs/operators';

import { StrategicObjectiveService } from '../../admin/services/strategic-objective.service';
import { KpiService } from '../../admin/services/kpi.service';
import { StrategicObjective } from '../../admin/models/strategic-objectives.models';
import {
  Kpi,
  KpiTarget,
  MeasurementPeriod,
  MeasurementPeriodLabels,
} from '../../admin/models/kpi.models';
import { KpiTargetsComponent } from '../../admin/components/kpis/kpi-targets.component';
import { KpiFormComponent } from '../../admin/components/kpis/kpi-form.component';
import { ObjectiveFormComponent } from '../../admin/components/strategic-objectives/objective-form.component';
import { BreadcrumbComponent } from '../../../shared/components/breadcrumb/breadcrumb.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { HasPermissionDirective } from '../../../shared/directives/has-permission.directive';
import { StrategicPlanStatus } from '../../admin/models/strategic-plans.models';
import { ToastService } from '../../../core/services/toast.service';

interface PeriodSlot {
  /** Month value the backend uses for this period slot (3/6/9/12 for quarters etc.). */
  month: number;
  label: string;
}

interface KpiBlock {
  kpi: Kpi;
  slots: PeriodSlot[];
  /** Map keyed by month → target row for the current year. */
  targetsByMonth: Map<number, KpiTarget>;
  achievementPct: number | null;
}

const MONTH_NAMES = [
  '', 'January', 'February', 'March', 'April', 'May', 'June',
  'July', 'August', 'September', 'October', 'November', 'December',
];

/** Period-aware slot labels. Mirrors `KpiTargetsComponent.visibleMonths`. */
function buildSlots(period: MeasurementPeriod): PeriodSlot[] {
  switch (period) {
    case 2: return [
      { month: 3,  label: 'Q1' },
      { month: 6,  label: 'Q2' },
      { month: 9,  label: 'Q3' },
      { month: 12, label: 'Q4' },
    ];
    case 3: return [
      { month: 6,  label: 'H1' },
      { month: 12, label: 'H2' },
    ];
    case 4: return [
      { month: 12, label: 'Annual' },
    ];
    default:
      return Array.from({ length: 12 }, (_, i) => ({
        month: i + 1,
        label: MONTH_NAMES[i + 1]!,
      }));
  }
}

@Component({
  selector: 'app-objective-detail',
  standalone: true,
  imports: [
    CommonModule,
    BreadcrumbComponent,
    PageHeaderComponent,
    HasPermissionDirective,
    KpiTargetsComponent,
    KpiFormComponent,
    ObjectiveFormComponent,
  ],
  templateUrl: './objective-detail.component.html',
  styleUrl: './objective-detail.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ObjectiveDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly objectiveService = inject(StrategicObjectiveService);
  private readonly kpiService = inject(KpiService);
  private readonly toast = inject(ToastService);

  readonly PeriodLabels = MeasurementPeriodLabels;
  readonly currentYear = new Date().getFullYear();

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly objective = signal<StrategicObjective | null>(null);
  readonly blocks = signal<KpiBlock[]>([]);

  readonly totalTargets = computed(() =>
    this.blocks().reduce((sum, b) => sum + b.targetsByMonth.size, 0),
  );

  /** True when the parent strategic plan is locked (Approved without pending revision,
   *  or Archived). Falls back to `false` when the API didn't enrich the field. */
  readonly isPlanLocked = computed(() => this.objective()?.isPlanLocked === true);

  /** True only when the parent plan is in Draft. Used to gate inline KPI creation —
   *  the backend lock guard already blocks creation on PendingApproval/Approved/Archived
   *  plans, but we hide the button to avoid showing a path that would 403. */
  readonly canAddKpi = computed(
    () => this.objective()?.strategicPlanStatus === StrategicPlanStatus.Draft,
  );

  /** Same gate as add: Draft only. Used by the Edit-objective and Edit-KPI buttons. */
  readonly canEdit = this.canAddKpi;

  // ── "Manage targets" drawer state ───────────────────────────────────────
  readonly drawerOpen = signal(false);
  readonly drawerKpiId = signal<string | null>(null);
  readonly drawerKpiName = signal<string>('');

  // ── KPI form offcanvas state — handles both Add and Edit ───────────────
  readonly kpiFormOpen = signal(false);
  readonly editingKpi = signal<Kpi | null>(null);

  // ── Objective edit offcanvas state ─────────────────────────────────────
  readonly objectiveFormOpen = signal(false);

  openTargetsDrawer(kpi: Kpi): void {
    this.drawerKpiId.set(kpi.id);
    this.drawerKpiName.set(kpi.name);
    this.drawerOpen.set(true);
  }

  closeTargetsDrawer(): void {
    this.drawerOpen.set(false);
    this.drawerKpiId.set(null);
    this.drawerKpiName.set('');
    // Refresh objective so any saved target changes are reflected on the cards.
    const obj = this.objective();
    if (obj) this.load(obj.id);
  }

  // ── Add / Edit KPI (Draft plans only) ─────────────────────────────────
  openAddKpi(): void {
    this.editingKpi.set(null);
    this.kpiFormOpen.set(true);
  }

  openEditKpi(kpi: Kpi): void {
    this.editingKpi.set(kpi);
    this.kpiFormOpen.set(true);
  }

  closeKpiForm(): void {
    this.kpiFormOpen.set(false);
    this.editingKpi.set(null);
  }

  onKpiSaved(kpi: Kpi): void {
    const wasEdit = !!this.editingKpi();
    this.kpiFormOpen.set(false);
    this.editingKpi.set(null);
    this.toast.success(wasEdit ? 'KPI updated.' : 'KPI created.');

    const obj = this.objective();
    if (!obj) return;

    // Refresh the objective + KPI list so the new/updated card reflects the
    // change. After a *create* (not edit), chain straight into the targets
    // drawer for the new KPI so the user doesn't have to hunt for the card —
    // matches the "add KPIs and their targets" flow we built earlier.
    forkJoin({
      objective: this.objectiveService.getById(obj.id),
      kpis: this.kpiService.getByObjective(obj.id).pipe(
        catchError(() => of([] as Kpi[])),
      ),
    })
      .pipe(
        switchMap(({ objective, kpis }) => {
          if (kpis.length === 0) {
            return of({ objective, kpis, targetLists: [] as KpiTarget[][] });
          }
          const targetRequests = kpis.map(k =>
            this.kpiService.getTargets(k.id, this.currentYear).pipe(
              catchError(() => of([] as KpiTarget[])),
            ),
          );
          return forkJoin(targetRequests).pipe(
            map(targetLists => ({ objective, kpis, targetLists })),
          );
        }),
      )
      .subscribe({
        next: ({ objective, kpis, targetLists }) => {
          this.objective.set(objective);
          this.blocks.set(this.buildBlocks(kpis, targetLists));
          if (!wasEdit) this.openTargetsDrawer(kpi);
        },
      });
  }

  // ── Edit Objective (Draft plans only) ─────────────────────────────────
  openEditObjective(): void {
    this.objectiveFormOpen.set(true);
  }

  closeObjectiveForm(): void {
    this.objectiveFormOpen.set(false);
  }

  onObjectiveSaved(saved: StrategicObjective): void {
    this.objectiveFormOpen.set(false);
    this.toast.success('Strategic objective updated.');
    // Re-fetch via the same `load()` path so plan-locked / plan-status fields
    // get refreshed alongside the objective itself.
    this.load(saved.id);
  }

  ngOnInit(): void {
    this.route.paramMap.subscribe(params => {
      const objectiveId = params.get('objectiveId') ?? '';
      if (!objectiveId) {
        this.error.set('Missing objective id.');
        this.loading.set(false);
        return;
      }
      this.load(objectiveId);
    });
  }

  private load(objectiveId: string): void {
    this.loading.set(true);
    this.error.set(null);

    forkJoin({
      objective: this.objectiveService.getById(objectiveId),
      kpis: this.kpiService.getByObjective(objectiveId).pipe(
        catchError(() => of([] as Kpi[])),
      ),
    })
      .pipe(
        switchMap(({ objective, kpis }) => {
          if (kpis.length === 0) {
            return of({ objective, kpis, targetLists: [] as KpiTarget[][] });
          }
          // Fetch targets for the current year for each KPI in parallel.
          // Per-KPI failures fall through to an empty target list so a single
          // 404 doesn't blank the whole page.
          const targetRequests = kpis.map(k =>
            this.kpiService.getTargets(k.id, this.currentYear).pipe(
              catchError(() => of([] as KpiTarget[])),
            ),
          );
          return forkJoin(targetRequests).pipe(
            map(targetLists => ({ objective, kpis, targetLists })),
          );
        }),
      )
      .subscribe({
        next: ({ objective, kpis, targetLists }) => {
          this.objective.set(objective);
          this.blocks.set(this.buildBlocks(kpis, targetLists));
          this.loading.set(false);
        },
        error: err => {
          this.error.set(err?.error?.message ?? 'Failed to load objective.');
          this.loading.set(false);
        },
      });
  }

  private buildBlocks(kpis: Kpi[], targetLists: KpiTarget[][]): KpiBlock[] {
    return kpis.map((kpi, idx) => {
      const targets = targetLists[idx] ?? [];
      const targetsByMonth = new Map<number, KpiTarget>(
        targets.map(t => [t.month, t]),
      );
      return {
        kpi,
        slots: buildSlots(kpi.periodValue),
        targetsByMonth,
        achievementPct: this.computeAchievement(kpi, targets),
      };
    });
  }

  /**
   * Year-to-date achievement %: averages the per-slot ratios for slots that
   * have both a target and an actual. Returns null when nothing has been
   * measured yet so the UI can show "—".
   */
  private computeAchievement(_kpi: Kpi, targets: KpiTarget[]): number | null {
    const measured = targets.filter(
      t => t.target != null && t.actual != null && t.target !== 0,
    );
    if (measured.length === 0) return null;
    const sum = measured.reduce((acc, t) => acc + (t.actual! / t.target!) * 100, 0);
    return Math.round(sum / measured.length);
  }

  kpiCode(kpi: Kpi): string {
    return `${kpi.objectiveCode}-KPI-${kpi.kpiNumber}`;
  }

  achievementClass(pct: number | null): string {
    if (pct == null) return 'od-pct--neutral';
    if (pct >= 90) return 'od-pct--good';
    if (pct >= 60) return 'od-pct--warn';
    return 'od-pct--bad';
  }

  /** Returns the target row's `target` formatted with the KPI unit, or "—". */
  formatTargetCell(value: number | null | undefined, unit: string | undefined): string {
    if (value == null) return '—';
    return unit ? `${value} ${unit}` : String(value);
  }
}
