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

import { UserManagementService } from '../../services/user-management.service';
import { ToastService } from '../../../../core/services/toast.service';
import {
  UserListItem,
  UpdateUserRoleRequest,
} from '../../models/user-management.models';

const AVAILABLE_ROLES = ['Admin', 'Manager', 'User', 'Viewer'] as const;

@Component({
  selector: 'app-user-list',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './user-list.component.html',
  styleUrl: './user-list.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class UserListComponent implements OnInit {
  private readonly userMgmtService = inject(UserManagementService);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);
  private readonly destroyRef = inject(DestroyRef);

  // ── State signals ───────────────────────────────────────────────────────────
  readonly users = signal<UserListItem[]>([]);
  readonly totalCount = signal(0);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly currentPage = signal(1);
  readonly pageSize = signal(20);

  /** ID of the row currently showing the role selector. */
  readonly roleEditingUserId = signal<string | null>(null);
  /** The role value held in the inline selector before submitting. */
  readonly pendingRole = signal<string>('');

  readonly totalPages = computed(() =>
    Math.max(1, Math.ceil(this.totalCount() / this.pageSize()))
  );

  readonly availableRoles = AVAILABLE_ROLES;

  ngOnInit(): void {
    this.loadUsers();
  }

  loadUsers(): void {
    this.loading.set(true);
    this.error.set(null);

    this.userMgmtService
      .getUsers(this.currentPage(), this.pageSize())
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
    this.pendingRole.set(user.roles[0] ?? 'User');
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

  // ── Navigation ──────────────────────────────────────────────────────────────

  navigateToRegisterExternal(): void {
    this.router.navigate(['/admin/users/register-external']);
  }

  navigateToRegisterAD(): void {
    this.router.navigate(['/admin/users/register-ad']);
  }
}
