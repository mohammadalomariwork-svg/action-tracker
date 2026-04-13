import {
  Component, OnInit, ChangeDetectionStrategy,
  inject, signal, input, computed,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { HasPermissionDirective } from '../../../../shared';

import { ProjectPhase, ProjectPhaseLabels, ProjectPhaseFromApi } from '../../models/milestone.models';
import { MilestoneService } from '../../services/milestone.service';
import { ToastService } from '../../../../core/services/toast.service';
import { ProjectService } from '../../services/project.service';
import { ActionItemService } from '../../../../core/services/action-item.service';
import {
  MilestoneResponse,
  MilestoneCreate,
  MilestoneUpdate,
  MilestoneStatus,
  MilestoneStatusLabels,
  MilestoneStatusFromApi,
} from '../../models/milestone.models';
import {
  AssignableUser, ActionItem, ActionItemCreate, ActionStatus, ActionPriority,
} from '../../../../core/models/action-item.model';
import { PagedResult } from '../../../../core/models/api-response.model';

@Component({
  selector: 'app-milestone-section',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, FormsModule, RouterLink, HasPermissionDirective],
  templateUrl: './milestone-section.component.html',
  styleUrl: './milestone-section.component.scss',
})
export class MilestoneSectionComponent implements OnInit {
  readonly projectId = input.required<string>();
  readonly workspaceId = input<string>('');
  readonly isBaselined = input<boolean>(false);
  readonly projectStatus = input<string>('draft');

  private readonly milestoneSvc       = inject(MilestoneService);
  private readonly projectSvc         = inject(ProjectService);
  private readonly toastSvc           = inject(ToastService);
  private readonly actionItemSvc      = inject(ActionItemService);

  readonly isProjectFrozen = computed(() => this.projectStatus() !== 'draft');

  readonly milestones = signal<MilestoneResponse[]>([]);
  readonly loading = signal(false);
  readonly users = signal<AssignableUser[]>([]);

  // Form state
  readonly showForm = signal(false);
  readonly editingId = signal<string | null>(null);
  readonly submitting = signal(false);

  // Expand / action-item state
  readonly expandedIds        = signal<Set<string>>(new Set());
  readonly actionsByMilestone = signal<Record<string, ActionItem[]>>({});
  readonly loadingActionIds   = signal<Set<string>>(new Set());

  // Form model
  readonly formName = signal('');
  readonly formDescription = signal('');
  readonly formSequenceOrder = signal(1);
  readonly formPlannedStartDate = signal('');
  readonly formPlannedDueDate = signal('');
  readonly formActualCompletionDate = signal('');
  readonly formPhase = signal<ProjectPhase>(ProjectPhase.Initiation);
  readonly formIsDeadlineFixed = signal(false);
  readonly formStatus = signal<MilestoneStatus>(MilestoneStatus.NotStarted);
  readonly formCompletionPercentage = signal(0);
  readonly formApproverUserId = signal('');

  readonly ProjectPhase = ProjectPhase;
  readonly ProjectPhaseLabels = ProjectPhaseLabels;
  readonly phaseOptions = [
    ProjectPhase.Initiation,
    ProjectPhase.Planning,
    ProjectPhase.Execution,
    ProjectPhase.MonitoringAndControlling,
    ProjectPhase.Closing,
  ];
  readonly MilestoneStatus = MilestoneStatus;
  readonly MilestoneStatusLabels = MilestoneStatusLabels;
  readonly statusOptions = [
    MilestoneStatus.NotStarted,
    MilestoneStatus.InProgress,
    MilestoneStatus.Completed,
    MilestoneStatus.Delayed,
    MilestoneStatus.Cancelled,
  ];

  // ── Action Item form state ─────────────────────────────
  readonly showActionForm = signal(false);
  readonly actionSubmitting = signal(false);
  readonly aiTitle = signal('');
  readonly aiDescription = signal('');
  readonly aiMilestoneId = signal('');
  readonly aiPriority = signal<ActionPriority>(ActionPriority.Medium);
  readonly aiStartDate = signal('');
  readonly aiDueDate = signal('');
  readonly aiAssigneeIds = signal<string[]>([]);
  actionAssigneeDropdownOpen = false;
  actionAssigneeSearchTerm = '';
  actionFormError: string | null = null;

