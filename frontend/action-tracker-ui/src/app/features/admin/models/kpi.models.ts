export type MeasurementPeriod = 1 | 2 | 3 | 4;

export const MeasurementPeriodLabels: Record<MeasurementPeriod, string> = {
  1: 'Monthly',
  2: 'Quarterly',
  3: 'Semi-Annual',
  4: 'Yearly',
};

export interface Kpi {
  id: string;
  kpiNumber: number;
  name: string;
  description: string;
  calculationMethod: string;
  period: string;
  periodValue: MeasurementPeriod;
  unit?: string;
  strategicObjectiveId: string;
  objectiveCode: string;
  objectiveStatement: string;
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
  targetCount: number;
}

export enum KpiDirection {
  HigherIsBetter = 0,
  LowerIsBetter = 1,
  OnTarget = 2,
}

export const KpiDirectionLabels: Record<KpiDirection, string> = {
  [KpiDirection.HigherIsBetter]: 'Higher is better',
  [KpiDirection.LowerIsBetter]: 'Lower is better',
  [KpiDirection.OnTarget]: 'On target',
};

export interface KpiTarget {
  id: string;
  kpiId: string;
  year: number;
  month: number;
  monthName: string;
  target?: number;
  actual?: number;
  notes?: string;
  baseline?: number | null;
  dataSource?: string | null;
  calculationNotes?: string | null;
  direction: KpiDirection;
  createdAt?: string;
  updatedAt?: string;
  createdBy?: string;
  updatedBy?: string;
  createdByName?: string;
  updatedByName?: string;
}

export interface KpiWithTargets extends Kpi {
  targets: KpiTarget[];
}

export interface CreateKpiRequest {
  name: string;
  description: string;
  calculationMethod: string;
  period: MeasurementPeriod;
  unit?: string;
  strategicObjectiveId: string;
}

export interface UpdateKpiRequest {
  name: string;
  description: string;
  calculationMethod: string;
  period: MeasurementPeriod;
  unit?: string;
}

export interface MonthTarget {
  month: number;
  target?: number;
  actual?: number;
  notes?: string;
  baseline?: number | null;
  dataSource?: string | null;
  calculationNotes?: string | null;
  direction?: KpiDirection | null;
}

export interface BulkUpsertKpiTargetsRequest {
  kpiId: string;
  year: number;
  targets: MonthTarget[];
}

export interface KpiListResponse {
  kpis: Kpi[];
  totalCount: number;
  page: number;
  pageSize: number;
}
