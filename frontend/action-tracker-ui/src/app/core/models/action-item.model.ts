export enum ActionStatus {
  ToDo = 1,
  InProgress = 2,
  InReview = 3,
  Done = 4,
  Overdue = 5,
  Deferred = 6,
  Cancelled = 7
}

export enum ActionPriority {
  Low = 1,
  Medium = 2,
  High = 3,
  Critical = 4
}

export interface AssigneeInfo {
  userId: string;
  fullName: string;
  email: string;
}

export interface EscalationInfo {
  id: string;
  explanation: string;
  escalatedByUserId: string;
  escalatedByName: string;
  createdAt: string;
}

/** A tag on an action item (WO-2026-11 A-5) — always of the item's own workspace. */
export interface TagInfo {
  id: string;
  name: string;
}

export interface ActionItem {
  id: string;
  actionId: string;
  title: string;
  description: string;
  workspaceId: string;
  workspaceTitle: string;
  projectId: string | null;
  projectName: string | null;
  milestoneId: string | null;
  milestoneName: string | null;
  isStandalone: boolean;
  assignees: AssigneeInfo[];
  priority: ActionPriority;
  priorityCode: number;
  priorityLabel: string;
  status: ActionStatus;
  statusCode: number;
  statusLabel: string;
  /** Planned start. */
  startDate: string | null;
  /** Planned end. */
  dueDate: string;
  /** Actual start (WO-2026-11 A-1): set on the first move into In Progress; editable. */
  actualStartDate: string | null;
  /** Actual end (WO-2026-11 A-1): set on the move into Done, cleared when the item leaves Done. */
  actualEndDate: string | null;
  /** Actual end minus planned end in calendar days (positive = late); null without an actual end. */
  actualEndVarianceDays: number | null;
  progress: number;
  isEscalated: boolean;
  /** Live sub-items (WO-2026-11 A-2) — shown as "x of y done"; never counted as action items. */
  subItemCount: number;
  /** Live sub-items marked done. */
  subItemDoneCount: number;
  /** How many of the sub-items the caller is assigned to; only from `getAssignedViaSubItems`. */
  mySubItemCount?: number | null;
  escalations: EscalationInfo[];
  /** Tags (WO-2026-11 A-5), sorted by name; empty while the tag tables are missing on the server. */
  tags: TagInfo[];
  /**
   * WO-2026-11 P-2: the caller may work on this item — edit, status, requests,
   * updates, files, sub-items. False for a viewer / sponsor, or a team member
   * on someone else's item.
   */
  canWork: boolean;
  /**
   * WO-2026-11 P-2b (fix 9): on a live item the delete would succeed, on a deleted
   * one the restore (the `ActionItems.Delete` / `Edit` permission is needed on top).
   * A project item: the caller manages the project (and, for a delete, it is still
   * Draft); a standalone item: its creator, the workspace's admins or an Admin.
   */
  canDelete?: boolean;
  /**
   * WO-2026-11 P-2b (B4): the item is escalated and the caller may give direction
   * (its creator, a line manager of an assignee, or whoever manages it). Only on
   * GET by id; absent on lists.
   */
  canGiveDirection?: boolean | null;
  /**
   * WO-2026-11 P-2: the caller reaches this item only through a role on its
   * project — it cannot be moved, its tags are read-only (D5) and the
   * workspace itself cannot be opened.
   */
  viaProjectRole: boolean;
  createdAt: string;
  updatedAt: string;
  daysUntilDue: number;
  isOverdue: boolean;
  isDeleted: boolean;
}

export interface ActionItemCreate {
  title: string;
  description: string;
  workspaceId: string;
  projectId?: string;
  milestoneId?: string;
  /** Ignored by the server, which sets it from the project link (WO-2026-11 A-1b). */
  isStandalone?: boolean;
  assigneeIds: string[];
  priority: ActionPriority;
  status: ActionStatus;
  /** Planned start ('yyyy-MM-dd'). */
  startDate: string | null;
  /** Planned end ('yyyy-MM-dd'). */
  dueDate: string;
  progress: number;
  isEscalated?: boolean;
  escalationExplanation?: string;
  /**
   * Tag names (WO-2026-11 A-5), looked up in or added to the item's workspace.
   * On create: optional. On edit: replaces the tags (`[]` removes them all);
   * leave it out to keep them — build it with `tagsPatch`. Rules in
   * `tag-rules.util.ts` (10 tags, 30 characters, no commas).
   */
  tags?: string[];
}

/**
 * PUT payload. Only supplied fields change. Actual dates (WO-2026-11 A-1) are
 * not frozen, not sent for approval and not checked against the calendar; a
 * Done item accepts a payload that carries nothing but actual dates.
 * There is no isStandalone: it follows the project link and changes only when
 * projectId does (WO-2026-11 A-1b). Build edit bodies with `toActionItemUpdate`.
 */
export interface ActionItemUpdate extends Partial<Omit<ActionItemCreate, 'isStandalone'>> {
  /** 'yyyy-MM-dd'; any day up to today. */
  actualStartDate?: string;
  /** True clears the actual start (cannot be combined with actualStartDate). */
  clearActualStartDate?: boolean;
  /** 'yyyy-MM-dd'; only when the item is or becomes Done in this save. */
  actualEndDate?: string;
}

export interface AssignableUser {
  userId: string;
  userName: string;
  email: string;
  orgUnitName?: string | null;
  sources: ('Admin' | 'Member' | 'ProjectManager')[];
}

export interface ActionItemMyStats {
  totalCount: number;
  criticalCount: number;
  inProgressCount: number;
  completedCount: number;
  overdueCount: number;
  completionRate: number;
  onTimeCompletionRate: number;
}

export interface ActionItemFilter {
  status?: ActionStatus;
  priority?: ActionPriority;
  assigneeId?: string;
  workspaceId?: string;
  projectId?: string;
  milestoneId?: string;
  isStandalone?: boolean;
  searchTerm?: string;
  pageNumber: number;
  pageSize: number;
  sortBy: string;
  sortDescending: boolean;
  includeDeleted?: boolean;

  // Dashboard drill-down filters — the server applies the same definitions
  // the dashboards count by, so a list matches the chart segment it came from.
  /** Only items that still need work. */
  openOnly?: boolean;
  /** Only open items flagged Overdue or past their due date. */
  overdueOnly?: boolean;
  isEscalated?: boolean;
  /** overdue | next7 | next30 | later */
  dueBucket?: string;
  /** 0-7 | 8-30 | 31-90 | 90+ (days since creation) */
  ageBucket?: string;
  /** Tag filter (WO-2026-11 A-5): items with any of these tag ids. Needs ActionItems.View. */
  tagIds?: string[];
}
