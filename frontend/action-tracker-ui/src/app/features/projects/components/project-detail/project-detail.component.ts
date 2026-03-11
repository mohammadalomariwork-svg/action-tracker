import { Component, OnInit, DestroyRef, inject, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { ProjectService } from '../../services/project.service';
import { ToastService } from '../../../../core/services/toast.service';
import {
  ProjectResponse, ProjectUpdate, ProjectStats,
  ProjectType, ProjectStatus, ProjectPriority,
  StrategicObjectiveOption,
} from '../../models/project.models';
import { AssignableUser } from '../../../../core/models/action-item.model';
import { BreadcrumbComponent } from '../../../../shared/components/breadcrumb/breadcrumb.component';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { CommentsSectionComponent } from '../../../../shared/components/comments-section/comments-section.component';
import { DocumentsSectionComponent } from '../../../../shared/components/documents-section/documents-section.component';
import { MilestoneSectionComponent } from '../milestone-section/milestone-section.component';

interface EditFormData {
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

@Component({
  selector: 'app-project-detail',
  standalone: true,
  imports: [
    CommonModule, RouterLink, FormsModule,
    CommentsSectionComponent, DocumentsSectionComponent,
    MilestoneSectionComponent, BreadcrumbComponent, PageHeaderComponent,
  ],
  templateUrl: './project-detail.component.html',
  styleUrl: './project-detail.component.scss',
})
export class ProjectDetailComponent implements OnInit {
  private readonly projectService = inject(ProjectService);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);
  private readonly toastSvc = inject(ToastService);

  projectId!: string;
  project: ProjectResponse | null = null;
  stats: ProjectStats | null = null;
  isLoading = false;
  errorMessage: string | null = null;

  readonly ProjectType = ProjectType;
  readonly ProjectStatus = ProjectStatus;
  readonly ProjectPriority = ProjectPriority;

  // ── Edit form state ──────────────────────────────────
  showEditForm = false;
  saving = false;
  allUsers: AssignableUser[] = [];
  strategicObjectives: StrategicObjectiveOption[] = [];
  strategicObjectivesLoaded = false;
  sponsorDropdownOpen = false;
  sponsorSearchTerm = '';

  editForm: EditFormData = this.emptyEditForm();

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

  ngOnInit(): void {
    this.projectId = this.route.snapshot.paramMap.get('id')!;
    this.loadProject();
    this.loadStats();
    this.loadUsers();
  }

  // ── Data loading ───────────────────────────────────────
  private loadProject(): void {
    this.isLoading = true;
    this.errorMessage = null;

    this.projectService.getById(this.projectId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          this.project = res.data ?? null;
          this.isLoading = false;
        },
        error: (err) => {
          this.errorMessage = err?.error?.message ?? 'Failed to load project details.';
          this.isLoading = false;
        },
      });
  }

  loadStats(): void {
    this.projectService.getStats(this.projectId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => { this.stats = res.data ?? null; },
        error: () => {},
      });
  }

  private loadUsers(): void {
    this.projectService.getAssignableUsers()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => { this.allUsers = res.data ?? []; },
        error: () => {},
      });
  }

  // ── Edit form ──────────────────────────────────────────
  private emptyEditForm(): EditFormData {
    return {
      name: '', description: '',
      projectType: ProjectType.Operational,
      strategicObjectiveId: null,
      priority: ProjectPriority.Medium,
      projectManagerUserId: '',
      sponsorUserIds: [],
      plannedStartDate: '', plannedEndDate: '',
      approvedBudget: null,
      status: ProjectStatus.Draft,
      actualStartDate: '',
    };
  }

  openEditForm(): void {
    const prj = this.project;
    if (!prj) return;

    this.editForm = {
      name: prj.name,
      description: prj.description ?? '',
      projectType: prj.projectType,
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

    this.sponsorDropdownOpen = false;
    this.sponsorSearchTerm = '';

    if (prj.projectType === ProjectType.Strategic) {
      this.loadStrategicObjectives();
    } else {
      this.strategicObjectives = [];
      this.strategicObjectivesLoaded = false;
    }

    this.showEditForm = true;
  }

  cancelEditForm(): void {
    this.showEditForm = false;
    this.sponsorDropdownOpen = false;
    this.sponsorSearchTerm = '';
  }

  onProjectTypeChange(): void {
    if (this.editForm.projectType === ProjectType.Strategic) {
      this.loadStrategicObjectives();
    } else {
      this.editForm.strategicObjectiveId = null;
      this.strategicObjectives = [];
      this.strategicObjectivesLoaded = false;
    }
  }

  private loadStrategicObjectives(): void {
    if (!this.project) return;
    this.projectService.getStrategicObjectivesForWorkspace(this.project.workspaceId)
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
    return this.editForm.projectType === ProjectType.Strategic;
  }

  get hasDateRangeError(): boolean {
    return !!(this.editForm.plannedStartDate && this.editForm.plannedEndDate
      && this.editForm.plannedEndDate <= this.editForm.plannedStartDate);
  }

  saveEdit(): void {
    if (!this.editForm.name.trim() || !this.editForm.projectManagerUserId
      || this.editForm.sponsorUserIds.length === 0
      || !this.editForm.plannedStartDate || !this.editForm.plannedEndDate
      || this.hasDateRangeError) {
      return;
    }
    if (this.isProjectStrategic && !this.editForm.strategicObjectiveId) {
      return;
    }

    this.saving = true;

    const payload: ProjectUpdate = {
      name: this.editForm.name.trim(),
      description: this.editForm.description?.trim() || undefined,
      projectType: this.editForm.projectType,
      status: this.editForm.status,
      strategicObjectiveId: this.editForm.strategicObjectiveId || undefined,
      priority: this.editForm.priority,
      projectManagerUserId: this.editForm.projectManagerUserId,
      sponsorUserIds: this.editForm.sponsorUserIds,
      plannedStartDate: this.editForm.plannedStartDate,
      plannedEndDate: this.editForm.plannedEndDate,
      actualStartDate: this.editForm.actualStartDate || undefined,
      approvedBudget: this.editForm.approvedBudget ? +this.editForm.approvedBudget : undefined,
    };

    this.projectService.update(this.projectId, payload)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.saving = false;
          this.showEditForm = false;
          this.toastSvc.success('Project updated.');
          this.loadProject();
          this.loadStats();
        },
        error: (err) => {
          this.saving = false;
          this.toastSvc.error(err?.error?.message ?? 'Failed to update project.');
        },
      });
  }

  // ── Sponsor helpers ────────────────────────────────────
  @HostListener('document:click')
  onDocumentClick(): void {
    this.sponsorDropdownOpen = false;
  }

  get filteredSponsorUsers(): AssignableUser[] {
    if (!this.sponsorSearchTerm.trim()) return this.allUsers;
    const term = this.sponsorSearchTerm.toLowerCase();
    return this.allUsers.filter(u => u.fullName.toLowerCase().includes(term));
  }

  toggleSponsor(userId: string): void {
    const idx = this.editForm.sponsorUserIds.indexOf(userId);
    if (idx >= 0) this.editForm.sponsorUserIds.splice(idx, 1);
    else this.editForm.sponsorUserIds.push(userId);
  }

  isSponsorSelected(userId: string): boolean {
    return this.editForm.sponsorUserIds.includes(userId);
  }

  getSponsorName(userId: string): string {
    return this.allUsers.find(u => u.id === userId)?.fullName ?? userId;
  }

  // ── Display helpers ────────────────────────────────────
  priorityClass(p: ProjectPriority): string {
    switch (p) {
      case ProjectPriority.Critical: return 'badge bg-danger';
      case ProjectPriority.High:     return 'badge bg-warning text-dark';
      case ProjectPriority.Medium:   return 'badge bg-info text-dark';
      case ProjectPriority.Low:      return 'badge bg-secondary';
      default:                       return 'badge bg-light text-dark';
    }
  }

  statusClass(s: ProjectStatus): string {
    switch (s) {
      case ProjectStatus.Draft:     return 'badge bg-secondary';
      case ProjectStatus.Active:    return 'badge bg-primary';
      case ProjectStatus.OnHold:    return 'badge bg-warning text-dark';
      case ProjectStatus.Completed: return 'badge bg-success';
      case ProjectStatus.Cancelled: return 'badge bg-danger';
      default:                      return 'badge bg-light text-dark';
    }
  }

  typeClass(t: ProjectType): string {
    switch (t) {
      case ProjectType.Strategic:   return 'badge bg-primary';
      case ProjectType.Operational: return 'badge bg-info text-dark';
      default:                      return 'badge bg-light text-dark';
    }
  }
}
