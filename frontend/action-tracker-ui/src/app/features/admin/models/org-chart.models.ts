export interface OrgUnit {
  id: string;
  name: string;
  description?: string;
  code?: string;
  level: number;
  parentId?: string;
  parentName?: string;
  isDeleted: boolean;
  createdAt: string;
  updatedAt?: string;
  deletedAt?: string;
  createdBy?: string;
  updatedBy?: string;
  deletedBy?: string;
  createdByName?: string;
  updatedByName?: string;
  deletedByName?: string;
  childrenCount: number;
}

export interface OrgUnitTree {
  id: string;
  name: string;
  code?: string;
  description?: string;
  level: number;
  parentId?: string;
  isDeleted: boolean;
  createdBy?: string;
  updatedBy?: string;
  deletedBy?: string;
  createdByName?: string;
  updatedByName?: string;
  deletedByName?: string;
  children: OrgUnitTree[];
}

export interface CreateOrgUnitRequest {
  name: string;
  description?: string;
  parentId?: string;
}

export interface UpdateOrgUnitRequest {
  name: string;
  description?: string;
  parentId?: string;
}

export interface OrgUnitListResponse {
  orgUnits: OrgUnit[];
  totalCount: number;
}
