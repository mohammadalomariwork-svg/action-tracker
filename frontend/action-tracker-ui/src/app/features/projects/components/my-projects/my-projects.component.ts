import {
  Component, OnInit, ChangeDetectionStrategy, ChangeDetectorRef,
  inject, signal, HostListener, DestroyRef,
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

function dateRangeValidator(group: AbstractControl): ValidationErrors | null {
  const start = group.get('plannedStartDate')?.value;
  const end = group.get('plannedEndDate')?.value;
  if (start && end && end <= start) return { dateRange: true };
  return null;
}

export type MyProjectRole = 'manager' | 'sponsor';

export interface MyProjectEntry {
  project: ProjectResponse;
  role: MyProjectRole;
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
            BreadcrumbComponent, PageHeaderComponent],
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

  readonly currentUser = toSignal(this.authSvc.currentUser$, { initialValue: null });

  readonly loading    = signal(false);
  readonly allEntries = signal<MyProjectEntry[]>([]);

  // Filters
  searchTerm   = '';
  statusFilter: ProjectStatus | '' = '';
  roleFilter:   MyProjectRole  | '' = '';

  // Sort
  sortField: 'name' | 'plannedStartDate' | 'plannedEndDate' | 'status' = 'name';
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
  selectedSponsorIds: string[] = [];
  sponsorDropdownOpen = false;
  sponsorSearchTerm   = '';

  // ── Edit drawer ──────────────────────────────────────
  showEditDrawer    = false;
  editDrawerSaving  = false;
  editDrawerError:  string | null = null;
  editDrawerLoading = false;
  editForm!:        FormGroup;
  editProjectId:    string | null = null;
  editOriginalStatus: ProjectStatus | null = null;
  editIsBaselined   = false;
  editSponsorIds:   string[] = [];
  editSponsorDropdownOpen = false;
  editSponsorSearchTerm   = '';
  editStrategicObjectives: StrategicObjectiveOption[] = [];

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

  @HostListener('document:click')
  onDocumentClick(): void {
    this.sponsorDropdownOpen = false;
    this.editSponsorDropdownOpen = false;
  }

  ngOnInit(): void {
    this.loadProjects();
    this.buildCreateForm();
  }

  private loadProjects(): void {
    const userId = this.currentUser()?.userId;
    if (!userId) return;

    this.loading.set(true);
    this.projectSvc.getAll({
      pageNumber: 1, pageSize: 500,
      sortBy: 'name', sortDescending: false,
    }).subscribe({
      next: res => {
        const projects = (res.data as PagedResult<ProjectResponse>).items ?? [];
        const entries: MyProjectEntry[] = [];

        for (const p of projects) {
          if (p.projectManagerUserId === userId) {
            entries.push({ project: p, role: 'manager' });
          } else if (p.sponsors.some(s => s.userId === userId)) {
            entries.push({ project: p, role: 'sponsor' });
          }
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
      result = result.filter(e => e.role === this.roleFilter);
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

  get managerCount(): number { return this.allEntries().filter(e => e.role === 'manager').length; }
  get sponsorCount(): number { return this.allEntries().filter(e => e.role === 'sponsor').length; }
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
      strategicObjectiveId: [null as string | null],
      priority:             [ProjectPriority.Medium, [Validators.required]],
      projectManagerUserId: ['', [Validators.required]],
      plannedStartDate:     ['', [Validators.required]],
      plannedEndDate:       ['', [Validators.required]],
      approvedBudget:       [null as number | null],
    }, { validators: dateRangeValidator });

    this.createForm.get('workspaceId')!.valueChanges.subscribe(wsId => {
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
      strategicObjectiveId: null,
      priority: ProjectPriority.Medium,
      projectManagerUserId: '',
      plannedStartDate: '', plannedEndDate: '',
      approvedBudget: null,
    });
    this.selectedSponsorIds = [];
    this.drawerError = null;
    this.sponsorSearchTerm = '';

    if (this.workspaces.length === 0) {
      this.workspaceSvc.getWorkspaces().subscribe({
        next: res => this.workspaces = (res.data ?? []).filter(w => w.isActive),
      });
    }
    if (this.availableUsers.length === 0) {
      this.projectSvc.getAssignableUsers().subscribe({
        next: res => this.availableUsers = res.data ?? [],
      });
    }

    this.showDrawer = true;
  }

  closeDrawer(): void {
    this.showDrawer = false;
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

  get filteredSponsorUsers(): AssignableUser[] {
    if (!this.sponsorSearchTerm.trim()) return this.availableUsers;
    const term = this.sponsorSearchTerm.toLowerCase();
    return this.availableUsers.filter(u => u.fullName.toLowerCase().includes(term));
  }

  toggleSponsor(userId: string): void {
    const idx = this.selectedSponsorIds.indexOf(userId);
    if (idx >= 0) this.selectedSponsorIds.splice(idx, 1);
    else this.selectedSponsorIds.push(userId);
  }

  isSponsorSelected(userId: string): boolean {
    return this.selectedSponsorIds.includes(userId);
  }

  getSponsorName(userId: string): string {
    return this.availableUsers.find(u => u.id === userId)?.fullName ?? userId;
  }

  private loadStrategicObjectives(workspaceId: string): void {
    this.projectSvc.getStrategicObjectivesForWorkspace(workspaceId).subscribe({
      next: res => this.strategicObjectives = res.data ?? [],
      error: () => this.strategicObjectives = [],
    });
  }

  onDrawerSubmit(): void {
    this.createForm.markAllAsTouched();
    if (this.createForm.invalid || this.selectedSponsorIds.length === 0) return;

    this.drawerSaving = true;
    this.drawerError = null;
    const v = this.createForm.getRawValue();

    this.projectSvc.create({
      name: v.name,
      description: v.description || undefined,
      workspaceId: v.workspaceId,
      projectType: v.projectType,
      strategicObjectiveId: v.strategicObjectiveId || undefined,
      priority: v.priority,
      projectManagerUserId: v.projectManagerUserId,
      sponsorUserIds: this.selectedSponsorIds,
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
        this.drawerError = err?.error?.message ?? 'Failed to create project.';
      },
    });
  }

  // ── Edit drawer methods ─────────────────────────────────────────────────

  private buildEditForm(): void {
    this.editForm = this.fb.group({
      name:                 ['', [Validators.required, Validators.maxLength(255)]],
      description:          [''],
      projectType:          [ProjectType.Operational, [Validators.required]],
      strategicObjectiveId: [null as string | null],
      priority:             [ProjectPriority.Medium, [Validators.required]],
      projectManagerUserId: ['', [Validators.required]],
      plannedStartDate:     ['', [Validators.required]],
      plannedEndDate:       ['', [Validators.required]],
      approvedBudget:       [null as number | null],
      status:               [ProjectStatus.Draft],
      actualStartDate:      [''],
    }, { validators: dateRangeValidator });
  }

  openEditDrawer(entry: MyProjectEntry): void {
    this.editProjectId = entry.project.id;
    this.editDrawerError = null;
    this.editSponsorSearchTerm = '';

    if (!this.editForm) this.buildEditForm();

    if (this.availableUsers.length === 0) {
      this.projectSvc.getAssignableUsers().subscribe({
        next: res => this.availableUsers = res.data ?? [],
      });
    }

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
          this.editSponsorIds = p.sponsors.map(s => s.userId);

          this.editForm.patchValue({
            name: p.name,
            description: p.description ?? '',
            projectType: p.projectType,
            strategicObjectiveId: p.strategicObjectiveId ?? null,
            priority: p.priority,
            projectManagerUserId: p.projectManagerUserId,
            plannedStartDate: p.plannedStartDate ? String(p.plannedStartDate).substring(0, 10) : '',
            plannedEndDate: p.plannedEndDate ? String(p.plannedEndDate).substring(0, 10) : '',
            approvedBudget: p.approvedBudget ?? null,
            status: p.status,
            actualStartDate: p.actualStartDate ? String(p.actualStartDate).substring(0, 10) : '',
          });

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

          if (p.projectType === ProjectType.Strategic) {
            this.loadEditStrategicObjectives(p.workspaceId);
          }

          this.editDrawerLoading = false;
          this.cdr.markForCheck();
        },
        error: () => {
          this.editDrawerError = 'Failed to load project.';
          this.editDrawerLoading = false;
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

  get filteredEditSponsorUsers(): AssignableUser[] {
    if (!this.editSponsorSearchTerm.trim()) return this.availableUsers;
    const term = this.editSponsorSearchTerm.toLowerCase();
    return this.availableUsers.filter(u => u.fullName.toLowerCase().includes(term));
  }

  toggleEditSponsor(userId: string): void {
    const idx = this.editSponsorIds.indexOf(userId);
    if (idx >= 0) this.editSponsorIds.splice(idx, 1);
    else this.editSponsorIds.push(userId);
  }

  isEditSponsorSelected(userId: string): boolean {
    return this.editSponsorIds.includes(userId);
  }

  getEditSponsorName(userId: string): string {
    return this.availableUsers.find(u => u.id === userId)?.fullName ?? userId;
  }

  private loadEditStrategicObjectives(workspaceId: string): void {
    this.projectSvc.getStrategicObjectivesForWorkspace(workspaceId).subscribe({
      next: res => { this.editStrategicObjectives = res.data ?? []; this.cdr.markForCheck(); },
      error: () => { this.editStrategicObjectives = []; this.cdr.markForCheck(); },
    });
  }

  onEditSubmit(): void {
    this.editForm.markAllAsTouched();
    if (this.editForm.invalid || this.editSponsorIds.length === 0) {
      if (this.editSponsorIds.length === 0)
        this.editDrawerError = 'At least one sponsor is required.';
      return;
    }

    this.editDrawerSaving = true;
    this.editDrawerError = null;
    const v = this.editForm.getRawValue();

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
      status: v.status,
      strategicObjectiveId: v.strategicObjectiveId || undefined,
      priority: v.priority,
      projectManagerUserId: v.projectManagerUserId,
      sponsorUserIds: this.editSponsorIds,
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
        this.editDrawerError = err?.error?.message ?? err?.error?.detail ?? 'Failed to update project.';
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

  canEdit(entry: MyProjectEntry): boolean {
    return entry.role === 'manager'
      && entry.project.status !== ProjectStatus.Completed
      && entry.project.status !== ProjectStatus.Cancelled;
  }
}
