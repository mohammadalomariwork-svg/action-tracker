import { AreaActionMappingDto } from './permission-catalog.model';

export interface RolePermissionDto {
  id: string;
  roleName: string;
  areaId: string;
  areaName: string;
  actionId: string;
  actionName: string;
  isActive: boolean;
  createdAt: string;
  createdBy: string;
}

export interface CreateRolePermissionDto {
  roleName: string;
  areaId: string;
  actionId: string;
}

export interface UpdateRolePermissionDto {
  isActive: boolean;
}

export interface PermissionMatrixDto {
  roleName: string;
  permissions: RolePermissionDto[];
  availableMappings: AreaActionMappingDto[];
}
