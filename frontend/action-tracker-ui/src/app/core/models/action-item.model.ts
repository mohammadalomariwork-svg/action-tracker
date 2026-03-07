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

export enum ActionCategory {
  Operations = 1,
  Strategic = 2,
  HR = 3,
  Finance = 4,
  IT = 5,
  Compliance = 6,
  Communication = 7
}

export interface ActionItem {
  id: string;
  actionId: string;
  title: string;
  description: string;
  assigneeId: string;
  assigneeName: string;
  assigneeEmail: string;
  category: ActionCategory;
  categoryLabel: string;
  priority: ActionPriority;
  priorityLabel: string;
  status: ActionStatus;
  statusLabel: string;
  dueDate: string;
  progress: number;
  isEscalated: boolean;
  notes: string;
  createdAt: string;
  updatedAt: string;
  daysUntilDue: number;
  isOverdue: boolean;
}

export interface ActionItemCreate {
  title: string;
  description: string;
  assigneeId: string;
  category: ActionCategory;
  priority: ActionPriority;
  status: ActionStatus;
  dueDate: string;
  progress: number;
  isEscalated: boolean;
  notes: string;
}

export interface ActionItemFilter {
  status?: ActionStatus;
  priority?: ActionPriority;
  assigneeId?: string;
  category?: ActionCategory;
  searchTerm?: string;
  pageNumber: number;
  pageSize: number;
  sortBy: string;
  sortDescending: boolean;
}
