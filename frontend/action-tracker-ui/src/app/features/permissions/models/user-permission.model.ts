export interface UserPermissionOverrideDto {
  id: string;
  userId: string;
  userDisplayName: string;
  areaId: string;
  areaName: string;
  actionId: string;
  actionName: string;
  orgUnitScope: number;
  orgUnitScopeLabel: string;
  orgUnitId?: string;
  orgUnitName?: string;
  isGranted: boolean;
  reason?: string;
  expiresAt?: string;
  isActive: boolean;
  createdAt: string;
}

export interface CreateUserPermissionOverrideDto {
  userId: string;
  userDisplayName: string;
  areaId: string;
  actionId: string;
  orgUnitScope: number;
  orgUnitId?: string;
  orgUnitName?: string;
  isGranted: boolean;
  reason?: string;
  expiresAt?: string;
}

export interface UpdateUserPermissionOverrideDto {
  isGranted: boolean;
  orgUnitScope: number;
  orgUnitId?: string;
  orgUnitName?: string;
  reason?: string;
  expiresAt?: string;
  isActive: boolean;
}

export interface EffectivePermissionDto {
  userId: string;
  userDisplayName: string;
  areaId: string;
  areaName: string;
  actionId: string;
  actionName: string;
  isAllowed: boolean;
  source: string;
  orgUnitScope: number;
  orgUnitScopeLabel: string;
  orgUnitId?: string;
  orgUnitName?: string;
}
