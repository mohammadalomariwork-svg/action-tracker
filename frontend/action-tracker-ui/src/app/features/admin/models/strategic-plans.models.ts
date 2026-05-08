/**
 * Mirrors the backend `StrategicPlanStatus` enum. The API uses
 * `JsonStringEnumConverter` with `CamelCase` policy, so the wire format is
 * lower-camel string ("draft" / "pendingApproval" / "approved" / "archived").
 * Keep the values aligned with that contract — comparisons against the enum
 * member must match the JSON shape we receive.
 */
export enum StrategicPlanStatus {
  Draft = 'draft',
  PendingApproval = 'pendingApproval',
  Approved = 'approved',
  Archived = 'archived',
}

export interface StrategicPlan {
  id: string;
  name: string;
  orgUnitId: string;
  orgUnitName: string;
  orgUnitCode?: string;
  startYear: number;
  durationYears: number;
  endYear: number;
  status: StrategicPlanStatus;
  approvedAtUtc?: string;
  approvedByUserId?: string;
  approvedByName?: string;
  objectiveCount: number;
  isDeleted: boolean;
  createdAt: string;
  updatedAt?: string;
  deletedAt?: string;
  createdBy?: string;
  updatedBy?: string;
  deletedBy?: string;
  createdByName?: string;
  updatedByName?: string;
  deletedByName?: string;
}

export interface CreateStrategicPlanRequest {
  name: string;
  orgUnitId: string;
  startYear: number;
  durationYears: number;
}

export interface UpdateStrategicPlanRequest {
  name: string;
  startYear: number;
  durationYears: number;
  /**
   * Optional re-assignment to a different org unit. Server-side this is gated to
   * Draft plans only and rejects targets that already host another in-flight plan.
   * Linked objectives have their `OrgUnitId` cascaded to keep the plan + objective
   * org-unit references consistent.
   */
  orgUnitId?: string;
}

export interface StrategicPlanListResponse {
  plans: StrategicPlan[];
  totalCount: number;
  page: number;
  pageSize: number;
}
