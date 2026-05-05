import {
  Component,
  Input,
  OnInit,
  ChangeDetectionStrategy,
  DestroyRef,
  inject,
  signal,
} from '@angular/core';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { CommonModule }       from '@angular/common';
import { FormsModule }        from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { KpiService }   from '../../services/kpi.service';
import { ToastService } from '../../../../core/services/toast.service';
import { DocumentService } from '../../../../core/services/document.service';
import { BreadcrumbComponent } from '../../../../shared/components/breadcrumb/breadcrumb.component';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { DocumentsSectionComponent } from '../../../../shared/components/documents-section/documents-section.component';
import {
  Kpi,
  KpiTarget,
  KpiWithTargets,
  MeasurementPeriod,
  MeasurementPeriodLabels,
} from '../../models/kpi.models';

interface TargetRow {
  /** KpiTarget primary key. Undefined for months that have not been saved yet. */
  id?: string;
  month: number;
  monthName: string;
  target: number | null;
  actual: number | null;
  notes: string;
  editing: boolean;
  // Inline edit draft values
  draftTarget: string;
  draftActual: string;
  draftNotes: string;
  // Audit
  createdByName?: string;
  updatedByName?: string;
  createdAt?: string;
  updatedAt?: string;
  /** Number of evidence files attached to this target. */
  evidenceCount: number;
}

const MONTH_NAMES = [
  '', 'January', 'February', 'March', 'April', 'May', 'June',
  'July', 'August', 'September', 'October', 'November', 'December',
];

/** Returns which months to display based on measurement period. */
function visibleMonths(period: MeasurementPeriod): number[] {
  switch (period) {
    case 2: return [3, 6, 9, 12];         // Quarterly
    case 3: return [6, 12];               // Semi-Annual
    case 4: return [12];                  // Yearly
    default: return [1,2,3,4,5,6,7,8,9,10,11,12]; // Monthly
  }
}

