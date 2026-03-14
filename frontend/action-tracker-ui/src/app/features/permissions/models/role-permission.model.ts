export interface RolePermissionDto {
  id:           string;
  roleName:     string;
  area:         string;
  action:       string;
  orgUnitScope: string;
  orgUnitId:    string | null;
  orgUnitName:  string | null;
  isActive:     boolean;
  createdAt:    string;
  createdBy:    string;
}

export interface CreateRolePermissionDto {
  roleName:     string;
  area:         number;
  action:       number;
  orgUnitScope: number;
  orgUnitId?:   string;
  orgUnitName?: string;
}

export interface UpdateRolePermissionDto {
  orgUnitScope: number;
  orgUnitId?:   string;
  orgUnitName?: string;
  isActive:     boolean;
}

export interface PermissionMatrixDto {
  roleName:    string;
  permissions: RolePermissionDto[];
}
