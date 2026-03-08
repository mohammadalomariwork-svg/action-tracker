export enum ActionStatus {
  ToDo = 1,
  InProgress = 2,
  InReview = 3,
  Done = 4,
  Overdue = 5
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
  assignees: AssigneeInfo[];
  priority: ActionPriority;
  priorityLabel: string;
  status: ActionStatus;
  statusLabel: string;
  startDate: string | null;
  dueDate: string;
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
  assigneeIds: string[];
  priority: ActionPriority;
  status: ActionStatus;
  startDate: string | null;
  dueDate: string;
  progress: number;
  isEscalated: boolean;
  escalationExplanation?: string;
}

export interface AssignableUser {
  id: string;
  fullName: string;
  email: string;
}

export interface ActionItemFilter {
  status?: ActionStatus;
  priority?: ActionPriority;
  assigneeId?: string;
  workspaceId?: string;
  searchTerm?: string;
  pageNumber: number;
  pageSize: number;
  sortBy: string;
  sortDescending: boolean;
  includeDeleted?: boolean;
}
