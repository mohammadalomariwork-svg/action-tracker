export interface AppPermissionAreaDto {
  id: string; // Guid as string
  name: string;
  displayName: string;
  description?: string;
  displayOrder: number;
  isActive: boolean;
}

export interface AppPermissionActionDto {
  id: string;
  name: string;
  displayName: string;
  description?: string;
  displayOrder: number;
  isActive: boolean;
}

export interface AreaActionMappingDto {
  id: string;
  areaId: string;
  areaName: string;
  areaDisplayName: string;
  actionId: string;
  actionName: string;
  actionDisplayName: string;
}

export interface CreateAreaDto {
  name: string;
  displayName: string;
  description?: string;
  displayOrder: number;
}

export interface CreateActionDto {
  name: string;
  displayName: string;
  description?: string;
  displayOrder: number;
}
