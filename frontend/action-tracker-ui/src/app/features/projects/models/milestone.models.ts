export enum ProjectPhase {
  Initiation = 1,
  Planning = 2,
  Execution = 3,
  MonitoringAndControlling = 4,
  Closing = 5,
}

export const ProjectPhaseLabels: Record<ProjectPhase, string> = {
  [ProjectPhase.Initiation]: 'Initiation',
  [ProjectPhase.Planning]: 'Planning',
  [ProjectPhase.Execution]: 'Execution',
  [ProjectPhase.MonitoringAndControlling]: 'Monitoring & Controlling',
  [ProjectPhase.Closing]: 'Closing',
};

/** Maps camelCase API strings to numeric enum values */
export const ProjectPhaseFromApi: Record<string, ProjectPhase> = {
  initiation: ProjectPhase.Initiation,
  planning: ProjectPhase.Planning,
  execution: ProjectPhase.Execution,
  monitoringAndControlling: ProjectPhase.MonitoringAndControlling,
  closing: ProjectPhase.Closing,
};

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

/** Maps camelCase API strings to numeric enum values */
export const MilestoneStatusFromApi: Record<string, MilestoneStatus> = {
  notStarted: MilestoneStatus.NotStarted,
  inProgress: MilestoneStatus.InProgress,
  completed: MilestoneStatus.Completed,
  delayed: MilestoneStatus.Delayed,
  cancelled: MilestoneStatus.Cancelled,
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
  phase: ProjectPhase;
  phaseLabel: string;
  status: MilestoneStatus;
  completionPercentage: number;
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
  phase: ProjectPhase;
  plannedStartDate: string;
  plannedDueDate: string;
  isDeadlineFixed: boolean;
  completionPercentage: number;
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
  phase: ProjectPhase;
  plannedStartDate: string;
  plannedDueDate: string;
  actualCompletionDate?: string;
  isDeadlineFixed: boolean;
  status: MilestoneStatus;
  completionPercentage: number;
  approverUserId?: string;
}
