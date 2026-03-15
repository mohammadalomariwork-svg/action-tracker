import { AreaActionMappingDto } from './permission-catalog.model';

export interface RolePermissionDto {
  id: string;
  roleName: string;
  areaId: string;
  areaName: string;
  actionId: string;
  actionName: string;
  orgUnitScope: number; // 0=All, 1=SpecificOrgUnit, 2=OwnOnly
  orgUnitScopeLabel: string;
  orgUnitId?: string;
  orgUnitName?: string;
  isActive: boolean;
  createdAt: string;
  createdBy: string;
}

export interface CreateRolePermissionDto {
  roleName: string;
  areaId: string;
  actionId: string;
  orgUnitScope: number;
  orgUnitId?: string;
  orgUnitName?: string;
}

export interface UpdateRolePermissionDto {
  orgUnitScope: number;
  orgUnitId?: string;
  orgUnitName?: string;
  isActive: boolean;
}

export interface PermissionMatrixDto {
  roleName: string;
  permissions: RolePermissionDto[];
  availableMappings: AreaActionMappingDto[];
}
