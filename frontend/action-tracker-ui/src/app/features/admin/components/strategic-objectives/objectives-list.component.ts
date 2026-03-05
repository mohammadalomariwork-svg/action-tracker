import {
  Component,
  OnInit,
  ChangeDetectionStrategy,
  DestroyRef,
  inject,
  signal,
  computed,
} from '@angular/core';
import { Router } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { StrategicObjectiveService } from '../../services/strategic-objective.service';
import { OrgUnitService }            from '../../services/org-unit.service';
import { ToastService }              from '../../../../core/services/toast.service';
import { StrategicObjective }        from '../../models/strategic-objectives.models';
import { OrgUnit }                   from '../../models/org-chart.models';
import { ObjectiveFormComponent }    from './objective-form.component';
import { AdminBreadcrumbComponent }  from '../shared/admin-breadcrumb/admin-breadcrumb.component';

@Component({
  selector: 'app-objectives-list',
  standalone: true,
  imports: [CommonModule, FormsModule, ObjectiveFormComponent, AdminBreadcrumbComponent],
  templateUrl: './objectives-list.component.html',
  styleUrl: './objectives-list.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ObjectivesListComponent implements OnInit {
  private readonly objectiveService = inject(StrategicObjectiveService);
  private readonly orgUnitService   = inject(OrgUnitService);
  private readonly router           = inject(Router);
  private readonly toast            = inject(ToastService);
  private readonly destroyRef       = inject(DestroyRef);

  // ── State signals ────────────────────────────────────────────────────────────
  readonly objectives       = signal<StrategicObjective[]>([]);
  readonly orgUnits         = signal<OrgUnit[]>([]);
  readonly loading          = signal(false);
  readonly error            = signal<string | null>(null);
  readonly currentPage      = signal(1);
  readonly pageSize         = signal(20);
  readonly totalCount       = signal(0);
  readonly filterOrgUnitId  = signal<string>('');
  readonly showDeleted      = signal(false);
  readonly searchText       = signal('');
  readonly offcanvasOpen    = signal(false);
  readonly editingObjective = signal<StrategicObjective | null>(null);

  readonly totalPages = computed(() =>
    Math.max(1, Math.ceil(this.totalCount() / this.pageSize()))
  );

  /** Locally filtered view for search-by-code/statement */
  readonly filteredObjectives = computed(() => {
    const term = this.searchText().toLowerCase().trim();
    if (!term) return this.objectives();
    return this.objectives().filter(
      (o) =>
        o.objectiveCode.toLowerCase().includes(term) ||
        o.statement.toLowerCase().includes(term)
    );
  });

  ngOnInit(): void {
    this.loadOrgUnits();
    this.loadObjectives();
  }

  // ── Data loading ─────────────────────────────────────────────────────────────

  loadObjectives(): void {
    this.loading.set(true);
    this.error.set(null);

    const orgUnitId = this.filterOrgUnitId() || undefined;

    this.objectiveService
      .getAll(this.currentPage(), this.pageSize(), orgUnitId, this.showDeleted())
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          this.objectives.set(res.objectives);
          this.totalCount.set(res.totalCount);
          this.loading.set(false);
        },
        error: (err) => {
          this.error.set(err?.error?.message ?? 'Failed to load strategic objectives.');
          this.loading.set(false);
        },
      });
  }

  loadOrgUnits(): void {
    this.orgUnitService
      .getAll(1, 200)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => this.orgUnits.set(res.orgUnits),
        error: () => { /* non-critical — filter just won't populate */ },
      });
  }

  // ── Filters ──────────────────────────────────────────────────────────────────

  onFilterOrgUnitChange(value: string): void {
    this.filterOrgUnitId.set(value);
    this.currentPage.set(1);
    this.loadObjectives();
  }

  onShowDeletedChange(value: boolean): void {
    this.showDeleted.set(value);
    this.currentPage.set(1);
    this.loadObjectives();
  }

  goToPage(page: number): void {
    if (page < 1 || page > this.totalPages()) return;
    this.currentPage.set(page);
    this.loadObjectives();
  }

  // ── Offcanvas ────────────────────────────────────────────────────────────────

  openAdd(): void {
    this.editingObjective.set(null);
    this.offcanvasOpen.set(true);
  }

  openEdit(obj: StrategicObjective): void {
    this.editingObjective.set(obj);
    this.offcanvasOpen.set(true);
  }

  closeOffcanvas(): void {
    this.offcanvasOpen.set(false);
    this.editingObjective.set(null);
  }

  onSaved(obj: StrategicObjective): void {
    this.toast.success(
      this.editingObjective()
        ? `"${obj.objectiveCode}" updated.`
        : `Strategic objective ${obj.objectiveCode} created.`
    );
    this.closeOffcanvas();
    this.loadObjectives();
  }

  // ── Row actions ──────────────────────────────────────────────────────────────

  onDelete(obj: StrategicObjective): void {
    if (!confirm(`Delete strategic objective "${obj.objectiveCode} — ${obj.statement}"?`)) return;

    this.objectiveService
      .softDelete(obj.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.toast.success(`"${obj.objectiveCode}" deleted.`);
          this.loadObjectives();
        },
        error: (err) => this.toast.error(err?.error?.message ?? 'Failed to delete.'),
      });
  }

  onRestore(obj: StrategicObjective): void {
    this.objectiveService
      .restore(obj.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.toast.success(`"${obj.objectiveCode}" restored.`);
          this.loadObjectives();
        },
        error: (err) => this.toast.error(err?.error?.message ?? 'Failed to restore.'),
      });
  }

  onViewKpis(obj: StrategicObjective): void {
    this.router.navigate(['/admin/kpis'], { queryParams: { objectiveId: obj.id } });
  }
}
