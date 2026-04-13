import { Component, OnInit, OnDestroy, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { Subject, takeUntil } from 'rxjs';
import { formatDistanceToNow } from 'date-fns';

import { WorkflowService } from '../../../services/workflow.service';
import { WorkflowStateService } from '../../../services/workflow-state.service';
import { ProjectWorkflowService } from '../../../services/project-workflow.service';
import {
  WorkflowRequest,
  WorkflowRequestSummary,
  WORKFLOW_STATUS_CONFIG,
  WORKFLOW_TYPE_LABELS,
} from '../../../models/workflow.model';
import { ProjectApprovalRequest } from '../../../features/projects/models/project-approval.models';
import { PagedResult } from '../../../core/models/api-response.model';
import { ToastService } from '../../../core/services/toast.service';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { BreadcrumbComponent } from '../../../shared/components/breadcrumb/breadcrumb.component';

@Component({
  selector: 'app-my-approvals',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule, PageHeaderComponent, BreadcrumbComponent],
  templateUrl: './my-approvals.component.html',
  styleUrls: ['./my-approvals.component.scss'],
})
export class MyApprovalsComponent implements OnInit, OnDestroy {
  private readonly workflowSvc = inject(WorkflowService);
  private readonly workflowState = inject(WorkflowStateService);
  private readonly projectWorkflowSvc = inject(ProjectWorkflowService);
  private readonly toastSvc = inject(ToastService);
  private readonly destroy$ = new Subject<void>();

  // ── Tab state ──────────────────────────────────────────
  readonly activeTab = signal<'pending' | 'my' | 'projects'>('pending');

  // ── Data signals ───────────────────────────────────────
  readonly pendingReviews = signal<WorkflowRequest[]>([]);
  readonly myRequests = signal<WorkflowRequest[]>([]);
  readonly summary = signal<WorkflowRequestSummary>({
    pendingDateChanges: 0,
    pendingStatusChanges: 0,
    totalPending: 0,
  });

  // ── Pagination ─────────────────────────────────────────
  readonly page = signal(1);
  readonly pageSize = signal(20);
  readonly totalCount = signal(0);
  readonly totalPages = computed(() => Math.ceil(this.totalCount() / this.pageSize()) || 1);
  readonly showingFrom = computed(() =>
    this.totalCount() === 0 ? 0 : (this.page() - 1) * this.pageSize() + 1
  );
  readonly showingTo = computed(() =>
    Math.min(this.page() * this.pageSize(), this.totalCount())
  );

  // ── Project approval signals ────────────────────────────
  readonly projectPendingReviews = signal<ProjectApprovalRequest[]>([]);
  readonly projectMyRequests = signal<ProjectApprovalRequest[]>([]);
  readonly projectPendingCount = signal(0);

  // ── Loading / UI state ─────────────────────────────────
  readonly isLoading = signal(false);
  readonly summaryLoading = signal(false);
  readonly selectedRequest = signal<WorkflowRequest | null>(null);
  readonly reviewComment = signal('');
  readonly showReviewModal = signal(false);
  readonly isApproving = signal(true);
  readonly isSubmitting = signal(false);
  readonly selectedProjectRequest = signal<ProjectApprovalRequest | null>(null);
  readonly showProjectReviewModal = signal(false);
  readonly projectReviewComment = signal('');
  readonly isProjectApproving = signal(true);
  readonly isProjectSubmitting = signal(false);

  // ── Template constants ─────────────────────────────────
  readonly WORKFLOW_STATUS_CONFIG = WORKFLOW_STATUS_CONFIG;
  readonly WORKFLOW_TYPE_LABELS = WORKFLOW_TYPE_LABELS;
  readonly PAGE_SIZE_OPTIONS = [10, 20, 50];

