import { Component, OnInit, DestroyRef, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, ActivatedRoute, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { forkJoin } from 'rxjs';

import { ProjectService } from '../../services/project.service';
import { MilestoneService } from '../../services/milestone.service';
import { ProjectActionItemService } from '../../services/action-item.service';
import { BaselineService } from '../../services/baseline.service';
import { AuthService } from '../../../../core/services/auth.service';
import {
  ProjectDetail,
  ProjectType,
  ProjectStatus,
  MilestoneList,
  ActionItemList,
  ActionItemStatus,
  ActionItemPriority,
  MilestoneStatus,
  ProjectBaseline,
  BaselineChangeRequest,
} from '../../models/project.models';

export type ProjectTab =
  | 'overview'
  | 'milestones'
  | 'actions'
  | 'documents'
  | 'budget'
  | 'baseline'
  | 'comments';

@Component({
  selector: 'app-project-detail',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './project-detail.component.html',
  styleUrl: './project-detail.component.scss',
})
export class ProjectDetailComponent implements OnInit {
  private readonly projectService    = inject(ProjectService);
  private readonly milestoneService  = inject(MilestoneService);
  private readonly actionItemService = inject(ProjectActionItemService);
  private readonly baselineService   = inject(BaselineService);
  private readonly authService       = inject(AuthService);
  private readonly router            = inject(Router);
  private readonly route             = inject(ActivatedRoute);
  private readonly destroyRef        = inject(DestroyRef);

  projectId!: string;
  project: ProjectDetail | null = null;
  milestones: MilestoneList[] = [];
  projectActions: ActionItemList[] = [];
  baseline: ProjectBaseline | null = null;
  changeRequests: BaselineChangeRequest[] = [];

  activeTab: ProjectTab = 'overview';
  isLoading = false;
  errorMessage: string | null = null;
  successMessage: string | null = null;

  isProjectManager = false;
  isSponsor = false;
  isAdmin = false;
  canEdit = false;

  // Expose enums to template
  readonly ProjectType = ProjectType;
  readonly ProjectStatus = ProjectStatus;
  readonly MilestoneStatus = MilestoneStatus;
  readonly ActionItemStatus = ActionItemStatus;
  readonly ActionItemPriority = ActionItemPriority;

  ngOnInit(): void {
    this.projectId = this.route.snapshot.paramMap.get('id')!;
    this.loadData();
  }

  /** Calculates overall progress as average of milestone completion percentages. */
  get overallProgress(): number {
    if (!this.milestones.length) return this.project?.completionPercentage ?? 0;
    const total = this.milestones.reduce((sum, m) => sum + m.completionPercentage, 0);
    return Math.round(total / this.milestones.length);
  }

  /** Returns the number of days remaining until the planned end date. */
  get daysRemaining(): number {
    if (!this.project?.plannedEndDate) return 0;
    const end = new Date(this.project.plannedEndDate);
    const now = new Date();
    const diff = end.getTime() - now.getTime();
    return Math.max(0, Math.ceil(diff / (1000 * 60 * 60 * 24)));
  }

  /** Switches the active tab. */
  setTab(tab: ProjectTab): void {
    this.activeTab = tab;
  }

  /** Navigates to the edit form for the current project. */
  onEditProject(): void {
    this.router.navigate(['/projects', this.projectId, 'edit']);
  }

  /** Navigates to a milestone detail (or could open modal). */
  navigateToMilestone(milestoneId: string): void {
    this.router.navigate(['/projects', this.projectId, 'milestones', milestoneId]);
  }

  /** Baselines the project. */
  onBaselineProject(): void {
    if (!confirm('Are you sure you want to baseline this project? Planned dates will be locked.')) {
      return;
    }

    this.projectService
      .baselineProject(this.projectId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.successMessage = 'Project has been baselined successfully.';
          this.loadData();
          setTimeout(() => (this.successMessage = null), 4000);
        },
        error: (err) => {
          this.errorMessage = err?.error?.message ?? 'Failed to baseline project.';
        },
      });
  }

  // ── Badge helpers ──────────────────────────────────────────────────────────

  getStatusClass(status: ProjectStatus): string {
    switch (status) {
      case ProjectStatus.Draft:     return 'bg-secondary';
      case ProjectStatus.Active:    return 'bg-success';
      case ProjectStatus.OnHold:    return 'bg-warning text-dark';
      case ProjectStatus.Completed: return 'bg-info';
      case ProjectStatus.Cancelled: return 'bg-danger';
      default:                      return 'bg-secondary';
    }
  }

  getStatusLabel(status: ProjectStatus): string {
    return ProjectStatus[status] ?? 'Unknown';
  }

  getTypeClass(type: ProjectType): string {
    return type === ProjectType.Strategic ? 'bg-primary' : 'bg-info text-dark';
  }

  getTypeLabel(type: ProjectType): string {
    return ProjectType[type] ?? 'Unknown';
  }

  getMilestoneStatusClass(status: MilestoneStatus): string {
    switch (status) {
      case MilestoneStatus.NotStarted:  return 'bg-secondary';
      case MilestoneStatus.InProgress:  return 'bg-primary';
      case MilestoneStatus.Completed:   return 'bg-success';
      case MilestoneStatus.Delayed:     return 'bg-warning text-dark';
      case MilestoneStatus.Cancelled:   return 'bg-danger';
      default:                          return 'bg-secondary';
    }
  }

  getMilestoneStatusLabel(status: MilestoneStatus): string {
    return MilestoneStatus[status] ?? 'Unknown';
  }

  getActionStatusClass(status: ActionItemStatus): string {
    switch (status) {
      case ActionItemStatus.NotStarted: return 'bg-secondary';
      case ActionItemStatus.InProgress: return 'bg-primary';
      case ActionItemStatus.Completed:  return 'bg-success';
      case ActionItemStatus.Deferred:   return 'bg-warning text-dark';
      case ActionItemStatus.Cancelled:  return 'bg-danger';
      default:                          return 'bg-secondary';
    }
  }

  getActionStatusLabel(status: ActionItemStatus): string {
    return ActionItemStatus[status] ?? 'Unknown';
  }

  getPriorityClass(priority: ActionItemPriority): string {
    switch (priority) {
      case ActionItemPriority.Critical: return 'bg-danger';
      case ActionItemPriority.High:     return 'bg-warning text-dark';
      case ActionItemPriority.Medium:   return 'bg-info text-dark';
      case ActionItemPriority.Low:      return 'bg-secondary';
      default:                          return 'bg-secondary';
    }
  }

  getPriorityLabel(priority: ActionItemPriority): string {
    return ActionItemPriority[priority] ?? 'Unknown';
  }

  getProgressClass(percentage: number): string {
    if (percentage >= 75) return 'bg-success';
    if (percentage >= 40) return 'bg-warning';
    return 'bg-danger';
  }

  // ── Private helpers ─────────────────────────────────────────────────────────

  /** Loads project, milestones, action items, baseline, and change requests. */
  private loadData(): void {
    this.isLoading = true;
    this.errorMessage = null;

    forkJoin({
      project: this.projectService.getById(this.projectId),
      milestones: this.milestoneService.getByProject(this.projectId),
      actions: this.actionItemService.getByProject(this.projectId),
      baseline: this.baselineService.getByProject(this.projectId),
      changeRequests: this.baselineService.getChangeRequests(this.projectId),
    })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (result) => {
          this.project = result.project;
          this.milestones = result.milestones ?? [];
          this.projectActions = result.actions ?? [];
          this.baseline = result.baseline;
          this.changeRequests = result.changeRequests ?? [];
          this.resolvePermissions();
          this.isLoading = false;
        },
        error: (err) => {
          this.errorMessage = err?.error?.message ?? 'Failed to load project details.';
          this.isLoading = false;
        },
      });
  }

  /** Determines current user's relationship to the project. */
  private resolvePermissions(): void {
    this.isAdmin = this.authService.hasRole('Admin');

    this.authService.currentUser$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(user => {
        if (user && this.project) {
          this.isProjectManager = user.email === this.project.projectManagerUserId;
          this.isSponsor = user.email === this.project.sponsorUserId;
        }
        this.canEdit = this.isProjectManager || this.isAdmin;
      });
  }
}
