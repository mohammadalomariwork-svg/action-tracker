/**
 * Represents a single admin user assigned to a workspace.
 */
export interface WorkspaceAdmin {
  /** The AspNetUsers.Id of the admin user. */
  userId: string;
  /** Display name of the admin user. */
  userName: string;
}

/**
 * Full workspace representation returned from GET /api/workspaces/{id}.
 */
export interface Workspace {
  /** Primary key of the workspace. */
  id: string;
  /** Human-readable title of the workspace. */
  title: string;
  /** Name of the organisational unit this workspace belongs to. */
  organizationUnit: string;
  /** All admin users assigned to this workspace. */
  admins: WorkspaceAdmin[];
  /** Whether the workspace is currently active. */
  isActive: boolean;
  /** UTC timestamp when the workspace was created. */
  createdAt: Date;
  /** UTC timestamp of the most recent update, absent if never updated. */
  updatedAt?: Date;
}

/**
 * Lightweight workspace item used in list views.
 */
export interface WorkspaceList {
  /** Primary key of the workspace. */
  id: string;
  /** Human-readable title of the workspace. */
  title: string;
  /** Name of the organisational unit this workspace belongs to. */
  organizationUnit: string;
  /** Comma-separated display names of all workspace admins. */
  adminUserNames: string;
  /** Whether the workspace is currently active. */
  isActive: boolean;
}

/**
 * Payload sent to POST /api/workspaces to create a new workspace.
 */
export interface CreateWorkspace {
  /** Human-readable title for the new workspace. */
  title: string;
  /** Name of the organisational unit the workspace belongs to. */
  organizationUnit: string;
  /** One or more admin users to assign (at least one required). */
  admins: WorkspaceAdmin[];
}

/**
 * Payload sent to PUT /api/workspaces/{id} to update an existing workspace.
 */
export interface UpdateWorkspace {
  /** Primary key of the workspace to update — must match the route id. */
  id: string;
  /** Updated title of the workspace. */
  title: string;
  /** Updated organisational unit name. */
  organizationUnit: string;
  /** Replacement admin user list (replaces the entire existing list). */
  admins: WorkspaceAdmin[];
  /** Set to `false` to deactivate the workspace. */
  isActive: boolean;
}

/**
 * Lightweight org unit item returned by GET /api/workspaces/org-units.
 */
export interface OrgUnitDropdownItem {
  /** Org unit ID (Guid as string). */
  id: string;
  /** Display name of the org unit. */
  name: string;
  /** Auto-generated short code, e.g. "OC-1". */
  code?: string;
  /** Hierarchy depth — 1 for root, 2 for children, etc. */
  level: number;
}

/**
 * Lightweight user item returned by GET /api/workspaces/active-users.
 */
export interface UserDropdownItem {
  /** AspNetUsers.Id of the user. */
  id: string;
  /** Display name shown in the dropdown. */
  displayName: string;
}
