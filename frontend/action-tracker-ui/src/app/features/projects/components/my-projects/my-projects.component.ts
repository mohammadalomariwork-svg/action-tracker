import {
  Component, OnInit, ChangeDetectionStrategy, ChangeDetectorRef,
  inject, signal, DestroyRef, effect, untracked,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators, AbstractControl, ValidationErrors } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { toSignal, takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { forkJoin } from 'rxjs';

import { AuthService } from '../../../../core/services/auth.service';
import { ProjectService } from '../../services/project.service';
import { MilestoneService } from '../../services/milestone.service';
import { ActionItemService } from '../../../../core/services/action-item.service';
import { WorkspaceService } from '../../../workspaces/services/workspace.service';
import { WorkspaceList } from '../../../workspaces/models/workspace.model';
import { ToastService } from '../../../../core/services/toast.service';
import { injectWorkspaceCalendar } from '../../../../services/workspace-calendar.service';
import { WorkingDayHintComponent } from '../../../../shared/components/working-day-hint/working-day-hint.component';
import { WorkingDaysComponent } from '../../../../shared/components/working-days/working-days.component';
import { nonWorkingDatesMessage, workingDayValidator } from '../../../../shared/validators/working-day.validator';
import {
  ProjectResponse, ProjectUpdate, ProjectStatus, ProjectPriority, ProjectType,
  StrategicObjectiveOption,
} from '../../models/project.models';
import { MilestoneResponse } from '../../models/milestone.models';
import { AssignableUser, ActionItem, ActionItemFilter, ActionStatus } from '../../../../core/models/action-item.model';
import { PagedResult } from '../../../../core/models/api-response.model';
import { HasPermissionDirective } from '../../../../shared';
import { BreadcrumbComponent } from '../../../../shared/components/breadcrumb/breadcrumb.component';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import {
  CurrentProjectClassification, ProjectClassificationSelectComponent,
} from '../../../../shared/components/project-classification-select/project-classification-select.component';
import { internalClassificationPatch } from '../../../../shared/utils/project-classification-rules.util';
import { showErrorHere } from '../../../../core/http/server-reason';
import { ProjectPeopleFieldsComponent } from '../../../../shared/components/project-people-fields/project-people-fields.component';
import { ProjectPeopleService } from '../../../../services/project-people.service';
import {
  PROJECT_ROLE_LABELS, PROJECT_ROLE_ORDER, PersonOption, ProjectPeopleValue, ProjectRoleName,
} from '../../../../models/project-people.model';
import {
  emptyPeopleValue, isProjectManagerOf, peoplePatch, peopleValueFrom, primaryProjectRole, userListOptions,
} from '../../../../shared/utils/project-people.util';

function dateRangeValidator(group: AbstractControl): ValidationErrors | null {
  const start = group.get('plannedStartDate')?.value;
  const end = group.get('plannedEndDate')?.value;
  if (start && end && end <= start) return { dateRange: true };
  return null;
}

/** WO-2026-11 P-2: every role a person can hold on a project. */
export type MyProjectRole = ProjectRoleName;

export interface MyProjectEntry {
  project: ProjectResponse;
  /** The strongest of the caller's roles (sorting, filters). */
  role: MyProjectRole;
  /** All of the caller's roles, strongest first. */
  roles: MyProjectRole[];
}

// Palette used for generated avatar colours (matches workspace list)
const AVATAR_COLORS = [
  '#3b82f6','#10b981','#f59e0b','#ef4444','#8b5cf6',
  '#06b6d4','#f97316','#ec4899','#14b8a6','#6366f1',
];

@Component({
  selector: 'app-my-projects',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, RouterLink, HasPermissionDirective,
            BreadcrumbComponent, PageHeaderComponent, WorkingDayHintComponent, WorkingDaysComponent,
            ProjectClassificationSelectComponent, ProjectPeopleFieldsComponent],
  templateUrl: './my-projects.component.html',
  styleUrl: './my-projects.component.scss',
})
export class MyProjectsComponent implements OnInit {
  private readonly authSvc        = inject(AuthService);
  private readonly projectSvc     = inject(ProjectService);
  private readonly milestoneSvc   = inject(MilestoneService);
  private readonly actionItemSvc  = inject(ActionItemService);
  private readonly workspaceSvc   = inject(WorkspaceService);
  private readonly toastSvc       = inject(ToastService);
  private readonly fb             = inject(FormBuilder);
  private readonly destroyRef     = inject(DestroyRef);
  private readonly cdr            = inject(ChangeDetectorRef);
  private readonly peopleSvc      = inject(ProjectPeopleService);