@Component({
  selector: 'app-kpi-targets',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, BreadcrumbComponent, PageHeaderComponent, DocumentsSectionComponent],
  templateUrl: './kpi-targets.component.html',
  styleUrl: './kpi-targets.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KpiTargetsComponent implements OnInit {
  @Input() kpiId!: string;

  private readonly kpiService      = inject(KpiService);
  private readonly documentService = inject(DocumentService);
  private readonly route           = inject(ActivatedRoute);
  private readonly router          = inject(Router);
  private readonly toast           = inject(ToastService);
  private readonly destroyRef      = inject(DestroyRef);

  readonly PeriodLabels = MeasurementPeriodLabels;

  readonly kpi         = signal<KpiWithTargets | null>(null);
  readonly rows        = signal<TargetRow[]>([]);
  readonly loading     = signal(false);
  readonly saving      = signal(false);
  readonly error       = signal<string | null>(null);
  readonly selectedYear = signal(new Date().getFullYear());

  // Evidence modal state
  readonly evidenceModalOpen = signal(false);
  readonly evidenceTargetId  = signal<string | null>(null);
  readonly evidenceMonthName = signal('');

  ngOnInit(): void {
    // Support both route param and @Input
    const routeId = this.route.snapshot.paramMap.get('kpiId');
    if (routeId) this.kpiId = routeId;
    this.loadKpi();
  }

  // ── Data loading ─────────────────────────────────────────────────────────────

  loadKpi(): void {
    if (!this.kpiId) return;
    this.loading.set(true);
    this.error.set(null);

    this.kpiService
      .getById(this.kpiId, this.selectedYear())
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (kpiWithTargets) => {
          this.kpi.set(kpiWithTargets);
          this.buildRows(kpiWithTargets);
          this.loading.set(false);
        },
        error: (err) => {
          this.error.set(err?.error?.message ?? 'Failed to load KPI.');
          this.loading.set(false);
        },
      });
  }

  private buildRows(kpiWithTargets: KpiWithTargets): void {
    const months = visibleMonths(kpiWithTargets.periodValue);
    const targetMap = new Map<number, KpiTarget>(
      kpiWithTargets.targets.map((t) => [t.month, t])
    );

    const rows: TargetRow[] = months.map((m) => {
      const existing = targetMap.get(m);
      return {
        id: existing?.id,
        month: m,
        monthName: MONTH_NAMES[m],
        target: existing?.target ?? null,
        actual: existing?.actual ?? null,
        notes:  existing?.notes  ?? '',
        editing: false,
        draftTarget: String(existing?.target ?? ''),
        draftActual: String(existing?.actual ?? ''),
        draftNotes:  existing?.notes ?? '',
        createdByName: existing?.createdByName,
        updatedByName: existing?.updatedByName,
        createdAt: existing?.createdAt,
        updatedAt: existing?.updatedAt,
        evidenceCount: 0,
      };
    });

    this.rows.set(rows);
    this.loadEvidenceCounts();
  }

  /** Fetches the evidence count for each saved target row in parallel. */
  private loadEvidenceCounts(): void {
    const saved = this.rows().filter(r => r.id);
    if (saved.length === 0) return;

    saved.forEach((row) => {
      this.documentService.getByEntity('KpiTarget', row.id!)
        .pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe({
          next: (res) => {
            const count = res.data?.length ?? 0;
            this.rows.update((rows) =>
              rows.map(r => r.month === row.month ? { ...r, evidenceCount: count } : r)
            );
          },
          error: () => { /* leave count at 0 on failure */ },
        });
    });
  }

  // ── Year navigation ──────────────────────────────────────────────────────────

  changeYear(delta: number): void {
    this.selectedYear.update((y) => y + delta);
    this.loadKpi();
  }

  // ── Inline edit ──────────────────────────────────────────────────────────────

  startEdit(row: TargetRow): void {
    this.rows.update((rows) =>
      rows.map((r) => ({
        ...r,
        editing: r.month === row.month,
        draftTarget: r.month === row.month ? String(r.target ?? '') : r.draftTarget,
        draftActual: r.month === row.month ? String(r.actual ?? '') : r.draftActual,
        draftNotes:  r.month === row.month ? (r.notes ?? '')        : r.draftNotes,
      }))
    );
  }

  commitEdit(row: TargetRow): void {
    this.rows.update((rows) =>
      rows.map((r) => {
        if (r.month !== row.month) return r;
        const t = parseFloat(r.draftTarget);
        const a = parseFloat(r.draftActual);
        return {
          ...r,
          target:  isNaN(t) ? null : t,
          actual:  isNaN(a) ? null : a,
          notes:   r.draftNotes,
          editing: false,
        };
      })
    );
  }

  cancelEdit(row: TargetRow): void {
    this.rows.update((rows) =>
      rows.map((r) =>
        r.month === row.month ? { ...r, editing: false } : r
      )
    );
  }

  onCellKeydown(event: KeyboardEvent, row: TargetRow): void {
    if (event.key === 'Enter')  { event.preventDefault(); this.commitEdit(row); }
    if (event.key === 'Escape') { event.preventDefault(); this.cancelEdit(row); }
  }

  // ── Row draft field mutators ─────────────────────────────────────────────────

  setDraft(month: number, field: 'draftTarget' | 'draftActual' | 'draftNotes', value: string): void {
    this.rows.update((rows) =>
      rows.map((r) => r.month === month ? { ...r, [field]: value } : r)
    );
  }

  // ── Computed per-row helpers ─────────────────────────────────────────────────

  variance(row: TargetRow): number | null {
    if (row.actual === null || row.target === null) return null;
    return row.actual - row.target;
  }

  achievement(row: TargetRow): string | null {
    if (row.target === null || row.target === 0) return null;
    if (row.actual === null) return null;
    return ((row.actual / row.target) * 100).toFixed(1);
  }

  varianceClass(row: TargetRow): string {
    const v = this.variance(row);
    if (v === null) return '';
    return v >= 0 ? 'kt-positive' : 'kt-negative';
  }

  // ── Save all ────────────────────────────────────────────────────────────────

  saveAll(): void {
    if (!this.kpiId) return;
    this.saving.set(true);
    this.error.set(null);

    const targets = this.rows().map((r) => ({
      month:  r.month,
      target: r.target ?? undefined,
      actual: r.actual ?? undefined,
      notes:  r.notes  || undefined,
    }));

    this.kpiService
      .bulkUpsertTargets({ kpiId: this.kpiId, year: this.selectedYear(), targets })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.saving.set(false);
          this.toast.success(`Targets for ${this.selectedYear()} saved successfully.`);
          this.loadKpi();
        },
        error: (err) => {
          this.saving.set(false);
          this.error.set(err?.error?.message ?? 'Failed to save targets.');
        },
      });
  }

  // ── Evidence modal ──────────────────────────────────────────────────────────

  /** Opens the evidence modal for a target row. Requires the row to be saved. */
  openEvidence(row: TargetRow): void {
    if (!row.id) {
      this.toast.error('Save the target first before attaching evidence.');
      return;
    }
    this.evidenceTargetId.set(row.id);
    this.evidenceMonthName.set(row.monthName);
    this.evidenceModalOpen.set(true);
  }

  /** Closes the modal and refreshes the count for the current target. */
  closeEvidence(): void {
    const targetId = this.evidenceTargetId();
    this.evidenceModalOpen.set(false);
    this.evidenceTargetId.set(null);
    this.evidenceMonthName.set('');
    if (targetId) this.refreshEvidenceCount(targetId);
  }

  private refreshEvidenceCount(targetId: string): void {
    this.documentService.getByEntity('KpiTarget', targetId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          const count = res.data?.length ?? 0;
          this.rows.update((rows) =>
            rows.map(r => r.id === targetId ? { ...r, evidenceCount: count } : r)
          );
        },
        error: () => { /* keep stale count on error */ },
      });
  }

  goBack(): void {
    const kpi = this.kpi();
    if (kpi) {
      this.router.navigate(['/admin/kpis'], {
        queryParams: { objectiveId: kpi.strategicObjectiveId },
      });
    } else {
      this.router.navigate(['/admin/kpis']);
    }
  }
}
