import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AdminBreadcrumbComponent } from '../../../admin/components/shared/admin-breadcrumb/admin-breadcrumb.component';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { RolePermissionMatrixComponent } from './components/role-permission-matrix/role-permission-matrix.component';
import { RolePermissionService } from '../../services/role-permission.service';
import { PermissionMatrixDto } from '../../models/role-permission.model';

@Component({
  selector: 'app-role-permissions-page',
  standalone: true,
  imports: [
    CommonModule,
    AdminBreadcrumbComponent,
    PageHeaderComponent,
    RolePermissionMatrixComponent,
  ],
  templateUrl: './role-permissions-page.component.html',
  styleUrl: './role-permissions-page.component.scss',
})
export class RolePermissionsPageComponent implements OnInit {
  private readonly rolePermissionService = inject(RolePermissionService);

  readonly roles        = signal<string[]>([]);
  readonly selectedRole = signal<string | null>(null);
  readonly matrix       = signal<PermissionMatrixDto | null>(null);

  readonly loadingRoles  = signal(false);
  readonly loadingMatrix = signal(false);
  readonly errorRoles    = signal<string | null>(null);
  readonly errorMatrix   = signal<string | null>(null);

  ngOnInit(): void {
    this.loadRoles();
  }

  private loadRoles(): void {
    this.loadingRoles.set(true);
    this.errorRoles.set(null);

    this.rolePermissionService.getAllRoles().subscribe({
      next: (roles) => {
        this.roles.set(roles);
        this.loadingRoles.set(false);
        if (roles.length > 0) {
          this.selectRole(roles[0]);
        }
      },
      error: (err) => {
        this.loadingRoles.set(false);
        this.errorRoles.set(err?.error?.message ?? 'Failed to load roles.');
      },
    });
  }

  selectRole(role: string): void {
    if (this.selectedRole() === role && this.matrix() !== null) return;
    this.selectedRole.set(role);
    this.loadMatrix(role);
  }

  private loadMatrix(roleName: string): void {
    this.loadingMatrix.set(true);
    this.errorMatrix.set(null);
    this.matrix.set(null);

    this.rolePermissionService.getMatrix(roleName).subscribe({
      next: (matrix) => {
        this.matrix.set(matrix);
        this.loadingMatrix.set(false);
      },
      error: (err) => {
        this.loadingMatrix.set(false);
        this.errorMatrix.set(err?.error?.message ?? 'Failed to load permission matrix.');
      },
    });
  }

  onPermissionChanged(): void {
    const role = this.selectedRole();
    if (role) {
      this.loadMatrix(role);
    }
  }
}
