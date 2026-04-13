export enum WorkflowRequestType {
  DateChangeRequest = 0,
  StatusChangeRequest = 1
}

export enum WorkflowRequestStatus {
  Pending = 0,
  Approved = 1,
  Rejected = 2
}

export interface WorkflowRequest {
  id: string;
  actionItemId: string;
  actionItemCode: string;
  actionItemTitle: string;
  requestType: string;
  status: string;
  requestedByUserId: string;
  requestedByDisplayName: string;
  requestedNewStartDate: string | null;
  requestedNewDueDate: string | null;
  requestedNewStatus: string | null;
  currentStartDate: string | null;
  currentDueDate: string | null;
  currentStatus: string | null;
  reason: string;
  reviewedByUserId: string | null;
  reviewedByDisplayName: string | null;
  reviewComment: string | null;
  reviewedAt: string | null;
  createdAt: string;
}

export interface WorkflowRequestSummary {
  pendingDateChanges: number;
  pendingStatusChanges: number;
  totalPending: number;
}

export interface CreateDateChangeRequest {
  actionItemId: string;
  newStartDate: string | null;
  newDueDate: string | null;
  reason: string;
}

export interface CreateStatusChangeRequest {
  actionItemId: string;
  newStatus: number;
  reason: string;
}

export interface ReviewWorkflowRequest {
  isApproved: boolean;
  reviewComment: string | null;
}

export interface WorkflowDirection {
  actionItemId: string;
  directionText: string;
}

export interface CanReviewResponse {
  canReview: boolean;
}

export const WORKFLOW_STATUS_CONFIG: Record<string, { label: string; cssClass: string }> = {
  Pending:  { label: 'Pending',  cssClass: 'bg-warning text-dark' },
  Approved: { label: 'Approved', cssClass: 'bg-success text-white' },
  Rejected: { label: 'Rejected', cssClass: 'bg-danger text-white' }
};

export const WORKFLOW_TYPE_LABELS: Record<string, string> = {
  DateChangeRequest: 'Date Change',
  StatusChangeRequest: 'Status Change'
};
