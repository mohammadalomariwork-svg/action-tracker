import { Component, OnInit, DestroyRef, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { WorkspaceService } from '../../services/workspace.service';
import { WorkspaceList } from '../../models/workspace.model';

@Component({
  selector: 'app-workspace-list',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './workspace-list.component.html',
  styleUrl: './workspace-list.component.scss',
})
export class WorkspaceListComponent implements OnInit {
  private readonly workspaceService = inject(WorkspaceService);
  private readonly router           = inject(Router);
  private readonly destroyRef       = inject(DestroyRef);

  workspaces: WorkspaceList[] = [];
  isLoading   = false;
  errorMessage: string | null = null;

  ngOnInit(): void {
    this.loadWorkspaces();
  }

  /** Loads all workspaces (active and inactive) from the API. */
  loadWorkspaces(): void {
    this.isLoading    = true;
    this.errorMessage = null;

    this.workspaceService
      .getWorkspaces()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          this.workspaces = res.data ?? [];
          this.isLoading  = false;
        },
        error: (err) => {
          this.errorMessage = err?.error?.message ?? 'Failed to load workspaces.';
          this.isLoading    = false;
        },
      });
  }

  /** Navigates to the workspace detail / homepage. */
  onView(id: number): void {
    this.router.navigate(['/workspaces', id]);
  }

  /** Navigates to the edit form for the given workspace. */
  onEdit(id: number): void {
    this.router.navigate(['/workspaces/edit', id]);
  }

  /** Soft-deletes a workspace after user confirmation, then reloads the list. */
  onDelete(id: number): void {
    if (!confirm('Are you sure you want to delete this workspace?')) return;

    this.workspaceService
      .deleteWorkspace(id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => this.loadWorkspaces(),
        error: (err) => {
          this.errorMessage = err?.error?.message ?? 'Failed to delete workspace.';
        },
      });
  }

  /** Restores a soft-deleted workspace after user confirmation, then reloads the list. */
  onRestore(id: number): void {
    if (!confirm('Restore this workspace?')) return;

    this.workspaceService
      .restoreWorkspace(id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => this.loadWorkspaces(),
        error: (err) => {
          this.errorMessage = err?.error?.message ?? 'Failed to restore workspace.';
        },
      });
  }

  /** Navigates to the create workspace form. */
  onCreateNew(): void {
    this.router.navigate(['/workspaces/new']);
  }
}
