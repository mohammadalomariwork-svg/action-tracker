import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { CommonModule, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { KpiActualsService } from '../../../services/kpi-actuals.service';
import { ToastService } from '../../../core/services/toast.service';
import { WorkflowStateService } from '../../../services/workflow-state.service';
import { BreadcrumbComponent } from '../../../shared/components/breadcrumb/breadcrumb.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { DocumentsSectionComponent } from '../../../shared/components/documents-section/documents-section.component';
import { HasPermissionDirective } from '../../../shared/directives/has-permission.directive';
import {
  KPI_ACTUAL_STATUS_CONFIG,
  KpiActual,
  KpiActualGridResponse,
  KpiActualGridRow,
  statusName,
} from '../../../models/kpi-actual.model';

interface EditableRow extends KpiActualGridRow {
  draftActual: string;
  draftNotes: string;
  saving: boolean;
  submitting: boolean;
}

/**
 * CR-001-F — Period-aware actuals grid for a KPI Target.
 * Rows: Q1..Q4 / H1..H2 / Annual depending on the parent KPI.
 */
@Component({
  selector: 'app-kpi-actuals-grid',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    DecimalPipe,
    BreadcrumbComponent,
    PageHeaderComponent,
    DocumentsSectionComponent,
    HasPermissionDirective,
  ],
  templateUrl: './kpi-actuals-grid.component.html',
  styleUrl: './kpi-actuals-grid.component.scss',
})
export class KpiActualsGridComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly svc = inject(KpiActualsService);
  private readonly toast = inject(ToastService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly workflowState = inject(WorkflowStateService);

  readonly STATUS_CONFIG = KPI_ACTUAL_STATUS_CONFIG;

  readonly kpiId = signal<string>('');
  readonly targetId = signal<string>('');
  readonly year = signal<number>(new Date().getFullYear());

  readonly grid = signal<KpiActualGridResponse | null>(null);
  readonly rows = signal<EditableRow[]>([]);
  readonly loading = signal(false);

  readonly evidenceModalOpen = signal(false);
  readonly evidenceActualId = signal<string | null>(null);
  readonly evidenceLabel = signal('');

  readonly historyModalOpen = signal(false);
  readonly historyRows = signal<KpiActual[]>([]);
  readonly historyLabel = signal('');

  readonly reviewModalOpen = signal(false);
  readonly reviewActual = signal<KpiActual | null>(null);
  readonly isApproving = signal(true);
  readonly reviewComment = signal('');
  readonly reviewSubmitting = signal(false);

  readonly hasRows = computed(() => this.rows().length > 0);

  ngOnInit(): void {
    this.kpiId.set(this.route.snapshot.paramMap.get('kpiId') ?? '');
    this.targetId.set(this.route.snapshot.paramMap.get('targetId') ?? '');
    this.load();
  }

  load(): void {
    if (!this.targetId()) return;
    this.loading.set(true);
    this.svc.getGrid(this.targetId(), this.year())
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: r => {
          if (r.success) {
            this.grid.set(r.data);
            this.rows.set(r.data.rows.map(row => this.toEditable(row)));
          }
          this.loading.set(false);
        },
        error: () => {
          this.loading.set(false);
          this.toast.error('Failed to load actuals.');
        },
      });
  }

  changeYear(year: number): void {
    this.year.set(+year);
    this.load();
  }

  private toEditable(row: KpiActualGridRow): EditableRow {
    return {
      ...row,
      draftActual: row.current?.actualValue?.toString() ?? '',
      draftNotes: row.current?.notes ?? '',
      saving: false,
      submitting: false,
    };
  }

  // ── Status helpers ──────────────────────────────────────────────────────

  statusName(s: string | number): string {
    return statusName(s as any);
  }

  statusClass(s: string | number): string {
    return this.STATUS_CONFIG[this.statusName(s)]?.cssClass ?? 'bg-secondary';
  }

  achievementClass(row: EditableRow): string {
    const pct = row.current?.achievementPercent;
    if (pct == null) return 'text-muted';
    if (pct >= 100) return 'text-success fw-semibold';
    if (pct >= 70)  return 'text-warning fw-semibold';
    return 'text-danger fw-semibold';
  }

  isEditable(row: EditableRow): boolean {
    if (!row.current) return true;
    return this.statusName(row.current.status) === 'Draft';
  }

  // ── Save / Create ───────────────────────────────────────────────────────

  saveDraft(row: EditableRow): void {
    const actualValue = parseFloat(row.draftActual);
    if (Number.isNaN(actualValue)) {
      this.toast.warning('Enter a numeric actual value.');
      return;
    }

    row.saving = true;
    const notes = row.draftNotes?.trim() || null;

    if (row.current) {
      this.svc.update(row.current.id, { actualValue, notes })
        .subscribe({
          next: r => {
            row.saving = false;
            if (r.success) {
              this.toast.success('Draft saved.');
              this.load();
            }
          },
          error: err => {
            row.saving = false;
            this.toast.error(err?.error?.message ?? 'Failed to save draft.');
          },
        });
    } else {
      this.svc.create({
        kpiTargetId: this.targetId(),
        periodType: row.periodType as any,
        periodIndex: row.periodIndex,
        year: row.year,
        actualValue,
        notes,
      }).subscribe({
        next: r => {
          row.saving = false;
          if (r.success) {
            this.toast.success('Draft created.');
            this.load();
          }
        },
        error: err => {
          row.saving = false;
          const msg = err?.error?.message ?? 'Failed to create actual.';
          this.toast.error(msg);
        },
      });
    }
  }

  submitForApproval(row: EditableRow): void {
    if (!row.current) {
      this.toast.warning('Save a draft first.');
      return;
    }
    if (!row.periodHasEnded) {
      this.toast.warning(`Available after ${row.periodLabel} ends on ${this.formatDate(row.periodEndUtc)}.`);
      return;
    }

    row.submitting = true;
    this.svc.submit(row.current.id).subscribe({
      next: r => {
        row.submitting = false;
        if (r.success) {
          this.toast.success('Submitted for approval.');
          this.workflowState.refreshPendingCount();
          this.load();
        }
      },
      error: err => {
        row.submitting = false;
        this.toast.error(err?.error?.message ?? 'Failed to submit for approval.');
      },
    });
  }

  deleteDraft(row: EditableRow): void {
    if (!row.current) return;
    if (!confirm('Delete this draft? This cannot be undone.')) return;
    this.svc.delete(row.current.id).subscribe({
      next: () => { this.toast.success('Draft deleted.'); this.load(); },
      error: err => this.toast.error(err?.error?.message ?? 'Failed to delete draft.'),
    });
  }

  // ── Evidence modal ──────────────────────────────────────────────────────

  openEvidence(row: EditableRow): void {
    if (!row.current) {
      this.toast.warning('Save a draft before attaching evidence.');
      return;
    }
    this.evidenceActualId.set(row.current.id);
    this.evidenceLabel.set(`${row.periodLabel} ${row.year}`);
    this.evidenceModalOpen.set(true);
  }

  closeEvidence(): void {
    this.evidenceModalOpen.set(false);
    this.evidenceActualId.set(null);
    this.load();
  }

  // ── History modal ───────────────────────────────────────────────────────

  openHistory(row: EditableRow): void {
    this.historyLabel.set(`${row.periodLabel} ${row.year}`);
    this.svc.getHistory(this.targetId(), row.periodType as number, row.periodIndex, row.year)
      .subscribe({
        next: r => {
          if (r.success) {
            this.historyRows.set(r.data ?? []);
            this.historyModalOpen.set(true);
          }
        },
        error: () => this.toast.error('Failed to load history.'),
      });
  }

  closeHistory(): void {
    this.historyModalOpen.set(false);
    this.historyRows.set([]);
  }

  // ── Review modal (reviewer side) ────────────────────────────────────────

  openReview(actual: KpiActual, isApprove: boolean): void {
    this.reviewActual.set(actual);
    this.isApproving.set(isApprove);
    this.reviewComment.set('');
    this.reviewModalOpen.set(true);
  }

  closeReview(): void {
    this.reviewModalOpen.set(false);
    this.reviewActual.set(null);
    this.reviewComment.set('');
  }

  submitReview(): void {
    const actual = this.reviewActual();
    if (!actual) return;
    if (!this.isApproving() && !this.reviewComment().trim()) {
      this.toast.warning('A comment is required when rejecting.');
      return;
    }
    this.reviewSubmitting.set(true);
    this.svc.review(actual.id, {
      actualId: actual.id,
      isApproved: this.isApproving(),
      reviewComment: this.reviewComment().trim() || null,
    }).subscribe({
      next: r => {
        this.reviewSubmitting.set(false);
        if (r.success) {
          this.toast.success(this.isApproving() ? 'Approved.' : 'Rejected.');
          this.workflowState.refreshPendingCount();
          this.closeReview();
          this.load();
        }
      },
      error: err => {
        this.reviewSubmitting.set(false);
        this.toast.error(err?.error?.message ?? 'Failed to submit review.');
      },
    });
  }

  formatDate(iso: string): string {
    if (!iso) return '';
    try {
      return new Date(iso).toLocaleDateString();
    } catch { return iso; }
  }

  trackByPeriod = (_: number, row: EditableRow) => `${row.periodType}-${row.periodIndex}-${row.year}`;
  trackByActualId = (_: number, a: KpiActual) => a.id;
}
