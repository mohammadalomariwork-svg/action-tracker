import { Component, OnInit, ViewChild, inject, signal } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';

import { ProjectRiskService } from '../../../../services/project-risk.service';
import { ProjectRisk } from '../../../../models/project-risk.model';
import { ToastService } from '../../../../core/services/toast.service';
import { PermissionStateService } from '../../../permissions/services/permission-state.service';
import { BreadcrumbComponent, BreadcrumbItem } from '../../../../shared/components/breadcrumb/breadcrumb.component';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { CommentsSectionComponent } from '../../../../shared/components/comments-section/comments-section.component';
import { DocumentsSectionComponent } from '../../../../shared/components/documents-section/documents-section.component';
import { ConfirmDialogComponent } from '../../../../shared/components/confirm-dialog/confirm-dialog.component';
import { RiskRatingBadgeComponent } from '../../../../shared/components/risk-rating-badge/risk-rating-badge.component';
import { RiskFormComponent } from '../../risk-form/risk-form.component';
import { HasPermissionDirective } from '../../../../shared/directives/has-permission.directive';

const RATING_COLORS: Record<string, string> = {
  Critical: '#dc3545', High: '#fd7e14', Medium: '#ffc107', Low: '#198754',
};
const STATUS_COLORS: Record<string, string> = {
  Open: '#0d6efd', Mitigating: '#6f42c1', Accepted: '#20c997', Transferred: '#6c757d', Closed: '#198754',
};

@Component({
  selector: 'app-risk-detail',
  standalone: true,
  imports: [
    CommonModule, DatePipe,
    BreadcrumbComponent, PageHeaderComponent,
    CommentsSectionComponent, DocumentsSectionComponent,
    ConfirmDialogComponent, RiskRatingBadgeComponent,
    RiskFormComponent, HasPermissionDirective,
  ],
  templateUrl: './risk-detail.component.html',
  styleUrl: './risk-detail.component.scss',
})
export class RiskDetailComponent implements OnInit {
  @ViewChild('deleteConfirm') deleteConfirm!: ConfirmDialogComponent;

  private readonly route       = inject(ActivatedRoute);
  private readonly router      = inject(Router);
  private readonly riskService = inject(ProjectRiskService);
  private readonly toast       = inject(ToastService);
  private readonly permState   = inject(PermissionStateService);

  risk       = signal<ProjectRisk | null>(null);
  loading    = signal(true);
  error      = signal('');
  showEdit   = signal(false);

  projectId = '';
  riskId    = '';

  breadcrumbs: BreadcrumbItem[] = [];

  // Matrix cells
  readonly matrixCells = this.buildMatrix();

  ngOnInit(): void {
    this.projectId = this.route.snapshot.paramMap.get('projectId') ?? '';
    this.riskId    = this.route.snapshot.paramMap.get('riskId') ?? '';
    this.loadRisk();
  }

  get canEdit(): boolean {
    return this.permState.hasPermission('Projects', 'Edit');
  }

  get canDelete(): boolean {
    return this.permState.hasPermission('Projects', 'Delete');
  }

  loadRisk(): void {
    this.loading.set(true);
    this.riskService.getRiskById(this.projectId, this.riskId).subscribe({
      next: res => {
        if (res.success) {
          this.risk.set(res.data);
          this.breadcrumbs = [
            { label: 'Projects', route: '/projects/my' },
            { label: res.data.projectName, route: `/projects/${this.projectId}` },
            { label: 'Risk Register', route: `/projects/${this.projectId}` },
            { label: res.data.riskCode },
          ];
        } else {
          this.error.set('Risk not found.');
        }
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Failed to load risk.');
        this.loading.set(false);
      },
    });
  }

  // ── Actions ──────────────────────────────────────────

  openEdit(): void {
    this.showEdit.set(true);
  }

  onEditSaved(updated: ProjectRisk): void {
    this.showEdit.set(false);
    this.risk.set(updated);
    this.loadRisk();
  }

  onEditCancelled(): void {
    this.showEdit.set(false);
  }

  confirmDelete(): void {
    this.deleteConfirm.open();
  }

  onDeleteConfirmed(confirmed: boolean): void {
    if (!confirmed) return;
    this.riskService.deleteRisk(this.projectId, this.riskId).subscribe({
      next: () => {
        this.toast.success('Risk deleted.');
        this.router.navigate(['/projects', this.projectId]);
      },
      error: () => this.toast.error('Failed to delete risk.'),
    });
  }

  goBack(): void {
    this.router.navigate(['/projects', this.projectId]);
  }

  // ── Helpers ──────────────────────────────────────────

  ratingColor(rating: string): string {
    return RATING_COLORS[rating] ?? '#6c757d';
  }

  statusColor(status: string): string {
    return STATUS_COLORS[status] ?? '#6c757d';
  }

  probLabel(score: number): string {
    const labels: Record<number, string> = { 1: 'Rare', 2: 'Unlikely', 3: 'Possible', 4: 'Likely', 5: 'Almost Certain' };
    return labels[score] ?? '';
  }

  impactLabel(score: number): string {
    const labels: Record<number, string> = { 1: 'Negligible', 2: 'Minor', 3: 'Moderate', 4: 'Major', 5: 'Severe' };
    return labels[score] ?? '';
  }

  // ── Matrix ───────────────────────────────────────────

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

  isCurrentCell(prob: number, impact: number): boolean {
    const r = this.risk();
    if (!r) return false;
    return r.probabilityScore === prob && r.impactScore === impact;
  }
}
