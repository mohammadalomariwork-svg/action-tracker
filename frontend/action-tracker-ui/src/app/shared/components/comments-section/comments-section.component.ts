import {
  Component, OnInit, ChangeDetectionStrategy,
  inject, signal, input,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DatePipe } from '@angular/common';

import { CommentService } from '../../../core/services/comment.service';
import { ToastService } from '../../../core/services/toast.service';
import { CommentInfo } from '../../../core/models/comment.model';

@Component({
  selector: 'app-comments-section',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, DatePipe],
  templateUrl: './comments-section.component.html',
  styleUrl: './comments-section.component.scss',
})
export class CommentsSectionComponent implements OnInit {
  readonly entityType = input.required<string>();
  readonly entityId = input.required<string>();

  private readonly commentSvc = inject(CommentService);
  private readonly toastSvc = inject(ToastService);

  readonly comments = signal<CommentInfo[]>([]);
  readonly loading = signal(false);

  // New comment form
  readonly newComment = signal('');
  readonly newCommentImportant = signal(false);
  readonly submitting = signal(false);

  // Edit comment
  readonly editingCommentId = signal<string | null>(null);
  readonly editCommentContent = signal('');
  readonly editCommentImportant = signal(false);

  ngOnInit(): void {
    this.loadComments();
  }

  private loadComments(): void {
    this.loading.set(true);
    this.commentSvc.getByEntity(this.entityType(), this.entityId()).subscribe({
      next: r => {
        this.comments.set(r.data ?? []);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  submitComment(): void {
    const content = this.newComment().trim();
    if (!content) return;

    this.submitting.set(true);
    this.commentSvc.add(this.entityType(), this.entityId(), {
      content,
      isHighImportance: this.newCommentImportant(),
    }).subscribe({
      next: r => {
        this.comments.update(list => [r.data, ...list]);
        this.newComment.set('');
        this.newCommentImportant.set(false);
        this.submitting.set(false);
        this.toastSvc.success('Comment added.');
      },
      error: () => {
        this.submitting.set(false);
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
    if (!commentId) return;

    this.commentSvc.update(commentId, {
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
    this.commentSvc.delete(commentId).subscribe({
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
}
