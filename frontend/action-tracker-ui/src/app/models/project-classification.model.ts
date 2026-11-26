/**
 * Internal project classifications of a workspace (WO-2026-11 P-1) — next to the
 * global classification (`ProjectType`: Operational / Strategic), which they do
 * not replace. A project carries at most one, always of its own workspace.
 */
export interface ProjectClassification {
  id: string;
  workspaceId: string;
  name: string;
  /** 1-based position in the workspace's list. */
  sortOrder: number;
  /** False = kept on the projects that have it, not offered for new choices. */
  isActive: boolean;
  /** Live projects that carry it. */
  projectCount: number;
  /** Soft-deleted projects that still carry it — they block a delete too. */
  deletedProjectCount: number;
  /** True when no project (live or deleted) uses it. */
  canDelete: boolean;
}

/** Body of POST / PUT `/workspaces/:id/project-classifications[/:classificationId]`. */
export interface ProjectClassificationInput {
  name: string;
  isActive?: boolean;
}

/** Same limit as the server's `ProjectClassificationRules.MaxNameLength`. */
export const PROJECT_CLASSIFICATION_MAX_LENGTH = 100;
