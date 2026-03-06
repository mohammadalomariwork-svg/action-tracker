/**
 * Full workspace representation returned from GET /api/workspaces/{id}.
 * Includes audit timestamps and the admin user's identity fields.
 */
export interface Workspace {
  /** Primary key of the workspace. */
  id: number;

  /** Human-readable title of the workspace. */
  title: string;

  /** Name of the organisational unit this workspace belongs to. */
  organizationUnit: string;

  /** The AspNetUsers.Id of the designated workspace admin. */
  adminUserId: string;

  /** Display name of the workspace admin (denormalised for fast reads). */
  adminUserName: string;

  /** Whether the workspace is currently active. */
  isActive: boolean;

  /** UTC timestamp when the workspace was created. */
  createdAt: Date;

  /** UTC timestamp of the most recent update, absent if never updated. */
  updatedAt?: Date;
}

/**
 * Lightweight workspace item used in list views.
 * Omits the admin user ID and audit timestamps to reduce payload size.
 */
export interface WorkspaceList {
  /** Primary key of the workspace. */
  id: number;

  /** Human-readable title of the workspace. */
  title: string;

  /** Name of the organisational unit this workspace belongs to. */
  organizationUnit: string;

  /** Display name of the workspace admin. */
  adminUserName: string;

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

  /** The AspNetUsers.Id of the user to designate as workspace admin. */
  adminUserId: string;

  /** Display name of the admin user (cached to avoid future joins). */
  adminUserName: string;
}

/**
 * Payload sent to PUT /api/workspaces/{id} to update an existing workspace.
 * The `id` field must match the route parameter.
 */
export interface UpdateWorkspace {
  /** Primary key of the workspace to update — must match the route id. */
  id: number;

  /** Updated title of the workspace. */
  title: string;

  /** Updated organisational unit name. */
  organizationUnit: string;

  /** The AspNetUsers.Id of the workspace admin. */
  adminUserId: string;

  /** Display name of the workspace admin. */
  adminUserName: string;

  /** Set to `false` to deactivate the workspace. */
  isActive: boolean;
}
