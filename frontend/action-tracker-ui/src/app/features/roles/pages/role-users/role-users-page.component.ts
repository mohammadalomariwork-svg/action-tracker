import {
  Component, OnInit, ChangeDetectionStrategy,
  inject, signal,
} from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { debounceTime, Subject } from 'rxjs';

import { RoleManagementService }    from '../../services/role-management.service';
import { UserManagementService }    from '../../../user-management/services/user-management.service';
import { ToastService }             from '../../../../core/services/toast.service';
import { RoleUserDto }              from '../../models/role.model';
import { UserListItem }             from '../../../user-management/models/user-management.models';
import { HasPermissionDirective }   from '../../../../shared';
import { AdminBreadcrumbComponent } from '../../../admin/components/shared/admin-breadcrumb/admin-breadcrumb.component';
import { PageHeaderComponent }      from '../../../../shared/components/page-header/page-header.component';

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
  private readonly toast       = inject(ToastService);

  readonly roleName    = signal('');
  readonly roleUsers   = signal<RoleUserDto[]>([]);
  readonly loading     = signal(false);
  readonly error       = signal<string | null>(null);

  // Remove user state
  readonly removingUserId       = signal<string | null>(null);
  readonly confirmRemoveUser    = signal<RoleUserDto | null>(null);
  readonly removingInProgress   = signal(false);

  // Add users modal state
  readonly showAddModal     = signal(false);
  readonly searchQuery      = signal('');
  readonly searchResults    = signal<UserListItem[]>([]);
  readonly searchLoading    = signal(false);
  readonly selectedUserIds  = signal<Set<string>>(new Set());
  readonly assigning        = signal(false);

  private readonly searchSubject = new Subject<string>();

  ngOnInit(): void {
    const name = this.route.snapshot.paramMap.get('roleName') ?? '';
    this.roleName.set(name);
    this.loadRoleUsers();

    this.searchSubject.pipe(debounceTime(300)).subscribe(q => {
      this.fetchSearchResults(q);
    });
  }

  loadRoleUsers(): void {
    this.loading.set(true);
    this.error.set(null);
    this.roleSvc.getRoleUsers(this.roleName()).subscribe({
      next: (users) => { this.roleUsers.set(users); this.loading.set(false); },
      error: ()     => { this.error.set('Failed to load role users.'); this.loading.set(false); },
    });
  }

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

  goBack(): void {
    this.router.navigate(['/admin/roles']);
  }
}
