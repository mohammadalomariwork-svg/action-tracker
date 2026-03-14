export interface UserPermissionOverrideDto {
  id:              string;
  userId:          string;
  userDisplayName: string;
  area:            string;
  action:          string;
  orgUnitScope:    string;
  orgUnitId:       string | null;
  orgUnitName:     string | null;
  isGranted:       boolean;
  reason:          string | null;
  expiresAt:       string | null;
  isActive:        boolean;
  createdAt:       string;
}

export interface CreateUserPermissionOverrideDto {
  userId:          string;
  userDisplayName: string;
  area:            number;
  action:          number;
  orgUnitScope:    number;
  orgUnitId?:      string;
  orgUnitName?:    string;
  isGranted:       boolean;
  reason?:         string;
  expiresAt?:      string;
}

export interface UpdateUserPermissionOverrideDto {
  orgUnitScope: number;
  orgUnitId?:   string;
  orgUnitName?: string;
  isGranted:    boolean;
  reason?:      string;
  expiresAt?:   string;
  isActive:     boolean;
}

export interface EffectivePermissionDto {
  userId:          string;
  userDisplayName: string;
  area:            string;
  action:          string;
  isAllowed:       boolean;
  source:          string;
  orgUnitScope:    string;
  orgUnitId:       string | null;
  orgUnitName:     string | null;
}
