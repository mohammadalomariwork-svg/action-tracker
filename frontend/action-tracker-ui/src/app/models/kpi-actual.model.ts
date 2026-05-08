/**
 * CR-001-F — KPI Actuals models for the period grid + approval workflow.
 *
 * The actual itself is the workflow record — Status / Submitted* / Reviewed*
 * fields all live on the entity. There is no separate "request" table.
 */

export type KpiActualStatus = 'Draft' | 'PendingApproval' | 'Approved' | 'Rejected';
export type KpiActualPeriodType = 'Quarter' | 'SemiAnnual' | 'Annual';
export type KpiDirection = 'HigherIsBetter' | 'LowerIsBetter' | 'OnTarget';

export const KPI_ACTUAL_PERIOD_TYPE: Record<KpiActualPeriodType, number> = {
  Quarter: 0,
  SemiAnnual: 1,
  Annual: 2,
};

export interface KpiActual {
  id: string;
  kpiTargetId: string;
  kpiId: string;
  kpiName: string;
  strategicObjectiveId: string;
  orgUnitId: string;
  orgUnitName: string;

  periodType: KpiActualPeriodType | number;
  periodTypeName: string;
  periodIndex: number;
  year: number;
  periodLabel: string;

  actualValue: number;
  targetValue?: number | null;
  direction: KpiDirection | number;
  directionName: string;
  achievementPercent?: number | null;

  notes?: string | null;

  status: KpiActualStatus | number;
  statusName: string;

  submittedByUserId?: string | null;
  submittedByDisplayName?: string | null;
  submittedAtUtc?: string | null;
  assignedReviewerUserId?: string | null;
  assignedReviewerDisplayName?: string | null;
  reviewedByUserId?: string | null;
  reviewedByDisplayName?: string | null;
  reviewedAtUtc?: string | null;
  reviewerComment?: string | null;

  createdAtUtc: string;
  modifiedAtUtc?: string | null;

  documentCount: number;
}

export interface KpiActualGridRow {
  periodType: KpiActualPeriodType | number;
  periodIndex: number;
  year: number;
  periodLabel: string;
  targetValue?: number | null;
  direction: KpiDirection | number;
  current?: KpiActual | null;
  periodEndUtc: string;
  periodHasEnded: boolean;
}

export interface KpiActualGridResponse {
  kpiId: string;
  kpiTargetId: string;
  kpiName: string;
  periodType: KpiActualPeriodType | number;
  periodTypeName: string;
  year: number;
  rows: KpiActualGridRow[];
}

export interface CreateKpiActualRequest {
  kpiTargetId: string;
  periodType: KpiActualPeriodType | number;
  periodIndex: number;
  year: number;
  actualValue: number;
  notes?: string | null;
}

export interface UpdateKpiActualRequest {
  actualValue: number;
  notes?: string | null;
}

export interface SubmitKpiActualRequest {
  actualId: string;
}

export interface ReviewKpiActualRequest {
  actualId: string;
  isApproved: boolean;
  reviewComment?: string | null;
}

export interface KpiActualSummary {
  pendingKpiActuals: number;
}

export const KPI_ACTUAL_STATUS_CONFIG: Record<string, { label: string; cssClass: string }> = {
  Draft:           { label: 'Draft',            cssClass: 'bg-secondary text-white' },
  PendingApproval: { label: 'Pending Approval', cssClass: 'bg-warning text-dark' },
  Approved:        { label: 'Approved',         cssClass: 'bg-success text-white' },
  Rejected:        { label: 'Rejected',         cssClass: 'bg-danger text-white' },
};

export function statusName(status: KpiActualStatus | number): string {
  if (typeof status === 'number') {
    return ['Draft', 'PendingApproval', 'Approved', 'Rejected'][status] ?? 'Draft';
  }
  return status;
}