  readonly currentUser = toSignal(this.authSvc.currentUser$, { initialValue: null });

  readonly loading    = signal(false);
  readonly allEntries = signal<MyProjectEntry[]>([]);

  // Filters
  searchTerm   = '';
  statusFilter: ProjectStatus | '' = '';
  roleFilter:   MyProjectRole  | '' = '';

  readonly roleLabels = PROJECT_ROLE_LABELS;
  readonly roleOrder  = PROJECT_ROLE_ORDER;

  // Sort
  sortField: 'name' | 'plannedStartDate' | 'plannedEndDate' | 'status' | 'role' = 'name';
  sortDirection: 'asc' | 'desc' = 'asc';

  // Pagination
  currentPage  = 1;
  pageSize     = 15;
  totalPages   = 1;
  filteredEntries: MyProjectEntry[] = [];
  pagedEntries:    MyProjectEntry[] = [];

  readonly ProjectStatus   = ProjectStatus;
  readonly ProjectPriority = ProjectPriority;

  readonly statusOptions: Array<{ value: ProjectStatus; label: string }> = [
    { value: ProjectStatus.Draft,     label: 'Draft' },
    { value: ProjectStatus.Active,    label: 'Active' },
    { value: ProjectStatus.OnHold,    label: 'On Hold' },
    { value: ProjectStatus.Completed, label: 'Completed' },
    { value: ProjectStatus.Cancelled, label: 'Cancelled' },
  ];

  // ── Create drawer ──────────────────────────────────────
  showDrawer    = false;
  drawerSaving  = false;
  drawerError:  string | null = null;
  createForm!:  FormGroup;
  workspaces:   WorkspaceList[] = [];
  availableUsers: AssignableUser[] = [];
  strategicObjectives: StrategicObjectiveOption[] = [];
  /** People of the new project (WO-2026-11 P-2). */
  createPeople: ProjectPeopleValue = emptyPeopleValue();
  createPmOptions: PersonOption[] | null = null;
  createTriedSave = false;

  // ── Edit drawer ──────────────────────────────────────
  showEditDrawer    = false;
  editDrawerSaving  = false;
  editDrawerError:  string | null = null;
  editDrawerLoading = false;
  editForm!:        FormGroup;
  editProjectId:    string | null = null;
  editOriginalStatus: ProjectStatus | null = null;
  editIsBaselined   = false;
  /** People of the edited project (WO-2026-11 P-2). */
  editPeople: ProjectPeopleValue = emptyPeopleValue();
  private editOriginalPeople: ProjectPeopleValue | null = null;
  editPeopleLoading = false;
  editCanAddProjectManagers = false;
  editPmOptions: PersonOption[] | null = null;
  editTriedSave = false;
  /** False when the caller reaches the edited project only through a project role (D4). */
  editCanOpenWorkspace = true;
  editLeadPmName = '';
  editStrategicObjectives: StrategicObjectiveOption[] = [];
  /** Internal classification of the edited project (WO-2026-11 P-1). */
  editCurrentClassification: CurrentProjectClassification | null = null;
  private editOriginalClassificationId: string | null = null;

  // ── Working calendars (WO-2026-11 C-3) ────────────────
  private readonly createWorkspaceId = signal('');
  readonly editWorkspaceId           = signal('');
  /** Set when the edited project's workspace can only be reached through the project (D6). */
  private readonly editViaProject     = signal<string | null>(null);
  /** Calendar of the workspace picked in the create drawer. */
  readonly createCalendar = injectWorkspaceCalendar(() => this.createWorkspaceId());
  /** Calendar of the edited project's workspace. */
  readonly editCalendar   = injectWorkspaceCalendar(() => this.editWorkspaceId(), () => this.editViaProject());
  /** Planned dates the edited project holds now; unchanged dates are never flagged (Q8). */
  editOriginalDates: { start: string | null; end: string | null } = { start: null, end: null };
  /** Re-runs the date validators when a calendar finishes loading. */
  private readonly revalidateCreateDates = effect(() => {
    this.createCalendar();
    untracked(() => this.revalidate(this.createForm, ['plannedStartDate', 'plannedEndDate']));
  });
  private readonly revalidateEditDates = effect(() => {
    this.editCalendar();
    untracked(() => this.revalidate(this.editForm, ['plannedStartDate', 'plannedEndDate']));
  });

