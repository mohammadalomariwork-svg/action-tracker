import { Component, OnInit, DestroyRef, inject, signal, computed, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { forkJoin } from 'rxjs';
import * as XLSX from 'xlsx';

import { WorkspaceService } from '../../services/workspace.service';
import { AuthService } from '../../../../core/services/auth.service';
import { BreadcrumbComponent } from '../../../../shared/components/breadcrumb/breadcrumb.component';
import { HasPermissionDirective } from '../../../../shared';
import { WorkingDayHintComponent } from '../../../../shared/components/working-day-hint/working-day-hint.component';
import { WorkingDaysComponent } from '../../../../shared/components/working-days/working-days.component';
import { nonWorkingDatesMessage } from '../../../../shared/validators/working-day.validator';
import { injectWorkspaceCalendar } from '../../../../services/workspace-calendar.service';
import { ToastService } from '../../../../core/services/toast.service';
import { ActionItemService } from '../../../../core/services/action-item.service';
import { ProjectService } from '../../../projects/services/project.service';
import { MilestoneService } from '../../../projects/services/milestone.service';
import {
  Workspace, WorkspaceStats, WorkspaceMember, WorkspaceProjectManager,
} from '../../models/workspace.model';
import {
  ActionItem, ActionItemCreate, ActionItemFilter,
  ActionStatus, ActionPriority, AssignableUser, EscalationInfo,
} from '../../../../core/models/action-item.model';
import { MilestoneResponse } from '../../../projects/models/milestone.models';
import {
  ProjectResponse, ProjectStatus, ProjectPriority, ProjectFilter,
  ProjectType, StrategicObjectiveOption,
} from '../../../projects/models/project.models';
import { PagedResult } from '../../../../core/models/api-response.model';
import { ActualDatesFieldsComponent } from '../../../../shared/components/actual-dates-fields/actual-dates-fields.component';
import {
  actualDatesPatch, actualDatesProblem, actualEndEditableFor, todayInTimeZone, toDateInput,
} from '../../../../shared/utils/actual-dates.util';
import { toActionItemUpdate } from '../../../../shared/utils/action-item-payload.util';
import { SubItemCountComponent } from '../../../../shared/components/sub-item-count/sub-item-count.component';
import { TagChipsComponent } from '../../../../shared/components/tag-chips/tag-chips.component';
import { TagFilterComponent } from '../../../../shared/components/tag-filter/tag-filter.component';
import { tagsCell } from '../../../../shared/utils/tag-rules.util';
import { ActionItemTagService } from '../../../../services/action-item-tag.service';
import { TagFilterOption } from '../../../../models/action-item-tag.model';
import { PermissionStateService } from '../../../permissions/services/permission-state.service';
import { ProjectClassificationService } from '../../../../services/project-classification.service';
import { ProjectClassification } from '../../../../models/project-classification.model';
import {
  CurrentProjectClassification, ProjectClassificationSelectComponent,
} from '../../../../shared/components/project-classification-select/project-classification-select.component';
import { internalClassificationPatch } from '../../../../shared/utils/project-classification-rules.util';
import { showErrorHere } from '../../../../core/http/server-reason';

@Component({
  selector: 'app-workspace-detail',
  standalone: true,
  imports: [
    SubItemCountComponent, TagChipsComponent, TagFilterComponent,
    CommonModule, RouterLink, FormsModule, ReactiveFormsModule, BreadcrumbComponent, HasPermissionDirective,
    WorkingDayHintComponent, WorkingDaysComponent, ActualDatesFieldsComponent, ProjectClassificationSelectComponent,
  ],
  templateUrl: './workspace-detail.component.html',
  styleUrl: './workspace-detail.component.scss',
})
export class WorkspaceDetailComponent implements OnInit {
  private readonly workspaceService = inject(WorkspaceService);
  private readonly authService      = inject(AuthService);
  private readonly actionService    = inject(ActionItemService);
  private readonly projectService   = inject(ProjectService);
  private readonly milestoneService = inject(MilestoneService);
  private readonly route            = inject(ActivatedRoute);
  private readonly destroyRef       = inject(DestroyRef);
  private readonly toastSvc         = inject(ToastService);

  workspaceId!: string;

  // Working calendar of this workspace (WO-2026-11 C-3)
  private readonly calendarWorkspaceId = signal<string | null>(null);
  readonly calendar = injectWorkspaceCalendar(() => this.calendarWorkspaceId());
  /** Dates the edited record holds now; unchanged dates are never flagged (Q8). */
  originalActionDates: { start: string | null; due: string | null } = { start: null, due: null };
  originalProjectDates: { start: string | null; end: string | null } = { start: null, end: null };
  // Actual dates of the edited action item (WO-2026-11 A-1)
  /** Today in this workspace calendar's time zone: the latest day an actual date may be. */
  readonly today = computed(() => todayInTimeZone(this.calendar()?.calendar.timeZoneId));
  editingActionStandalone = false;
  private originalActionActual: { start: string | null; end: string | null } = { start: null, end: null };
  workspace: Workspace | null = null;
  isLoading = false;
  errorMessage: string | null = null;
  successMessage: string | null = null;
  canManage = false;

  // Stats
  workspaceStats: WorkspaceStats | null = null;
  statsLoading = false;

  // Member count (read-only on this page; editing happens at /settings)
  workspaceMembers: WorkspaceMember[] = [];

  // Workspace-scoped Project Managers (drives the PM dropdown in the project drawer)
  workspaceProjectManagers: WorkspaceProjectManager[] = [];

  // ── Action Items ─────────────────────────────────────────
  actionItems: ActionItem[] = [];
  actionTotalCount = 0;
  actionPageNumber = 1;

  // ── Tag filter (WO-2026-11 A-5): this workspace's tags, by id, any of ──
  private readonly tagSvc    = inject(ActionItemTagService);
  private readonly permState = inject(PermissionStateService);
  private readonly classificationSvc = inject(ProjectClassificationService);

  // ── Internal classification (WO-2026-11 P-1) ──
  /** The workspace's classifications (active and inactive) — the project list filter's options. */
  classificationOptions: ProjectClassification[] = [];
  /** '' = all, 'none' = not classified, else a classification id. */
  projectClassificationFilter = '';
  /** The edited project's classification, for the drawer's dropdown. */
  editingProjectClassification: CurrentProjectClassification | null = null;
  private editingProjectOriginalClassificationId: string | null = null;
  readonly filterTagIds      = signal<string[]>([]);
  readonly tagOptions        = signal<TagFilterOption[]>([]);
  readonly tagOptionsLoading = signal(false);
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
  private originalEscalated = false;
  private originalEscalationText = '';

  // Delete
  deletingActionId: string | null = null;

  // Show deleted toggle
  showDeleted = false;

  // ── Projects ───────────────────────────────────────────
  projects: ProjectResponse[] = [];
  projectTotalCount = 0;
  projectPageNumber = 1;
  projectPageSize = 10;
  projectLoading = false;
  deletingProjectId: string | null = null;
  showDeletedProjects = false;

  // Project drawer form
  showProjectForm = false;
  editingProjectId: string | null = null;
  editingProjectOriginalStatus: ProjectStatus | null = null;
  projectSaving = false;
  projectFormError: string | null = null;
  projectForm: ProjectFormData = this.emptyProjectForm();
  strategicObjectives: StrategicObjectiveOption[] = [];
  strategicObjectivesLoaded = false;
  sponsorDropdownOpen = false;
  sponsorSearchTerm = '';

  // Expose enums to template
  readonly ActionStatus = ActionStatus;
  readonly ActionPriority = ActionPriority;
  readonly ProjectStatus = ProjectStatus;
  readonly ProjectPriority = ProjectPriority;
  readonly ProjectType = ProjectType;

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

  readonly PROJECT_PRIORITY_OPTIONS = [
    { value: ProjectPriority.Low,      label: 'Low'      },
    { value: ProjectPriority.Medium,   label: 'Medium'   },
    { value: ProjectPriority.High,     label: 'High'     },
    { value: ProjectPriority.Critical, label: 'Critical' },
  ];

  readonly PROJECT_STATUS_OPTIONS = [
    { value: ProjectStatus.Draft,     label: 'Draft'     },
    { value: ProjectStatus.Active,    label: 'Active'    },
    { value: ProjectStatus.OnHold,    label: 'On Hold'   },
    { value: ProjectStatus.Completed, label: 'Completed' },
    { value: ProjectStatus.Cancelled, label: 'Cancelled' },
  ];

  private readonly STATUS_MAP: Record<string, ActionStatus> = {
    todo: ActionStatus.ToDo, inprogress: ActionStatus.InProgress,
    inreview: ActionStatus.InReview, done: ActionStatus.Done, overdue: ActionStatus.Overdue,
  };
  private readonly PRIORITY_MAP: Record<string, ActionPriority> = {
    low: ActionPriority.Low, medium: ActionPriority.Medium,
    high: ActionPriority.High, critical: ActionPriority.Critical,
  };

  private resolveStatus(val: unknown): ActionStatus {
    if (typeof val === 'number') return val;
    return this.STATUS_MAP[String(val).toLowerCase()] ?? ActionStatus.ToDo;
  }

  private resolvePriority(val: unknown): ActionPriority {
    if (typeof val === 'number') return val;
    return this.PRIORITY_MAP[String(val).toLowerCase()] ?? ActionPriority.Medium;
  }

  get actionTotalPages(): number {
    return Math.ceil(this.actionTotalCount / this.actionPageSize) || 1;
  }

  get projectTotalPages(): number {
    return Math.ceil(this.projectTotalCount / this.projectPageSize) || 1;
  }

  ngOnInit(): void {
    this.workspaceId = this.route.snapshot.paramMap.get('id')!;
    this.calendarWorkspaceId.set(this.workspaceId);
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
          this.workspaceMembers = this.workspace?.members ?? [];
          this.workspaceProjectManagers = this.workspace?.projectManagers ?? [];
          this.resolvePermissions();
          this.isLoading = false;
          this.loadActionItems();
          this.loadTagOptions();
          this.loadClassificationOptions();
          this.loadProjects();
          this.loadAllUsers();
          this.loadStats();
        },
        error: (err) => {
          this.errorMessage = showErrorHere(err) ?? 'Failed to load workspace details.';
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

  private loadStats(): void {
    this.statsLoading = true;
    this.workspaceService.getWorkspaceStats(this.workspaceId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          this.workspaceStats = res.data ?? null;
          this.statsLoading = false;
        },
        error: () => {
          this.statsLoading = false;
        },
      });
  }

  private loadAllUsers(): void {
    if (!this.workspaceId) {
      this.allUsers = [];
      return;
    }
    this.workspaceService.getAssignableUsers(this.workspaceId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => this.allUsers = res.data ?? [],
        error: () => { this.allUsers = []; },
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
      tagIds:         this.filterTagIds().length ? this.filterTagIds() : undefined,
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
          // The error toast gives the server's reason (e.g. 503: the tag filter while the tag tables are missing).
          this.actionLoading = false;
        },
      });
  }

  /** The workspace's tags for the filter (needs ActionItems.View). */
  private loadTagOptions(): void {
    if (!this.workspaceId || !this.permState.hasPermission('ActionItems', 'View')) return;
    this.tagOptionsLoading.set(true);
    this.tagSvc.workspaceTags(this.workspaceId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          this.tagOptions.set((res.data ?? []).map(t => ({ id: t.id, name: t.name, count: t.itemCount })));
          this.tagOptionsLoading.set(false);
        },
        error: () => { this.tagOptions.set([]); this.tagOptionsLoading.set(false); },
      });
  }

  onTagFilterChange(ids: string[]): void {
    this.filterTagIds.set(ids);
    this.actionPageNumber = 1;
    this.loadActionItems();
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
      actualStartDate: '',
      actualEndDate: '',
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

  /** True when this save can set the actual end (editing, moving to Done directly). */
  actualEndEditable(): boolean {
    return !!this.editingActionId && actualEndEditableFor(this.actionForm.status, this.editingActionStandalone);
  }

  openNewActionForm(): void {
    this.editingActionId = null;
    this.actionForm = this.emptyActionForm();
    this.originalActionDates = { start: null, due: null };
    this.editingEscalations = [];
    this.originalEscalated = false;
    this.originalEscalationText = '';
    this.assigneeDropdownOpen = false;
    this.assigneeSearchTerm = '';
    this.showActionForm = true;
  }

  openEditActionForm(item: ActionItem): void {
    this.editingActionId = item.id;
    this.editingEscalations = [...(item.escalations ?? [])]
      .sort((a, b) => new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime());

    let latestExplanation = '';
    if (item.isEscalated && this.editingEscalations.length > 0) {
      const mostRecent = this.editingEscalations[this.editingEscalations.length - 1];
      latestExplanation = mostRecent.explanation ?? '';
    }

    this.actionForm = {
      title:       item.title,
      description: item.description,
      assigneeIds: item.assignees.map(a => a.userId),
      priority:    this.resolvePriority(item.priority),
      status:      this.resolveStatus(item.status),
      startDate:   item.startDate ? item.startDate.slice(0, 10) : '',
      dueDate:     item.dueDate.slice(0, 10),
      actualStartDate: toDateInput(item.actualStartDate),
      actualEndDate:   toDateInput(item.actualEndDate),
      progress:    item.progress,
      isEscalated: !!item.isEscalated,
      escalationExplanation: latestExplanation,
    };
    this.originalActionDates = { start: this.actionForm.startDate || null, due: this.actionForm.dueDate || null };
    this.originalActionActual = { start: item.actualStartDate, end: item.actualEndDate };
    this.editingActionStandalone = item.isStandalone;

    this.originalEscalated = !!item.isEscalated;
    this.originalEscalationText = latestExplanation;
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
    this.sponsorDropdownOpen = false;
  }

  get latestEscalation(): EscalationInfo | null {
    if (this.editingEscalations.length === 0) return null;
    return this.editingEscalations[this.editingEscalations.length - 1];
  }

  get filteredUsers(): AssignableUser[] {
    if (!this.assigneeSearchTerm.trim()) return this.allUsers;
    const term = this.assigneeSearchTerm.toLowerCase();
    return this.allUsers.filter(u => u.userName.toLowerCase().includes(term));
  }

  getAssigneeName(userId: string): string {
    return this.allUsers.find(u => u.userId === userId)?.userName ?? userId;
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

    // Working-day rule — the server applies the same check and stays the authority.
    const dateProblem = nonWorkingDatesMessage(this.calendar(), [
      { field: 'Planned start', value: this.actionForm.startDate, original: this.originalActionDates.start },
      { field: 'Planned end',   value: this.actionForm.dueDate,   original: this.originalActionDates.due },
    ]);
    if (dateProblem) {
      this.toastSvc.error(dateProblem);
      return;
    }
    const endAllowed = this.actualEndEditable();
    const actualProblem = this.editingActionId ? actualDatesProblem({
      start: this.actionForm.actualStartDate,
      end: endAllowed ? this.actionForm.actualEndDate : '',
      doneAfterSave: endAllowed,
      today: this.today(),
    }) : null;
    if (actualProblem) {
      this.toastSvc.error(actualProblem);
      return;
    }

    this.actionSaving = true;

    // Determine if escalation actually changed
    const escalatedChanged = this.actionForm.isEscalated !== this.originalEscalated;
    const explanationChanged = this.actionForm.escalationExplanation?.trim() !== this.originalEscalationText.trim();
    const shouldSendEscalation = escalatedChanged || explanationChanged;

    const payload: ActionItemCreate = {
      title:       this.actionForm.title.trim(),
      description: this.actionForm.description?.trim() ?? '',
      workspaceId: this.workspaceId,
      isStandalone: true,
      assigneeIds: this.actionForm.assigneeIds,
      priority:    +this.actionForm.priority as ActionPriority,
      status:      +this.actionForm.status as ActionStatus,
      startDate:   this.actionForm.startDate || null,
      dueDate:     this.actionForm.dueDate,
      progress:    +this.actionForm.progress,
      isEscalated: !!this.actionForm.isEscalated,
      escalationExplanation: (this.actionForm.isEscalated && shouldSendEscalation)
        ? this.actionForm.escalationExplanation?.trim()
        : undefined,
    };

    // An edit never sends isStandalone: it would detach a project item (A-1b).
    const obs$ = this.editingActionId
      ? this.actionService.update(this.editingActionId, {
          ...toActionItemUpdate(payload),
          ...actualDatesPatch(this.originalActionActual,
            { start: this.actionForm.actualStartDate, end: this.actionForm.actualEndDate }, endAllowed),
        })
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
          this.loadStats();
        },
        error: (err) => {
          this.deletingActionId = null;
          this.errorMessage = showErrorHere(err) ?? 'Failed to delete action item.';
        },
      });
  }

  restoringActionId: string | null = null;

  restoreAction(item: ActionItem): void {
    if (!confirm(`Restore action "${item.title}"?`)) return;
    this.restoringActionId = item.id;

    this.actionService.restore(item.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.restoringActionId = null;
          this.successMessage = `Action "${item.actionId}" restored.`;
          this.loadActionItems();
          this.loadStats();
        },
        error: (err) => {
          this.restoringActionId = null;
          this.errorMessage = showErrorHere(err) ?? 'Failed to restore action item.';
        },
      });
  }

  // ── Projects ───────────────────────────────────────────
  loadProjects(): void {
    this.projectLoading = true;
    const filter: ProjectFilter = {
      workspaceId:    this.workspaceId,
      pageNumber:     this.projectPageNumber,
      pageSize:       this.projectPageSize,
      sortBy:         'createdAt',
      sortDescending: true,
      includeDeleted: this.showDeletedProjects || undefined,
      internalClassificationId:
        this.projectClassificationFilter && this.projectClassificationFilter !== 'none'
          ? this.projectClassificationFilter : undefined,
      unclassified: this.projectClassificationFilter === 'none' || undefined,
    };

    this.projectService.getAll(filter)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          const paged: PagedResult<ProjectResponse> = res.data;
          this.projects = paged.items;
          this.projectTotalCount = paged.totalCount;
          this.projectLoading = false;
        },
        error: () => this.projectLoading = false,
      });
  }

  projectPrevPage(): void {
    if (this.projectPageNumber > 1) {
      this.projectPageNumber--;
      this.loadProjects();
    }
  }

  projectNextPage(): void {
    if (this.projectPageNumber < this.projectTotalPages) {
      this.projectPageNumber++;
      this.loadProjects();
    }
  }

  /** Options of the project list's classification filter (empty: the filter is not shown). */
  private loadClassificationOptions(): void {
    this.classificationSvc.options(this.workspaceId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(list => this.classificationOptions = list);
  }

  onProjectClassificationFilterChange(value: string): void {
    this.projectClassificationFilter = value;
    this.projectPageNumber = 1;
    this.loadProjects();
  }

  toggleShowDeletedProjects(): void {
    this.showDeletedProjects = !this.showDeletedProjects;
    this.projectPageNumber = 1;
    this.loadProjects();
  }

  deleteProject(prj: ProjectResponse): void {
    if (!confirm(
      `Are you sure you want to delete project "${prj.name}"?\n\n` +
      `This will also delete all milestones and action items associated with this project.`
    )) return;
    this.deletingProjectId = prj.id;

    this.projectService.delete(prj.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.deletingProjectId = null;
          this.successMessage = `Project "${prj.projectCode}" and its related milestones and action items have been deleted.`;
          this.loadProjects();
          this.loadActionItems();
          this.loadStats();
        },
        error: (err) => {
          this.deletingProjectId = null;
          this.errorMessage = showErrorHere(err) ?? 'Failed to delete project.';
        },
      });
  }

  restoringProjectId: string | null = null;

  restoreProject(prj: ProjectResponse): void {
    if (!confirm(
      `Restore project "${prj.name}"?\n\n` +
      `All related milestones and action items will be restored as well.`
    )) return;
    this.restoringProjectId = prj.id;

    this.projectService.restore(prj.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.restoringProjectId = null;
          this.successMessage = `Project "${prj.projectCode}" and its related milestones and action items have been restored.`;
          this.loadProjects();
          this.loadActionItems();
          this.loadStats();
        },
        error: (err) => {
          this.restoringProjectId = null;
          this.errorMessage = showErrorHere(err) ?? 'Failed to restore project.';
        },
      });
  }

  // ── Project Form ──────────────────────────────────────
  private emptyProjectForm(): ProjectFormData {
    return {
      name: '',
      description: '',
      projectType: ProjectType.Operational,
      internalClassificationId: null,
      strategicObjectiveId: null,
      priority: ProjectPriority.Medium,
      projectManagerUserId: '',
      sponsorUserIds: [],
      plannedStartDate: '',
      plannedEndDate: '',
      approvedBudget: null,
      status: ProjectStatus.Draft,
      actualStartDate: '',
    };
  }

  openNewProjectForm(): void {
    this.editingProjectId = null;
    this.editingProjectOriginalStatus = null;
    this.projectFormError = null;
    this.projectForm = this.emptyProjectForm();
    this.editingProjectClassification = null;
    this.editingProjectOriginalClassificationId = null;
    this.originalProjectDates = { start: null, end: null };
    this.sponsorDropdownOpen = false;
    this.sponsorSearchTerm = '';
    this.strategicObjectives = [];
    this.strategicObjectivesLoaded = false;
    this.showProjectForm = true;
  }

  openEditProjectForm(prj: ProjectResponse): void {
    this.editingProjectId = prj.id;
    this.editingProjectOriginalStatus = prj.status;
    this.projectFormError = null;
    this.projectForm = {
      name: prj.name,
      description: prj.description ?? '',
      projectType: prj.projectType,
      internalClassificationId: prj.internalClassificationId ?? null,
      strategicObjectiveId: prj.strategicObjectiveId ?? null,
      priority: prj.priority,
      projectManagerUserId: prj.projectManagerUserId,
      sponsorUserIds: prj.sponsors.map(s => s.userId),
      plannedStartDate: prj.plannedStartDate ? String(prj.plannedStartDate).substring(0, 10) : '',
      plannedEndDate: prj.plannedEndDate ? String(prj.plannedEndDate).substring(0, 10) : '',
      approvedBudget: prj.approvedBudget ?? null,
      status: prj.status,
      actualStartDate: prj.actualStartDate ? String(prj.actualStartDate).substring(0, 10) : '',
    };
    this.originalProjectDates = {
      start: this.projectForm.plannedStartDate || null,
      end:   this.projectForm.plannedEndDate || null,
    };
    this.editingProjectOriginalClassificationId = prj.internalClassificationId ?? null;
    this.editingProjectClassification = prj.internalClassificationId
      ? { id: prj.internalClassificationId, name: prj.internalClassificationName, isActive: prj.internalClassificationIsActive }
      : null;
    this.sponsorDropdownOpen = false;
    this.sponsorSearchTerm = '';

    if (prj.projectType === ProjectType.Strategic) {
      this.loadStrategicObjectives();
    } else {
      this.strategicObjectives = [];
      this.strategicObjectivesLoaded = false;
    }

    this.showProjectForm = true;
  }

  cancelProjectForm(): void {
    this.showProjectForm = false;
    this.editingProjectId = null;
    this.sponsorDropdownOpen = false;
    this.sponsorSearchTerm = '';
  }

  onProjectTypeChange(): void {
    if (this.projectForm.projectType === ProjectType.Strategic) {
      this.loadStrategicObjectives();
    } else {
      this.projectForm.strategicObjectiveId = null;
      this.strategicObjectives = [];
      this.strategicObjectivesLoaded = false;
    }
  }

  private loadStrategicObjectives(): void {
    this.projectService.getStrategicObjectivesForWorkspace(this.workspaceId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          this.strategicObjectives = res.data ?? [];
          this.strategicObjectivesLoaded = true;
        },
        error: () => {
          this.strategicObjectives = [];
          this.strategicObjectivesLoaded = true;
        },
      });
  }

  get isProjectStrategic(): boolean {
    return this.projectForm.projectType === ProjectType.Strategic;
  }

  get filteredSponsorUsers(): AssignableUser[] {
    if (!this.sponsorSearchTerm.trim()) return this.allUsers;
    const term = this.sponsorSearchTerm.toLowerCase();
    return this.allUsers.filter(u => u.userName.toLowerCase().includes(term));
  }

  toggleSponsor(userId: string): void {
    const idx = this.projectForm.sponsorUserIds.indexOf(userId);
    if (idx >= 0) this.projectForm.sponsorUserIds.splice(idx, 1);
    else this.projectForm.sponsorUserIds.push(userId);
  }

  isSponsorSelected(userId: string): boolean {
    return this.projectForm.sponsorUserIds.includes(userId);
  }

  getSponsorName(userId: string): string {
    return this.allUsers.find(u => u.userId === userId)?.userName ?? userId;
  }

  get hasProjectDateRangeError(): boolean {
    return !!(this.projectForm.plannedStartDate && this.projectForm.plannedEndDate
      && this.projectForm.plannedEndDate <= this.projectForm.plannedStartDate);
  }

  saveProject(): void {
    if (!this.projectForm.name.trim() || !this.projectForm.projectManagerUserId
      || this.projectForm.sponsorUserIds.length === 0
      || !this.projectForm.plannedStartDate || !this.projectForm.plannedEndDate
      || this.hasProjectDateRangeError) {
      return;
    }
    if (this.isProjectStrategic && !this.projectForm.strategicObjectiveId) {
      return;
    }

    // Working-day rule on the planned dates — the server applies the same check.
    const dateProblem = nonWorkingDatesMessage(this.calendar(), [
      { field: 'Planned start date', value: this.projectForm.plannedStartDate, original: this.originalProjectDates.start },
      { field: 'Planned end date',   value: this.projectForm.plannedEndDate,   original: this.originalProjectDates.end },
    ]);
    if (dateProblem) {
      this.projectFormError = dateProblem;
      return;
    }

    this.projectFormError = null;

    // Validate milestone structure when transitioning to Active or Completed
    if (this.editingProjectId) {
      const needsValidation =
        (this.projectForm.status === ProjectStatus.Active    && this.editingProjectOriginalStatus !== ProjectStatus.Active) ||
        (this.projectForm.status === ProjectStatus.Completed && this.editingProjectOriginalStatus !== ProjectStatus.Completed);

      if (needsValidation) {
        this.projectSaving = true;
        this.validateProjectMilestones(this.editingProjectId, this.projectForm.status, () => this.doSaveProject());
        return;
      }
    }

    this.projectSaving = true;
    this.doSaveProject();
  }

  private doSaveProject(): void {
    if (this.editingProjectId) {
      this.projectService.update(this.editingProjectId, {
        name: this.projectForm.name.trim(),
        description: this.projectForm.description?.trim() || undefined,
        projectType: this.projectForm.projectType,
        ...internalClassificationPatch(this.editingProjectOriginalClassificationId, this.projectForm.internalClassificationId),
        status: this.projectForm.status,
        strategicObjectiveId: this.projectForm.strategicObjectiveId || undefined,
        priority: this.projectForm.priority,
        projectManagerUserId: this.projectForm.projectManagerUserId,
        sponsorUserIds: this.projectForm.sponsorUserIds,
        plannedStartDate: this.projectForm.plannedStartDate,
        plannedEndDate: this.projectForm.plannedEndDate,
        actualStartDate: this.projectForm.actualStartDate || undefined,
        approvedBudget: this.projectForm.approvedBudget ? +this.projectForm.approvedBudget : undefined,
      }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: () => {
          this.projectSaving = false;
          this.showProjectForm = false;
          this.editingProjectId = null;
          this.successMessage = 'Project updated.';
          this.loadProjects();
        },
        error: (err) => {
          this.projectSaving = false;
          this.projectFormError = showErrorHere(err) ?? 'Failed to update project.';
        },
      });
    } else {
      this.projectService.create({
        name: this.projectForm.name.trim(),
        description: this.projectForm.description?.trim() || undefined,
        workspaceId: this.workspaceId,
        projectType: this.projectForm.projectType,
        internalClassificationId: this.projectForm.internalClassificationId || undefined,
        strategicObjectiveId: this.projectForm.strategicObjectiveId || undefined,
        priority: this.projectForm.priority,
        projectManagerUserId: this.projectForm.projectManagerUserId,
        sponsorUserIds: this.projectForm.sponsorUserIds,
        plannedStartDate: this.projectForm.plannedStartDate,
        plannedEndDate: this.projectForm.plannedEndDate,
        approvedBudget: this.projectForm.approvedBudget ? +this.projectForm.approvedBudget : undefined,
      }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: () => {
          this.projectSaving = false;
          this.showProjectForm = false;
          this.editingProjectId = null;
          this.successMessage = 'Project created.';
          this.loadProjects();
        },
        error: (err) => {
          this.projectSaving = false;
          this.projectFormError = showErrorHere(err) ?? 'Failed to create project.';
        },
      });
    }
  }

  private validateProjectMilestones(projectId: string, targetStatus: ProjectStatus, onValid: () => void): void {
    const label = targetStatus === ProjectStatus.Completed ? 'Completed' : 'Active';
    const actionFilter: ActionItemFilter = {
      projectId,
      pageNumber:     1,
      pageSize:       500,
      sortBy:         'dueDate',
      sortDescending: false,
    };

    forkJoin({
      milestones: this.milestoneService.getByProject(projectId),
      actions:    this.actionService.getAll(actionFilter),
    })
    .pipe(takeUntilDestroyed(this.destroyRef))
    .subscribe({
      next: ({ milestones, actions }) => {
        const msList     = milestones.data ?? [];
        const actionList = (actions.data as PagedResult<ActionItem>).items ?? [];

        if (msList.length === 0) {
          this.projectFormError = `Cannot set to ${label}: the project must have at least one milestone.`;
          this.projectSaving = false;
          return;
        }

        const emptyMilestones = msList.filter((ms: MilestoneResponse) =>
          !actionList.some(a => a.milestoneId === ms.id)
        );

        if (emptyMilestones.length > 0) {
          const names = emptyMilestones.map((m: MilestoneResponse) => `"${m.name}"`).join(', ');
          this.projectFormError =
            `Cannot set to ${label}: the following milestone(s) have no action items — ${names}. ` +
            `Please add at least one action item to each milestone first.`;
          this.projectSaving = false;
          return;
        }

        // When completing, all action items must be Done or Cancelled
        if (targetStatus === ProjectStatus.Completed) {
          const doneStatuses: (string | number)[] = [
            ActionStatus.Done, ActionStatus.Cancelled,
            'done', 'cancelled',
          ];
          const incompleteActions = actionList.filter(a =>
            !doneStatuses.includes(a.status as string | number)
          );

          if (incompleteActions.length > 0) {
            const names = incompleteActions.map(a => `"${a.title}"`).join(', ');
            this.projectFormError =
              `Cannot complete the project: all action items must be Done or Cancelled. ` +
              `Incomplete action items: ${names}.`;
            this.projectSaving = false;
            return;
          }
        }

        onValid();
      },
      error: () => {
        this.projectFormError = 'Could not validate milestones. Please try again.';
        this.projectSaving = false;
      },
    });
  }

  projectPriorityClass(p: ProjectPriority): string {
    switch (p) {
      case ProjectPriority.Critical: return 'badge bg-danger';
      case ProjectPriority.High:     return 'badge bg-warning text-dark';
      case ProjectPriority.Medium:   return 'badge bg-info text-dark';
      case ProjectPriority.Low:      return 'badge bg-secondary';
      default:                       return 'badge bg-light text-dark';
    }
  }

  projectStatusClass(s: ProjectStatus): string {
    switch (s) {
      case ProjectStatus.Draft:     return 'badge bg-secondary';
      case ProjectStatus.Active:    return 'badge bg-primary';
      case ProjectStatus.OnHold:    return 'badge bg-warning text-dark';
      case ProjectStatus.Completed: return 'badge bg-success';
      case ProjectStatus.Cancelled: return 'badge bg-danger';
      default:                      return 'badge bg-light text-dark';
    }
  }

  // ── Export ──────────────────────────────────────────────
  exportToExcel(): void {
    const wb = XLSX.utils.book_new();

    // Sheet 1: Workspace Info
    const infoRows: Record<string, unknown>[] = [
      { Field: 'Title',             Value: this.workspace?.title ?? '' },
      { Field: 'Organization Unit', Value: this.workspace?.organizationUnit ?? '' },
      { Field: 'Status',            Value: this.workspace?.isActive ? 'Active' : 'Inactive' },
      { Field: 'Visibility',        Value: this.workspace?.isPrivate ? 'Private' : 'Standard' },
      { Field: 'Created',           Value: this.workspace?.createdAt ? new Date(this.workspace.createdAt).toLocaleDateString() : '' },
      { Field: 'Last Updated',      Value: this.workspace?.updatedAt  ? new Date(this.workspace.updatedAt).toLocaleDateString()  : '' },
      { Field: 'Admins',            Value: (this.workspace?.admins ?? []).map(a => a.userName).join(', ') },
    ];
    if (this.workspaceStats) {
      infoRows.push(
        { Field: 'Total Projects',              Value: this.workspaceStats.totalProjects },
        { Field: 'Strategic Projects',          Value: this.workspaceStats.strategicProjects },
        { Field: 'Project Completion Rate',     Value: `${this.workspaceStats.projectCompletionRate}%` },
        { Field: 'Project On-Time Delivery',    Value: `${this.workspaceStats.projectOnTimeDeliveryRate}%` },
        { Field: 'Total Action Items',          Value: this.workspaceStats.totalActionItems },
        { Field: 'Standalone Actions',          Value: this.workspaceStats.standaloneActionItems },
        { Field: 'Escalated Actions',           Value: this.workspaceStats.escalatedActionItems },
        { Field: 'Standalone Completion Rate',  Value: `${this.workspaceStats.standaloneCompletionRate}%` },
        { Field: 'Standalone On-Time Delivery', Value: `${this.workspaceStats.standaloneOnTimeDeliveryRate}%` },
      );
    }
    XLSX.utils.book_append_sheet(wb, XLSX.utils.json_to_sheet(infoRows), 'Workspace Info');

    // Sheet 2: Projects
    const projectRows = this.projects.map(p => ({
      'Code':          p.projectCode,
      'Name':          p.name,
      'Type':          p.projectTypeLabel,
      'Internal classification': p.internalClassificationName ?? '',
      'Status':        p.statusLabel,
      'Priority':      p.priorityLabel,
      'Manager':       p.projectManagerName,
      'Sponsors':      p.sponsors.map(s => s.fullName).join(', '),
      'Planned Start': p.plannedStartDate ? new Date(p.plannedStartDate).toLocaleDateString() : '',
      'Planned End':   p.plannedEndDate   ? new Date(p.plannedEndDate).toLocaleDateString()   : '',
      'Baselined':     p.isBaselined ? 'Yes' : 'No',
      'Action Items':  p.actionItemCount,
    }));
    XLSX.utils.book_append_sheet(wb, XLSX.utils.json_to_sheet(projectRows.length ? projectRows : [{}]), 'Projects');

    // Sheet 3: Action Items
    const actionRows = this.actionItems.map(a => ({
      'ID':         a.actionId,
      'Title':      a.title,
      'Assignees':  a.assignees.map(x => x.fullName).join(', '),
      'Status':     a.statusLabel,
      'Priority':   a.priorityLabel,
      'Planned Start': a.startDate ? new Date(a.startDate).toLocaleDateString() : '',
      'Planned End':   new Date(a.dueDate).toLocaleDateString(),
      'Actual Start':  a.actualStartDate ? new Date(a.actualStartDate).toLocaleDateString() : '',
      'Actual End':    a.actualEndDate ? new Date(a.actualEndDate).toLocaleDateString() : '',
      'Progress':   `${a.progress}%`,
      'Escalated':  a.isEscalated ? 'Yes' : 'No',
      'Tags':       tagsCell(a.tags),
    }));
    XLSX.utils.book_append_sheet(wb, XLSX.utils.json_to_sheet(actionRows.length ? actionRows : [{}]), 'Action Items');

    const filename = `workspace-${this.workspace?.title ?? this.workspaceId}-${new Date().toISOString().slice(0, 10)}.xlsx`;
    XLSX.writeFile(wb, filename);
  }

  printToPDF(): void {
    window.print();
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
  actualStartDate: string;
  actualEndDate: string;
  progress: number;
  isEscalated: boolean;
  escalationExplanation: string;
}

interface ProjectFormData {
  name: string;
  description: string;
  projectType: ProjectType;
  /** Internal classification (WO-2026-11 P-1); null = none. */
  internalClassificationId: string | null;
  strategicObjectiveId: string | null;
  priority: ProjectPriority;
  projectManagerUserId: string;
  sponsorUserIds: string[];
  plannedStartDate: string;
  plannedEndDate: string;
  approvedBudget: number | null;
  status: ProjectStatus;
  actualStartDate: string;
}
