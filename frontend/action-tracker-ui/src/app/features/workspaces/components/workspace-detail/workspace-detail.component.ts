import { Component, OnInit, DestroyRef, inject, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { WorkspaceService } from '../../services/workspace.service';
import { AuthService } from '../../../../core/services/auth.service';
import { BreadcrumbComponent } from '../../../../shared/components/breadcrumb/breadcrumb.component';
import { ActionItemService } from '../../../../core/services/action-item.service';
import { ProjectService } from '../../../projects/services/project.service';
import { Workspace, WorkspaceAdmin, UserDropdownItem } from '../../models/workspace.model';
import {
  ActionItem, ActionItemCreate, ActionItemFilter,
  ActionStatus, ActionPriority, AssignableUser, EscalationInfo,
} from '../../../../core/models/action-item.model';
import {
  ProjectResponse, ProjectStatus, ProjectPriority, ProjectFilter,
  ProjectType, StrategicObjectiveOption,
} from '../../../projects/models/project.models';
import { PagedResult } from '../../../../core/models/api-response.model';

@Component({
  selector: 'app-workspace-detail',
  standalone: true,
  imports: [CommonModule, RouterLink, FormsModule, ReactiveFormsModule, BreadcrumbComponent],
  templateUrl: './workspace-detail.component.html',
  styleUrl: './workspace-detail.component.scss',
})
export class WorkspaceDetailComponent implements OnInit {
  private readonly workspaceService = inject(WorkspaceService);
  private readonly authService      = inject(AuthService);
  private readonly actionService    = inject(ActionItemService);
  private readonly projectService   = inject(ProjectService);
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

  // ── Projects ───────────────────────────────────────────
  projects: ProjectResponse[] = [];
  projectTotalCount = 0;
  projectPageNumber = 1;
  projectPageSize = 10;
  projectLoading = false;
  deletingProjectId: string | null = null;

  // Project drawer form
  showProjectForm = false;
  editingProjectId: string | null = null;
  projectSaving = false;
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

  get projectTotalPages(): number {
    return Math.ceil(this.projectTotalCount / this.projectPageSize) || 1;
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
          this.loadProjects();
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
    this.sponsorDropdownOpen = false;
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

  // ── Projects ───────────────────────────────────────────
  loadProjects(): void {
    this.projectLoading = true;
    const filter: ProjectFilter = {
      workspaceId:    this.workspaceId,
      pageNumber:     this.projectPageNumber,
      pageSize:       this.projectPageSize,
      sortBy:         'createdAt',
      sortDescending: true,
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

  deleteProject(prj: ProjectResponse): void {
    if (!confirm(`Delete project "${prj.name}"?`)) return;
    this.deletingProjectId = prj.id;

    this.projectService.delete(prj.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.deletingProjectId = null;
          this.successMessage = `Project "${prj.projectCode}" deleted.`;
          this.loadProjects();
        },
        error: (err) => {
          this.deletingProjectId = null;
          this.errorMessage = err?.error?.message ?? 'Failed to delete project.';
        },
      });
  }

  // ── Project Form ──────────────────────────────────────
  private emptyProjectForm(): ProjectFormData {
    return {
      name: '',
      description: '',
      projectType: ProjectType.Operational,
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
    this.projectForm = this.emptyProjectForm();
    this.sponsorDropdownOpen = false;
    this.sponsorSearchTerm = '';
    this.strategicObjectives = [];
    this.strategicObjectivesLoaded = false;
    this.showProjectForm = true;
  }

  openEditProjectForm(prj: ProjectResponse): void {
    this.editingProjectId = prj.id;
    this.projectForm = {
      name: prj.name,
      description: prj.description ?? '',
      projectType: prj.projectType,
      strategicObjectiveId: prj.strategicObjectiveId ?? null,
      priority: prj.priority,
      projectManagerUserId: prj.projectManagerUserId,
      sponsorUserIds: prj.sponsors.map(s => s.userId),
      plannedStartDate: prj.plannedStartDate ? new Date(prj.plannedStartDate).toISOString().substring(0, 10) : '',
      plannedEndDate: prj.plannedEndDate ? new Date(prj.plannedEndDate).toISOString().substring(0, 10) : '',
      approvedBudget: prj.approvedBudget ?? null,
      status: prj.status,
      actualStartDate: prj.actualStartDate ? new Date(prj.actualStartDate).toISOString().substring(0, 10) : '',
    };
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
    if (+this.projectForm.projectType === ProjectType.Strategic) {
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
    return +this.projectForm.projectType === ProjectType.Strategic;
  }

  get filteredSponsorUsers(): AssignableUser[] {
    if (!this.sponsorSearchTerm.trim()) return this.allUsers;
    const term = this.sponsorSearchTerm.toLowerCase();
    return this.allUsers.filter(u => u.fullName.toLowerCase().includes(term));
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
    return this.allUsers.find(u => u.id === userId)?.fullName ?? userId;
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

    this.projectSaving = true;

    if (this.editingProjectId) {
      this.projectService.update(this.editingProjectId, {
        name: this.projectForm.name.trim(),
        description: this.projectForm.description?.trim() || undefined,
        projectType: +this.projectForm.projectType,
        status: +this.projectForm.status,
        strategicObjectiveId: this.projectForm.strategicObjectiveId || undefined,
        priority: +this.projectForm.priority,
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
          this.errorMessage = err?.error?.message ?? 'Failed to update project.';
        },
      });
    } else {
      this.projectService.create({
        name: this.projectForm.name.trim(),
        description: this.projectForm.description?.trim() || undefined,
        workspaceId: this.workspaceId,
        projectType: +this.projectForm.projectType,
        strategicObjectiveId: this.projectForm.strategicObjectiveId || undefined,
        priority: +this.projectForm.priority,
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
          this.errorMessage = err?.error?.message ?? 'Failed to create project.';
        },
      });
    }
  }

  projectPriorityClass(p: ProjectPriority): string {
    switch (+p) {
      case ProjectPriority.Critical: return 'badge bg-danger';
      case ProjectPriority.High:     return 'badge bg-warning text-dark';
      case ProjectPriority.Medium:   return 'badge bg-info text-dark';
      case ProjectPriority.Low:      return 'badge bg-secondary';
      default:                       return 'badge bg-light text-dark';
    }
  }

  projectStatusClass(s: ProjectStatus): string {
    switch (+s) {
      case ProjectStatus.Draft:     return 'badge bg-secondary';
      case ProjectStatus.Active:    return 'badge bg-primary';
      case ProjectStatus.OnHold:    return 'badge bg-warning text-dark';
      case ProjectStatus.Completed: return 'badge bg-success';
      case ProjectStatus.Cancelled: return 'badge bg-danger';
      default:                      return 'badge bg-light text-dark';
    }
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

interface ProjectFormData {
  name: string;
  description: string;
  projectType: ProjectType;
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
