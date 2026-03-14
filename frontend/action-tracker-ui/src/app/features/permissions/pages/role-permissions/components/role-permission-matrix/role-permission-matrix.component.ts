import { Component, computed, inject, input, output, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  PermissionAction,
  PermissionArea,
  PERMISSION_ACTION_LABELS,
  PERMISSION_AREA_LABELS,
} from '../../../../models/permission.enums';
import { PermissionMatrixDto, RolePermissionDto } from '../../../../models/role-permission.model';
import { RolePermissionService } from '../../../../services/role-permission.service';
import {
  RolePermissionRowComponent,
  ToggleChangeEvent,
} from '../role-permission-row/role-permission-row.component';

@Component({
  selector: 'app-role-permission-matrix',
  standalone: true,
  imports: [CommonModule, RolePermissionRowComponent],
  templateUrl: './role-permission-matrix.component.html',
  styleUrl: './role-permission-matrix.component.scss',
})
export class RolePermissionMatrixComponent {
  private readonly rolePermissionService = inject(RolePermissionService);

  readonly matrix  = input.required<PermissionMatrixDto>();
  readonly loading = input<boolean>(false);

  readonly permissionChanged = output<void>();

  readonly saving = signal(false);
  readonly error  = signal<string | null>(null);

  readonly actionLabels = PERMISSION_ACTION_LABELS;

  /** All PermissionArea enum values in display order. */
  readonly allAreas: PermissionArea[] = (
    Object.values(PermissionArea).filter((v): v is PermissionArea => typeof v === 'number')
  );

  /** All PermissionAction enum values in display order. */
  readonly allActions: PermissionAction[] = (
    Object.values(PermissionAction).filter((v): v is PermissionAction => typeof v === 'number')
  );

  /**
   * O(1) lookup map: "AreaLabel:ActionLabel" → RolePermissionDto.
   * Recomputed whenever the matrix input signal changes.
   */
  readonly permissionMap = computed<Map<string, RolePermissionDto>>(() => {
    const map = new Map<string, RolePermissionDto>();
    for (const perm of this.matrix().permissions) {
      map.set(`${perm.area}:${perm.action}`, perm);
    }
    return map;
  });

  onToggleChanged({ area, action, checked }: ToggleChangeEvent): void {
    if (checked) {
      this.createPermission(area, action);
    } else {
      const key  = `${PERMISSION_AREA_LABELS[area]}:${PERMISSION_ACTION_LABELS[action]}`;
      const perm = this.permissionMap().get(key);
      if (perm) {
        this.deletePermission(perm.id);
      }
    }
  }

  private createPermission(area: PermissionArea, action: PermissionAction): void {
    this.saving.set(true);
    this.error.set(null);

    // Frontend enums are 0-based; backend enums are 1-based → add 1 for the wire value.
    this.rolePermissionService
      .create({
        roleName:     this.matrix().roleName,
        area:         area + 1,
        action:       action + 1,
        orgUnitScope: 1, // OrgUnitScope.All = 1 on the backend
      })
      .subscribe({
        next:  () => { this.saving.set(false); this.permissionChanged.emit(); },
        error: (err) => {
          this.saving.set(false);
          this.error.set(err?.error?.message ?? 'Failed to grant permission.');
        },
      });
  }

  private deletePermission(id: string): void {
    this.saving.set(true);
    this.error.set(null);

    this.rolePermissionService.delete(id).subscribe({
      next:  () => { this.saving.set(false); this.permissionChanged.emit(); },
      error: (err) => {
        this.saving.set(false);
        this.error.set(err?.error?.message ?? 'Failed to revoke permission.');
      },
    });
  }
}