  readonly STATUS_OPTIONS = [
    { value: ProjectStatus.Draft,     label: 'Draft' },
    { value: ProjectStatus.Active,    label: 'Active' },
    { value: ProjectStatus.OnHold,    label: 'On Hold' },
    { value: ProjectStatus.Completed, label: 'Completed' },
    { value: ProjectStatus.Cancelled, label: 'Cancelled' },
  ];

  readonly ProjectType     = ProjectType;
  readonly PRIORITY_OPTIONS = [
    { value: ProjectPriority.Low,      label: 'Low'      },
    { value: ProjectPriority.Medium,   label: 'Medium'   },
    { value: ProjectPriority.High,     label: 'High'     },
    { value: ProjectPriority.Critical, label: 'Critical' },
  ];

  ngOnInit(): void {
    this.loadProjects();
    this.buildCreateForm();
  }

  private loadProjects(): void {
    const userId = this.currentUser()?.userId;
    if (!userId) return;

    this.loading.set(true);
    // WO-2026-11 P-2: every project where the caller holds a project role —
    // lead PM, additional PM, sponsor, viewer or team member (`mine=true`).
    this.projectSvc.getAll({
      mine: true,
      pageNumber: 1, pageSize: 500,
      sortBy: 'name', sortDescending: false,
    }).subscribe({
      next: res => {
        const projects = (res.data as PagedResult<ProjectResponse>).items ?? [];
        const entries: MyProjectEntry[] = [];

        for (const p of projects) {
          const roles = PROJECT_ROLE_ORDER.filter(r => p.myRoles?.includes(r));
          // Older replies without myRoles: the lead PM and sponsors as before.
          if (!roles.length) {
            if (p.projectManagerUserId === userId) roles.push('leadProjectManager');
            else if (p.sponsors.some(s => s.userId === userId)) roles.push('sponsor');
          }
          const role = primaryProjectRole(roles);
          if (role) entries.push({ project: p, role, roles });
        }

        this.allEntries.set(entries);
        this.applyFilters();
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.toastSvc.error('Failed to load projects.');
      },
    });
  }

  // ── Search / Filter / Sort / Pagination ─────────────────────────────────────

  applyFilters(): void {
    let result = [...this.allEntries()];

    if (this.searchTerm.trim()) {
      const term = this.searchTerm.toLowerCase();
      result = result.filter(e =>
        e.project.name.toLowerCase().includes(term) ||
        e.project.projectCode.toLowerCase().includes(term) ||
        (e.project.description ?? '').toLowerCase().includes(term)
      );
    }

    if (this.statusFilter) {
      result = result.filter(e => e.project.status === this.statusFilter);
    }

    if (this.roleFilter) {
      const wanted = this.roleFilter;
      result = result.filter(e => e.roles.includes(wanted));
    }

    result.sort((a, b) => {
      let cmp = 0;
      switch (this.sortField) {
        case 'name':
          cmp = a.project.name.localeCompare(b.project.name); break;
        case 'plannedStartDate':
          cmp = new Date(a.project.plannedStartDate).getTime() - new Date(b.project.plannedStartDate).getTime(); break;
        case 'plannedEndDate':
          cmp = new Date(a.project.plannedEndDate).getTime() - new Date(b.project.plannedEndDate).getTime(); break;
        case 'status':
          cmp = a.project.status.localeCompare(b.project.status); break;
        case 'role':
          cmp = PROJECT_ROLE_ORDER.indexOf(a.role) - PROJECT_ROLE_ORDER.indexOf(b.role); break;
      }
      return this.sortDirection === 'asc' ? cmp : -cmp;
    });

    this.filteredEntries = result;
    this.totalPages = Math.max(1, Math.ceil(result.length / this.pageSize));
    if (this.currentPage > this.totalPages) this.currentPage = 1;
    this.updatePage();
  }

  updatePage(): void {
    const start = (this.currentPage - 1) * this.pageSize;
    this.pagedEntries = this.filteredEntries.slice(start, start + this.pageSize);
  }

  onSearchChange(): void {
    this.currentPage = 1;
    this.applyFilters();
  }

