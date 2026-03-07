export enum ProjectType {
  Operational = 1,
  Strategic = 2,
}

export enum ProjectStatus {
  Draft = 1,
  Active = 2,
  OnHold = 3,
  Completed = 4,
  Cancelled = 5,
}

export enum MilestoneStatus {
  NotStarted = 1,
  InProgress = 2,
  Completed = 3,
  Delayed = 4,
  Cancelled = 5,
}

export enum ActionItemStatus {
  NotStarted = 1,
  InProgress = 2,
  Completed = 3,
  Deferred = 4,
  Cancelled = 5,
}

export enum ActionItemPriority {
  Low = 1,
  Medium = 2,
  High = 3,
  Critical = 4,
}

export enum ChangeRequestStatus {
  Pending = 1,
  ApprovedBySponsor = 2,
  Rejected = 3,
  Implemented = 4,
}

/** Represents a strategic objective linked to projects. */
export interface StrategicObjective {
  id: string;
  title: string;
  description?: string;
  organizationUnit: string;
  fiscalYear: number;
  isActive: boolean;
}

/** Summary representation of a project used in list views. */
export interface ProjectList {
  id: string;
  workspaceId: string;
  title: string;
  projectType: ProjectType;
  status: ProjectStatus;
  projectManagerUserName: string;
  sponsorUserName: string;
  plannedStartDate: Date;
  plannedEndDate: Date;
  isBaselined: boolean;
  completionPercentage: number;
}

/** Detailed representation of a project including audit and aggregate fields. */
export interface ProjectDetail extends ProjectList {
  description?: string;
  strategicObjectiveId?: string;
  strategicObjectiveTitle?: string;
  sponsorUserId: string;
  projectManagerUserId: string;
  actualStartDate?: Date;
  actualEndDate?: Date;
  baselinedAt?: Date;
  createdAt: Date;
  updatedAt?: Date;
  createdByUserId: string;
  milestoneCount: number;
  actionItemCount: number;
  hasBudget: boolean;
}

/** Payload for creating a new project. */
export interface CreateProject {
  workspaceId: string;
  title: string;
  description?: string;
  projectType: ProjectType;
  strategicObjectiveId?: string;
  sponsorUserId: string;
  sponsorUserName: string;
  projectManagerUserId: string;
  projectManagerUserName: string;
  plannedStartDate: string;
  plannedEndDate: string;
  createdByUserId: string;
}

/** Payload for updating an existing project. */
export interface UpdateProject {
  id: string;
  title: string;
  description?: string;
  projectType: ProjectType;
  status: ProjectStatus;
  strategicObjectiveId?: string;
  sponsorUserId: string;
  sponsorUserName: string;
  projectManagerUserId: string;
  projectManagerUserName: string;
  plannedStartDate: string;
  plannedEndDate: string;
  actualStartDate?: string;
  actualEndDate?: string;
}

/** Summary representation of a milestone used in list views. */
export interface MilestoneList {
  id: string;
  projectId: string;
  title: string;
  sequenceOrder: number;
  status: MilestoneStatus;
  plannedStartDate: Date;
  plannedEndDate: Date;
  actualStartDate?: Date;
  actualEndDate?: Date;
  completionPercentage: number;
  actionItemCount: number;
}

/** Detailed representation of a milestone including its action items and comments. */
export interface MilestoneDetail extends MilestoneList {
  description?: string;
  actionItems: ActionItemList[];
  comments: Comment[];
  updatedAt?: Date;
}

/** Payload for creating a new milestone. */
export interface CreateMilestone {
  projectId: string;
  title: string;
  description?: string;
  sequenceOrder: number;
  plannedStartDate: string;
  plannedEndDate: string;
}

/** Payload for updating an existing milestone. */
export interface UpdateMilestone {
  id: string;
  title: string;
  description?: string;
  sequenceOrder: number;
  status: MilestoneStatus;
  plannedStartDate: string;
  plannedEndDate: string;
  actualStartDate?: string;
  actualEndDate?: string;
  completionPercentage: number;
}

/** Summary representation of an action item used in list views. */
export interface ActionItemList {
  id: string;
  workspaceId: string;
  projectId?: string;
  milestoneId?: string;
  title: string;
  status: ActionItemStatus;
  priority: ActionItemPriority;
  plannedStartDate: Date;
  dueDate: Date;
  actualCompletionDate?: Date;
  assignedToUserName?: string;
  assignedToExternalName?: string;
  isExternalAssignee: boolean;
  completionPercentage: number;
}

/** Detailed representation of an action item including documents and comments. */
export interface ActionItemDetail extends ActionItemList {
  description?: string;
  assignedToUserId?: string;
  assignedToExternalEmail?: string;
  createdByUserId: string;
  createdAt: Date;
  updatedAt?: Date;
  documents: DocumentInfo[];
  comments: Comment[];
}

/** Payload for creating a new action item. */
export interface CreateActionItem {
  workspaceId: string;
  projectId?: string;
  milestoneId?: string;
  title: string;
  description?: string;
  status: ActionItemStatus;
  priority: ActionItemPriority;
  plannedStartDate: string;
  dueDate: string;
  assignedToUserId?: string;
  assignedToUserName?: string;
  assignedToExternalName?: string;
  assignedToExternalEmail?: string;
  isExternalAssignee: boolean;
  createdByUserId: string;
}

/** Represents a comment on a project, milestone, or action item. */
export interface Comment {
  id: string;
  content: string;
  authorUserId: string;
  authorUserName: string;
  actionItemId?: string;
  milestoneId?: string;
  projectId?: string;
  createdAt: Date;
  updatedAt?: Date;
  isEdited: boolean;
}

/** Payload for creating a new comment. */
export interface CreateComment {
  content: string;
  authorUserId: string;
  authorUserName: string;
  actionItemId?: string;
  milestoneId?: string;
  projectId?: string;
}

/** Metadata for an uploaded document attached to an action item. */
export interface DocumentInfo {
  id: string;
  title: string;
  fileName: string;
  contentType: string;
  fileSizeBytes: number;
  uploadedByUserName: string;
  uploadedAt: Date;
}

/** Budget information associated with a project. */
export interface ProjectBudget {
  id: string;
  projectId: string;
  totalBudget: number;
  spentAmount: number;
  currency: string;
  budgetNotes?: string;
  remainingBudget: number;
}

/** Represents a contract linked to a project. */
export interface Contract {
  id: string;
  projectId: string;
  contractNumber: string;
  contractorName: string;
  contractorContact?: string;
  contractValue: number;
  currency: string;
  startDate: Date;
  endDate?: Date;
  description?: string;
  isActive: boolean;
}

/** Snapshot of a project's planned dates at the time of baselining. */
export interface ProjectBaseline {
  id: string;
  projectId: string;
  baselinedAt: Date;
  baselinedByUserName: string;
  baselinePlannedStartDate: Date;
  baselinePlannedEndDate: Date;
}

/** Represents a request to change a baselined project's scope or schedule. */
export interface BaselineChangeRequest {
  id: string;
  projectId: string;
  requestedByUserName: string;
  changeJustification: string;
  proposedChangesJson: string;
  status: ChangeRequestStatus;
  reviewedByUserName?: string;
  reviewedAt?: Date;
  reviewNotes?: string;
  createdAt: Date;
}
