import { Component, OnInit, DestroyRef, HostListener, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { forkJoin } from 'rxjs';
import * as XLSX from 'xlsx';

import { WorkspaceService } from '../../services/workspace.service';
import { WorkspaceList, WorkspaceSummary, OrgUnitDropdownItem, UserDropdownItem, WorkspaceAdmin } from '../../models/workspace.model';
import { BreadcrumbComponent } from '../../../../shared/components/breadcrumb/breadcrumb.component';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { HasPermissionDirective } from '../../../../shared/directives/has-permission.directive';
import { OrgUnitSelectComponent } from '../../../../shared';

@Component({
  selector: 'app-workspace-list',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, RouterLink, BreadcrumbComponent, PageHeaderComponent, HasPermissionDirective, OrgUnitSelectComponent],
  templateUrl: './workspace-list.component.html',
  styleUrl: './workspace-list.component.scss',
})
export class WorkspaceListComponent implements OnInit {
  private readonly workspaceService   = inject(WorkspaceService);
  private readonly router             = inject(Router);
  private readonly destroyRef         = inject(DestroyRef);
  private readonly fb                 = inject(FormBuilder);

  // Data
  allWorkspaces: WorkspaceList[] = [];
  filteredWorkspaces: WorkspaceList[] = [];
  pagedWorkspaces: WorkspaceList[] = [];
  summary: WorkspaceSummary | null = null;

  // State
  isLoading   = false;
  errorMessage: string | null = null;

  // Search, sort & org unit filter
  searchTerm = '';
  orgUnitFilter = '';
  uniqueOrgUnits: { id: string; name: string; level: number }[] = [];
  sortField: 'title' | 'organizationUnit' | 'createdAt' = 'createdAt';
  sortDirection: 'asc' | 'desc' = 'desc';

  // Pagination
  currentPage = 1;
  pageSize = 10;
  totalPages = 1;

  // ── Drawer (create / edit) ──────────────────────────────────────────────────
  showDrawer      = false;
  isEditDrawer    = false;
  editingWorkspaceId: string | null = null;
  drawerLoading   = false;
  drawerError: string | null = null;

  form!: FormGroup;
  orgUnits: OrgUnitDropdownItem[]  = [];
  adminUsers: UserDropdownItem[]   = [];
  selectedAdminIds: string[]       = [];
  adminsSubmitAttempted            = false;
  adminDropdownOpen                = false;
  adminSearchTerm                  = '';
  private adminUsersLoaded         = false;

  ngOnInit(): void {
    this.loadData();
  }

  loadData(): void {
    this.isLoading    = true;
    this.errorMessage = null;

    forkJoin({
      workspaces: this.workspaceService.getWorkspaces(),
      summary:    this.workspaceService.getSummary(this.orgUnitFilter || undefined),
      // Eager-load the org-unit catalog so the toolbar filter has level data
      // for the searchable dropdown without waiting for the drawer to open.
      orgUnits:   this.workspaceService.getOrgUnitsForDropdown(),
    })
    .pipe(takeUntilDestroyed(this.destroyRef))
    .subscribe({
      next: ({ workspaces, summary, orgUnits }) => {
        this.allWorkspaces = workspaces.data ?? [];
        this.summary       = summary.data ?? null;
        this.orgUnits      = orgUnits.data ?? [];
        this.deriveUniqueOrgUnits();
        this.applyFilters();
        this.isLoading = false;
      },
      error: (err) => {
        this.errorMessage = err?.error?.message ?? 'Failed to load workspaces.';
        this.isLoading    = false;
      },
    });
  }

