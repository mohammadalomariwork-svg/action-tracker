export enum MilestoneStatus {
  NotStarted = 1,
  InProgress = 2,
  Completed = 3,
  Delayed = 4,
  Cancelled = 5,
}

export const MilestoneStatusLabels: Record<MilestoneStatus, string> = {
  [MilestoneStatus.NotStarted]: 'Not Started',
  [MilestoneStatus.InProgress]: 'In Progress',
  [MilestoneStatus.Completed]: 'Completed',
  [MilestoneStatus.Delayed]: 'Delayed',
  [MilestoneStatus.Cancelled]: 'Cancelled',
};

export interface MilestoneResponse {
  id: string;
  milestoneCode: string;
  name: string;
  description?: string;
  projectId: string;
  sequenceOrder: number;
  plannedStartDate: string;
  plannedDueDate: string;
  actualCompletionDate?: string;
  isDeadlineFixed: boolean;
  status: MilestoneStatus;
  completionPercentage: number;
  weight: number;
  approverUserId?: string;
  approverName?: string;
  baselinePlannedStartDate?: string;
  baselinePlannedDueDate?: string;
  scheduleVarianceDays?: number;
  createdAt: string;
  updatedAt?: string;
  statusLabel: string;
}

export interface MilestoneCreate {
  name: string;
  description?: string;
  sequenceOrder: number;
  plannedStartDate: string;
  plannedDueDate: string;
  isDeadlineFixed: boolean;
  completionPercentage: number;
  weight: number;
  approverUserId?: string;
}

export interface MilestoneStats {
  totalActionItems: number;
  completionRate: number;
  onTimeDeliveryRate: number;
  escalatedActionItems: number;
}

export interface MilestoneUpdate {
  name: string;
  description?: string;
  sequenceOrder: number;
  plannedStartDate: string;
  plannedDueDate: string;
  actualCompletionDate?: string;
  isDeadlineFixed: boolean;
  status: MilestoneStatus;
  completionPercentage: number;
  weight: number;
  approverUserId?: string;
}
