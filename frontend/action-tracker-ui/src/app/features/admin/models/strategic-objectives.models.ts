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
}

export interface CreateStrategicObjectiveRequest {
  statement: string;
  description: string;
  orgUnitId: string;
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
