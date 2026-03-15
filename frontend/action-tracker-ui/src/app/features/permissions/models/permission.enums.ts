/**
 * Frontend enum mirrors for backend PermissionArea / PermissionAction.
 * Values are 0-based on the frontend; add 1 for wire format if the backend
 * uses 1-based integers (legacy endpoints only — new endpoints use GUIDs).
 *
 * The string label values in PERMISSION_AREA_LABELS / PERMISSION_ACTION_LABELS
 * MUST match the `areaName` / `actionName` values stored in the permission
 * catalog on the backend so that client-side permission checks work correctly.
 */

// ── Permission Areas ─────────────────────────────────────────────────────────

export enum PermissionArea {
  Dashboard             = 0,
  Workspaces            = 1,
  Projects              = 2,
  Milestones            = 3,
  ActionItems           = 4,
  StrategicObjectives   = 5,
  KPIs                  = 6,
  Reports               = 7,
  OrgChart              = 8,
  UserManagement        = 9,
  PermissionsManagement = 10,
  Roles                 = 11,
}

export const PERMISSION_AREA_LABELS: Record<PermissionArea, string> = {
  [PermissionArea.Dashboard]:             'Dashboard',
  [PermissionArea.Workspaces]:            'Workspaces',
  [PermissionArea.Projects]:              'Projects',
  [PermissionArea.Milestones]:            'Milestones',
  [PermissionArea.ActionItems]:           'ActionItems',
  [PermissionArea.StrategicObjectives]:   'StrategicObjectives',
  [PermissionArea.KPIs]:                  'KPIs',
  [PermissionArea.Reports]:               'Reports',
  [PermissionArea.OrgChart]:              'OrgChart',
  [PermissionArea.UserManagement]:        'UserManagement',
  [PermissionArea.PermissionsManagement]: 'PermissionsManagement',
  [PermissionArea.Roles]:                 'Roles',
};

// ── Permission Actions ───────────────────────────────────────────────────────

export enum PermissionAction {
  View   = 0,
  Create = 1,
  Edit   = 2,
  Delete = 3,
  Approve = 4,
  Export  = 5,
  Assign  = 6,
}

export const PERMISSION_ACTION_LABELS: Record<PermissionAction, string> = {
  [PermissionAction.View]:    'View',
  [PermissionAction.Create]:  'Create',
  [PermissionAction.Edit]:    'Edit',
  [PermissionAction.Delete]:  'Delete',
  [PermissionAction.Approve]: 'Approve',
  [PermissionAction.Export]:  'Export',
  [PermissionAction.Assign]:  'Assign',
};

// ── Org Unit Scope ───────────────────────────────────────────────────────────

export enum OrgUnitScope {
  All             = 0,
  SpecificOrgUnit = 1,
  OwnOnly         = 2,
}

export const ORG_UNIT_SCOPE_LABELS: Record<OrgUnitScope, string> = {
  [OrgUnitScope.All]:             'All Org Units',
  [OrgUnitScope.SpecificOrgUnit]: 'Specific Org Unit',
  [OrgUnitScope.OwnOnly]:         'Own Only',
};
