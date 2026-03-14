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
}

export enum PermissionAction {
  View    = 0,
  Create  = 1,
  Edit    = 2,
  Delete  = 3,
  Approve = 4,
  Export  = 5,
  Assign  = 6,
}

export enum OrgUnitScope {
  All             = 0,
  SpecificOrgUnit = 1,
  OwnOnly         = 2,
}

export const PERMISSION_AREA_LABELS: Record<PermissionArea, string> = {
  [PermissionArea.Dashboard]:             'Dashboard',
  [PermissionArea.Workspaces]:            'Workspaces',
  [PermissionArea.Projects]:              'Projects',
  [PermissionArea.Milestones]:            'Milestones',
  [PermissionArea.ActionItems]:           'Action Items',
  [PermissionArea.StrategicObjectives]:   'Strategic Objectives',
  [PermissionArea.KPIs]:                  'KPIs',
  [PermissionArea.Reports]:               'Reports',
  [PermissionArea.OrgChart]:              'Org Chart',
  [PermissionArea.UserManagement]:        'User Management',
  [PermissionArea.PermissionsManagement]: 'Permissions Management',
};

export const PERMISSION_ACTION_LABELS: Record<PermissionAction, string> = {
  [PermissionAction.View]:    'View',
  [PermissionAction.Create]:  'Create',
  [PermissionAction.Edit]:    'Edit',
  [PermissionAction.Delete]:  'Delete',
  [PermissionAction.Approve]: 'Approve',
  [PermissionAction.Export]:  'Export',
  [PermissionAction.Assign]:  'Assign',
};

export const ORG_UNIT_SCOPE_LABELS: Record<OrgUnitScope, string> = {
  [OrgUnitScope.All]:             'All',
  [OrgUnitScope.SpecificOrgUnit]: 'Specific Org Unit',
  [OrgUnitScope.OwnOnly]:         'Own Only',
};
