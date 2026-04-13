import {
  Component, OnInit, ChangeDetectionStrategy,
  inject, signal, HostListener,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators, AbstractControl, ValidationErrors } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';

import { AuthService } from '../../../../core/services/auth.service';
import { ProjectService } from '../../services/project.service';
import { WorkspaceService } from '../../../workspaces/services/workspace.service';
import { WorkspaceList } from '../../../workspaces/models/workspace.model';
import { ToastService } from '../../../../core/services/toast.service';
import {
  ProjectResponse, ProjectStatus, ProjectPriority, ProjectType,
  StrategicObjectiveOption,
} from '../../models/project.models';
import { AssignableUser } from '../../../../core/models/action-item.model';
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
  private readonly authSvc      = inject(AuthService);
  private readonly projectSvc   = inject(ProjectService);
  private readonly workspaceSvc = inject(WorkspaceService);
  private readonly toastSvc     = inject(ToastService);
  private readonly fb           = inject(FormBuilder);

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

  readonly ProjectType     = ProjectType;
  readonly PRIORITY_OPTIONS = [
    { value: ProjectPriority.Low,      label: 'Low'      },
    { value: ProjectPriority.Medium,   label: 'Medium'   },
    { value: ProjectPriority.High,     label: 'High'     },
    { value: ProjectPriority.Critical, label: 'Critical' },
  ];

  @HostListener('document:click')
  onDocumentClick(): void { this.sponsorDropdownOpen = false; }

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
}
