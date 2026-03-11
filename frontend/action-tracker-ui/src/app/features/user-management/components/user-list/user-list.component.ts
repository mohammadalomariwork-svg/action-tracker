import {
  Component,
  OnInit,
  ChangeDetectionStrategy,
  DestroyRef,
  inject,
  signal,
  computed,
} from '@angular/core';
import { Router } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subject, debounceTime, distinctUntilChanged } from 'rxjs';

import { UserManagementService } from '../../services/user-management.service';
import { OrgUnitService } from '../../../admin/services/org-unit.service';
import { OrgUnitTree } from '../../../admin/models/org-chart.models';
import { ToastService } from '../../../../core/services/toast.service';
import {
  UserListItem,
  UpdateUserRoleRequest,
} from '../../models/user-management.models';
import { BreadcrumbComponent } from '../../../../shared/components/breadcrumb/breadcrumb.component';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';

interface OrgUnitOption { id: string; label: string; }

const AVAILABLE_ROLES = ['Admin', 'Manager', 'User', 'Viewer'] as const;

@Component({
  selector: 'app-user-list',
  standalone: true,
  imports: [CommonModule, FormsModule, BreadcrumbComponent, PageHeaderComponent],
  templateUrl: './user-list.component.html',
  styleUrl: './user-list.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class UserListComponent implements OnInit {
  private readonly userMgmtService = inject(UserManagementService);
  private readonly orgUnitService  = inject(OrgUnitService);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);
  private readonly destroyRef = inject(DestroyRef);

  // ── State signals ───────────────────────────────────────────────────────────
  readonly users = signal<UserListItem[]>([]);
  readonly totalCount = signal(0);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly currentPage = signal(1);
  readonly pageSize = signal(10);

  readonly searchTerm = signal('');
  readonly sortBy = signal('fullName');
  readonly sortDir = signal<'asc' | 'desc'>('asc');

  /** ID of the row currently showing the role selector. */
  readonly roleEditingUserId = signal<string | null>(null);
  /** The role value held in the inline selector before submitting. */
  readonly pendingRole = signal<string>('');

  /** Flat list of org units for the assignment dropdown. */
  readonly orgUnitOptions = signal<OrgUnitOption[]>([]);
  /** ID of the row currently showing the org unit picker. */
  readonly orgUnitEditingUserId = signal<string | null>(null);
  /** Pending org unit ID in the inline picker (empty string = unassign). */
  readonly pendingOrgUnitId = signal<string>('');

  readonly totalPages = computed(() =>
    Math.max(1, Math.ceil(this.totalCount() / this.pageSize()))
  );

  readonly availableRoles = AVAILABLE_ROLES;

  private readonly searchInput$ = new Subject<string>();

  ngOnInit(): void {
    this.searchInput$
      .pipe(
        debounceTime(350),
        distinctUntilChanged(),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe((term) => {
        this.searchTerm.set(term);
        this.currentPage.set(1);
        this.loadUsers();
      });

    this.loadUsers();
    this.loadOrgUnits();
  }

  private loadOrgUnits(): void {
    this.orgUnitService
      .getTree()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (tree) => {
          this.orgUnitOptions.set(tree ? this.flattenTree(tree) : []);
        },
      });
  }

  private flattenTree(node: OrgUnitTree, depth = 0): OrgUnitOption[] {
    const indent  = '\u00A0\u00A0\u00A0\u00A0'.repeat(depth);
    const prefix  = node.code ? `[${node.code}] ` : '';
    const options: OrgUnitOption[] = [{ id: node.id, label: `${indent}${prefix}${node.name}` }];
    for (const child of node.children ?? []) {
      options.push(...this.flattenTree(child, depth + 1));
    }
    return options;
  }

  onSearchInput(value: string): void {
    this.searchInput$.next(value);
  }

  sort(column: string): void {
    if (this.sortBy() === column) {
      this.sortDir.set(this.sortDir() === 'asc' ? 'desc' : 'asc');
    } else {
      this.sortBy.set(column);
      this.sortDir.set('asc');
    }
    this.currentPage.set(1);
    this.loadUsers();
  }

  loadUsers(): void {
    this.loading.set(true);
    this.error.set(null);

    this.userMgmtService
      .getUsers(
        this.currentPage(),
        this.pageSize(),
        this.searchTerm(),
        this.sortBy(),
        this.sortDir()
      )
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          this.users.set(res.users);
          this.totalCount.set(res.totalCount);
          this.loading.set(false);
        },
        error: (err) => {
          this.error.set(err?.error?.message ?? 'Failed to load users.');
          this.loading.set(false);
        },
      });
  }

  goToPage(page: number): void {
    if (page < 1 || page > this.totalPages()) return;
    this.currentPage.set(page);
    this.loadUsers();
  }

  // ── Change Role ─────────────────────────────────────────────────────────────

  openRoleEdit(user: UserListItem): void {
    this.roleEditingUserId.set(user.id);
    this.pendingRole.set(user.roles?.[0] ?? 'User');
  }

  cancelRoleEdit(): void {
    this.roleEditingUserId.set(null);
  }

  submitRoleChange(userId: string): void {
    const req: UpdateUserRoleRequest = {
      userId,
      roleName: this.pendingRole(),
    };

    this.userMgmtService
      .updateUserRole(req)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.roleEditingUserId.set(null);
          this.toast.success('Role updated successfully.');
          this.loadUsers();
        },
        error: (err) => {
          this.toast.error(err?.error?.message ?? 'Failed to update role.');
        },
      });
  }

  // ── Deactivate ──────────────────────────────────────────────────────────────

  deactivate(user: UserListItem): void {
    if (!window.confirm(`Deactivate ${user.fullName}? They will no longer be able to log in.`)) {
      return;
    }

    this.userMgmtService
      .deactivateUser(user.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.toast.success(`${user.fullName} has been deactivated.`);
          this.loadUsers();
        },
        error: (err) => {
          this.toast.error(err?.error?.message ?? 'Failed to deactivate user.');
        },
      });
  }

  // ── Reactivate ──────────────────────────────────────────────────────────────

  reactivate(user: UserListItem): void {
    if (!window.confirm(`Reactivate ${user.fullName}? They will be able to log in again.`)) {
      return;
    }

    this.userMgmtService
      .reactivateUser(user.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.toast.success(`${user.fullName} has been reactivated.`);
          this.loadUsers();
        },
        error: (err) => {
          this.toast.error(err?.error?.message ?? 'Failed to reactivate user.');
        },
      });
  }

  // ── Assign Org Unit ─────────────────────────────────────────────────────────

  openOrgUnitEdit(user: UserListItem): void {
    this.orgUnitEditingUserId.set(user.id);
    this.pendingOrgUnitId.set(user.orgUnitId ?? '');
  }

  cancelOrgUnitEdit(): void {
    this.orgUnitEditingUserId.set(null);
  }

  submitOrgUnitChange(userId: string): void {
    const rawId = this.pendingOrgUnitId();
    const orgUnitId = rawId === '' ? null : rawId;

    this.userMgmtService
      .assignOrgUnit(userId, { orgUnitId })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.orgUnitEditingUserId.set(null);
          this.toast.success('Org unit updated successfully.');
          this.loadUsers();
        },
        error: (err) => {
          this.toast.error(err?.error?.message ?? 'Failed to update org unit.');
        },
      });
  }

  // ── Navigation ──────────────────────────────────────────────────────────────

  navigateToRegisterExternal(): void {
    this.router.navigate(['/admin/users/register-external']);
  }

  navigateToRegisterAD(): void {
    this.router.navigate(['/admin/users/register-ad']);
  }
}
