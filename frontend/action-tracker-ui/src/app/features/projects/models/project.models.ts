export enum ProjectType {
  Operational = 1,
  Strategic = 2,
}

export enum ProjectStatus {
  Draft = 1,
  Active = 2,
  OnHold = 3,
  Completed = 4,
  Cancelled = 5,
}

export enum ProjectPriority {
  Low = 1,
  Medium = 2,
  High = 3,
  Critical = 4,
}

export interface SponsorInfo {
  userId: string;
  fullName: string;
  email: string;
}

export interface ProjectResponse {
  id: string;
  projectCode: string;
  name: string;
  description?: string;
  workspaceId: string;
  workspaceTitle: string;
  projectType: ProjectType;
  status: ProjectStatus;
  priority: ProjectPriority;
  strategicObjectiveId?: string;
  strategicObjectiveStatement?: string;
  projectManagerUserId: string;
  projectManagerName: string;
  sponsors: SponsorInfo[];
  ownerOrgUnitId?: string;
  ownerOrgUnitName?: string;
  plannedStartDate: string;
  plannedEndDate: string;
  actualStartDate?: string;
  approvedBudget?: number;
  currency: string;
  isBaselined: boolean;
  isDeleted: boolean;
  createdAt: string;
  updatedAt?: string;
  projectTypeLabel: string;
  statusLabel: string;
  priorityLabel: string;
}

export interface ProjectCreate {
  name: string;
  description?: string;
  workspaceId: string;
  projectType: ProjectType;
  strategicObjectiveId?: string;
  priority: ProjectPriority;
  projectManagerUserId: string;
  sponsorUserIds: string[];
  plannedStartDate: string;
  plannedEndDate: string;
  approvedBudget?: number;
}

export interface ProjectUpdate {
  name: string;
  description?: string;
  projectType: ProjectType;
  status: ProjectStatus;
  strategicObjectiveId?: string;
  priority: ProjectPriority;
  projectManagerUserId: string;
  sponsorUserIds: string[];
  plannedStartDate: string;
  plannedEndDate: string;
  actualStartDate?: string;
  approvedBudget?: number;
}

export interface ProjectFilter {
  workspaceId?: string;
  status?: ProjectStatus;
  projectType?: ProjectType;
  priority?: ProjectPriority;
  searchTerm?: string;
  pageNumber: number;
  pageSize: number;
  sortBy: string;
  sortDescending: boolean;
}

export interface StrategicObjectiveOption {
  id: string;
  statement: string;
  objectiveCode: string;
}

export interface OrgUnitOption {
  id: string;
  name: string;
  code?: string;
}