  applyFilters(): void {
    let result = [...this.allWorkspaces];

    if (this.orgUnitFilter) {
      result = result.filter(w => w.orgUnitId === this.orgUnitFilter);
    }

    if (this.searchTerm.trim()) {
      const term = this.searchTerm.toLowerCase();
      result = result.filter(w =>
        w.title.toLowerCase().includes(term) ||
        w.organizationUnit.toLowerCase().includes(term) ||
        w.adminUserNames.toLowerCase().includes(term)
      );
    }

    result.sort((a, b) => {
      let cmp = 0;
      if (this.sortField === 'title') {
        cmp = a.title.localeCompare(b.title);
      } else if (this.sortField === 'organizationUnit') {
        cmp = a.organizationUnit.localeCompare(b.organizationUnit);
      } else {
        cmp = new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime();
      }
      return this.sortDirection === 'asc' ? cmp : -cmp;
    });

    this.filteredWorkspaces = result;
    this.totalPages = Math.max(1, Math.ceil(result.length / this.pageSize));
    if (this.currentPage > this.totalPages) this.currentPage = 1;
    this.updatePage();
  }

  updatePage(): void {
    const start = (this.currentPage - 1) * this.pageSize;
    this.pagedWorkspaces = this.filteredWorkspaces.slice(start, start + this.pageSize);
  }

  onSearchChange(): void {
    this.currentPage = 1;
    this.applyFilters();
  }

  onOrgUnitFilterChange(): void {
    this.currentPage = 1;
    this.workspaceService.getSummary(this.orgUnitFilter || undefined)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({ next: (res) => { this.summary = res.data ?? null; } });
    this.applyFilters();
  }

  private deriveUniqueOrgUnits(): void {
    const seen = new Set<string>();
    this.uniqueOrgUnits = [];
    for (const w of this.allWorkspaces) {
      if (!w.orgUnitId || seen.has(w.orgUnitId)) continue;
      seen.add(w.orgUnitId);
      const level = this.orgUnits.find(o => o.id === w.orgUnitId)?.level ?? 1;
      this.uniqueOrgUnits.push({ id: w.orgUnitId, name: w.organizationUnit, level });
    }
    this.uniqueOrgUnits.sort((a, b) => a.level - b.level || a.name.localeCompare(b.name));
  }