  readonly ActionPriority = ActionPriority;
  readonly PRIORITY_OPTIONS = [
    { value: ActionPriority.Low,      label: 'Low' },
    { value: ActionPriority.Medium,   label: 'Medium' },
    { value: ActionPriority.High,     label: 'High' },
    { value: ActionPriority.Critical, label: 'Critical' },
  ];

  // Search & sort
  searchTerm = '';
  sortField: 'sequenceOrder' | 'name' | 'plannedStartDate' | 'plannedDueDate' = 'sequenceOrder';
  sortDirection: 'asc' | 'desc' = 'asc';

  // Pagination
  currentPage = 1;
  pageSize = 10;
  totalPages = 1;
  filteredMilestones: MilestoneResponse[] = [];
  pagedMilestones: MilestoneResponse[] = [];

  ngOnInit(): void {
    this.loadMilestones();
    this.loadUsers();
  }

  private loadMilestones(): void {
    this.loading.set(true);
    this.milestoneSvc.getByProject(this.projectId()).subscribe({
      next: r => {
        const all = r.data ?? [];
        this.milestones.set(all);
        this.applyFilters();
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.toastSvc.error('Failed to load milestones.');
      },
    });
  }

  private loadUsers(): void {
    this.projectSvc.getAssignableUsers().subscribe({
      next: r => this.users.set(r.data ?? []),
      error: () => {},
    });
  }

  // ── Search, Sort, Pagination ────────────────────────────────────────────────

  applyFilters(): void {
    let result = [...this.milestones()];

    // Search
    if (this.searchTerm.trim()) {
      const term = this.searchTerm.toLowerCase();
      result = result.filter(m =>
        m.name.toLowerCase().includes(term) ||
        m.milestoneCode.toLowerCase().includes(term) ||
        (m.description ?? '').toLowerCase().includes(term) ||
        (m.statusLabel ?? '').toLowerCase().includes(term)
      );
    }

    // Sort
    result.sort((a, b) => {
      let cmp = 0;
      if (this.sortField === 'sequenceOrder') {
        cmp = a.sequenceOrder - b.sequenceOrder;
      } else if (this.sortField === 'name') {
        cmp = a.name.localeCompare(b.name);
      } else if (this.sortField === 'plannedStartDate') {
        cmp = new Date(a.plannedStartDate).getTime() - new Date(b.plannedStartDate).getTime();
      } else if (this.sortField === 'plannedDueDate') {
        cmp = new Date(a.plannedDueDate).getTime() - new Date(b.plannedDueDate).getTime();
      }
      return this.sortDirection === 'asc' ? cmp : -cmp;
    });

    this.filteredMilestones = result;
    this.totalPages = Math.max(1, Math.ceil(result.length / this.pageSize));
    if (this.currentPage > this.totalPages) this.currentPage = 1;
    this.updatePage();
  }

  updatePage(): void {
    const start = (this.currentPage - 1) * this.pageSize;
    this.pagedMilestones = this.filteredMilestones.slice(start, start + this.pageSize);
  }

  onSearchChange(): void {
    this.currentPage = 1;
    this.applyFilters();
  }

  toggleSort(field: 'sequenceOrder' | 'name' | 'plannedStartDate' | 'plannedDueDate'): void {
    if (this.sortField === field) {
      this.sortDirection = this.sortDirection === 'asc' ? 'desc' : 'asc';
    } else {
      this.sortField = field;
      this.sortDirection = 'asc';
    }
    this.applyFilters();
  }

  goToPage(page: number): void {
    if (page < 1 || page > this.totalPages) return;
    this.currentPage = page;
    this.updatePage();
  }

  get pageStart(): number {
    return (this.currentPage - 1) * this.pageSize + 1;
  }

  get pageEnd(): number {
    return Math.min(this.currentPage * this.pageSize, this.filteredMilestones.length);
  }

  get pages(): number[] {
    const pages: number[] = [];
    for (let i = 1; i <= this.totalPages; i++) pages.push(i);
    return pages;
  }

  // ── Status badge class ──────────────────────────────────────────────────────

  statusBadgeClass(s: MilestoneStatus): string {
    switch (+s) {
      case MilestoneStatus.NotStarted: return 'ms-status-badge--not-started';
      case MilestoneStatus.InProgress: return 'ms-status-badge--in-progress';
      case MilestoneStatus.Completed:  return 'ms-status-badge--completed';
      case MilestoneStatus.Delayed:    return 'ms-status-badge--delayed';
      case MilestoneStatus.Cancelled:  return 'ms-status-badge--cancelled';
      default:                         return 'ms-status-badge--not-started';
    }
  }

