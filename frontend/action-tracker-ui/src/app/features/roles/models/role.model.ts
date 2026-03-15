export interface AppRoleDto {
  id: string;
  name: string;
  userCount: number;
  permissionCount: number;
}

export interface RoleUserDto {
  userId: string;
  userDisplayName: string;
  email: string;
  orgUnitId?: string;
  orgUnitName?: string;
}

export interface AssignPermissionEntryDto {
  areaId: string;
  actionId: string;
}

export interface AssignRolePermissionsDto {
  roleName: string;
  permissions: AssignPermissionEntryDto[];
}

export interface AssignUsersToRoleDto {
  roleName: string;
  userIds: string[];
}
