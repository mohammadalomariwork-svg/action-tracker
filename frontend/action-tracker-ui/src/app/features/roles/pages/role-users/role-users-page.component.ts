import {
  Component, OnInit, ChangeDetectionStrategy,
  DestroyRef, inject, signal,
} from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { debounceTime, Subject } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { RoleManagementService }    from '../../services/role-management.service';
import { UserManagementService }    from '../../../user-management/services/user-management.service';
import { OrgUnitService }           from '../../../admin/services/org-unit.service';
import { OrgUnitTree }              from '../../../admin/models/org-chart.models';
import { ToastService }             from '../../../../core/services/toast.service';
import { RoleUserDto }              from '../../models/role.model';
import { UserListItem }             from '../../../user-management/models/user-management.models';
import { HasPermissionDirective }   from '../../../../shared';
import { AdminBreadcrumbComponent } from '../../../admin/components/shared/admin-breadcrumb/admin-breadcrumb.component';
import { PageHeaderComponent }      from '../../../../shared/components/page-header/page-header.component';

interface OrgUnitOption { id: string; label: string; }

@Component({
  selector: 'app-role-users-page',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule, FormsModule,
    HasPermissionDirective,
    AdminBreadcrumbComponent,
    PageHeaderComponent,
  ],
  templateUrl: './role-users-page.component.html',
  styleUrl:    './role-users-page.component.scss',
})
export class RoleUsersPageComponent implements OnInit {
  private readonly route       = inject(ActivatedRoute);
  private readonly router      = inject(Router);
  private readonly roleSvc     = inject(RoleManagementService);
  private readonly userMgmtSvc = inject(UserManagementService);
  private readonly orgUnitSvc  = inject(OrgUnitService);
  private readonly toast       = inject(ToastService);
  private readonly destroyRef  = inject(DestroyRef);

  readonly roleName    = signal('');
  readonly roleUsers   = signal<RoleUserDto[]>([]);
  readonly loading     = signal(false);
  readonly error       = signal<string | null>(null);

  // Remove user state
  readonly confirmRemoveUser    = signal<RoleUserDto | null>(null);
  readonly removingInProgress   = signal(false);

  // Add users modal state
  readonly showAddModal     = signal(false);
  readonly searchQuery      = signal('');
  readonly searchResults    = signal<UserListItem[]>([]);
  readonly searchLoading    = signal(false);
  readonly selectedUserIds  = signal<Set<string>>(new Set());
  readonly assigning        = signal(false);

  // Org unit inline editing
  readonly orgUnitOptions        = signal<OrgUnitOption[]>([]);
  readonly orgUnitEditingUserId  = signal<string | null>(null);
  readonly pendingOrgUnitId      = signal<string>('');

  private readonly searchSubject = new Subject<string>();

  ngOnInit(): void {
    const name = this.route.snapshot.paramMap.get('roleName') ?? '';
    this.roleName.set(name);
    this.loadRoleUsers();
    this.loadOrgUnits();

    this.searchSubject.pipe(debounceTime(300)).subscribe(q => {
      this.fetchSearchResults(q);
    });
  }

