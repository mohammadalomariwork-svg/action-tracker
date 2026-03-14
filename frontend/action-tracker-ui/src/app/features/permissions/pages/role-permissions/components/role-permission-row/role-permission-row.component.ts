import { Component, input, output } from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  PermissionAction,
  PermissionArea,
  PERMISSION_ACTION_LABELS,
  PERMISSION_AREA_LABELS,
} from '../../../../models/permission.enums';
import { RolePermissionDto } from '../../../../models/role-permission.model';

export interface ToggleChangeEvent {
  area:    PermissionArea;
  action:  PermissionAction;
  checked: boolean;
}

/** Set of (area → applicable actions). Cells outside this set are grayed out. */
const APPLICABLE_ACTIONS = new Map<PermissionArea, Set<PermissionAction>>([
  [PermissionArea.Dashboard,             new Set([PermissionAction.View])],
  [PermissionArea.Workspaces,            new Set([PermissionAction.View, PermissionAction.Create, PermissionAction.Edit, PermissionAction.Delete])],
  [PermissionArea.Projects,              new Set([PermissionAction.View, PermissionAction.Create, PermissionAction.Edit, PermissionAction.Delete, PermissionAction.Approve, PermissionAction.Export])],
  [PermissionArea.Milestones,            new Set([PermissionAction.View, PermissionAction.Create, PermissionAction.Edit, PermissionAction.Delete])],
  [PermissionArea.ActionItems,           new Set([PermissionAction.View, PermissionAction.Create, PermissionAction.Edit, PermissionAction.Delete, PermissionAction.Assign])],
  [PermissionArea.StrategicObjectives,   new Set([PermissionAction.View, PermissionAction.Create, PermissionAction.Edit, PermissionAction.Delete])],
  [PermissionArea.KPIs,                  new Set([PermissionAction.View, PermissionAction.Create, PermissionAction.Edit, PermissionAction.Delete])],
  [PermissionArea.Reports,               new Set([PermissionAction.View, PermissionAction.Export])],
  [PermissionArea.OrgChart,              new Set([PermissionAction.View, PermissionAction.Create, PermissionAction.Edit, PermissionAction.Delete])],
  [PermissionArea.UserManagement,        new Set([PermissionAction.View, PermissionAction.Create, PermissionAction.Edit, PermissionAction.Delete])],
  [PermissionArea.PermissionsManagement, new Set([PermissionAction.View, PermissionAction.Create, PermissionAction.Edit, PermissionAction.Delete])],
]);

@Component({
  selector: 'app-role-permission-row',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './role-permission-row.component.html',
  styleUrl: './role-permission-row.component.scss',
})
export class RolePermissionRowComponent {
  readonly area          = input.required<PermissionArea>();
  readonly permissionMap = input.required<Map<string, RolePermissionDto>>();
  readonly saving        = input<boolean>(false);

  readonly toggleChanged = output<ToggleChangeEvent>();

  readonly areaLabels   = PERMISSION_AREA_LABELS;
  readonly actionLabels = PERMISSION_ACTION_LABELS;

  /** All action enum values in display order. */
  readonly allActions: PermissionAction[] = (
    Object.values(PermissionAction).filter((v): v is PermissionAction => typeof v === 'number')
  );

  isApplicable(action: PermissionAction): boolean {
    return APPLICABLE_ACTIONS.get(this.area())?.has(action) ?? false;
  }

  isChecked(action: PermissionAction): boolean {
    const key = `${this.areaLabels[this.area()]}:${this.actionLabels[action]}`;
    return this.permissionMap().has(key);
  }

  onToggle(action: PermissionAction, event: Event): void {
    if (!this.isApplicable(action)) return;
    const checked = (event.target as HTMLInputElement).checked;
    this.toggleChanged.emit({ area: this.area(), action, checked });
  }
}
