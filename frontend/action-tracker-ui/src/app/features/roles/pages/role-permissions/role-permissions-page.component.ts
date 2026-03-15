import {
  Component, OnInit, ChangeDetectionStrategy,
  inject, signal, computed,
} from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

import { RoleManagementService }  from '../../services/role-management.service';
import { ToastService }           from '../../../../core/services/toast.service';
import { PermissionMatrixDto }    from '../../../permissions/models/role-permission.model';
import { AreaActionMappingDto }   from '../../../permissions/models/permission-catalog.model';
import { AssignPermissionEntryDto } from '../../models/role.model';
import { HasPermissionDirective } from '../../../../shared';
import { AdminBreadcrumbComponent }  from '../../../admin/components/shared/admin-breadcrumb/admin-breadcrumb.component';
import { PageHeaderComponent }       from '../../../../shared/components/page-header/page-header.component';

interface CellState {
  checked: boolean;
}

@Component({
  selector: 'app-role-permissions-page',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule, FormsModule,
    HasPermissionDirective,
    AdminBreadcrumbComponent,
    PageHeaderComponent,
  ],
  templateUrl: './role-permissions-page.component.html',
  styleUrl:    './role-permissions-page.component.scss',
})
export class RolePermissionsPageComponent implements OnInit {
  private readonly route    = inject(ActivatedRoute);
  private readonly router   = inject(Router);
  private readonly roleSvc  = inject(RoleManagementService);
  private readonly toast    = inject(ToastService);

  readonly roleName  = signal('');
  readonly matrix    = signal<PermissionMatrixDto | null>(null);
  readonly loading   = signal(false);
  readonly error     = signal<string | null>(null);
  readonly saving    = signal(false);

  // cellStates[areaId][actionId] = CellState
  readonly cellStates = signal<Record<string, Record<string, CellState>>>({});

  readonly areas = computed(() => {
    const m = this.matrix();
    if (!m) return [];
    const seen = new Map<string, { id: string; name: string; displayName: string }>();
    for (const mp of m.availableMappings) {
      if (!seen.has(mp.areaId)) {
        seen.set(mp.areaId, { id: mp.areaId, name: mp.areaName, displayName: mp.areaDisplayName });
      }
    }
    return [...seen.values()].sort((a, b) => a.displayName.localeCompare(b.displayName));
  });

  readonly actions = computed(() => {
    const m = this.matrix();
    if (!m) return [];
    const seen = new Map<string, { id: string; name: string; displayName: string }>();
    for (const mp of m.availableMappings) {
      if (!seen.has(mp.actionId)) {
        seen.set(mp.actionId, { id: mp.actionId, name: mp.actionName, displayName: mp.actionDisplayName });
      }
    }
    return [...seen.values()].sort((a, b) => a.displayName.localeCompare(b.displayName));
  });

  ngOnInit(): void {
    const name = this.route.snapshot.paramMap.get('roleName') ?? '';
    this.roleName.set(name);
    this.loadMatrix();
  }

  loadMatrix(): void {
    this.loading.set(true);
    this.error.set(null);
    this.roleSvc.getRolePermissions(this.roleName()).subscribe({
      next: (m) => {
        this.matrix.set(m);
        this.initCellStates(m);
        this.loading.set(false);
      },
      error: () => { this.error.set('Failed to load permissions matrix.'); this.loading.set(false); },
    });
  }

  private initCellStates(m: PermissionMatrixDto): void {
    const states: Record<string, Record<string, CellState>> = {};

    const permSet = new Set(m.permissions.map(p => `${p.areaId}|${p.actionId}`));

    for (const mp of m.availableMappings) {
      if (!states[mp.areaId]) states[mp.areaId] = {};
      states[mp.areaId][mp.actionId] = {
        checked: permSet.has(`${mp.areaId}|${mp.actionId}`),
      };
    }

    this.cellStates.set(states);
  }

  hasMappingFor(areaId: string, actionId: string): boolean {
    const m = this.matrix();
    if (!m) return false;
    return m.availableMappings.some(mp => mp.areaId === areaId && mp.actionId === actionId);
  }

  getCellState(areaId: string, actionId: string): CellState | null {
    return this.cellStates()[areaId]?.[actionId] ?? null;
  }

  toggleCell(areaId: string, actionId: string): void {
    const states = { ...this.cellStates() };
    if (!states[areaId]) states[areaId] = {};
    const cell = states[areaId][actionId];
    if (!cell) return;
    states[areaId] = { ...states[areaId], [actionId]: { checked: !cell.checked } };
    this.cellStates.set(states);
  }

  savePermissions(): void {
    this.saving.set(true);
    const permissions: AssignPermissionEntryDto[] = [];
    const states = this.cellStates();

    for (const areaId of Object.keys(states)) {
      for (const actionId of Object.keys(states[areaId])) {
        if (states[areaId][actionId].checked) {
          permissions.push({ areaId, actionId });
        }
      }
    }

    this.roleSvc.assignPermissionsToRole({ roleName: this.roleName(), permissions }).subscribe({
      next: () => {
        this.toast.success('Permissions saved successfully.');
        this.saving.set(false);
        this.loadMatrix();
      },
      error: () => { this.toast.error('Failed to save permissions.'); this.saving.set(false); },
    });
  }

  goBack(): void {
    this.router.navigate(['/admin/roles']);
  }
}
