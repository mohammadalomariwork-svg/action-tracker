import { Component, OnInit, DestroyRef, inject, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { WorkspaceService } from '../../services/workspace.service';
import { AuthService } from '../../../../core/services/auth.service';
import { ActionItemService } from '../../../../core/services/action-item.service';
import { Workspace, WorkspaceAdmin, UserDropdownItem } from '../../models/workspace.model';
import {
  ActionItem, ActionItemCreate, ActionItemFilter,
  ActionStatus, ActionPriority, AssignableUser, EscalationInfo,
} from '../../../../core/models/action-item.model';
import { PagedResult } from '../../../../core/models/api-response.model';

@Component({
  selector: 'app-workspace-detail',
  standalone: true,
  imports: [CommonModule, RouterLink, FormsModule, ReactiveFormsModule],
  templateUrl: './workspace-detail.component.html',
  styleUrl: './workspace-detail.component.scss',
})
export class WorkspaceDetailComponent implements OnInit {
  private readonly workspaceService = inject(WorkspaceService);
  private readonly authService      = inject(AuthService);
  private readonly actionService    = inject(ActionItemService);
  private readonly route            = inject(ActivatedRoute);
  private readonly destroyRef       = inject(DestroyRef);

  workspaceId!: string;
  workspace: Workspace | null = null;
  isLoading = false;
  errorMessage: string | null = null;
  successMessage: string | null = null;
  canManage = false;

  // Admin management
  availableUsers: UserDropdownItem[] = [];
  selectedUserId = '';
  isAddingAdmin = false;

  // ── Action Items ─────────────────────────────────────────
  actionItems: ActionItem[] = [];
  actionTotalCount = 0;
  actionPageNumber = 1;
  actionPageSize = 10;
  actionLoading = false;
  allUsers: AssignableUser[] = [];

  // Drawer form
  showActionForm = false;
  editingActionId: string | null = null;
  actionSaving = false;
  actionForm: ActionItemFormData = this.emptyActionForm();
  assigneeDropdownOpen = false;
  assigneeSearchTerm = '';
  editingEscalations: EscalationInfo[] = [];

  // Delete
  deletingActionId: string | null = null;

  // Show deleted toggle
  showDeleted = false;

  // Expose enums to template
  readonly ActionStatus = ActionStatus;
  readonly ActionPriority = ActionPriority;

  readonly STATUS_OPTIONS = [
    { value: ActionStatus.ToDo,       label: 'To Do'       },
    { value: ActionStatus.InProgress, label: 'In Progress' },
    { value: ActionStatus.InReview,   label: 'In Review'   },
    { value: ActionStatus.Done,       label: 'Done'        },
    { value: ActionStatus.Overdue,    label: 'Overdue'     },
  ];

  readonly PRIORITY_OPTIONS = [
    { value: ActionPriority.Low,      label: 'Low'      },
    { value: ActionPriority.Medium,   label: 'Medium'   },
    { value: ActionPriority.High,     label: 'High'     },
    { value: ActionPriority.Critical, label: 'Critical' },
  ];

  private readonly STATUS_MAP: Record<string, ActionStatus> = {
    toDo: ActionStatus.ToDo, inProgress: ActionStatus.InProgress,
    inReview: ActionStatus.InReview, done: ActionStatus.Done, overdue: ActionStatus.Overdue,
  };
  private readonly PRIORITY_MAP: Record<string, ActionPriority> = {
    low: ActionPriority.Low, medium: ActionPriority.Medium,
    high: ActionPriority.High, critical: ActionPriority.Critical,
  };

  private resolveStatus(val: unknown): ActionStatus {
    if (typeof val === 'number') return val;
    return this.STATUS_MAP[String(val)] ?? ActionStatus.ToDo;
  }

  private resolvePriority(val: unknown): ActionPriority {
    if (typeof val === 'number') return val;
    return this.PRIORITY_MAP[String(val)] ?? ActionPriority.Medium;
  }

  get actionTotalPages(): number {
    return Math.ceil(this.actionTotalCount / this.actionPageSize) || 1;
  }

  ngOnInit(): void {
    this.workspaceId = this.route.snapshot.paramMap.get('id')!;
    this.loadData();
  }

  // ── Workspace Data ──────────────────────────────────────
  private loadData(): void {
    this.isLoading = true;
    this.errorMessage = null;

    this.workspaceService.getWorkspaceById(this.workspaceId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          this.workspace = res.data ?? null;
          this.resolvePermissions();
          this.isLoading = false;
          if (this.canManage) {
            this.loadAvailableUsers();
          }
          this.loadActionItems();
          this.loadAllUsers();
        },
        error: (err) => {
          this.errorMessage = err?.error?.message ?? 'Failed to load workspace details.';
          this.isLoading = false;
        },
      });
  }

  private resolvePermissions(): void {
    if (this.authService.hasRole('Admin') || this.authService.hasRole('Manager')) {
      this.canManage = true;
      return;
    }
    this.authService.currentUser$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(user => {
        if (user && this.workspace?.admins?.some(a => a.userId === user.email)) {
          this.canManage = true;
        }
      });
  }

  private loadAvailableUsers(): void {
    this.workspaceService.getActiveUsersForDropdown()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          const allUsers = res.data ?? [];
          const existingIds = new Set(this.workspace?.admins?.map(a => a.userId) ?? []);
          this.availableUsers = allUsers.filter(u => !existingIds.has(u.id));
        },
        error: () => {},
      });
  }

  private loadAllUsers(): void {
    this.actionService.getAssignableUsers()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => this.allUsers = res.data ?? [],
        error: () => {},
      });
  }

  // ── Admin CRUD ──────────────────────────────────────────
  onAddAdmin(): void {
    if (!this.selectedUserId || !this.workspace) return;

    const user = this.availableUsers.find(u => u.id === this.selectedUserId);
    if (!user) return;

    this.isAddingAdmin = true;
    this.errorMessage = null;
    this.successMessage = null;

    const updatedAdmins: WorkspaceAdmin[] = [
      ...(this.workspace.admins ?? []),
      { userId: user.id, userName: user.displayName, email: '', orgUnitName: '' },
    ];

    this.workspaceService.updateWorkspace(this.workspaceId, {
      id: this.workspaceId,
      title: this.workspace.title,
      organizationUnit: this.workspace.organizationUnit,
      admins: updatedAdmins,
      isActive: this.workspace.isActive,
    })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          this.workspace = res.data ?? this.workspace;
          this.selectedUserId = '';
          this.loadAvailableUsers();
          this.successMessage = `${user.displayName} added as admin.`;
          this.isAddingAdmin = false;
        },
        error: (err) => {
          this.errorMessage = err?.error?.message ?? 'Failed to add admin.';
          this.isAddingAdmin = false;
        },
      });
  }

  onRemoveAdmin(admin: WorkspaceAdmin): void {
    if (!this.workspace) return;
    if (!confirm(`Remove ${admin.userName} as admin?`)) return;

    if ((this.workspace.admins?.length ?? 0) <= 1) {
      this.errorMessage = 'A workspace must have at least one admin.';
      return;
    }

    this.errorMessage = null;
    this.successMessage = null;

    const updatedAdmins = this.workspace.admins.filter(a => a.userId !== admin.userId);

    this.workspaceService.updateWorkspace(this.workspaceId, {
      id: this.workspaceId,
      title: this.workspace.title,
      organizationUnit: this.workspace.organizationUnit,
      admins: updatedAdmins,
      isActive: this.workspace.isActive,
    })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          this.workspace = res.data ?? this.workspace;
          this.loadAvailableUsers();
          this.successMessage = `${admin.userName} removed from admins.`;
        },
        error: (err) => {
          this.errorMessage = err?.error?.message ?? 'Failed to remove admin.';
        },
      });
  }

  // ── Action Items ────────────────────────────────────────
  loadActionItems(): void {
    this.actionLoading = true;
    const filter: ActionItemFilter = {
      workspaceId:    this.workspaceId,
      pageNumber:     this.actionPageNumber,
      pageSize:       this.actionPageSize,
      sortBy:         'dueDate',
      sortDescending: false,
      includeDeleted: this.showDeleted || undefined,
    };

    this.actionService.getAll(filter)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          const paged: PagedResult<ActionItem> = res.data;
          this.actionItems = paged.items;
          this.actionTotalCount = paged.totalCount;
          this.actionLoading = false;
        },
        error: () => {
          this.actionLoading = false;
        },
      });
  }

  actionPrevPage(): void {
    if (this.actionPageNumber > 1) {
      this.actionPageNumber--;
      this.loadActionItems();
    }
  }

  toggleShowDeleted(): void {
    this.showDeleted = !this.showDeleted;
    this.actionPageNumber = 1;
    this.loadActionItems();
  }

  actionNextPage(): void {
    if (this.actionPageNumber < this.actionTotalPages) {
      this.actionPageNumber++;
      this.loadActionItems();
    }
  }

  // ── Action Item Form ────────────────────────────────────
  private emptyActionForm(): ActionItemFormData {
    return {
      title: '',
      description: '',
      assigneeIds: [],
      priority: ActionPriority.Medium,
      status: ActionStatus.ToDo,
      startDate: '',
      dueDate: '',
      progress: 0,
      isEscalated: false,
      escalationExplanation: '',
    };
  }

  onStatusChange(): void {
    if (+this.actionForm.status === ActionStatus.Done) {
      this.actionForm.progress = 100;
    }
  }

  openNewActionForm(): void {
    this.editingActionId = null;
    this.actionForm = this.emptyActionForm();
    this.editingEscalations = [];
    this.assigneeDropdownOpen = false;
    this.assigneeSearchTerm = '';
    this.showActionForm = true;
  }

  openEditActionForm(item: ActionItem): void {
    this.editingActionId = item.id;
    this.actionForm = {
      title:       item.title,
      description: item.description,
      assigneeIds: item.assignees.map(a => a.userId),
      priority:    this.resolvePriority(item.priority),
      status:      this.resolveStatus(item.status),
      startDate:   item.startDate ? item.startDate.slice(0, 10) : '',
      dueDate:     item.dueDate.slice(0, 10),
      progress:    item.progress,
      isEscalated: false,
      escalationExplanation: '',
    };
    this.editingEscalations = item.escalations ?? [];
    this.assigneeDropdownOpen = false;
    this.assigneeSearchTerm = '';
    this.showActionForm = true;
  }

  cancelActionForm(): void {
    this.showActionForm = false;
    this.editingActionId = null;
    this.assigneeDropdownOpen = false;
    this.assigneeSearchTerm = '';
  }

  @HostListener('document:click')
  onDocumentClick(): void {
    this.assigneeDropdownOpen = false;
  }

  get filteredUsers(): AssignableUser[] {
    if (!this.assigneeSearchTerm.trim()) return this.allUsers;
    const term = this.assigneeSearchTerm.toLowerCase();
    return this.allUsers.filter(u => u.fullName.toLowerCase().includes(term));
  }

  getAssigneeName(userId: string): string {
    return this.allUsers.find(u => u.id === userId)?.fullName ?? userId;
  }

  toggleAssignee(userId: string): void {
    const idx = this.actionForm.assigneeIds.indexOf(userId);
    if (idx >= 0) {
      this.actionForm.assigneeIds.splice(idx, 1);
    } else {
      this.actionForm.assigneeIds.push(userId);
    }
  }

  isAssigneeSelected(userId: string): boolean {
    return this.actionForm.assigneeIds.includes(userId);
  }

  saveAction(): void {
    if (!this.actionForm.title.trim() || this.actionForm.assigneeIds.length === 0 || !this.actionForm.dueDate) {
      return;
    }
    if (this.actionForm.isEscalated && !this.actionForm.escalationExplanation?.trim()) {
      return;
    }

    this.actionSaving = true;
    const payload: ActionItemCreate = {
      title:       this.actionForm.title.trim(),
      description: this.actionForm.description?.trim() ?? '',
      workspaceId: this.workspaceId,
      assigneeIds: this.actionForm.assigneeIds,
      priority:    +this.actionForm.priority as ActionPriority,
      status:      +this.actionForm.status as ActionStatus,
      startDate:   this.actionForm.startDate || null,
      dueDate:     this.actionForm.dueDate,
      progress:    +this.actionForm.progress,
      isEscalated: !!this.actionForm.isEscalated,
      escalationExplanation: this.actionForm.isEscalated ? this.actionForm.escalationExplanation?.trim() : undefined,
    };

    const obs$ = this.editingActionId
      ? this.actionService.update(this.editingActionId, payload)
      : this.actionService.create(payload);

    obs$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.actionSaving = false;
        this.showActionForm = false;
        this.editingActionId = null;
        this.successMessage = this.editingActionId ? 'Action item updated.' : 'Action item created.';
        this.loadActionItems();
      },
      error: (err) => {
        this.actionSaving = false;
        this.errorMessage = err?.error?.message ?? 'Failed to save action item.';
      },
    });
  }

  deleteAction(item: ActionItem): void {
    if (!confirm(`Delete action "${item.title}"?`)) return;
    this.deletingActionId = item.id;

    this.actionService.delete(item.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.deletingActionId = null;
          this.successMessage = `Action "${item.actionId}" deleted.`;
          this.loadActionItems();
        },
        error: (err) => {
          this.deletingActionId = null;
          this.errorMessage = err?.error?.message ?? 'Failed to delete action item.';
        },
      });
  }

  // ── Helpers ─────────────────────────────────────────────
  assigneeNames(item: ActionItem): string {
    return item.assignees?.map(a => a.fullName).join(', ') || '—';
  }

  assigneeInitial(item: ActionItem): string {
    return item.assignees?.[0]?.fullName?.charAt(0)?.toUpperCase() || '?';
  }

  priorityClass(p: ActionPriority): string {
    switch (+p) {
      case ActionPriority.Critical: return 'badge bg-danger';
      case ActionPriority.High:     return 'badge bg-warning text-dark';
      case ActionPriority.Medium:   return 'badge bg-info text-dark';
      case ActionPriority.Low:      return 'badge bg-secondary';
      default:                      return 'badge bg-light text-dark';
    }
  }

  statusClass(s: ActionStatus): string {
    switch (+s) {
      case ActionStatus.ToDo:       return 'badge bg-secondary';
      case ActionStatus.InProgress: return 'badge bg-primary';
      case ActionStatus.InReview:   return 'badge bg-warning text-dark';
      case ActionStatus.Done:       return 'badge bg-success';
      case ActionStatus.Overdue:    return 'badge bg-danger';
      default:                      return 'badge bg-light text-dark';
    }
  }

  dueDateClass(item: ActionItem): string {
    if (item.isOverdue || item.status === ActionStatus.Overdue) return 'text-danger fw-semibold';
    if (item.daysUntilDue <= 3) return 'text-warning fw-semibold';
    return '';
  }
}

interface ActionItemFormData {
  title: string;
  description: string;
  assigneeIds: string[];
  priority: ActionPriority;
  status: ActionStatus;
  startDate: string;
  dueDate: string;
  progress: number;
  isEscalated: boolean;
  escalationExplanation: string;
}