  private loadOrgUnits(): void {
    this.orgUnitSvc
      .getTree()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (tree) => this.orgUnitOptions.set(tree ? this.flattenTree(tree) : []),
      });
  }

  private flattenTree(node: OrgUnitTree, depth = 0): OrgUnitOption[] {
    const indent = '\u00A0\u00A0\u00A0\u00A0'.repeat(depth);
    const prefix = node.code ? `[${node.code}] ` : '';
    const options: OrgUnitOption[] = [{ id: node.id, label: `${indent}${prefix}${node.name}` }];
    for (const child of node.children ?? []) {
      options.push(...this.flattenTree(child, depth + 1));
    }
    return options;
  }

  loadRoleUsers(): void {
    this.loading.set(true);
    this.error.set(null);
    this.roleSvc.getRoleUsers(this.roleName()).subscribe({
      next: (users) => { this.roleUsers.set(users); this.loading.set(false); },
      error: ()     => { this.error.set('Failed to load role users.'); this.loading.set(false); },
    });
  }

  // ── Remove user ────────────────────────────────────────────────────────────

  openRemoveConfirm(user: RoleUserDto): void {
    this.confirmRemoveUser.set(user);
  }

  cancelRemove(): void {
    this.confirmRemoveUser.set(null);
  }

  doRemoveUser(): void {
    const user = this.confirmRemoveUser();
    if (!user) return;
    this.removingInProgress.set(true);
    this.roleSvc.removeUsersFromRole({ roleName: this.roleName(), userIds: [user.userId] }).subscribe({
      next: () => {
        this.toast.success(`${user.userDisplayName} removed from role.`);
        this.confirmRemoveUser.set(null);
        this.removingInProgress.set(false);
        this.loadRoleUsers();
      },
      error: () => {
        this.toast.error('Failed to remove user from role.');
        this.removingInProgress.set(false);
      },
    });
  }

  // ── Add users modal ────────────────────────────────────────────────────────

  openAddModal(): void {
    this.searchQuery.set('');
    this.searchResults.set([]);
    this.selectedUserIds.set(new Set());
    this.showAddModal.set(true);
    this.fetchSearchResults('');
  }

  closeAddModal(): void { this.showAddModal.set(false); }

  onSearchChange(q: string): void {
    this.searchQuery.set(q);
    this.searchSubject.next(q);
  }

  private fetchSearchResults(q: string): void {
    this.searchLoading.set(true);
    this.userMgmtSvc.getUsers(1, 20, q).subscribe({
      next: (res) => { this.searchResults.set(res.users); this.searchLoading.set(false); },
      error: ()   => { this.searchLoading.set(false); },
    });
  }

  toggleUserSelection(userId: string): void {
    const set = new Set(this.selectedUserIds());
    if (set.has(userId)) set.delete(userId); else set.add(userId);
    this.selectedUserIds.set(set);
  }

  isSelected(userId: string): boolean {
    return this.selectedUserIds().has(userId);
  }

  alreadyInRole(userId: string): boolean {
    return this.roleUsers().some(u => u.userId === userId);
  }

  assignSelected(): void {
    const ids = [...this.selectedUserIds()];
    if (ids.length === 0) return;
    this.assigning.set(true);
    this.roleSvc.assignUsersToRole({ roleName: this.roleName(), userIds: ids }).subscribe({
      next: () => {
        this.toast.success(`${ids.length} user(s) assigned to role.`);
        this.showAddModal.set(false);
        this.assigning.set(false);
        this.loadRoleUsers();
      },
      error: () => {
        this.toast.error('Failed to assign users to role.');
        this.assigning.set(false);
      },
    });
  }

  // ── Assign org unit ────────────────────────────────────────────────────────

  openOrgUnitEdit(user: RoleUserDto): void {
    this.orgUnitEditingUserId.set(user.userId);
    this.pendingOrgUnitId.set(user.orgUnitId ?? '');
  }

  cancelOrgUnitEdit(): void {
    this.orgUnitEditingUserId.set(null);
  }

  submitOrgUnitChange(userId: string): void {
    const rawId = this.pendingOrgUnitId();
    const orgUnitId = rawId === '' ? null : rawId;
    this.userMgmtSvc
      .assignOrgUnit(userId, { orgUnitId })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.orgUnitEditingUserId.set(null);
          this.toast.success('Org unit updated.');
          this.loadRoleUsers();
        },
        error: (err) => {
          this.toast.error(err?.error?.message ?? 'Failed to update org unit.');
        },
      });
  }

  // ── Navigation ─────────────────────────────────────────────────────────────

  goBack(): void {
    this.router.navigate(['/admin/roles']);
  }
}
