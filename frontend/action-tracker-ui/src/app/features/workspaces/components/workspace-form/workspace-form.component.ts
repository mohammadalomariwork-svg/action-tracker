import { Component, OnInit, DestroyRef, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { WorkspaceService } from '../../services/workspace.service';
import { OrgUnitDropdownItem, UserDropdownItem, WorkspaceAdmin } from '../../models/workspace.model';

@Component({
  selector: 'app-workspace-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, FormsModule],
  templateUrl: './workspace-form.component.html',
  styleUrl: './workspace-form.component.scss',
})
export class WorkspaceFormComponent implements OnInit {
  private readonly fb               = inject(FormBuilder);
  private readonly workspaceService = inject(WorkspaceService);
  private readonly route            = inject(ActivatedRoute);
  private readonly router           = inject(Router);
  private readonly destroyRef       = inject(DestroyRef);

  isEditMode    = false;
  workspaceId: number | null = null;
  isLoading     = false;
  errorMessage: string | null = null;

  orgUnits: OrgUnitDropdownItem[] = [];
  adminUsers: UserDropdownItem[]  = [];

  /** Currently selected admins for this workspace. */
  selectedAdmins: WorkspaceAdmin[] = [];
  /** The user ID chosen in the "Add admin" dropdown. */
  selectedUserId = '';
  /** Whether the form was submitted with an empty admin list. */
  adminsSubmitAttempted = false;

  form!: FormGroup;

  ngOnInit(): void {
    const idParam = this.route.snapshot.paramMap.get('id');
    if (idParam) {
      this.isEditMode  = true;
      this.workspaceId = +idParam;
    }

    this.buildForm();
    this.loadDropdownData();

    if (this.isEditMode && this.workspaceId !== null) {
      this.loadWorkspace(this.workspaceId);
    }
  }

  // ── Admin list helpers ────────────────────────────────────────────────────────

  /** Users not yet in selectedAdmins (prevents duplicates in the picker). */
  get availableUsers(): UserDropdownItem[] {
    return this.adminUsers.filter(u => !this.selectedAdmins.some(a => a.userId === u.id));
  }

  /** Adds the currently selected user to the admin list. */
  addAdmin(): void {
    if (!this.selectedUserId) return;
    const user = this.adminUsers.find(u => u.id === this.selectedUserId);
    if (!user) return;
    this.selectedAdmins = [...this.selectedAdmins, { userId: user.id, userName: user.displayName }];
    this.selectedUserId = '';
  }

  /** Removes an admin from the list by their user ID. */
  removeAdmin(userId: string): void {
    this.selectedAdmins = this.selectedAdmins.filter(a => a.userId !== userId);
  }

  /** True when the admin list is empty and user has tried to submit. */
  get hasAdminsError(): boolean {
    return this.adminsSubmitAttempted && this.selectedAdmins.length === 0;
  }

  // ── Org unit label ────────────────────────────────────────────────────────────

  /** Builds a visually indented label for an org-unit option. */
  unitLabel(unit: OrgUnitDropdownItem): string {
    const indent = '— '.repeat(unit.level - 1);
    return `${indent}${unit.name}${unit.code ? ' (' + unit.code + ')' : ''}`;
  }

  // ── Form helpers ──────────────────────────────────────────────────────────────

  /** Returns true when the given field is touched and has the given error. */
  hasError(field: string, error: string): boolean {
    const ctrl = this.form.get(field);
    return !!(ctrl?.touched && ctrl.hasError(error));
  }

  /** Returns true when the given field is touched and invalid (any error). */
  isInvalid(field: string): boolean {
    const ctrl = this.form.get(field);
    return !!(ctrl?.touched && ctrl.invalid);
  }

  // ── Private helpers ───────────────────────────────────────────────────────────

  private buildForm(): void {
    this.form = this.fb.group({
      title:            ['', [Validators.required, Validators.minLength(3), Validators.maxLength(200)]],
      organizationUnit: ['', [Validators.required]],
      isActive:         [true],
    });
  }

  private loadDropdownData(): void {
    this.workspaceService
      .getOrgUnitsForDropdown()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => { this.orgUnits = res.data ?? []; },
        error: () => { this.errorMessage = 'Failed to load organisation units.'; },
      });

    this.workspaceService
      .getActiveUsersForDropdown()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => { this.adminUsers = res.data ?? []; },
        error: () => { this.errorMessage = 'Failed to load users.'; },
      });
  }

  private loadWorkspace(id: number): void {
    this.isLoading = true;

    this.workspaceService
      .getWorkspaceById(id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          const w = res.data;
          this.form.patchValue({
            title:            w.title,
            organizationUnit: w.organizationUnit,
            isActive:         w.isActive,
          });
          this.selectedAdmins = (w.admins ?? []).map(a => ({
            userId:   a.userId,
            userName: a.userName,
          }));
          this.isLoading = false;
        },
        error: (err) => {
          this.errorMessage = err?.error?.message ?? 'Failed to load workspace.';
          this.isLoading    = false;
        },
      });
  }

  // ── Actions ───────────────────────────────────────────────────────────────────

  /** Submits the form — creates or updates depending on mode. */
  onSubmit(): void {
    this.adminsSubmitAttempted = true;

    if (this.form.invalid || this.selectedAdmins.length === 0) {
      this.form.markAllAsTouched();
      return;
    }

    this.isLoading    = true;
    this.errorMessage = null;

    const { title, organizationUnit, isActive } = this.form.value as {
      title: string;
      organizationUnit: string;
      isActive: boolean;
    };

    const request$ = this.isEditMode && this.workspaceId !== null
      ? this.workspaceService.updateWorkspace(this.workspaceId, {
          id: this.workspaceId,
          title,
          organizationUnit,
          admins: this.selectedAdmins,
          isActive,
        })
      : this.workspaceService.createWorkspace({
          title,
          organizationUnit,
          admins: this.selectedAdmins,
        });

    request$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.isLoading = false;
        this.router.navigate(['/workspaces']);
      },
      error: (err) => {
        this.errorMessage = err?.error?.message ?? 'An error occurred. Please try again.';
        this.isLoading    = false;
      },
    });
  }

  /** Navigates back to the workspace list without saving. */
  onCancel(): void {
    this.router.navigate(['/workspaces']);
  }
}
