import {
  Component, OnInit, ChangeDetectionStrategy,
  inject, signal, computed,
} from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { DatePipe } from '@angular/common';

import { ActionItemService }     from '../../../core/services/action-item.service';
import { ToastService }          from '../../../core/services/toast.service';

import {
  ActionItem, ActionStatus, ActionPriority, CommentInfo,
} from '../../../core/models/action-item.model';

import { StatusBadgeComponent }   from '../../../shared/components/status-badge/status-badge.component';
import { PriorityBadgeComponent } from '../../../shared/components/priority-badge/priority-badge.component';
import { ProgressBarComponent }   from '../../../shared/components/progress-bar/progress-bar.component';
import { PageHeaderComponent }    from '../../../shared/components/page-header/page-header.component';

@Component({
  selector: 'app-action-detail',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule, RouterLink, DatePipe,
    StatusBadgeComponent, PriorityBadgeComponent,
    ProgressBarComponent, PageHeaderComponent,
  ],
  templateUrl: './action-detail.component.html',
  styleUrl:    './action-detail.component.scss',
})
export class ActionDetailComponent implements OnInit {
  private readonly route      = inject(ActivatedRoute);
  private readonly router     = inject(Router);
  private readonly actionSvc  = inject(ActionItemService);
  private readonly toastSvc   = inject(ToastService);

  readonly item       = signal<ActionItem | null>(null);
  readonly loading    = signal(true);
  readonly comments   = signal<CommentInfo[]>([]);
  readonly loadingComments = signal(false);

  // Comment form
  readonly newComment        = signal('');
  readonly newCommentImportant = signal(false);
  readonly submittingComment = signal(false);

  // Edit comment
  readonly editingCommentId      = signal<string | null>(null);
  readonly editCommentContent    = signal('');
  readonly editCommentImportant  = signal(false);

  readonly ActionStatus   = ActionStatus;
  readonly ActionPriority = ActionPriority;

  readonly workspaceId = computed(() => this.item()?.workspaceId ?? '');

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.loadItem(id);
    this.loadComments(id);
  }

  // ── Data loading ───────────────────────────────────────
  private loadItem(id: string): void {
    this.loading.set(true);
    this.actionSvc.getById(id).subscribe({
      next: r => {
        this.item.set(r.data);
        this.loading.set(false);
      },
      error: () => {
        this.toastSvc.error('Failed to load action item.');
        this.loading.set(false);
      },
    });
  }

  private loadComments(id: string): void {
    this.loadingComments.set(true);
    this.actionSvc.getComments(id).subscribe({
      next: r => {
        this.comments.set(r.data ?? []);
        this.loadingComments.set(false);
      },
      error: () => {
        this.loadingComments.set(false);
      },
    });
  }

  // ── Comment actions ────────────────────────────────────
  submitComment(): void {
    const itemId = this.item()?.id;
    const content = this.newComment().trim();
    if (!itemId || !content) return;

    this.submittingComment.set(true);
    this.actionSvc.addComment(itemId, {
      content,
      isHighImportance: this.newCommentImportant(),
    }).subscribe({
      next: r => {
        this.comments.update(list => [r.data, ...list]);
        this.newComment.set('');
        this.newCommentImportant.set(false);
        this.submittingComment.set(false);
        this.toastSvc.success('Comment added.');
      },
      error: () => {
        this.submittingComment.set(false);
        this.toastSvc.error('Failed to add comment.');
      },
    });
  }

  startEditComment(c: CommentInfo): void {
    this.editingCommentId.set(c.id);
    this.editCommentContent.set(c.content);
    this.editCommentImportant.set(c.isHighImportance);
  }

  cancelEditComment(): void {
    this.editingCommentId.set(null);
  }

  saveEditComment(): void {
    const commentId = this.editingCommentId();
    const itemId = this.item()?.id;
    if (!commentId || !itemId) return;

    this.actionSvc.updateComment(itemId, commentId, {
      content: this.editCommentContent().trim(),
      isHighImportance: this.editCommentImportant(),
    }).subscribe({
      next: r => {
        this.comments.update(list =>
          list.map(c => c.id === commentId ? r.data : c)
        );
        this.editingCommentId.set(null);
        this.toastSvc.success('Comment updated.');
      },
      error: (err) => {
        const msg = err?.status === 403 ? 'You can only edit your own comments.' : 'Failed to update comment.';
        this.toastSvc.error(msg);
      },
    });
  }

  deleteComment(commentId: string): void {
    const itemId = this.item()?.id;
    if (!itemId) return;

    this.actionSvc.deleteComment(itemId, commentId).subscribe({
      next: () => {
        this.comments.update(list => list.filter(c => c.id !== commentId));
        this.toastSvc.success('Comment deleted.');
      },
      error: (err) => {
        const msg = err?.status === 403 ? 'You can only delete your own comments.' : 'Failed to delete comment.';
        this.toastSvc.error(msg);
      },
    });
  }

  // ── Helpers ────────────────────────────────────────────
  assigneeNames(): string {
    return this.item()?.assignees?.map(a => a.fullName).join(', ') || '—';
  }

  dueDateClass(): string {
    const i = this.item();
    if (!i) return '';
    if (i.isOverdue || i.status === ActionStatus.Overdue) return 'due--overdue';
    if (i.daysUntilDue <= 3) return 'due--warning';
    return 'due--ok';
  }
}
