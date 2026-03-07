import { Component, OnInit, Input, DestroyRef, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { forkJoin } from 'rxjs';

import { BaselineService } from '../../../services/baseline.service';
import { ProjectService } from '../../../services/project.service';
import { AuthService } from '../../../../../core/services/auth.service';
import {
  ProjectDetail,
  ProjectBaseline,
  BaselineChangeRequest,
  ChangeRequestStatus,
} from '../../../models/project.models';

@Component({
  selector: 'app-baseline-panel',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './baseline-panel.component.html',
  styleUrl: './baseline-panel.component.scss',
})
export class BaselinePanelComponent implements OnInit {
  @Input({ required: true }) project!: ProjectDetail;
  @Input() canBaseline = false;
  @Input() isSponsor = false;

  private readonly baselineService = inject(BaselineService);
  private readonly projectService = inject(ProjectService);
  private readonly authService = inject(AuthService);
  private readonly fb = inject(FormBuilder);
  private readonly destroyRef = inject(DestroyRef);

  baseline: ProjectBaseline | null = null;
  changeRequests: BaselineChangeRequest[] = [];
  isLoading = false;
  errorMessage: string | null = null;
  successMessage: string | null = null;

  showChangeRequestForm = false;
  changeRequestForm!: FormGroup;

  reviewingRequest: BaselineChangeRequest | null = null;
  reviewForm!: FormGroup;

  readonly ChangeRequestStatus = ChangeRequestStatus;

  ngOnInit(): void {
    this.buildForms();
    this.onLoadData();
  }

  // ── Data loading ────────────────────────────────────────────────────────────

  onLoadData(): void {
    this.isLoading = true;
    this.errorMessage = null;

    forkJoin({
      baseline: this.baselineService.getByProject(this.project.id),
      changeRequests: this.baselineService.getChangeRequests(this.project.id),
    })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (result) => {
          const b = (result.baseline as any)?.data ?? result.baseline;
          this.baseline = b ?? null;
          const cr = (result.changeRequests as any)?.data ?? result.changeRequests;
          this.changeRequests = Array.isArray(cr) ? cr : [];
          this.isLoading = false;
        },
        error: (err) => {
          this.errorMessage = err?.error?.message ?? 'Failed to load baseline data.';
          this.isLoading = false;
        },
      });
  }

  // ── Baseline project ────────────────────────────────────────────────────────

  onBaselineProject(): void {
    if (!confirm('Are you sure you want to baseline this project? Planned dates will be locked.')) {
      return;
    }

    this.projectService
      .baselineProject(this.project.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.successMessage = 'Project has been baselined successfully.';
          this.project.isBaselined = true;
          this.onLoadData();
          setTimeout(() => (this.successMessage = null), 4000);
        },
        error: (err) => {
          this.errorMessage = err?.error?.message ?? 'Failed to baseline project.';
        },
      });
  }

  // ── Change request submission ───────────────────────────────────────────────

  get hasPendingRequest(): boolean {
    return this.changeRequests.some(cr => cr.status === ChangeRequestStatus.Pending);
  }

  toggleChangeRequestForm(): void {
    this.showChangeRequestForm = !this.showChangeRequestForm;
    if (this.showChangeRequestForm) {
      this.changeRequestForm.reset({
        changeJustification: '',
        proposedChangesJson: '',
      });
    }
  }

  onSubmitChangeRequest(): void {
    if (this.changeRequestForm.invalid) {
      this.changeRequestForm.markAllAsTouched();
      return;
    }

    const v = this.changeRequestForm.value;
    this.authService.currentUser$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(user => {
        this.baselineService
          .submitChangeRequest({
            projectId: this.project.id,
            requestedByUserName: user?.displayName ?? '',
            changeJustification: v.changeJustification,
            proposedChangesJson: v.proposedChangesJson,
            status: ChangeRequestStatus.Pending,
          })
          .pipe(takeUntilDestroyed(this.destroyRef))
          .subscribe({
            next: () => {
              this.showChangeRequestForm = false;
              this.successMessage = 'Change request submitted successfully.';
              this.onLoadData();
              setTimeout(() => (this.successMessage = null), 4000);
            },
            error: (err) => {
              this.errorMessage = err?.error?.message ?? 'Failed to submit change request.';
            },
          });
      });
  }

  // ── Review (Sponsor) ───────────────────────────────────────────────────────

  onReviewRequest(request: BaselineChangeRequest): void {
    this.reviewingRequest = request;
    this.reviewForm.reset({
      status: ChangeRequestStatus.ApprovedBySponsor,
      reviewNotes: '',
    });
  }

  onCancelReview(): void {
    this.reviewingRequest = null;
  }

  onSubmitReview(): void {
    if (this.reviewForm.invalid || !this.reviewingRequest) {
      this.reviewForm.markAllAsTouched();
      return;
    }

    const v = this.reviewForm.value;
    this.baselineService
      .reviewChangeRequest(this.reviewingRequest.id, {
        status: v.status,
        reviewNotes: v.reviewNotes || undefined,
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.reviewingRequest = null;
          this.successMessage = 'Review submitted successfully.';
          this.onLoadData();
          setTimeout(() => (this.successMessage = null), 4000);
        },
        error: (err) => {
          this.errorMessage = err?.error?.message ?? 'Failed to submit review.';
        },
      });
  }

  // ── Implement (PM/Admin) ────────────────────────────────────────────────────

  onImplementChange(requestId: string): void {
    if (!confirm('Implement this approved change? The baseline dates will be updated.')) {
      return;
    }

    this.baselineService
      .implementChange(requestId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.successMessage = 'Change has been implemented.';
          this.onLoadData();
          setTimeout(() => (this.successMessage = null), 4000);
        },
        error: (err) => {
          this.errorMessage = err?.error?.message ?? 'Failed to implement change.';
        },
      });
  }

  // ── Helpers ─────────────────────────────────────────────────────────────────

  getStatusClass(status: ChangeRequestStatus): string {
    switch (status) {
      case ChangeRequestStatus.Pending:           return 'badge-pending';
      case ChangeRequestStatus.ApprovedBySponsor:  return 'badge-approved';
      case ChangeRequestStatus.Rejected:           return 'badge-rejected';
      case ChangeRequestStatus.Implemented:        return 'badge-implemented';
      default:                                     return 'bg-secondary';
    }
  }

  getStatusLabel(status: ChangeRequestStatus): string {
    switch (status) {
      case ChangeRequestStatus.Pending:           return 'Pending';
      case ChangeRequestStatus.ApprovedBySponsor:  return 'Approved';
      case ChangeRequestStatus.Rejected:           return 'Rejected';
      case ChangeRequestStatus.Implemented:        return 'Implemented';
      default:                                     return 'Unknown';
    }
  }

  hasCrFormError(field: string, error: string): boolean {
    const ctrl = this.changeRequestForm.get(field);
    return !!(ctrl?.touched && ctrl.hasError(error));
  }

  isCrFormInvalid(field: string): boolean {
    const ctrl = this.changeRequestForm.get(field);
    return !!(ctrl?.touched && ctrl.invalid);
  }

  // ── Private ─────────────────────────────────────────────────────────────────

  private buildForms(): void {
    this.changeRequestForm = this.fb.group({
      changeJustification: ['', [Validators.required, Validators.maxLength(2000)]],
      proposedChangesJson: ['', [Validators.required]],
    });

    this.reviewForm = this.fb.group({
      status: [ChangeRequestStatus.ApprovedBySponsor, [Validators.required]],
      reviewNotes: [''],
    });
  }
}
