import { Component, OnInit, DestroyRef, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { ProjectActionItemService } from '../../services/action-item.service';
import { AuthService } from '../../../../core/services/auth.service';
import { UserService } from '../../../../core/services/user.service';
import { DocumentPanelComponent } from '../documents/document-panel/document-panel.component';
import { CommentPanelComponent } from '../comments/comment-panel/comment-panel.component';
import {
  ActionItemDetail,
  ActionItemStatus,
  ActionItemPriority,
} from '../../models/project.models';
import { UserProfile } from '../../../../core/models/user.model';

@Component({
  selector: 'app-action-item-detail',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    RouterLink,
    DocumentPanelComponent,
    CommentPanelComponent,
  ],
  templateUrl: './action-item-detail.component.html',
  styleUrl: './action-item-detail.component.scss',
})
export class ActionItemDetailComponent implements OnInit {
  private readonly actionItemService = inject(ProjectActionItemService);
  private readonly authService = inject(AuthService);
  private readonly userService = inject(UserService);
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  actionItemId!: number;
  actionItem: ActionItemDetail | null = null;
  users: UserProfile[] = [];
  isLoading = false;
  errorMessage: string | null = null;
  successMessage: string | null = null;

  canEdit = false;
  isEditing = false;
  editForm!: FormGroup;

  readonly ActionItemStatus = ActionItemStatus;
  readonly ActionItemPriority = ActionItemPriority;

  ngOnInit(): void {
    this.actionItemId = Number(this.route.snapshot.paramMap.get('id'));
    this.buildEditForm();
    this.loadActionItem();
    this.loadUsers();
  }

  // ── Computed helpers ────────────────────────────────────────────────────────

  get isOverdue(): boolean {
    if (!this.actionItem) return false;
    if (this.actionItem.status === ActionItemStatus.Completed ||
        this.actionItem.status === ActionItemStatus.Cancelled) return false;
    return new Date(this.actionItem.dueDate) < new Date();
  }

  get assigneeDisplay(): string {
    if (!this.actionItem) return '—';
    if (this.actionItem.isExternalAssignee) {
      const name = this.actionItem.assignedToExternalName ?? 'Unknown';
      const email = this.actionItem.assignedToExternalEmail ?? '';
      return email ? `External: ${name} <${email}>` : `External: ${name}`;
    }
    return this.actionItem.assignedToUserName ?? '—';
  }

  get isExternalAssignee(): boolean {
    return this.editForm.get('isExternalAssignee')?.value ?? false;
  }

  // ── Quick actions ──────────────────────────────────────────────────────────

