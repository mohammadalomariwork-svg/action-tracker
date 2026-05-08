import { StrategicPlanStatus } from './strategic-plans.models';

export interface StrategicObjective {
  id: string;
  objectiveCode: string;
  statement: string;
  description: string;
  orgUnitId: string;
  orgUnitName: string;
  orgUnitCode?: string;
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
  kpiCount: number;

  // Parent strategic-plan context. The bare FK (`strategicPlanId`) is included
  // on every response — list endpoints can use it for plan-scoped filtering.
  // The enriched fields below (name / status / years / `isPlanLocked`) are only
  // populated by `GET /api/strategic-objectives/:id` to avoid an N+1 plan +
  // lock-guard probe per list row.
  strategicPlanId?: string;
  strategicPlanName?: string;
  strategicPlanStatus?: StrategicPlanStatus;
  planStartYear?: number;
  planEndYear?: number;
  /** True when the parent plan is locked (Approved without pending revision, or Archived). */
  isPlanLocked?: boolean;
}

export interface CreateStrategicObjectiveRequest {
  statement: string;
  description: string;
  orgUnitId: string;
  /**
   * Optional explicit attachment to a strategic plan. When omitted, the backend
   * auto-resolves the active plan for `orgUnitId`. When supplied, the backend
   * validates that `orgUnitId` is in the plan's subtree (plan's unit or a
   * descendant) and uses the supplied id directly — lets the Strategy page
   * create objectives in sub-units while keeping them attached to the parent
   * plan rather than landing with `StrategicPlanId = null`.
   */
  strategicPlanId?: string;
}

export interface UpdateStrategicObjectiveRequest {
  statement: string;
  description: string;
  orgUnitId: string;
}

export interface StrategicObjectiveListResponse {
  objectives: StrategicObjective[];
  totalCount: number;
  page: number;
  pageSize: number;
}
