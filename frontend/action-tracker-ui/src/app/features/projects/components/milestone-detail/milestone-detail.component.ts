import { Component, OnInit, DestroyRef, inject, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { MilestoneService } from '../../services/milestone.service';
import { ProjectService } from '../../services/project.service';
import { ActionItemService } from '../../../../core/services/action-item.service';
import {
  MilestoneResponse,
  MilestoneStatus,
  MilestoneStatusLabels,
} from '../../models/milestone.models';
import {
  ActionItem, ActionItemCreate, ActionItemFilter,
  ActionStatus, ActionPriority, AssignableUser, EscalationInfo,
} from '../../../../core/models/action-item.model';
import { PagedResult } from '../../../../core/models/api-response.model';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { BreadcrumbComponent } from '../../../../shared/components/breadcrumb/breadcrumb.component';
import { CommentsSectionComponent } from '../../../../shared/components/comments-section/comments-section.component';
import { DocumentsSectionComponent } from '../../../../shared/components/documents-section/documents-section.component';

@Component({
  selector: 'app-milestone-detail',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, PageHeaderComponent, BreadcrumbComponent, CommentsSectionComponent, DocumentsSectionComponent],
  templateUrl: './milestone-detail.component.html',
  styleUrl: './milestone-detail.component.scss',
})
export class MilestoneDetailComponent implements OnInit {
  private readonly milestoneService = inject(MilestoneService);
  private readonly projectService = inject(ProjectService);
  private readonly actionService = inject(ActionItemService);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);

  projectId!: string;
  milestoneId!: string;
  milestone: MilestoneResponse | null = null;
  isLoading = false;
  errorMessage: string | null = null;
  successMessage: string | null = null;

  // Workspace context (loaded from project)
  workspaceId: string | null = null;

  readonly MilestoneStatus = MilestoneStatus;
  readonly MilestoneStatusLabels = MilestoneStatusLabels;

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
    this.projectId = this.route.snapshot.paramMap.get('projectId')!;
    this.milestoneId = this.route.snapshot.paramMap.get('milestoneId')!;
    this.loadMilestone();
    this.loadProject();
    this.loadAllUsers();
  }

  private loadMilestone(): void {
    this.isLoading = true;
    this.errorMessage = null;

    this.milestoneService.getById(this.projectId, this.milestoneId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          this.milestone = res.data ?? null;
          this.isLoading = false;
        },
        error: (err) => {
          this.errorMessage = err?.error?.message ?? 'Failed to load milestone details.';
          this.isLoading = false;
        },
      });
  }

  private loadProject(): void {
    this.projectService.getById(this.projectId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          this.workspaceId = res.data?.workspaceId ?? null;
          this.loadActionItems();
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

  // ── Action Items ────────────────────────────────────────
  loadActionItems(): void {
    if (!this.workspaceId) return;
    this.actionLoading = true;
    const filter: ActionItemFilter = {
      milestoneId:    this.milestoneId,
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

  actionNextPage(): void {
    if (this.actionPageNumber < this.actionTotalPages) {
      this.actionPageNumber++;
      this.loadActionItems();
    }
  }

  toggleShowDeleted(): void {
    this.showDeleted = !this.showDeleted;
    this.actionPageNumber = 1;
    this.loadActionItems();
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
    if (!this.actionForm.title.trim() || this.actionForm.assigneeIds.length === 0 || !this.actionForm.dueDate || !this.workspaceId) {
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
      projectId:   this.projectId,
      milestoneId: this.milestoneId,
      isStandalone: false,
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

  // ── Milestone helpers ──────────────────────────────────
  milestoneStatusClass(s: MilestoneStatus): string {
    switch (+s) {
      case MilestoneStatus.NotStarted: return 'badge bg-secondary';
      case MilestoneStatus.InProgress: return 'badge bg-primary';
      case MilestoneStatus.Completed:  return 'badge bg-success';
      case MilestoneStatus.Delayed:    return 'badge bg-warning text-dark';
      case MilestoneStatus.Cancelled:  return 'badge bg-danger';
      default:                         return 'badge bg-light text-dark';
    }
  }

  varianceClass(days: number | null | undefined): string {
    if (days == null) return '';
    if (days > 0) return 'text-danger';
    if (days < 0) return 'text-success';
    return '';
  }

  varianceLabel(days: number | null | undefined): string {
    if (days == null) return '';
    if (days === 0) return 'On schedule';
    if (days > 0) return `${days} day${days > 1 ? 's' : ''} behind`;
    const abs = Math.abs(days);
    return `${abs} day${abs > 1 ? 's' : ''} ahead`;
  }

  // ── Action item helpers ────────────────────────────────
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
