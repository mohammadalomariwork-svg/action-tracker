import { Component, OnInit, DestroyRef, HostListener, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { WorkspaceService } from '../../services/workspace.service';
import { OrgUnitDropdownItem, UserDropdownItem, WorkspaceAdmin } from '../../models/workspace.model';
import { BreadcrumbComponent } from '../../../../shared/components/breadcrumb/breadcrumb.component';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { OrgUnitSelectComponent } from '../../../../shared';

@Component({
  selector: 'app-workspace-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, FormsModule, BreadcrumbComponent, PageHeaderComponent, OrgUnitSelectComponent],
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
  workspaceId: string | null = null;
  isLoading     = false;
  errorMessage: string | null = null;

  orgUnits: OrgUnitDropdownItem[] = [];
  adminUsers: UserDropdownItem[]  = [];

  /** IDs of users currently selected as admins (drives the multi-select binding). */
  selectedAdminIds: string[] = [];
  /** Whether the form was submitted with an empty admin list. */
  adminsSubmitAttempted = false;

  /** Whether the admin multi-select dropdown is open. */
  adminDropdownOpen = false;
  /** Search term for filtering admin users in the dropdown. */
  adminSearchTerm = '';

  form!: FormGroup;

  ngOnInit(): void {
    const idParam = this.route.snapshot.paramMap.get('id');
    if (idParam) {
      this.isEditMode  = true;
      this.workspaceId = idParam;
    }

    this.buildForm();
    this.loadDropdownData();

    if (this.isEditMode && this.workspaceId !== null) {
      this.loadWorkspace(this.workspaceId);
    }
  }

  // ── Admin list helpers ────────────────────────────────────────────────────────

  /** True when the admin list is empty and user has tried to submit. */
  get hasAdminsError(): boolean {
    return this.adminsSubmitAttempted && this.selectedAdminIds.length === 0;
  }

  /** Users matching the current search term across name, email, and org unit. */
  get filteredAdminUsers(): UserDropdownItem[] {
    const term = this.adminSearchTerm.trim().toLowerCase();
    if (!term) return this.adminUsers;
    return this.adminUsers.filter(u =>
      (u.displayName ?? '').toLowerCase().includes(term) ||
      (u.email ?? '').toLowerCase().includes(term) ||
      (u.orgUnitName ?? '').toLowerCase().includes(term)
    );
  }

  getAdminName(userId: string): string {
    return this.adminUsers.find(u => u.id === userId)?.displayName ?? userId;
  }

  toggleAdmin(userId: string): void {
    const idx = this.selectedAdminIds.indexOf(userId);
    if (idx >= 0) this.selectedAdminIds.splice(idx, 1);
    else          this.selectedAdminIds.push(userId);
  }

  isAdminSelected(userId: string): boolean {
    return this.selectedAdminIds.includes(userId);
  }

  @HostListener('document:click')
  onDocumentClick(): void {
    if (this.adminDropdownOpen) this.adminDropdownOpen = false;
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

  private loadWorkspace(id: string): void {
    this.isLoading = true;

    this.workspaceService
      .getWorkspaceById(id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          const w = res.data;
          this.form.patchValue({
            title:            w.title,
            organizationUnit: w.orgUnitId ?? w.organizationUnit,
            isActive:         w.isActive,
          });
          this.selectedAdminIds = (w.admins ?? []).map(a => a.userId);
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

    if (this.form.invalid || this.selectedAdminIds.length === 0) {
      this.form.markAllAsTouched();
      return;
    }

    this.isLoading    = true;
    this.errorMessage = null;

    const { title, organizationUnit: orgUnitId, isActive } = this.form.value as {
      title: string;
      organizationUnit: string;
      isActive: boolean;
    };

    const selectedUnit = this.orgUnits.find(u => u.id === orgUnitId);
    const orgUnitName = selectedUnit?.name ?? orgUnitId;

    const admins: WorkspaceAdmin[] = this.selectedAdminIds.map(id => {
      const user = this.adminUsers.find(u => u.id === id);
      return {
        userId:      id,
        userName:    user?.displayName ?? '',
        email:       user?.email ?? '',
        orgUnitName: user?.orgUnitName ?? '',
      };
    });

    const request$ = this.isEditMode && this.workspaceId !== null
      ? this.workspaceService.updateWorkspace(this.workspaceId, {
          id: this.workspaceId,
          title,
          organizationUnit: orgUnitName,
          orgUnitId: orgUnitId || undefined,
          admins,
          isActive,
        })
      : this.workspaceService.createWorkspace({
          title,
          organizationUnit: orgUnitName,
          orgUnitId: orgUnitId || undefined,
          admins,
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
