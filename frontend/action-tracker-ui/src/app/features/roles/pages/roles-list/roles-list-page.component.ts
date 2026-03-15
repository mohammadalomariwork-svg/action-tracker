import {
  Component, OnInit, ChangeDetectionStrategy,
  inject, signal,
} from '@angular/core';
import { Router } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

import { RoleManagementService } from '../../services/role-management.service';
import { ToastService }          from '../../../../core/services/toast.service';
import { AppRoleDto }            from '../../models/role.model';
import { HasPermissionDirective } from '../../../../shared';
import { AdminBreadcrumbComponent } from '../../../admin/components/shared/admin-breadcrumb/admin-breadcrumb.component';
import { PageHeaderComponent }      from '../../../../shared/components/page-header/page-header.component';

@Component({
  selector: 'app-roles-list-page',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule, FormsModule,
    HasPermissionDirective,
    AdminBreadcrumbComponent,
    PageHeaderComponent,
  ],
  templateUrl: './roles-list-page.component.html',
  styleUrl:    './roles-list-page.component.scss',
})
export class RolesListPageComponent implements OnInit {
  private readonly roleSvc  = inject(RoleManagementService);
  private readonly toast    = inject(ToastService);
  private readonly router   = inject(Router);

  readonly roles       = signal<AppRoleDto[]>([]);
  readonly loading     = signal(false);
  readonly error       = signal<string | null>(null);

  // Add-role modal state
  readonly showAddForm     = signal(false);
  readonly newRoleName     = signal('');
  readonly addingRole      = signal(false);

  // Delete confirmation state
  readonly deletingRole    = signal<AppRoleDto | null>(null);
  readonly confirmDelete   = signal(false);
  readonly deletingInProgress = signal(false);
  readonly deleteError     = signal<string | null>(null);

  ngOnInit(): void {
    this.loadRoles();
  }

  loadRoles(): void {
    this.loading.set(true);
    this.error.set(null);
    this.roleSvc.getAllRoles().subscribe({
      next: (list) => { this.roles.set(list); this.loading.set(false); },
      error: ()     => { this.error.set('Failed to load roles.'); this.loading.set(false); },
    });
  }

  openAddForm(): void {
    this.newRoleName.set('');
    this.showAddForm.set(true);
  }

  closeAddForm(): void { this.showAddForm.set(false); }

  createRole(): void {
    const name = this.newRoleName().trim();
    if (!name) return;
    this.addingRole.set(true);
    this.roleSvc.createRole(name).subscribe({
      next: () => {
        this.toast.success(`Role "${name}" created.`);
        this.showAddForm.set(false);
        this.addingRole.set(false);
        this.loadRoles();
      },
      error: () => {
        this.toast.error('Failed to create role.');
        this.addingRole.set(false);
      },
    });
  }

  openDeleteConfirm(role: AppRoleDto): void {
    this.deletingRole.set(role);
    this.confirmDelete.set(true);
    this.deleteError.set(null);
  }

  cancelDelete(): void {
    this.confirmDelete.set(false);
    this.deletingRole.set(null);
  }

  doDelete(): void {
    const role = this.deletingRole();
    if (!role) return;
    this.deletingInProgress.set(true);
    this.deleteError.set(null);
    this.roleSvc.deleteRole(role.name).subscribe({
      next: () => {
        this.toast.success(`Role "${role.name}" deleted.`);
        this.confirmDelete.set(false);
        this.deletingRole.set(null);
        this.deletingInProgress.set(false);
        this.loadRoles();
      },
      error: (err) => {
        this.deletingInProgress.set(false);
        if (err?.status === 409) {
          this.deleteError.set('Role has active users assigned. Remove users first.');
        } else {
          this.deleteError.set('Failed to delete role.');
        }
      },
    });
  }

  goToPermissions(role: AppRoleDto): void {
    this.router.navigate(['/admin/roles', role.name, 'permissions']);
  }

  goToUsers(role: AppRoleDto): void {
    this.router.navigate(['/admin/roles', role.name, 'users']);
  }
}