  // ── Form operations ─────────────────────────────────────────────────────────

  openCreateForm(): void {
    this.editingId.set(null);
    this.resetForm();
    const nextSeq = this.milestones().length > 0
      ? Math.max(...this.milestones().map(m => m.sequenceOrder)) + 1
      : 1;
    this.formSequenceOrder.set(nextSeq);
    this.showForm.set(true);
  }

  openEditForm(m: MilestoneResponse): void {
    this.editingId.set(m.id);
    this.formName.set(m.name);
    this.formDescription.set(m.description ?? '');
    this.formSequenceOrder.set(m.sequenceOrder);
    const phase = typeof m.phase === 'string'
      ? (ProjectPhaseFromApi[m.phase] ?? ProjectPhase.Initiation)
      : (m.phase ?? ProjectPhase.Initiation);
    this.formPhase.set(phase);
    this.formPlannedStartDate.set(m.plannedStartDate?.substring(0, 10) ?? '');
    this.formPlannedDueDate.set(m.plannedDueDate?.substring(0, 10) ?? '');
    this.formActualCompletionDate.set(m.actualCompletionDate?.substring(0, 10) ?? '');
    this.formIsDeadlineFixed.set(m.isDeadlineFixed);
    const status = typeof m.status === 'string'
      ? (MilestoneStatusFromApi[m.status] ?? MilestoneStatus.NotStarted)
      : m.status;
    this.formStatus.set(status);
    this.formCompletionPercentage.set(m.completionPercentage);
    this.formApproverUserId.set(m.approverUserId ?? '');
    this.showForm.set(true);
  }

  cancelForm(): void {
    this.showForm.set(false);
    this.editingId.set(null);
    this.resetForm();
  }

  submitForm(): void {
    if (!this.formName().trim()) {
      this.toastSvc.warning('Name is required.');
      return;
    }
    this.submitting.set(true);
    const editing = this.editingId();
    if (editing) {
      this.doUpdate(editing);
    } else {
      this.doCreate();
    }
  }

  private doCreate(): void {
    const dto: MilestoneCreate = {
      name: this.formName().trim(),
      description: this.formDescription().trim() || undefined,
      sequenceOrder: this.formSequenceOrder(),
      phase: this.formPhase(),
      plannedStartDate: this.formPlannedStartDate(),
      plannedDueDate: this.formPlannedDueDate(),
      isDeadlineFixed: this.formIsDeadlineFixed(),
      completionPercentage: this.formCompletionPercentage(),
      approverUserId: this.formApproverUserId() || undefined,
    };

    this.milestoneSvc.create(this.projectId(), dto).subscribe({
      next: r => {
        this.milestones.update(list => [...list, r.data].sort((a, b) => a.sequenceOrder - b.sequenceOrder));
        this.applyFilters();
        this.showForm.set(false);
        this.resetForm();
        this.submitting.set(false);
        this.toastSvc.success('Milestone created.');
      },
      error: (err) => {
        this.submitting.set(false);
        this.toastSvc.error(err?.error?.message ?? 'Failed to create milestone.');
      },
    });
  }

  private doUpdate(milestoneId: string): void {
    const dto: MilestoneUpdate = {
      name: this.formName().trim(),
      description: this.formDescription().trim() || undefined,
      sequenceOrder: this.formSequenceOrder(),
      phase: this.formPhase(),
      plannedStartDate: this.formPlannedStartDate(),
      plannedDueDate: this.formPlannedDueDate(),
      actualCompletionDate: this.formActualCompletionDate() || undefined,
      isDeadlineFixed: this.formIsDeadlineFixed(),
      status: this.formStatus(),
      completionPercentage: this.formCompletionPercentage(),
      approverUserId: this.formApproverUserId() || undefined,
    };

    this.milestoneSvc.update(this.projectId(), milestoneId, dto).subscribe({
      next: r => {
        this.milestones.update(list =>
          list.map(m => m.id === milestoneId ? r.data : m)
            .sort((a, b) => a.sequenceOrder - b.sequenceOrder)
        );
        this.applyFilters();
        this.showForm.set(false);
        this.editingId.set(null);
        this.resetForm();
        this.submitting.set(false);
        this.toastSvc.success('Milestone updated.');
      },
      error: (err) => {
        this.submitting.set(false);
        this.toastSvc.error(err?.error?.message ?? 'Failed to update milestone.');
      },
    });
  }