  // ── Lifecycle ──────────────────────────────────────────
  ngOnInit(): void {
    this.loadSummary();
    this.loadPendingReviews();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  // ── Data loading ───────────────────────────────────────
  loadSummary(): void {
    this.summaryLoading.set(true);
    this.workflowSvc.getPendingSummary().pipe(takeUntil(this.destroy$)).subscribe({
      next: r => {
        this.summary.set(r.data ?? { pendingDateChanges: 0, pendingStatusChanges: 0, totalPending: 0 });
        this.summaryLoading.set(false);
      },
      error: () => this.summaryLoading.set(false),
    });
  }

  loadPendingReviews(): void {
    this.isLoading.set(true);
    this.workflowSvc.getPendingReviews(this.page(), this.pageSize())
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: r => {
          const paged: PagedResult<WorkflowRequest> = r.data;
          this.pendingReviews.set(paged.items);
          this.totalCount.set(paged.totalCount);
          this.isLoading.set(false);
        },
        error: () => {
          this.isLoading.set(false);
          this.toastSvc.error('Failed to load pending reviews.');
        },
      });
  }

  loadMyRequests(): void {
    this.isLoading.set(true);
    this.workflowSvc.getMyRequests(this.page(), this.pageSize())
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: r => {
          const paged: PagedResult<WorkflowRequest> = r.data;
          this.myRequests.set(paged.items);
          this.totalCount.set(paged.totalCount);
          this.isLoading.set(false);
        },
        error: () => {
          this.isLoading.set(false);
          this.toastSvc.error('Failed to load your requests.');
        },
      });
  }

  // ── Tab switching ──────────────────────────────────────
  switchTab(tab: 'pending' | 'my' | 'projects'): void {
    if (this.activeTab() === tab) return;
    this.activeTab.set(tab);
    this.page.set(1);
    if (tab === 'pending') {
      this.loadPendingReviews();
    } else if (tab === 'my') {
      this.loadMyRequests();
    } else {
      this.loadProjectApprovals();
    }
  }

  // ── Review modal ───────────────────────────────────────
  openReview(request: WorkflowRequest, isApprove: boolean): void {
    this.selectedRequest.set(request);
    this.isApproving.set(isApprove);
    this.reviewComment.set('');
    this.showReviewModal.set(true);
  }

  closeReviewModal(): void {
    this.showReviewModal.set(false);
    this.selectedRequest.set(null);
    this.reviewComment.set('');
  }

  submitReview(): void {
    const request = this.selectedRequest();
    if (!request) return;

    this.isSubmitting.set(true);
    this.workflowSvc.reviewRequest(request.id, {
      isApproved: this.isApproving(),
      reviewComment: this.reviewComment() || null,
    }).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => {
        const action = this.isApproving() ? 'approved' : 'rejected';
        this.toastSvc.success(`Request ${action} successfully.`);
        this.isSubmitting.set(false);
        this.closeReviewModal();
        this.loadSummary();
        this.loadPendingReviews();
        this.workflowState.refreshPendingCount();
      },
      error: err => {
        this.isSubmitting.set(false);
        this.toastSvc.error(err?.error?.message ?? 'Failed to submit review.');
      },
    });
  }

  // ── Pagination ─────────────────────────────────────────
  prevPage(): void {
    if (this.page() > 1) {
      this.page.update(p => p - 1);
      this.loadCurrentTab();
    }
  }

  nextPage(): void {
    if (this.page() < this.totalPages()) {
      this.page.update(p => p + 1);
      this.loadCurrentTab();
    }
  }

  onPageSizeChange(size: number): void {
    this.pageSize.set(+size);
    this.page.set(1);
    this.loadCurrentTab();
  }

  // ── Project approvals ──────────────────────────────────
  loadProjectApprovals(): void {
    this.projectWorkflowSvc.getPendingReviews().pipe(takeUntil(this.destroy$)).subscribe({
      next: r => { if (r.success) this.projectPendingReviews.set(r.data); },
      error: () => {},
    });
    this.projectWorkflowSvc.getMyRequests().pipe(takeUntil(this.destroy$)).subscribe({
      next: r => { if (r.success) this.projectMyRequests.set(r.data); },
      error: () => {},
    });
    this.projectWorkflowSvc.getPendingSummary().pipe(takeUntil(this.destroy$)).subscribe({
      next: r => { if (r.success) this.projectPendingCount.set(r.data.pendingProjectApprovals); },
      error: () => {},
    });
  }

  openProjectReview(request: ProjectApprovalRequest, isApprove: boolean): void {
    this.selectedProjectRequest.set(request);
    this.isProjectApproving.set(isApprove);
    this.projectReviewComment.set('');
    this.showProjectReviewModal.set(true);
  }

  closeProjectReviewModal(): void {
    this.showProjectReviewModal.set(false);
    this.selectedProjectRequest.set(null);
    this.projectReviewComment.set('');
  }

  submitProjectReview(): void {
    const request = this.selectedProjectRequest();
    if (!request) return;
    this.isProjectSubmitting.set(true);
    this.projectWorkflowSvc.reviewApprovalRequest(request.id, {
      requestId: request.id,
      isApproved: this.isProjectApproving(),
      reviewComment: this.projectReviewComment() || null,
    }).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => {
        const action = this.isProjectApproving() ? 'approved' : 'rejected';
        this.toastSvc.success(`Project ${action} successfully.`);
        this.isProjectSubmitting.set(false);
        this.closeProjectReviewModal();
        this.loadProjectApprovals();
        this.workflowState.refreshPendingCount();
      },
      error: err => {
        this.isProjectSubmitting.set(false);
        this.toastSvc.error(err?.error?.message ?? 'Failed to submit review.');
      },
    });
  }

  projectApprovalStatusClass(status: string): string {
    switch (status) {
      case 'Pending':  return 'badge bg-warning text-dark';
      case 'Approved': return 'badge bg-success';
      case 'Rejected': return 'badge bg-danger';
      default:         return 'badge bg-secondary';
    }
  }

  // ── Helpers ────────────────────────────────────────────
  formatTime(date: string): string {
    if (!date) return '';
    try {
      return formatDistanceToNow(new Date(date), { addSuffix: true });
    } catch {
      return date;
    }
  }

  getStatusConfig(status: string): { label: string; cssClass: string } {
    return WORKFLOW_STATUS_CONFIG[status] ?? { label: status, cssClass: 'bg-secondary' };
  }

  getTypeLabel(type: string): string {
    return WORKFLOW_TYPE_LABELS[type] ?? type;
  }

  trackById(_: number, item: WorkflowRequest): string {
    return item.id;
  }

  private loadCurrentTab(): void {
    if (this.activeTab() === 'pending') {
      this.loadPendingReviews();
    } else {
      this.loadMyRequests();
    }
  }
}
