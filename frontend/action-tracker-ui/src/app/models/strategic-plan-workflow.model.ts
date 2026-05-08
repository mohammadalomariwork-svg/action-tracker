/**
 * Strategic plan approval workflow models (CR-001-D + CR-001-E).
 * Mirrors the project workflow shape so screens reuse existing patterns.
 */

import { StrategicPlanStatus } from '../features/admin/models/strategic-plans.models';

export type StrategicPlanApprovalStatus = 'Pending' | 'Approved' | 'Rejected';
export type StrategicPlanApprovalRequestType = 'Initial' | 'Revision';

export interface StrategicPlanApprovalRequest {
  id: string;
  strategicPlanId: string;
  planName: string;
  orgUnitId: string;
  orgUnitName: string;
  requestType: StrategicPlanApprovalRequestType | number;
  requestTypeName: string;
  status: StrategicPlanApprovalStatus | number;
  statusName: string;
  requestedByUserId: string;
  requestedByDisplayName: string;
  assignedReviewerUserId?: string;
  assignedReviewerDisplayName?: string;
  reviewedByUserId?: string;
  reviewedByDisplayName?: string;
  justification: string;
  reviewerComment?: string;
  createdAt: string;
  reviewedAt?: string;
  /** 1-based position in the plan's approval chain. Always 1 for legacy single-level plans. */
  level: number;
  /** Snapshot of the chain length at submit time. 1 for legacy single-level plans. */
  totalLevels: number;
}

/**
 * One row in a strategic plan's configured approval chain. The chain is the
 * source of truth for who reviews next on each Approve — see CR-001 multi-level
 * approval. Empty chain = legacy single-level fallback (level-3 head resolver).
 */
export interface StrategicPlanApprover {
  id: string;
  strategicPlanId: string;
  userId: string;
  displayName: string;
  email?: string;
  orgUnitName?: string;
  order: number;
}

export interface UpsertStrategicPlanApproverItem {
  userId: string;
  order: number;
}

export interface UpsertStrategicPlanApproversRequest {
  approvers: UpsertStrategicPlanApproverItem[];
}

export interface SubmitStrategicPlanApprovalRequest {
  planId: string;
  justification: string;
}

export interface RequestStrategicPlanRevision {
  planId: string;
  justification: string;
}

export interface ReviewStrategicPlanApprovalRequest {
  requestId: string;
  isApproved: boolean;
  reviewComment?: string | null;
}

export interface StrategicPlanApprovalSummary {
  pendingPlanApprovals: number;
}

export interface StrategicPlanLockState {
  planId: string;
  status: StrategicPlanStatus;
  statusName: string;
  isLocked: boolean;
  pendingRevisionId?: string | null;
}

export interface ApprovalAuthorityDto {
  userId: string;
  displayName: string;
  email: string;
  sourceOrgUnitId: string;
  sourceOrgUnitLevel: number;
  sourceOrgUnitName: string;
}

export const STRATEGIC_PLAN_APPROVAL_STATUS_CONFIG: Record<string, { label: string; cssClass: string }> = {
  Pending:  { label: 'Pending',  cssClass: 'bg-warning text-dark' },
  Approved: { label: 'Approved', cssClass: 'bg-success text-white' },
  Rejected: { label: 'Rejected', cssClass: 'bg-danger text-white' },
};
