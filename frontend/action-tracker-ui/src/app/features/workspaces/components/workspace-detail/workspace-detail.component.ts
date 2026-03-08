import { Component, OnInit, DestroyRef, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { WorkspaceService } from '../../services/workspace.service';
import { AuthService } from '../../../../core/services/auth.service';
import { Workspace, WorkspaceAdmin, UserDropdownItem } from '../../models/workspace.model';

@Component({
  selector: 'app-workspace-detail',
  standalone: true,
  imports: [CommonModule, RouterLink, FormsModule],
  templateUrl: './workspace-detail.component.html',
  styleUrl: './workspace-detail.component.scss',
})
export class WorkspaceDetailComponent implements OnInit {
  private readonly workspaceService = inject(WorkspaceService);
  private readonly authService      = inject(AuthService);
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

  ngOnInit(): void {
    this.workspaceId = this.route.snapshot.paramMap.get('id')!;
    this.loadData();
  }

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
        error: () => { /* silently fail */ },
      });
  }

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
}