  toggleSort(field: typeof this.sortField): void {
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

  get pageStart(): number { return (this.currentPage - 1) * this.pageSize + 1; }
  get pageEnd():   number { return Math.min(this.currentPage * this.pageSize, this.filteredEntries.length); }

  get pages(): number[] {
    const p: number[] = [];
    for (let i = 1; i <= this.totalPages; i++) p.push(i);
    return p;
  }

  // ── Stat getters ─────────────────────────────────────────────────────────────

  get managerCount(): number { return this.allEntries().filter(e => isProjectManagerOf(e.roles)).length; }
  get sponsorCount(): number { return this.allEntries().filter(e => e.roles.includes('sponsor')).length; }
  /** Viewer or team member, and nothing stronger. */
  get otherRoleCount(): number {
    return this.allEntries().filter(e => !isProjectManagerOf(e.roles) && !e.roles.includes('sponsor')).length;
  }

  roleIcon(role: MyProjectRole): string {
    switch (role) {
      case 'leadProjectManager': return 'bi-person-gear';
      case 'projectManager':     return 'bi-person-gear';
      case 'sponsor':            return 'bi-person-check';
      case 'viewer':             return 'bi-eye';
      case 'teamMember':         return 'bi-people';
    }
  }

  roleBadgeClass(role: MyProjectRole): string {
    switch (role) {
      case 'leadProjectManager':
      case 'projectManager':     return 'mp-role-badge mp-role-badge--manager';
      case 'sponsor':            return 'mp-role-badge mp-role-badge--sponsor';
      case 'viewer':             return 'mp-role-badge mp-role-badge--viewer';
      case 'teamMember':         return 'mp-role-badge mp-role-badge--team';
    }
  }

  /** What the caller's role lets them do, shown next to View for everyone but project managers (as before for sponsors). */
  accessHint(entry: MyProjectEntry): string | null {
    if (isProjectManagerOf(entry.roles)) return null;
    return entry.roles.includes('teamMember') ? 'Own items' : 'Read-only';
  }
  get activeCount():  number { return this.allEntries().filter(e => e.project.status === ProjectStatus.Active).length; }

  // ── Avatar helpers ────────────────────────────────────────────────────────────

  getInitials(name: string): string {
    return name.split(' ').filter(Boolean).slice(0, 2)
      .map(w => w[0].toUpperCase()).join('');
  }

  getAvatarColor(name: string): string {
    let hash = 0;
    for (let i = 0; i < name.length; i++) hash = name.charCodeAt(i) + ((hash << 5) - hash);
    return AVATAR_COLORS[Math.abs(hash) % AVATAR_COLORS.length];
  }

  // ── Badge helpers ─────────────────────────────────────────────────────────────

  statusBadgeClass(status: ProjectStatus): string {
    switch (status) {
      case ProjectStatus.Draft:      return 'mp-status-badge mp-status-badge--draft';
      case ProjectStatus.Active:     return 'mp-status-badge mp-status-badge--active';
      case ProjectStatus.OnHold:     return 'mp-status-badge mp-status-badge--onhold';
      case ProjectStatus.Completed:  return 'mp-status-badge mp-status-badge--completed';
      case ProjectStatus.Cancelled:       return 'mp-status-badge mp-status-badge--cancelled';
      case ProjectStatus.PendingApproval: return 'mp-status-badge mp-status-badge--pending';
      default:                            return 'mp-status-badge mp-status-badge--draft';
    }
  }

  priorityBadgeClass(priority: ProjectPriority): string {
    switch (priority) {
      case ProjectPriority.Low:      return 'mp-pri-badge mp-pri-badge--low';
      case ProjectPriority.Medium:   return 'mp-pri-badge mp-pri-badge--medium';
      case ProjectPriority.High:     return 'mp-pri-badge mp-pri-badge--high';
      case ProjectPriority.Critical: return 'mp-pri-badge mp-pri-badge--critical';
      default:                       return 'mp-pri-badge mp-pri-badge--low';
    }
  }

  // ── Create drawer methods ─────────────────────────────────────────────────

  private buildCreateForm(): void {
    this.createForm = this.fb.group({
      workspaceId:          ['', [Validators.required]],
      name:                 ['', [Validators.required, Validators.maxLength(255)]],
      description:          [''],
      projectType:          [ProjectType.Operational, [Validators.required]],
      internalClassificationId: [null as string | null],
      strategicObjectiveId: [null as string | null],
      priority:             [ProjectPriority.Medium, [Validators.required]],
      projectManagerUserId: ['', [Validators.required]],
      plannedStartDate:     ['', [Validators.required,
        workingDayValidator(() => this.createCalendar(), () => null, 'Planned start date')]],
      plannedEndDate:       ['', [Validators.required,
        workingDayValidator(() => this.createCalendar(), () => null, 'Planned end date')]],
      approvedBudget:       [null as number | null],
    }, { validators: dateRangeValidator });

    this.createForm.get('workspaceId')!.valueChanges.subscribe(wsId => {
      this.createWorkspaceId.set(wsId ?? '');
      // Classifications belong to one workspace (WO-2026-11 P-1).
      this.createForm.get('internalClassificationId')!.setValue(null, { emitEvent: false });
      // WO-2026-11 P-2: sponsors, viewers and team members are any active user
      // and stay; additional PMs come from the new workspace's PM list.
      this.createPeople = { ...this.createPeople, additionalManagers: [] };
      this.availableUsers = [];
      this.createPmOptions = null;
      if (wsId) {
        this.workspaceSvc.getAssignableUsers(wsId).subscribe({
          next: res => { this.availableUsers = res.data ?? []; this.cdr.markForCheck(); },
        });
        this.workspaceSvc.getProjectManagers(wsId).subscribe({
          next: res => { this.createPmOptions = userListOptions(res.data); this.cdr.markForCheck(); },
          error: err => { showErrorHere(err); this.createPmOptions = []; this.cdr.markForCheck(); },
        });
      }
      if (wsId && this.createForm.get('projectType')?.value === ProjectType.Strategic) {
        this.loadStrategicObjectives(wsId);
      }
    });

    this.createForm.get('projectType')!.valueChanges.subscribe((type: ProjectType) => {
      const soCtrl = this.createForm.get('strategicObjectiveId')!;
      if (type === ProjectType.Strategic) {
        soCtrl.setValidators([Validators.required]);
        const wsId = this.createForm.get('workspaceId')?.value;
        if (wsId) this.loadStrategicObjectives(wsId);
      } else {
        soCtrl.clearValidators();
        soCtrl.setValue(null);
        this.strategicObjectives = [];
      }
      soCtrl.updateValueAndValidity();
    });
  }

  openCreateDrawer(): void {
    this.createForm.reset({
      workspaceId: '', name: '', description: '',
      projectType: ProjectType.Operational,
      internalClassificationId: null,
      strategicObjectiveId: null,
      priority: ProjectPriority.Medium,
      projectManagerUserId: '',
      plannedStartDate: '', plannedEndDate: '',
      approvedBudget: null,
    });
    this.createPeople = emptyPeopleValue();
    this.createPmOptions = null;
    this.createTriedSave = false;
    this.drawerError = null;

    if (this.workspaces.length === 0) {
      this.workspaceSvc.getWorkspaces().subscribe({
        next: res => this.workspaces = (res.data ?? []).filter(w => w.isActive),
      });
    }
    // availableUsers is now workspace-scoped — loaded when the user picks a workspace.
    this.availableUsers = [];

    this.showDrawer = true;
  }

  closeDrawer(): void {
    this.showDrawer = false;
  }

  private revalidate(form: FormGroup | undefined, controls: string[]): void {
    if (!form) return;
    for (const name of controls) form.get(name)?.updateValueAndValidity({ emitEvent: false });
  }

  get isStrategic(): boolean {
    return this.createForm.get('projectType')?.value === ProjectType.Strategic;
  }

  get hasDateRangeError(): boolean {
    return !!(this.createForm.hasError('dateRange') && this.createForm.get('plannedEndDate')?.touched);
  }

  formHasError(field: string, error: string): boolean {
    const ctrl = this.createForm.get(field);
    return !!(ctrl?.touched && ctrl.hasError(error));
  }

  formIsInvalid(field: string): boolean {
    const ctrl = this.createForm.get(field);
    return !!(ctrl?.touched && ctrl.invalid);
  }

  private loadStrategicObjectives(workspaceId: string): void {
    this.projectSvc.getStrategicObjectivesForWorkspace(workspaceId).subscribe({
      next: res => this.strategicObjectives = res.data ?? [],
      error: () => this.strategicObjectives = [],
    });
  }

  onDrawerSubmit(): void {
    this.createForm.markAllAsTouched();
    this.createTriedSave = true;

    // Working-day rule on the planned dates (the validators block the save too;
    // this says why). The server applies the same check.
    const created = this.createForm.getRawValue();
    const dateProblem = nonWorkingDatesMessage(this.createCalendar(), [
      { field: 'Planned start date', value: created.plannedStartDate },
      { field: 'Planned end date',   value: created.plannedEndDate },
    ]);
    if (dateProblem) {
      this.drawerError = dateProblem;
      return;
    }

    if (this.createForm.invalid || this.createPeople.sponsors.length === 0) return;

    this.drawerSaving = true;
    this.drawerError = null;
    const v = this.createForm.getRawValue();

    this.projectSvc.create({
      name: v.name,
      description: v.description || undefined,
      workspaceId: v.workspaceId,
      projectType: v.projectType,
      internalClassificationId: v.internalClassificationId || undefined,
      strategicObjectiveId: v.strategicObjectiveId || undefined,
      priority: v.priority,
      projectManagerUserId: v.projectManagerUserId,
      ...peoplePatch(null, this.createPeople),
      plannedStartDate: v.plannedStartDate,
      plannedEndDate: v.plannedEndDate,
      approvedBudget: v.approvedBudget ? +v.approvedBudget : undefined,
    }).subscribe({
      next: () => {
        this.drawerSaving = false;
        this.showDrawer = false;
        this.toastSvc.success('Project created successfully.');
        this.loadProjects();
      },
      error: err => {
        this.drawerSaving = false;
        this.drawerError = showErrorHere(err) ?? 'Failed to create project.';
      },
    });
  }

  // ── Edit drawer methods ─────────────────────────────────────────────────

  private buildEditForm(): void {
    this.editForm = this.fb.group({
      name:                 ['', [Validators.required, Validators.maxLength(255)]],
      description:          [''],
      projectType:          [ProjectType.Operational, [Validators.required]],
      internalClassificationId: [null as string | null],
      strategicObjectiveId: [null as string | null],
      priority:             [ProjectPriority.Medium, [Validators.required]],
      projectManagerUserId: ['', [Validators.required]],
      plannedStartDate:     ['', [Validators.required,
        workingDayValidator(() => this.editCalendar(), () => this.editOriginalDates.start, 'Planned start date')]],
      plannedEndDate:       ['', [Validators.required,
        workingDayValidator(() => this.editCalendar(), () => this.editOriginalDates.end, 'Planned end date')]],
      approvedBudget:       [null as number | null],
      status:               [ProjectStatus.Draft],
      actualStartDate:      [''],
    }, { validators: dateRangeValidator });
  }

  openEditDrawer(entry: MyProjectEntry): void {
    this.editProjectId = entry.project.id;
    this.editDrawerError = null;
    this.editWorkspaceId.set('');
    this.editOriginalDates = { start: null, end: null };
    this.editCurrentClassification = null;
    this.editOriginalClassificationId = null;
    this.editViaProject.set(null);
    this.editTriedSave = false;
    this.editPeople = emptyPeopleValue();
    this.editOriginalPeople = null;
    this.editCanAddProjectManagers = false;
    this.editPmOptions = null;

    if (!this.editForm) this.buildEditForm();

    // availableUsers is workspace-scoped — load after we know the project's workspace.
    this.availableUsers = [];

    // Load fresh project data
    this.editDrawerLoading = true;
    this.showEditDrawer = true;

    this.projectSvc.getById(entry.project.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: res => {
          const p: ProjectResponse = res.data;
          this.editOriginalStatus = p.status;
          this.editIsBaselined = p.isBaselined;
          this.editCanOpenWorkspace = p.canOpenWorkspace !== false;
          this.editViaProject.set(this.editCanOpenWorkspace ? null : p.id);
          this.editLeadPmName = p.projectManagerName;
          this.loadEditPeople(p);
          this.editOriginalClassificationId = p.internalClassificationId ?? null;
          this.editCurrentClassification = p.internalClassificationId
            ? { id: p.internalClassificationId, name: p.internalClassificationName, isActive: p.internalClassificationIsActive }
            : null;
          this.editOriginalDates = {
            start: p.plannedStartDate ? String(p.plannedStartDate).substring(0, 10) : null,
            end:   p.plannedEndDate ? String(p.plannedEndDate).substring(0, 10) : null,
          };

          this.editForm.patchValue({
            name: p.name,
            description: p.description ?? '',
            projectType: p.projectType,
            internalClassificationId: p.internalClassificationId ?? null,
            strategicObjectiveId: p.strategicObjectiveId ?? null,
            priority: p.priority,
            projectManagerUserId: p.projectManagerUserId,
            plannedStartDate: p.plannedStartDate ? String(p.plannedStartDate).substring(0, 10) : '',
            plannedEndDate: p.plannedEndDate ? String(p.plannedEndDate).substring(0, 10) : '',
            approvedBudget: p.approvedBudget ?? null,
            status: p.status,
            actualStartDate: p.actualStartDate ? String(p.actualStartDate).substring(0, 10) : '',
          });
          this.editWorkspaceId.set(p.workspaceId);

          // Freeze dates when not Draft
          const isNotDraft = p.status !== ProjectStatus.Draft;
          if (this.editIsBaselined || isNotDraft) {
            this.editForm.get('plannedStartDate')!.disable();
            this.editForm.get('plannedEndDate')!.disable();
          } else {
            this.editForm.get('plannedStartDate')!.enable();
            this.editForm.get('plannedEndDate')!.enable();
          }

          // Disable status when PendingApproval
          if (p.status === ProjectStatus.PendingApproval) {
            this.editForm.get('status')?.disable();
          } else {
            this.editForm.get('status')?.enable();
          }

          // Project-only callers cannot change the lead PM (D4) nor read the
          // workspace's lists (404): those fields keep the project's values.
          if (this.editCanOpenWorkspace) this.editForm.get('projectManagerUserId')!.enable();
          else this.editForm.get('projectManagerUserId')!.disable();

          if (p.projectType === ProjectType.Strategic) {
            if (this.editCanOpenWorkspace) this.loadEditStrategicObjectives(p.workspaceId);
            else this.editStrategicObjectives = p.strategicObjectiveId
              ? [{ id: p.strategicObjectiveId, statement: p.strategicObjectiveStatement ?? 'Current objective', objectiveCode: '' }]
              : [];
          }

          if (this.editCanOpenWorkspace) {
            this.workspaceSvc.getAssignableUsers(p.workspaceId).subscribe({
              next: res => { this.availableUsers = res.data ?? []; this.cdr.markForCheck(); },
            });
            this.workspaceSvc.getProjectManagers(p.workspaceId).subscribe({
              next: res => { this.editPmOptions = userListOptions(res.data); this.cdr.markForCheck(); },
              error: err => { showErrorHere(err); this.editPmOptions = []; this.cdr.markForCheck(); },
            });
          }

          this.editDrawerLoading = false;
          this.cdr.markForCheck();
        },
        error: err => {
          this.editDrawerError = showErrorHere(err) ?? 'Failed to load project.';
          this.editDrawerLoading = false;
          this.cdr.markForCheck();
        },
      });
  }

