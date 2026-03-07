import { Component, OnInit, DestroyRef, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, ActivatedRoute, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { forkJoin } from 'rxjs';

import { WorkspaceService } from '../../services/workspace.service';
import { ProjectService } from '../../../projects/services/project.service';
import { ProjectActionItemService } from '../../../projects/services/action-item.service';
import { AuthService } from '../../../../core/services/auth.service';
import { WorkspaceList } from '../../models/workspace.model';
import {
  ProjectList,
  ProjectStatus,
  ProjectType,
  ActionItemList,
  ActionItemStatus,
  ActionItemPriority,
} from '../../../projects/models/project.models';

@Component({
  selector: 'app-workspace-detail',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './workspace-detail.component.html',
  styleUrl: './workspace-detail.component.scss',
})
export class WorkspaceDetailComponent implements OnInit {
  private readonly workspaceService = inject(WorkspaceService);
  private readonly projectService   = inject(ProjectService);
  private readonly actionItemService = inject(ProjectActionItemService);
  private readonly authService      = inject(AuthService);
  private readonly router           = inject(Router);
  private readonly route            = inject(ActivatedRoute);
  private readonly destroyRef       = inject(DestroyRef);

  workspaceId!: number;
  workspace: WorkspaceList | null = null;
  projects: ProjectList[] = [];
  standaloneActions: ActionItemList[] = [];
  activeTab: 'projects' | 'actions' = 'projects';
  isLoading = false;
  errorMessage: string | null = null;
  canManage = false;

  // Expose enums to template
  readonly ProjectStatus = ProjectStatus;
  readonly ProjectType = ProjectType;
  readonly ActionItemStatus = ActionItemStatus;
  readonly ActionItemPriority = ActionItemPriority;

  ngOnInit(): void {
    this.workspaceId = Number(this.route.snapshot.paramMap.get('id'));
    this.loadData();
  }

  /** Loads workspace info, projects, and standalone action items in parallel. */
  private loadData(): void {
    this.isLoading = true;
    this.errorMessage = null;

    forkJoin({
      workspace: this.workspaceService.getWorkspaceById(this.workspaceId),
      projects: this.projectService.getByWorkspace(this.workspaceId),
      actions: this.actionItemService.getStandaloneByWorkspace(this.workspaceId),
    })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (result) => {
          this.workspace = result.workspace.data as unknown as WorkspaceList;
          this.projects = result.projects ?? [];
          this.standaloneActions = result.actions ?? [];
          this.resolvePermissions();
          this.isLoading = false;
        },
        error: (err) => {
          this.errorMessage = err?.error?.message ?? 'Failed to load workspace details.';
          this.isLoading = false;
        },
      });
  }

  /** Determines whether the current user can manage this workspace. */
  private resolvePermissions(): void {
    if (this.authService.hasRole('Admin') || this.authService.hasRole('Manager')) {
      this.canManage = true;
      return;
    }
    // Check if the current user is listed as a workspace admin
    const adminNames = (this.workspace as any)?.adminUserNames ?? '';
    this.authService.currentUser$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(user => {
        if (user && adminNames.includes(user.displayName)) {
          this.canManage = true;
        }
      });
  }

  /** Switches the active tab. */
  setTab(tab: 'projects' | 'actions'): void {
    this.activeTab = tab;
  }

  /** Navigates to the project detail page. */
  navigateToProject(projectId: number): void {
    this.router.navigate(['/projects', projectId]);
  }

  /** Navigates to the new project form with the workspace pre-selected. */
  onAddProject(): void {
    this.router.navigate(['/projects/new'], {
      queryParams: { workspaceId: this.workspaceId },
    });
  }

  /** Navigates to the new standalone action item form. */
  onAddAction(): void {
    this.router.navigate(['/action-items/new'], {
      queryParams: { workspaceId: this.workspaceId },
    });
  }

  /** Confirms and deletes a project, then reloads the list. */
  onDeleteProject(projectId: number): void {
    if (!confirm('Are you sure you want to delete this project?')) return;

    this.projectService
      .delete(projectId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.projects = this.projects.filter(p => p.id !== projectId);
        },
        error: (err) => {
          this.errorMessage = err?.error?.message ?? 'Failed to delete project.';
        },
      });
  }

  /** Returns a Bootstrap badge class string based on the project status. */
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

  /** Returns a human-readable label for a project status. */
  getStatusLabel(status: ProjectStatus): string {
    return ProjectStatus[status] ?? 'Unknown';
  }

  /** Returns a Bootstrap badge class string based on the project type. */
  getTypeClass(type: ProjectType): string {
    return type === ProjectType.Strategic ? 'bg-primary' : 'bg-info text-dark';
  }

  /** Returns a human-readable label for a project type. */
  getTypeLabel(type: ProjectType): string {
    return ProjectType[type] ?? 'Unknown';
  }

  /** Returns a Bootstrap badge class for action item priority. */
  getPriorityClass(priority: ActionItemPriority): string {
    switch (priority) {
      case ActionItemPriority.Critical: return 'bg-danger';
      case ActionItemPriority.High:     return 'bg-warning text-dark';
      case ActionItemPriority.Medium:   return 'bg-info text-dark';
      case ActionItemPriority.Low:      return 'bg-secondary';
      default:                          return 'bg-secondary';
    }
  }

  /** Returns a human-readable label for action item priority. */
  getPriorityLabel(priority: ActionItemPriority): string {
    return ActionItemPriority[priority] ?? 'Unknown';
  }

  /** Returns a Bootstrap badge class for action item status. */
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

  /** Returns a human-readable label for action item status. */
  getActionStatusLabel(status: ActionItemStatus): string {
    return ActionItemStatus[status] ?? 'Unknown';
  }

  /** Returns a CSS class for the progress bar colour based on percentage. */
  getProgressClass(percentage: number): string {
    if (percentage >= 75) return 'bg-success';
    if (percentage >= 40) return 'bg-warning';
    return 'bg-danger';
  }
}