  deleteMilestone(m: MilestoneResponse): void {
    if (!confirm(`Delete milestone "${m.name}"?`)) return;

    this.milestoneSvc.delete(this.projectId(), m.id).subscribe({
      next: () => {
        this.milestones.update(list => list.filter(x => x.id !== m.id));
        this.applyFilters();
        this.toastSvc.success('Milestone deleted.');
      },
      error: () => this.toastSvc.error('Failed to delete milestone.'),
    });
  }

  // ── Expand / collapse ───────────────────────────────────────────────────────

  isExpanded(id: string): boolean {
    return this.expandedIds().has(id);
  }

  get isAllExpanded(): boolean {
    return this.pagedMilestones.length > 0 &&
      this.pagedMilestones.every(m => this.expandedIds().has(m.id));
  }

  toggleExpand(id: string): void {
    const next = new Set(this.expandedIds());
    if (next.has(id)) {
      next.delete(id);
    } else {
      next.add(id);
      this.ensureActionsLoaded(id);
    }
    this.expandedIds.set(next);
  }

  expandAll(): void {
    const next = new Set(this.pagedMilestones.map(m => m.id));
    this.expandedIds.set(next);
    this.loadAllProjectActions();
  }

  collapseAll(): void {
    this.expandedIds.set(new Set());
  }

  isLoadingActions(id: string): boolean {
    return this.loadingActionIds().has(id);
  }

  actionsForMilestone(id: string): ActionItem[] {
    return this.actionsByMilestone()[id] ?? [];
  }

  private ensureActionsLoaded(milestoneId: string): void {
    if (this.actionsByMilestone()[milestoneId] !== undefined) return;

    const loading = new Set(this.loadingActionIds());
    loading.add(milestoneId);
    this.loadingActionIds.set(loading);

    this.actionItemSvc.getAll({
      milestoneId,
      pageNumber: 1, pageSize: 200,
      sortBy: 'dueDate', sortDescending: false,
    }).subscribe({
      next: res => {
        const items = (res.data as PagedResult<ActionItem>).items ?? [];
        this.actionsByMilestone.update(m => ({ ...m, [milestoneId]: items }));
        const l = new Set(this.loadingActionIds()); l.delete(milestoneId);
        this.loadingActionIds.set(l);
      },
      error: () => {
        this.actionsByMilestone.update(m => ({ ...m, [milestoneId]: [] }));
        const l = new Set(this.loadingActionIds()); l.delete(milestoneId);
        this.loadingActionIds.set(l);
      },
    });
  }

  private loadAllProjectActions(): void {
    const loaded = this.actionsByMilestone();
    const missing = this.pagedMilestones.filter(m => loaded[m.id] === undefined);
    if (missing.length === 0) return;

    const loading = new Set(this.loadingActionIds());
    missing.forEach(m => loading.add(m.id));
    this.loadingActionIds.set(loading);

    this.actionItemSvc.getAll({
      projectId: this.projectId(),
      pageNumber: 1, pageSize: 500,
      sortBy: 'dueDate', sortDescending: false,
    }).subscribe({
      next: res => {
        const items = (res.data as PagedResult<ActionItem>).items ?? [];
        const grouped: Record<string, ActionItem[]> = {};
        missing.forEach(m => { grouped[m.id] = items.filter(a => a.milestoneId === m.id); });
        this.actionsByMilestone.update(map => ({ ...map, ...grouped }));
        const l = new Set(this.loadingActionIds());
        missing.forEach(m => l.delete(m.id));
        this.loadingActionIds.set(l);
      },
      error: () => {
        const grouped: Record<string, ActionItem[]> = {};
        missing.forEach(m => { grouped[m.id] = []; });
        this.actionsByMilestone.update(map => ({ ...map, ...grouped }));
        const l = new Set(this.loadingActionIds());
        missing.forEach(m => l.delete(m.id));
        this.loadingActionIds.set(l);
      },
    });
  }

