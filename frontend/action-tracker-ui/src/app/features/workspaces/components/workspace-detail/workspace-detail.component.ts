import { Component, OnInit, DestroyRef, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, ActivatedRoute, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { forkJoin } from 'rxjs';

import { WorkspaceService } from '../../services/workspace.service';
import { ProjectService } from '../../../projects/services/project.service';
import { ProjectActionItemService } from '../../../projects/services/action-item.service';
import { AuthService } from '../../../../core/services/auth.service';
import { Workspace, WorkspaceAdmin, UserDropdownItem } from '../../models/workspace.model';
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
  imports: [CommonModule, RouterLink, FormsModule],
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

  workspaceId!: string;
  workspace: Workspace | null = null;
  projects: ProjectList[] = [];
  standaloneActions: ActionItemList[] = [];
  activeTab: 'admins' | 'projects' | 'actions' = 'admins';
  isLoading = false;
  errorMessage: string | null = null;
  successMessage: string | null = null;
  canManage = false;

  // Admin management
  availableUsers: UserDropdownItem[] = [];
  selectedUserId = '';
  isAddingAdmin = false;

  // Expose enums to template
  readonly ProjectStatus = ProjectStatus;
  readonly ProjectType = ProjectType;
  readonly ActionItemStatus = ActionItemStatus;
  readonly ActionItemPriority = ActionItemPriority;

  ngOnInit(): void {
    this.workspaceId = this.route.snapshot.paramMap.get('id')!;
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
          this.workspace = result.workspace.data ?? null;
          this.projects = result.projects ?? [];
          this.standaloneActions = result.actions ?? [];
          this.resolvePermissions();
          this.isLoading = false;
          if (this.canManage) {
            this.loadAvailableUsers();
          }
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
    this.authService.currentUser$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(user => {
        if (user && this.workspace?.admins?.some(a => a.userId === user.email)) {
          this.canManage = true;
        }
      });
  }

  /** Loads available users for admin dropdown (filters out already-assigned admins). */
  private loadAvailableUsers(): void {
    this.workspaceService.getActiveUsersForDropdown()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          const allUsers = res.data ?? [];
          const existingIds = new Set(this.workspace?.admins?.map(a => a.userId) ?? []);
          this.availableUsers = allUsers.filter(u => !existingIds.has(u.id));
        },
        error: () => { /* silently fail */ },
      });
  }

  /** Adds selected user as a workspace admin. */
  onAddAdmin(): void {
    if (!this.selectedUserId || !this.workspace) return;

    const user = this.availableUsers.find(u => u.id === this.selectedUserId);
    if (!user) return;

    this.isAddingAdmin = true;
    this.errorMessage = null;
    this.successMessage = null;

    const updatedAdmins: WorkspaceAdmin[] = [
      ...(this.workspace.admins ?? []),
      { userId: user.id, userName: user.displayName },
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

  /** Removes an admin from the workspace. */
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

  /** Switches the active tab. */
  setTab(tab: 'admins' | 'projects' | 'actions'): void {
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

  getProgressClass(percentage: number): string {
    if (percentage >= 75) return 'bg-success';
    if (percentage >= 40) return 'bg-warning';
    return 'bg-danger';
  }
}
