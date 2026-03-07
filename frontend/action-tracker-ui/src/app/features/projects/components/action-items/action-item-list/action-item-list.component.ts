import { Component, OnInit, Input, DestroyRef, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { ProjectActionItemService } from '../../../services/action-item.service';
import { UserService } from '../../../../../core/services/user.service';
import { AuthService } from '../../../../../core/services/auth.service';
import {
  ActionItemList,
  ActionItemDetail,
  ActionItemStatus,
  ActionItemPriority,
} from '../../../models/project.models';
import { UserProfile } from '../../../../../core/models/user.model';

@Component({
  selector: 'app-action-item-list',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './action-item-list.component.html',
  styleUrl: './action-item-list.component.scss',
})
export class ActionItemListComponent implements OnInit {
  @Input() workspaceId?: number;
  @Input() projectId?: number;
  @Input() milestoneId?: number;
  @Input() canEdit = false;
  @Input() isCompact = false;

  private readonly actionItemService = inject(ProjectActionItemService);
  private readonly userService = inject(UserService);
  private readonly authService = inject(AuthService);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  actions: ActionItemList[] = [];
  users: UserProfile[] = [];
  isLoading = false;
  errorMessage: string | null = null;

  showForm = false;
  editingAction: ActionItemDetail | null = null;
  form!: FormGroup;

  readonly ActionItemStatus = ActionItemStatus;
  readonly ActionItemPriority = ActionItemPriority;

  ngOnInit(): void {
    this.buildForm();
    this.loadActions();
    this.loadUsers();
    this.setupAssigneeToggle();
  }

  // ── Actions ─────────────────────────────────────────────────────────────────

  onAddAction(): void {
    this.editingAction = null;
    this.showForm = !this.showForm;
    if (this.showForm) {
      this.form.reset({
        title: '',
        description: '',
        priority: ActionItemPriority.Medium,
        status: ActionItemStatus.NotStarted,
        plannedStartDate: '',
        dueDate: '',
        isExternalAssignee: false,
        assignedToUserId: '',
        assignedToUserName: '',
        assignedToExternalName: '',
        assignedToExternalEmail: '',
        completionPercentage: 0,
      });
    }
  }

  onEditAction(action: ActionItemList): void {
    this.actionItemService
      .getById(action.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (detail) => {
          const d = (detail as any)?.data ?? detail;
          this.editingAction = d;
          this.showForm = true;
          this.form.patchValue({
            title: d.title,
            description: d.description ?? '',
            priority: d.priority,
            status: d.status,
            plannedStartDate: d.plannedStartDate
              ? new Date(d.plannedStartDate).toISOString().substring(0, 10)
              : '',
            dueDate: d.dueDate
              ? new Date(d.dueDate).toISOString().substring(0, 10)
              : '',
            isExternalAssignee: d.isExternalAssignee,
            assignedToUserId: d.assignedToUserId ?? '',
            assignedToUserName: d.assignedToUserName ?? '',
            assignedToExternalName: d.assignedToExternalName ?? '',
            assignedToExternalEmail: d.assignedToExternalEmail ?? '',
            completionPercentage: d.completionPercentage,
          });
        },
        error: (err) => {
          this.errorMessage = err?.error?.message ?? 'Failed to load action item details.';
        },
      });
  }

  onDeleteAction(id: number): void {
    if (!confirm('Are you sure you want to delete this action item?')) return;

    this.actionItemService
      .delete(id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => this.loadActions(),
        error: (err) => {
          this.errorMessage = err?.error?.message ?? 'Failed to delete action item.';
        },
      });
  }

  onSave(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const v = this.form.value;

    if (this.editingAction) {
      this.actionItemService
        .update(this.editingAction.id, {
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
            this.showForm = false;
            this.editingAction = null;
            this.loadActions();
          },
          error: (err) => {
            this.errorMessage = err?.error?.message ?? 'Failed to update action item.';
          },
        });
    } else {
      this.authService.currentUser$
        .pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe(user => {
          this.actionItemService
            .create({
              workspaceId: this.workspaceId ?? 0,
              projectId: this.projectId,
              milestoneId: this.milestoneId,
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
              createdByUserId: user?.email ?? '',
            })
            .pipe(takeUntilDestroyed(this.destroyRef))
            .subscribe({
              next: () => {
                this.showForm = false;
                this.loadActions();
              },
              error: (err) => {
                this.errorMessage = err?.error?.message ?? 'Failed to create action item.';
              },
            });
        });
    }
  }

  onCancelForm(): void {
    this.showForm = false;
    this.editingAction = null;
  }

  onViewDetail(id: number): void {
    this.router.navigate(['/action-items', id]);
  }

  // ── Helpers ─────────────────────────────────────────────────────────────────

  isOverdue(action: ActionItemList): boolean {
    if (action.status === ActionItemStatus.Completed || action.status === ActionItemStatus.Cancelled) {
      return false;
    }
    return new Date(action.dueDate) < new Date();
  }

  getAssigneeDisplay(action: ActionItemList): string {
    if (action.isExternalAssignee) {
      return action.assignedToExternalName
        ? `${action.assignedToExternalName} (External)`
        : '—';
    }
    return action.assignedToUserName ?? '—';
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

  getProgressClass(percentage: number): string {
    if (percentage >= 75) return 'bg-success';
    if (percentage >= 40) return 'bg-warning';
    return 'bg-danger';
  }

  get isExternalAssignee(): boolean {
    return this.form.get('isExternalAssignee')?.value ?? false;
  }

  hasError(field: string, error: string): boolean {
    const ctrl = this.form.get(field);
    return !!(ctrl?.touched && ctrl.hasError(error));
  }

  isInvalid(field: string): boolean {
    const ctrl = this.form.get(field);
    return !!(ctrl?.touched && ctrl.invalid);
  }

  // ── Private ─────────────────────────────────────────────────────────────────

  private buildForm(): void {
    this.form = this.fb.group({
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
  }

  private setupAssigneeToggle(): void {
    this.form.get('isExternalAssignee')!.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((isExternal: boolean) => {
        if (isExternal) {
          this.form.patchValue({
            assignedToUserId: '',
            assignedToUserName: '',
          });
        } else {
          this.form.patchValue({
            assignedToExternalName: '',
            assignedToExternalEmail: '',
          });
        }
      });

    // Auto-fill user name when user ID changes
    this.form.get('assignedToUserId')!.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((userId: string) => {
        const user = this.users.find(u => u.id === userId);
        this.form.get('assignedToUserName')!.setValue(user?.fullName ?? '');
      });
  }

  private loadActions(): void {
    this.isLoading = true;
    this.errorMessage = null;

    let source$;
    if (this.milestoneId) {
      source$ = this.actionItemService.getByMilestone(this.milestoneId);
    } else if (this.projectId) {
      source$ = this.actionItemService.getByProject(this.projectId);
    } else if (this.workspaceId) {
      source$ = this.actionItemService.getStandaloneByWorkspace(this.workspaceId);
    } else {
      this.isLoading = false;
      return;
    }

    source$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (actions) => {
        const data = (actions as any)?.data ?? actions;
        this.actions = Array.isArray(data) ? data : [];
        this.isLoading = false;
      },
      error: (err) => {
        this.errorMessage = err?.error?.message ?? 'Failed to load action items.';
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
        error: () => { /* silently fail — users dropdown will be empty */ },
      });
  }
}
