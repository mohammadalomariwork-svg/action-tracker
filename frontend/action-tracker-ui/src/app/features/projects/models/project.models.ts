import { ProjectRoleName } from '../../../models/project-people.model';

export enum ProjectType {
  Operational = 'operational',
  Strategic = 'strategic',
}

export enum ProjectStatus {
  Draft = 'draft',
  Active = 'active',
  OnHold = 'onHold',
  Completed = 'completed',
  Cancelled = 'cancelled',
  PendingApproval = 'pendingApproval',
}

export enum ProjectPriority {
  Low = 'low',
  Medium = 'medium',
  High = 'high',
  Critical = 'critical',
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
  /** Global classification (Operational / Strategic) — shown as "Classification (global)". */
  projectType: ProjectType;
  /** Internal classification of the project's workspace (WO-2026-11 P-1), or null. */
  internalClassificationId?: string | null;
  internalClassificationName?: string | null;
  /** False when the project keeps a classification that has since been deactivated. */
  internalClassificationIsActive?: boolean | null;
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
  actionItemCount: number;
  /**
   * WO-2026-11 P-2: the caller's roles on this project — `leadProjectManager`,
   * `projectManager`, `sponsor`, `viewer`, `teamMember` (empty = none).
   */
  myRoles: ProjectRoleName[];
  /** The caller may change the project, its people, milestones and risks. */
  canManage: boolean;
  /** The caller sees the project's workspace itself (false = only through a project role). */
  canOpenWorkspace: boolean;
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
  /** Optional internal classification: an active one of `workspaceId` (WO-2026-11 P-1). */
  internalClassificationId?: string;
  strategicObjectiveId?: string;
  priority: ProjectPriority;
  projectManagerUserId: string;
  sponsorUserIds: string[];
  /** WO-2026-11 P-2: additional project managers (must be on the workspace's PM list). PUT: missing keeps. */
  additionalManagerUserIds?: string[];
  /** WO-2026-11 P-2: viewers (any active user). PUT: missing keeps. */
  viewerUserIds?: string[];
  /** WO-2026-11 P-2: team members (any active user). PUT: missing keeps. */
  teamMemberUserIds?: string[];
  plannedStartDate: string;
  plannedEndDate: string;
  approvedBudget?: number;
}

export interface ProjectUpdate {
  name: string;
  description?: string;
  projectType: ProjectType;
  /**
   * WO-2026-11 P-1 — build with `internalClassificationPatch`: an id sets it,
   * `clearInternalClassification` removes it, leaving both out keeps it.
   */
  internalClassificationId?: string;
  clearInternalClassification?: boolean;
  status: ProjectStatus;
  strategicObjectiveId?: string;
  priority: ProjectPriority;
  projectManagerUserId: string;
  sponsorUserIds: string[];
  /** WO-2026-11 P-2: additional project managers (must be on the workspace's PM list). PUT: missing keeps. */
  additionalManagerUserIds?: string[];
  /** WO-2026-11 P-2: viewers (any active user). PUT: missing keeps. */
  viewerUserIds?: string[];
  /** WO-2026-11 P-2: team members (any active user). PUT: missing keeps. */
  teamMemberUserIds?: string[];
  plannedStartDate: string;
  plannedEndDate: string;
  actualStartDate?: string;
  approvedBudget?: number;
}

export interface ProjectStats {
  milestoneCount: number;
  actionItemCount: number;
  completionRate: number;
  onTimeRate: number;
  escalatedCount: number;
}

export interface ProjectFilter {
  workspaceId?: string;
  status?: ProjectStatus;
  projectType?: ProjectType;
  priority?: ProjectPriority;
  /** Only projects with this internal classification (WO-2026-11 P-1). */
  internalClassificationId?: string;
  /** Only projects without an internal classification. */
  unclassified?: boolean;
  searchTerm?: string;
  /** WO-2026-11 P-2: only projects where the caller holds a project role (My Projects). */
  mine?: boolean;
  pageNumber: number;
  pageSize: number;
  sortBy: string;
  sortDescending: boolean;
  includeDeleted?: boolean;
  /** When set, the API returns only projects linked to one of these objectives. */
  strategicObjectiveIds?: string[];
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
