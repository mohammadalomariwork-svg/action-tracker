import {
  Component,
  OnInit,
  ChangeDetectionStrategy,
  DestroyRef,
  inject,
  signal,
  computed,
} from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { CommonModule }          from '@angular/common';
import { FormsModule }           from '@angular/forms';
import { takeUntilDestroyed }    from '@angular/core/rxjs-interop';

import { KpiService }                  from '../../services/kpi.service';
import { StrategicObjectiveService }   from '../../services/strategic-objective.service';
import { ToastService }                from '../../../../core/services/toast.service';
import { Kpi, MeasurementPeriod, MeasurementPeriodLabels } from '../../models/kpi.models';
import { StrategicObjective }          from '../../models/strategic-objectives.models';
import { KpiFormComponent }            from './kpi-form.component';
import { AdminBreadcrumbComponent }    from '../shared/admin-breadcrumb/admin-breadcrumb.component';
import { PageHeaderComponent }         from '../../../../shared/components/page-header/page-header.component';
import { HasPermissionDirective }      from '../../../../shared';

@Component({
  selector: 'app-kpi-list',
  standalone: true,
  imports: [CommonModule, FormsModule, KpiFormComponent, AdminBreadcrumbComponent, PageHeaderComponent, HasPermissionDirective],
  templateUrl: './kpi-list.component.html',
  styleUrl: './kpi-list.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KpiListComponent implements OnInit {
  private readonly kpiService       = inject(KpiService);
  private readonly objectiveService = inject(StrategicObjectiveService);
  private readonly route            = inject(ActivatedRoute);
  private readonly router           = inject(Router);
  private readonly toast            = inject(ToastService);
  private readonly destroyRef       = inject(DestroyRef);

  readonly PeriodLabels = MeasurementPeriodLabels;

  // ── State signals ────────────────────────────────────────────────────────────
  readonly kpis              = signal<Kpi[]>([]);
  readonly objectives        = signal<StrategicObjective[]>([]);
  readonly contextObjective  = signal<StrategicObjective | null>(null);
  readonly loading           = signal(false);
  readonly error             = signal<string | null>(null);
  readonly currentPage       = signal(1);
  readonly pageSize          = signal(20);
  readonly totalCount        = signal(0);
  readonly filterObjectiveId = signal<string>('');
  readonly showDeleted       = signal(false);
  readonly searchText        = signal('');
  readonly offcanvasOpen     = signal(false);
  readonly editingKpi        = signal<Kpi | null>(null);

  readonly totalPages = computed(() =>
    Math.max(1, Math.ceil(this.totalCount() / this.pageSize()))
  );

  readonly filteredKpis = computed(() => {
    const term = this.searchText().toLowerCase().trim();
    if (!term) return this.kpis();
    return this.kpis().filter((k) => k.name.toLowerCase().includes(term));
  });

  ngOnInit(): void {
    const queryObjId = this.route.snapshot.queryParamMap.get('objectiveId');
    if (queryObjId) {
      this.filterObjectiveId.set(queryObjId);
      this.loadContextObjective(queryObjId);
    }
    this.loadObjectives();
    this.loadKpis();
  }

  // ── Helpers ──────────────────────────────────────────────────────────────────

  kpiCode(k: Kpi): string {
    return `${k.objectiveCode}-KPI-${k.kpiNumber}`;
  }

  periodBadgeClass(periodValue: MeasurementPeriod): string {
    const map: Record<MeasurementPeriod, string> = {
      1: 'bg-primary',
      2: 'bg-info text-dark',
      3: 'bg-warning text-dark',
      4: 'bg-success',
    };
    return map[periodValue] ?? 'bg-secondary';
  }

  // ── Data loading ─────────────────────────────────────────────────────────────

  loadKpis(): void {
    this.loading.set(true);
    this.error.set(null);
    const objId = this.filterObjectiveId() || undefined;
    this.kpiService
      .getAll(this.currentPage(), this.pageSize(), objId, this.showDeleted())
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          this.kpis.set(res.kpis);
          this.totalCount.set(res.totalCount);
          this.loading.set(false);
        },
        error: (err) => {
          this.error.set(err?.error?.message ?? 'Failed to load KPIs.');
          this.loading.set(false);
        },
      });
  }

  loadObjectives(): void {
    this.objectiveService
      .getAll(1, 200)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => this.objectives.set(res.objectives),
        error: () => { /* non-critical */ },
      });
  }

  loadContextObjective(id: string): void {
    this.objectiveService
      .getById(id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (obj) => this.contextObjective.set(obj),
        error: () => { /* breadcrumb shows partial info only */ },
      });
  }

  // ── Filters ──────────────────────────────────────────────────────────────────

  onFilterObjectiveChange(value: string): void {
    this.filterObjectiveId.set(value);
    this.currentPage.set(1);
    if (value) {
      this.loadContextObjective(value);
      this.router.navigate([], {
        queryParams: { objectiveId: value },
        queryParamsHandling: 'merge',
      });
    } else {
      this.contextObjective.set(null);
      this.router.navigate([], { queryParams: {} });
    }
    this.loadKpis();
  }

  onShowDeletedChange(checked: boolean): void {
    this.showDeleted.set(checked);
    this.currentPage.set(1);
    this.loadKpis();
  }

  goToPage(page: number): void {
    if (page < 1 || page > this.totalPages()) return;
    this.currentPage.set(page);
    this.loadKpis();
  }

  // ── Offcanvas ────────────────────────────────────────────────────────────────

  openAdd(): void {
    this.editingKpi.set(null);
    this.offcanvasOpen.set(true);
  }

  openEdit(kpi: Kpi): void {
    this.editingKpi.set(kpi);
    this.offcanvasOpen.set(true);
  }

  closeOffcanvas(): void {
    this.offcanvasOpen.set(false);
    this.editingKpi.set(null);
  }

  onSaved(kpi: Kpi): void {
    this.toast.success(
      this.editingKpi()
        ? `KPI "${this.kpiCode(kpi)}" updated.`
        : `KPI ${this.kpiCode(kpi)} created.`
    );
    this.closeOffcanvas();
    this.loadKpis();
  }

  // ── Row actions ──────────────────────────────────────────────────────────────

  onDelete(kpi: Kpi): void {
    if (!confirm(`Delete KPI "${this.kpiCode(kpi)} — ${kpi.name}"?`)) return;
    this.kpiService
      .softDelete(kpi.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.toast.success(`KPI "${this.kpiCode(kpi)}" deleted.`);
          this.loadKpis();
        },
        error: (err) => this.toast.error(err?.error?.message ?? 'Failed to delete.'),
      });
  }

  onRestore(kpi: Kpi): void {
    this.kpiService
      .restore(kpi.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.toast.success(`KPI "${this.kpiCode(kpi)}" restored.`);
          this.loadKpis();
        },
        error: (err) => this.toast.error(err?.error?.message ?? 'Failed to restore.'),
      });
  }

  onManageTargets(kpi: Kpi): void {
    this.router.navigate(['/admin/kpis', kpi.id, 'targets']);
  }
}