  /** Everyone on the edited project (GET /projects/:id/people). */
  private loadEditPeople(p: ProjectResponse): void {
    this.editPeople = emptyPeopleValue(p.sponsors.map(s => ({ userId: s.userId, fullName: s.fullName, email: s.email })));
    this.editPeopleLoading = true;
    this.peopleSvc.get(p.id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: res => {
        if (res.data) {
          this.editPeople = peopleValueFrom(res.data);
          this.editOriginalPeople = peopleValueFrom(res.data);
          this.editCanAddProjectManagers = res.data.canAddProjectManagers;
        }
        this.editPeopleLoading = false;
        this.cdr.markForCheck();
      },
      error: err => {
        this.editDrawerError = showErrorHere(err) ?? "Could not load the project's people; they are left as they are.";
        this.editPeopleLoading = false;
        this.cdr.markForCheck();
      },
    });
  }

  closeEditDrawer(): void {
    this.showEditDrawer = false;
    this.editProjectId = null;
  }

  get editIsStrategic(): boolean {
    return this.editForm?.get('projectType')?.value === ProjectType.Strategic;
  }

  get editHasDateRangeError(): boolean {
    return !!(this.editForm?.hasError('dateRange') && this.editForm.get('plannedEndDate')?.touched);
  }

  editFormHasError(field: string, error: string): boolean {
    const ctrl = this.editForm?.get(field);
    return !!(ctrl?.touched && ctrl.hasError(error));
  }

  editFormIsInvalid(field: string): boolean {
    const ctrl = this.editForm?.get(field);
    return !!(ctrl?.touched && ctrl.invalid);
  }

  private loadEditStrategicObjectives(workspaceId: string): void {
    this.projectSvc.getStrategicObjectivesForWorkspace(workspaceId).subscribe({
      next: res => { this.editStrategicObjectives = res.data ?? []; this.cdr.markForCheck(); },
      error: () => { this.editStrategicObjectives = []; this.cdr.markForCheck(); },
    });
  }

  onEditSubmit(): void {
    this.editForm.markAllAsTouched();
    this.editTriedSave = true;
    const v = this.editForm.getRawValue();

    // Working-day rule on the planned dates (the validators block the save too;
    // this says why). The server applies the same check.
    const dateProblem = nonWorkingDatesMessage(this.editCalendar(), [
      { field: 'Planned start date', value: v.plannedStartDate, original: this.editOriginalDates.start },
      { field: 'Planned end date',   value: v.plannedEndDate,   original: this.editOriginalDates.end },
    ]);
    if (dateProblem) {
      this.editDrawerError = dateProblem;
      return;
    }

    if (this.editForm.invalid || this.editPeople.sponsors.length === 0 || this.editPeopleLoading) {
      if (this.editPeople.sponsors.length === 0)
        this.editDrawerError = 'At least one sponsor is required.';
      return;
    }

    this.editDrawerSaving = true;
    this.editDrawerError = null;

    const needsValidation =
      (v.status === ProjectStatus.Active || v.status === ProjectStatus.Completed)
      && v.status !== this.editOriginalStatus;

    if (needsValidation) {
      this.validateEditBeforeSave(v.status, () => this.doEditUpdate(v));
      return;
    }

    this.doEditUpdate(v);
  }

  private doEditUpdate(v: any): void {
    this.projectSvc.update(this.editProjectId!, {
      name: v.name,
      description: v.description || undefined,
      projectType: v.projectType,
      ...internalClassificationPatch(this.editOriginalClassificationId, v.internalClassificationId),
      status: v.status,
      strategicObjectiveId: v.strategicObjectiveId || undefined,
      priority: v.priority,
      projectManagerUserId: v.projectManagerUserId,
      ...peoplePatch(this.editOriginalPeople, this.editPeople),
      plannedStartDate: v.plannedStartDate,
      plannedEndDate: v.plannedEndDate,
      actualStartDate: v.actualStartDate || undefined,
      approvedBudget: v.approvedBudget ? +v.approvedBudget : undefined,
    }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.editDrawerSaving = false;
        this.showEditDrawer = false;
        this.toastSvc.success('Project updated successfully.');
        this.loadProjects();
        this.cdr.markForCheck();
      },
      error: err => {
        this.editDrawerSaving = false;
        this.editDrawerError = showErrorHere(err) ?? err?.error?.detail ?? 'Failed to update project.';
        this.cdr.markForCheck();
      },
    });
  }

  private validateEditBeforeSave(targetStatus: ProjectStatus, onValid: () => void): void {
    const label = targetStatus === ProjectStatus.Completed ? 'Completed' : 'Active';
    const actionFilter: ActionItemFilter = {
      projectId:      this.editProjectId!,
      pageNumber:     1,
      pageSize:       500,
      sortBy:         'dueDate',
      sortDescending: false,
    };

    forkJoin({
      milestones: this.milestoneSvc.getByProject(this.editProjectId!),
      actions:    this.actionItemSvc.getAll(actionFilter),
    })
    .pipe(takeUntilDestroyed(this.destroyRef))
    .subscribe({
      next: ({ milestones, actions }) => {
        const msList     = milestones.data ?? [];
        const actionList = (actions.data as PagedResult<ActionItem>).items ?? [];

        if (msList.length === 0) {
          this.editDrawerError = `Cannot set to ${label}: the project must have at least one milestone.`;
          this.editDrawerSaving = false;
          this.cdr.markForCheck();
          return;
        }

        const emptyMilestones = msList.filter((ms: MilestoneResponse) =>
          !actionList.some(a => a.milestoneId === ms.id)
        );

        if (emptyMilestones.length > 0) {
          const names = emptyMilestones.map((m: MilestoneResponse) => `"${m.name}"`).join(', ');
          this.editDrawerError =
            `Cannot set to ${label}: the following milestone(s) have no action items — ${names}. ` +
            `Please add at least one action item to each milestone first.`;
          this.editDrawerSaving = false;
          this.cdr.markForCheck();
          return;
        }

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
            this.editDrawerError =
              `Cannot complete the project: all action items must be Done or Cancelled. ` +
              `Incomplete action items: ${names}.`;
            this.editDrawerSaving = false;
            this.cdr.markForCheck();
            return;
          }
        }

        onValid();
      },
      error: () => {
        this.editDrawerError = 'Could not validate milestones. Please try again.';
        this.editDrawerSaving = false;
        this.cdr.markForCheck();
      },
    });
  }

  /** Lead or additional PM who may change the project (WO-2026-11 P-2), unless it is closed. */
  canEdit(entry: MyProjectEntry): boolean {
    return isProjectManagerOf(entry.roles)
      && entry.project.canManage !== false
      && entry.project.status !== ProjectStatus.Completed
      && entry.project.status !== ProjectStatus.Cancelled;
  }
}
