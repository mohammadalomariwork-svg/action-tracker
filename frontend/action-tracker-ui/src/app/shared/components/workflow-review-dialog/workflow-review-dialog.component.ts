import { Component, input, output, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import {
  WorkflowRequest,
  ReviewWorkflowRequest,
  WORKFLOW_STATUS_CONFIG,
  WORKFLOW_TYPE_LABELS,
} from '../../../models/workflow.model';

@Component({
  selector: 'app-workflow-review-dialog',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, FormsModule],
  templateUrl: './workflow-review-dialog.component.html',
  styleUrl: './workflow-review-dialog.component.scss',
})
export class WorkflowReviewDialogComponent {
  readonly request = input.required<WorkflowRequest | null>();
  readonly visible = input.required<boolean>();

  readonly reviewed = output<ReviewWorkflowRequest>();
  readonly closed = output<void>();

  readonly reviewComment = signal('');
  readonly submitting = signal(false);

  readonly WORKFLOW_STATUS_CONFIG = WORKFLOW_STATUS_CONFIG;
  readonly WORKFLOW_TYPE_LABELS = WORKFLOW_TYPE_LABELS;

  approve(): void {
    this.submitting.set(true);
    this.reviewed.emit({
      isApproved: true,
      reviewComment: this.reviewComment().trim() || null,
    });
  }

  reject(): void {
    if (!this.reviewComment().trim()) return;
    this.submitting.set(true);
    this.reviewed.emit({
      isApproved: false,
      reviewComment: this.reviewComment().trim(),
    });
  }

  close(): void {
    this.reviewComment.set('');
    this.submitting.set(false);
    this.closed.emit();
  }

  resetState(): void {
    this.reviewComment.set('');
    this.submitting.set(false);
  }
}
