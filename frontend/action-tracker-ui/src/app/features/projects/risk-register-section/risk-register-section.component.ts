import {
  Component, OnInit, ViewChild,
  inject, signal, input,
} from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import * as XLSX from 'xlsx';

import { ProjectRiskService } from '../../../services/project-risk.service';
import { ToastService } from '../../../core/services/toast.service';
import {
  ProjectRiskSummary, ProjectRiskStats, ProjectRisk,
  RISK_CATEGORIES, RISK_STATUSES,
} from '../../../models/project-risk.model';
import { RiskRatingBadgeComponent } from '../../../shared/components/risk-rating-badge/risk-rating-badge.component';
import { ConfirmDialogComponent } from '../../../shared/components/confirm-dialog/confirm-dialog.component';
import { HasPermissionDirective } from '../../../shared/directives/has-permission.directive';
import { RiskFormComponent } from '../risk-form/risk-form.component';

const STATUS_COLORS: Record<string, string> = {
  Open:        '#0d6efd',
  Mitigating:  '#6f42c1',
  Accepted:    '#20c997',
  Transferred: '#6c757d',
  Closed:      '#198754',
};

const RATING_COLORS: Record<string, string> = {
  Critical: '#dc3545',
  High:     '#fd7e14',
  Medium:   '#ffc107',
  Low:      '#198754',
};

const RATINGS = ['Critical', 'High', 'Medium', 'Low'];

@Component({
  selector: 'app-risk-register-section',
  standalone: true,
  imports: [
    CommonModule, DatePipe, FormsModule, RouterLink,
    RiskRatingBadgeComponent, ConfirmDialogComponent, HasPermissionDirective, RiskFormComponent,
  ],
  templateUrl: './risk-register-section.component.html',
  styleUrl: './risk-register-section.component.scss',
})
export class RiskRegisterSectionComponent implements OnInit {
  readonly projectId = input.required<string>();

  @ViewChild('deleteConfirm') deleteConfirm!: ConfirmDialogComponent;

  private readonly riskService = inject(ProjectRiskService);
  private readonly toast       = inject(ToastService);
  private readonly router      = inject(Router);
  readonly categories = RISK_CATEGORIES;
  readonly statuses   = RISK_STATUSES;
  readonly ratings    = RATINGS;

  // Data
  readonly risks    = signal<ProjectRiskSummary[]>([]);
  readonly stats    = signal<ProjectRiskStats | null>(null);
  readonly loading  = signal(false);

  // Pagination
  readonly page      = signal(1);
  readonly pageSize  = 10;
  readonly totalPages = signal(0);
  readonly totalCount = signal(0);

  // Filters
  readonly filterStatus   = signal('');
  readonly filterRating   = signal('');
  readonly filterCategory = signal('');

  // Modal form state
  readonly showForm    = signal(false);
  readonly editingRisk = signal<ProjectRisk | null>(null);

  // Delete
  private deleteTargetId = '';

  // Risk matrix
  readonly matrixCells = this.buildMatrix();

  ngOnInit(): void {
    this.loadStats();
    this.loadRisks();
  }

  // ── Data loading ──────────────────────────────────────

  loadStats(): void {
    this.riskService.getRiskStats(this.projectId()).subscribe({
      next: res => { if (res.success) this.stats.set(res.data); },
    });
  }

  loadRisks(): void {
    this.loading.set(true);
    const status   = this.filterStatus() || undefined;
    const rating   = this.filterRating() || undefined;
    const category = this.filterCategory() || undefined;

    this.riskService.getRisksByProject(
      this.projectId(), this.page(), this.pageSize, status, rating, category,
    ).subscribe({
      next: res => {
        if (res.success) {
          this.risks.set(res.data.items);
          this.totalPages.set(res.data.totalPages);
          this.totalCount.set(res.data.totalCount);
        }
        this.loading.set(false);
      },
      error: () => {
        this.toast.error('Failed to load risks.');
        this.loading.set(false);
      },
    });
  }

  onFilterChange(): void {
    this.page.set(1);
    this.loadRisks();
  }

  goToPage(p: number): void {
    if (p < 1 || p > this.totalPages()) return;
    this.page.set(p);
    this.loadRisks();
  }

  // ── Form ──────────────────────────────────────────────

  openCreate(): void {
    this.editingRisk.set(null);
    this.showForm.set(true);
  }

  openEdit(riskId: string): void {
    this.riskService.getRiskById(this.projectId(), riskId).subscribe({
      next: res => {
        if (res.success) {
          this.editingRisk.set(res.data);
          this.showForm.set(true);
        }
      },
      error: () => this.toast.error('Failed to load risk.'),
    });
  }

  closeForm(): void {
    this.showForm.set(false);
    this.editingRisk.set(null);
  }

  onFormSaved(_risk: ProjectRisk): void {
    this.closeForm();
    this.refresh();
  }

  // ── Delete ────────────────────────────────────────────

  confirmDelete(riskId: string): void {
    this.deleteTargetId = riskId;
    this.deleteConfirm.open();
  }

  onDeleteConfirmed(confirmed: boolean): void {
    if (!confirmed) return;
    this.riskService.deleteRisk(this.projectId(), this.deleteTargetId).subscribe({
      next: () => { this.toast.success('Risk deleted.'); this.refresh(); },
      error: () => this.toast.error('Failed to delete risk.'),
    });
  }

  // ── Navigation ────────────────────────────────────────

  viewRisk(riskId: string): void {
    this.router.navigate(['/projects', this.projectId(), 'risks', riskId]);
  }

  // ── Export ────────────────────────────────────────────

  exportToExcel(): void {
    const wb = XLSX.utils.book_new();
    const data = this.risks().map(r => ({
      'Risk Code':  r.riskCode,
      'Title':      r.title,
      'Category':   r.category,
      'Score':      r.riskScore,
      'Rating':     r.riskRating,
      'Status':     r.status,
      'Owner':      r.riskOwnerDisplayName ?? '',
      'Identified': r.identifiedDate ? new Date(r.identifiedDate).toLocaleDateString() : '',
      'Due Date':   r.dueDate ? new Date(r.dueDate).toLocaleDateString() : '',
    }));
    XLSX.utils.book_append_sheet(wb, XLSX.utils.json_to_sheet(data), 'Risks');
    XLSX.writeFile(wb, `risk-register-${new Date().toISOString().slice(0, 10)}.xlsx`);
  }

  printRisks(): void {
    window.print();
  }

  // ── Helpers ───────────────────────────────────────────

  statusColor(status: string): string {
    return STATUS_COLORS[status] ?? '#6c757d';
  }

  ratingColor(rating: string): string {
    return RATING_COLORS[rating] ?? '#6c757d';
  }

  private refresh(): void {
    this.loadStats();
    this.loadRisks();
  }

  // ── Risk matrix ───────────────────────────────────────

  private buildMatrix(): { prob: number; impact: number }[] {
    const cells: { prob: number; impact: number }[] = [];
    for (let p = 5; p >= 1; p--) {
      for (let i = 1; i <= 5; i++) {
        cells.push({ prob: p, impact: i });
      }
    }
    return cells;
  }

  matrixCellColor(prob: number, impact: number): string {
    const score = prob * impact;
    if (score >= 20) return '#dc3545';
    if (score >= 12) return '#fd7e14';
    if (score >= 5) return '#ffc107';
    return '#198754';
  }

  matrixCellCount(prob: number, impact: number): number {
    return this.risks().filter(
      r => r.riskScore === prob * impact,
    ).length;
  }
}