  actionStatusClass(s: ActionStatus): string {
    const STATUS_MAP: Record<string, ActionStatus> = {
      todo: ActionStatus.ToDo, inprogress: ActionStatus.InProgress,
      inreview: ActionStatus.InReview, done: ActionStatus.Done, overdue: ActionStatus.Overdue,
    };
    const st = typeof s === 'number' ? s
      : STATUS_MAP[String(s).toLowerCase()] ?? ActionStatus.ToDo;
    switch (st) {
      case ActionStatus.InProgress: return 'ms-ai-status ms-ai-status--inprogress';
      case ActionStatus.InReview:   return 'ms-ai-status ms-ai-status--inreview';
      case ActionStatus.Done:       return 'ms-ai-status ms-ai-status--done';
      case ActionStatus.Overdue:    return 'ms-ai-status ms-ai-status--overdue';
      default:                      return 'ms-ai-status ms-ai-status--todo';
    }
  }

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
    if (days == null) return 'N/A';
    if (days === 0) return 'On track';
    if (days > 0) return `${days}d behind`;
    return `${Math.abs(days)}d ahead`;
  }

  // ── Action Item form methods ───────────────────────────
  openActionForm(): void {
    this.aiTitle.set('');
    this.aiDescription.set('');
    this.aiMilestoneId.set('');
    this.aiPriority.set(ActionPriority.Medium);
    this.aiStartDate.set('');
    this.aiDueDate.set('');
    this.aiAssigneeIds.set([]);
    this.actionAssigneeSearchTerm = '';
    this.actionFormError = null;
    this.showActionForm.set(true);
  }

  cancelActionForm(): void {
    this.showActionForm.set(false);
  }

  get filteredActionAssignees(): AssignableUser[] {
    if (!this.actionAssigneeSearchTerm.trim()) return this.users();
    const term = this.actionAssigneeSearchTerm.toLowerCase();
    return this.users().filter(u => u.fullName.toLowerCase().includes(term));
  }

  isActionAssigneeSelected(userId: string): boolean {
    return this.aiAssigneeIds().includes(userId);
  }

  toggleActionAssignee(userId: string): void {
    const current = this.aiAssigneeIds();
    if (current.includes(userId)) {
      this.aiAssigneeIds.set(current.filter(id => id !== userId));
    } else {
      this.aiAssigneeIds.set([...current, userId]);
    }
  }

  getActionAssigneeName(userId: string): string {
    return this.users().find(u => u.id === userId)?.fullName ?? userId;
  }

  submitActionForm(): void {
    if (!this.aiTitle().trim()) {
      this.actionFormError = 'Title is required.';
      return;
    }
    if (!this.aiMilestoneId()) {
      this.actionFormError = 'Please select a milestone.';
      return;
    }
    if (!this.aiDueDate()) {
      this.actionFormError = 'Due date is required.';
      return;
    }
    if (this.aiAssigneeIds().length === 0) {
      this.actionFormError = 'At least one assignee is required.';
      return;
    }

    this.actionSubmitting.set(true);
    this.actionFormError = null;

    const dto: ActionItemCreate = {
      title: this.aiTitle().trim(),
      description: this.aiDescription().trim(),
      workspaceId: this.workspaceId(),
      projectId: this.projectId(),
      milestoneId: this.aiMilestoneId(),
      isStandalone: false,
      assigneeIds: this.aiAssigneeIds(),
      priority: this.aiPriority(),
      status: ActionStatus.ToDo,
      startDate: this.aiStartDate() || null,
      dueDate: this.aiDueDate(),
      progress: 0,
    };

    this.actionItemSvc.create(dto).subscribe({
      next: () => {
        this.actionSubmitting.set(false);
        this.showActionForm.set(false);
        this.toastSvc.success('Action item created.');
        // Force refresh action items for the selected milestone
        const msId = this.aiMilestoneId();
        this.actionsByMilestone.update(m => { const copy = { ...m }; delete copy[msId]; return copy; });
        if (this.expandedIds().has(msId)) {
          this.ensureActionsLoaded(msId);
        }
      },
      error: (err) => {
        this.actionSubmitting.set(false);
        this.actionFormError = err?.error?.message ?? err?.error?.detail ?? 'Failed to create action item.';
      },
    });
  }

  compareNumeric(a: any, b: any): boolean {
    return +a === +b;
  }

  private resetForm(): void {
    this.formName.set('');
    this.formDescription.set('');
    this.formSequenceOrder.set(1);
    this.formPhase.set(ProjectPhase.Initiation);
    this.formPlannedStartDate.set('');
    this.formPlannedDueDate.set('');
    this.formActualCompletionDate.set('');
    this.formIsDeadlineFixed.set(false);
    this.formStatus.set(MilestoneStatus.NotStarted);
    this.formCompletionPercentage.set(0);
    this.formApproverUserId.set('');
  }
}