  onStatusChange(status: ActionItemStatus): void {
    if (!this.actionItem) return;

    this.actionItemService
      .update(this.actionItem.id, { status })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => this.loadActionItem(),
        error: (err) => {
          this.errorMessage = err?.error?.message ?? 'Failed to update status.';
        },
      });
  }

  onProgressChange(value: number): void {
    if (!this.actionItem) return;

    this.actionItemService
      .update(this.actionItem.id, { completionPercentage: value } as any)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => this.loadActionItem(),
        error: (err) => {
          this.errorMessage = err?.error?.message ?? 'Failed to update progress.';
        },
      });
  }

  // ── Edit mode ──────────────────────────────────────────────────────────────

  toggleEdit(): void {
    this.isEditing = !this.isEditing;
    if (this.isEditing && this.actionItem) {
      this.populateEditForm();
    }
  }

  onSave(): void {
    if (this.editForm.invalid) {
      this.editForm.markAllAsTouched();
      return;
    }

    const v = this.editForm.value;
    this.actionItemService
      .update(this.actionItemId, {
        title: v.title,
        description: v.description || undefined,
        priority: v.priority,
        status: v.status,
        plannedStartDate: v.plannedStartDate,
        dueDate: v.dueDate,
        isExternalAssignee: v.isExternalAssignee,
        assignedToUserId: v.isExternalAssignee ? undefined : v.assignedToUserId || undefined,
        assignedToUserName: v.isExternalAssignee ? undefined : v.assignedToUserName || undefined,
        assignedToExternalName: v.isExternalAssignee ? v.assignedToExternalName || undefined : undefined,
        assignedToExternalEmail: v.isExternalAssignee ? v.assignedToExternalEmail || undefined : undefined,
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.isEditing = false;
          this.successMessage = 'Action item updated successfully.';
          this.loadActionItem();
          setTimeout(() => (this.successMessage = null), 3000);
        },
        error: (err) => {
          this.errorMessage = err?.error?.message ?? 'Failed to update action item.';
        },
      });
  }

  onCancelEdit(): void {
    this.isEditing = false;
  }

  // ── Badge helpers ──────────────────────────────────────────────────────────

  getStatusClass(status: ActionItemStatus): string {
    switch (status) {
      case ActionItemStatus.NotStarted: return 'bg-secondary';
      case ActionItemStatus.InProgress: return 'bg-primary';
      case ActionItemStatus.Completed:  return 'bg-success';
      case ActionItemStatus.Deferred:   return 'bg-warning text-dark';
      case ActionItemStatus.Cancelled:  return 'bg-danger';
      default:                          return 'bg-secondary';
    }
  }

  getStatusLabel(status: ActionItemStatus): string {
    return ActionItemStatus[status] ?? 'Unknown';
  }

  getPriorityClass(priority: ActionItemPriority): string {
    switch (priority) {
      case ActionItemPriority.Critical: return 'badge-critical';
      case ActionItemPriority.High:     return 'badge-high';
      case ActionItemPriority.Medium:   return 'badge-medium';
      case ActionItemPriority.Low:      return 'badge-low';
      default:                          return 'bg-secondary';
    }
  }

  getPriorityLabel(priority: ActionItemPriority): string {
    return ActionItemPriority[priority] ?? 'Unknown';
  }

  getPriorityBorderClass(priority: ActionItemPriority): string {
    switch (priority) {
      case ActionItemPriority.Critical: return 'aid-border-critical';
      case ActionItemPriority.High:     return 'aid-border-high';
      case ActionItemPriority.Medium:   return 'aid-border-medium';
      case ActionItemPriority.Low:      return 'aid-border-low';
      default:                          return '';
    }
  }

  getProgressClass(percentage: number): string {
    if (percentage >= 75) return 'bg-success';
    if (percentage >= 40) return 'bg-warning';
    return 'bg-danger';
  }

  hasError(field: string, error: string): boolean {
    const ctrl = this.editForm.get(field);
    return !!(ctrl?.touched && ctrl.hasError(error));
  }

  isInvalid(field: string): boolean {
    const ctrl = this.editForm.get(field);
    return !!(ctrl?.touched && ctrl.invalid);
  }

  // ── Private helpers ─────────────────────────────────────────────────────────

  private buildEditForm(): void {
    this.editForm = this.fb.group({
      title: ['', [Validators.required, Validators.maxLength(300)]],
      description: [''],
      priority: [ActionItemPriority.Medium, [Validators.required]],
      status: [ActionItemStatus.NotStarted, [Validators.required]],
      plannedStartDate: ['', [Validators.required]],
      dueDate: ['', [Validators.required]],
      isExternalAssignee: [false],
      assignedToUserId: [''],
      assignedToUserName: [''],
      assignedToExternalName: [''],
      assignedToExternalEmail: [''],
      completionPercentage: [0],
    });

    this.editForm.get('assignedToUserId')!.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((userId: string) => {
        const user = this.users.find(u => u.id === userId);
        this.editForm.get('assignedToUserName')!.setValue(user?.fullName ?? '');
      });
  }

  private populateEditForm(): void {
    if (!this.actionItem) return;
    const a = this.actionItem;
    this.editForm.patchValue({
      title: a.title,
      description: a.description ?? '',
      priority: a.priority,
      status: a.status,
      plannedStartDate: a.plannedStartDate
        ? new Date(a.plannedStartDate).toISOString().substring(0, 10) : '',
      dueDate: a.dueDate
        ? new Date(a.dueDate).toISOString().substring(0, 10) : '',
      isExternalAssignee: a.isExternalAssignee,
      assignedToUserId: a.assignedToUserId ?? '',
      assignedToUserName: a.assignedToUserName ?? '',
      assignedToExternalName: a.assignedToExternalName ?? '',
      assignedToExternalEmail: a.assignedToExternalEmail ?? '',
      completionPercentage: a.completionPercentage,
    });
  }

  private loadActionItem(): void {
    this.isLoading = true;
    this.errorMessage = null;

    this.actionItemService
      .getById(this.actionItemId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (item) => {
          const data = (item as any)?.data ?? item;
          this.actionItem = data;
          this.resolvePermissions();
          this.isLoading = false;
        },
        error: (err) => {
          this.errorMessage = err?.error?.message ?? 'Failed to load action item.';
          this.isLoading = false;
        },
      });
  }

  private loadUsers(): void {
    this.userService
      .getAll()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          this.users = (res.data ?? []).filter(u => u.isActive);
        },
        error: () => { /* silently fail */ },
      });
  }

  private resolvePermissions(): void {
    this.authService.currentUser$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(user => {
        if (!user || !this.actionItem) return;
        const isCreator = user.email === this.actionItem.createdByUserId;
        const isAdmin = this.authService.hasRole('Admin');
        const isManager = this.authService.hasRole('Manager');
        this.canEdit = isCreator || isAdmin || isManager;
      });
  }
}
