import { Component, OnInit, Input, DestroyRef, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { CommentService } from '../../../services/comment.service';
import { AuthService } from '../../../../../core/services/auth.service';
import { Comment } from '../../../models/project.models';

@Component({
  selector: 'app-comment-panel',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './comment-panel.component.html',
  styleUrl: './comment-panel.component.scss',
})
export class CommentPanelComponent implements OnInit {
  @Input() projectId?: number;
  @Input() milestoneId?: number;
  @Input() actionItemId?: number;

  private readonly commentService = inject(CommentService);
  private readonly authService = inject(AuthService);
  private readonly destroyRef = inject(DestroyRef);

  comments: Comment[] = [];
  isLoading = false;
  isSubmitting = false;
  errorMessage: string | null = null;

  newCommentContent = '';
  editingCommentId: number | null = null;
  editContent = '';

  currentUserId = '';
  currentUserName = '';

  ngOnInit(): void {
    this.authService.currentUser$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(user => {
        this.currentUserId = user?.email ?? '';
        this.currentUserName = user?.displayName ?? '';
      });

    this.onLoadComments();
  }

  // ── Data loading ────────────────────────────────────────────────────────────

  onLoadComments(): void {
    this.isLoading = true;
    this.errorMessage = null;

    let source$;
    if (this.actionItemId) {
      source$ = this.commentService.getByActionItem(this.actionItemId);
    } else if (this.milestoneId) {
      source$ = this.commentService.getByMilestone(this.milestoneId);
    } else if (this.projectId) {
      source$ = this.commentService.getByProject(this.projectId);
    } else {
      this.isLoading = false;
      return;
    }

    source$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (comments) => {
        const data = (comments as any)?.data ?? comments;
        this.comments = Array.isArray(data) ? data : [];
        this.isLoading = false;
      },
      error: (err) => {
        this.errorMessage = err?.error?.message ?? 'Failed to load comments.';
        this.isLoading = false;
      },
    });
  }

  // ── Submit new comment ──────────────────────────────────────────────────────

  onSubmitComment(): void {
    const content = this.newCommentContent.trim();
    if (!content) return;

    this.isSubmitting = true;

    this.commentService
      .create({
        content,
        authorUserId: this.currentUserId,
        authorUserName: this.currentUserName,
        projectId: this.projectId,
        milestoneId: this.milestoneId,
        actionItemId: this.actionItemId,
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.newCommentContent = '';
          this.isSubmitting = false;
          this.onLoadComments();
        },
        error: (err) => {
          this.errorMessage = err?.error?.message ?? 'Failed to post comment.';
          this.isSubmitting = false;
        },
      });
  }

  // ── Edit comment ────────────────────────────────────────────────────────────

  onEditComment(comment: Comment): void {
    this.editingCommentId = comment.id;
    this.editContent = comment.content;
  }

  onCancelEdit(): void {
    this.editingCommentId = null;
    this.editContent = '';
  }

  onSaveEdit(id: number): void {
    const content = this.editContent.trim();
    if (!content) return;

    this.commentService
      .update(id, content)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.editingCommentId = null;
          this.editContent = '';
          this.onLoadComments();
        },
        error: (err) => {
          this.errorMessage = err?.error?.message ?? 'Failed to update comment.';
        },
      });
  }

  // ── Delete comment ──────────────────────────────────────────────────────────

  onDeleteComment(id: number): void {
    if (!confirm('Are you sure you want to delete this comment?')) return;

    this.commentService
      .delete(id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => this.onLoadComments(),
        error: (err) => {
          this.errorMessage = err?.error?.message ?? 'Failed to delete comment.';
        },
      });
  }

  // ── Helpers ─────────────────────────────────────────────────────────────────

  isOwnComment(comment: Comment): boolean {
    return comment.authorUserId === this.currentUserId;
  }

  getInitials(name: string): string {
    if (!name) return '?';
    const parts = name.trim().split(/\s+/);
    if (parts.length === 1) return parts[0][0].toUpperCase();
    return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
  }

  getAvatarColor(name: string): string {
    let hash = 0;
    for (let i = 0; i < name.length; i++) {
      hash = name.charCodeAt(i) + ((hash << 5) - hash);
    }
    const colors = [
      '#4f46e5', '#7c3aed', '#db2777', '#dc2626',
      '#ea580c', '#d97706', '#16a34a', '#0d9488',
      '#0284c7', '#2563eb', '#6366f1', '#9333ea',
    ];
    return colors[Math.abs(hash) % colors.length];
  }

  formatRelativeTime(date: Date | string): string {
    const now = new Date();
    const then = new Date(date);
    const diffMs = now.getTime() - then.getTime();
    const diffSec = Math.floor(diffMs / 1000);
    const diffMin = Math.floor(diffSec / 60);
    const diffHour = Math.floor(diffMin / 60);
    const diffDay = Math.floor(diffHour / 24);

    if (diffSec < 60) return 'just now';
    if (diffMin < 60) return `${diffMin} minute${diffMin === 1 ? '' : 's'} ago`;
    if (diffHour < 24) return `${diffHour} hour${diffHour === 1 ? '' : 's'} ago`;
    if (diffDay < 30) return `${diffDay} day${diffDay === 1 ? '' : 's'} ago`;
    return then.toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
  }
}