  toggleSort(field: 'title' | 'organizationUnit'): void {
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

  get pageStart(): number {
    return (this.currentPage - 1) * this.pageSize + 1;
  }

  get pageEnd(): number {
    return Math.min(this.currentPage * this.pageSize, this.filteredWorkspaces.length);
  }

  get pages(): number[] {
    const pages: number[] = [];
    for (let i = 1; i <= this.totalPages; i++) pages.push(i);
    return pages;
  }

  getInitials(title: string): string {
    return title
      .split(/\s+/)
      .filter(w => w.length > 0)
      .map(w => w[0].toUpperCase())
      .slice(0, 2)
      .join('');
  }

  getAvatarColor(name: string): string {
    const colors = [
      '#3b82f6', '#ef4444', '#f59e0b', '#10b981', '#8b5cf6',
      '#ec4899', '#06b6d4', '#f97316', '#6366f1', '#14b8a6'
    ];
    let hash = 0;
    for (let i = 0; i < name.length; i++) {
      hash = name.charCodeAt(i) + ((hash << 5) - hash);
    }
    return colors[Math.abs(hash) % colors.length];
  }

  formatDate(dateStr: string): string {
    const d = new Date(dateStr);
    return d.toLocaleDateString('en-US', { month: 'short', year: 'numeric' });
  }

  onView(id: string): void {
    this.router.navigate(['/workspaces', id]);
  }

  onEdit(id: string): void {
    this.isEditDrawer      = true;
    this.editingWorkspaceId = id;
    this.drawerError       = null;
    this.adminsSubmitAttempted = false;
    this.selectedAdminIds  = [];
    this.adminSearchTerm   = '';
    this.adminDropdownOpen = false;
    this.buildDrawerForm();
    this.ensureDropdownsLoaded();
    this.drawerLoading = true;

    this.workspaceService.getWorkspaceById(id)
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
          this.drawerLoading = false;
        },
        error: (err) => {
          this.drawerError   = err?.error?.message ?? 'Failed to load workspace.';
          this.drawerLoading = false;
        },
      });

    this.showDrawer = true;
  }

  onDelete(id: string): void {
    if (!confirm('Are you sure you want to delete this workspace?')) return;

    this.workspaceService
      .deleteWorkspace(id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => this.loadData(),
        error: (err) => {
          this.errorMessage = err?.error?.message ?? 'Failed to delete workspace.';
        },
      });
  }

  onRestore(id: string): void {
    if (!confirm('Restore this workspace?')) return;

    this.workspaceService
      .restoreWorkspace(id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => this.loadData(),
        error: (err) => {
          this.errorMessage = err?.error?.message ?? 'Failed to restore workspace.';
        },
      });
  }

  exportToExcel(): void {
    const rows = this.filteredWorkspaces.map(w => ({
      'Title': w.title,
      'Organization Unit': w.organizationUnit,
      'Admins': w.adminUserNames,
      'Status': w.isActive ? 'Active' : 'Inactive',
      'Projects': w.projectCount,
      'Open Actions': w.openActionItemCount,
      'Created': this.formatDate(w.createdAt),
    }));

    const ws = XLSX.utils.json_to_sheet(rows);
    const wb = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(wb, ws, 'Workspaces');
    XLSX.writeFile(wb, `workspaces-${new Date().toISOString().slice(0, 10)}.xlsx`);
  }

  printToPDF(): void {
    window.print();
  }

  onCreateNew(): void {
    this.isEditDrawer      = false;
    this.editingWorkspaceId = null;
    this.drawerError       = null;
    this.adminsSubmitAttempted = false;
    this.selectedAdminIds  = [];
    this.adminSearchTerm   = '';
    this.adminDropdownOpen = false;
    this.buildDrawerForm();
    this.ensureDropdownsLoaded();
    this.showDrawer = true;
  }

  closeDrawer(): void {
    this.showDrawer = false;
  }

  // ── Drawer form helpers ───────────────────────────────────────────────────────

  get hasAdminsError(): boolean {
    return this.adminsSubmitAttempted && this.selectedAdminIds.length === 0;
  }

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

  unitLabel(unit: OrgUnitDropdownItem): string {
    const indent = '— '.repeat(unit.level - 1);
    return `${indent}${unit.name}${unit.code ? ' (' + unit.code + ')' : ''}`;
  }

  hasError(field: string, error: string): boolean {
    const ctrl = this.form?.get(field);
    return !!(ctrl?.touched && ctrl.hasError(error));
  }

  isInvalid(field: string): boolean {
    const ctrl = this.form?.get(field);
    return !!(ctrl?.touched && ctrl.invalid);
  }

  onDrawerSubmit(): void {
    this.adminsSubmitAttempted = true;

    if (this.form.invalid || this.selectedAdminIds.length === 0) {
      this.form.markAllAsTouched();
      return;
    }

    this.drawerLoading = true;
    this.drawerError   = null;

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

    const request$ = this.isEditDrawer && this.editingWorkspaceId !== null
      ? this.workspaceService.updateWorkspace(this.editingWorkspaceId, {
          id: this.editingWorkspaceId,
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
        this.drawerLoading = false;
        this.showDrawer    = false;
        this.loadData();
      },
      error: (err) => {
        this.drawerError   = err?.error?.message ?? 'An error occurred. Please try again.';
        this.drawerLoading = false;
      },
    });
  }

  private buildDrawerForm(): void {
    this.form = this.fb.group({
      title:            ['', [Validators.required, Validators.minLength(3), Validators.maxLength(200)]],
      organizationUnit: ['', [Validators.required]],
      isActive:         [true],
    });
  }

  private ensureDropdownsLoaded(): void {
    // Org units are eagerly loaded in loadData(); only the admin user list
    // is fetched lazily when the drawer is first opened.
    if (this.adminUsersLoaded) return;

    this.workspaceService.getActiveUsersForDropdown()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => { this.adminUsers = res.data ?? []; this.adminUsersLoaded = true; },
        error: () => { this.drawerError = 'Failed to load users.'; },
      });
  }
}
