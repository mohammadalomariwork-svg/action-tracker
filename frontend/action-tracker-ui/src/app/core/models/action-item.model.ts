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
  createdAt: string;
  updatedAt: string;
  daysUntilDue: number;
  isOverdue: boolean;
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
}
