export enum ProjectApprovalStatus {
  Pending = 'Pending',
  Approved = 'Approved',
  Rejected = 'Rejected',
}

export interface ProjectApprovalRequest {
  id: string;
  projectId: string;
  projectCode: string;
  projectName: string;
  requestedByUserId: string;
  requestedByDisplayName: string;
  reviewedByUserId: string | null;
  reviewedByDisplayName: string | null;
  status: string;
  reason: string;
  reviewComment: string | null;
  createdAt: string;
  reviewedAt: string | null;
}

export interface ProjectApprovalSummary {
  pendingProjectApprovals: number;
}

export interface SubmitProjectApprovalRequest {
  projectId: string;
  reason: string;
}

export interface ReviewProjectApprovalRequest {
  requestId: string;
  isApproved: boolean;
  reviewComment: string | null;
}
