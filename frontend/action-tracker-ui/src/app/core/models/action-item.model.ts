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

export interface CommentInfo {
  id: string;
  content: string;
  authorUserId: string;
  authorName: string;
  isHighImportance: boolean;
  createdAt: string;
  updatedAt: string | null;
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
  escalations: EscalationInfo[];
  comments: CommentInfo[];
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
}
