import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../../../environments/environment';
import { ApiResponse } from '../../user-management/models/user-management.models';
import {
  CreateStrategicPlanRequest,
  StrategicPlan,
  StrategicPlanListResponse,
  UpdateStrategicPlanRequest,
} from '../models/strategic-plans.models';

@Injectable({ providedIn: 'root' })
export class StrategicPlanService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/strategic-plans`;

  getAll(
    page: number,
    pageSize: number,
    options: {
      orgUnitId?: string;
      status?: number;
      year?: number;
      includeDeleted?: boolean;
    } = {},
  ): Observable<StrategicPlanListResponse> {
    let params = new HttpParams()
      .set('page', String(page))
      .set('pageSize', String(pageSize))
      .set('includeDeleted', String(options.includeDeleted ?? false));
    if (options.orgUnitId) params = params.set('orgUnitId', options.orgUnitId);
    if (options.status !== undefined) params = params.set('status', String(options.status));
    if (options.year !== undefined) params = params.set('year', String(options.year));
    return this.http
      .get<ApiResponse<StrategicPlanListResponse>>(this.base, { params })
      .pipe(map((res) => res.data));
  }

  getById(id: string): Observable<StrategicPlan> {
    return this.http
      .get<ApiResponse<StrategicPlan>>(`${this.base}/${id}`)
      .pipe(map((res) => res.data));
  }

  getByOrgUnit(orgUnitId: string): Observable<StrategicPlan[]> {
    return this.http
      .get<ApiResponse<StrategicPlan[]>>(`${this.base}/by-orgunit/${orgUnitId}`)
      .pipe(map((res) => res.data));
  }

  getActiveByOrgUnit(orgUnitId: string): Observable<StrategicPlan | null> {
    return this.http
      .get<ApiResponse<StrategicPlan>>(`${this.base}/by-orgunit/${orgUnitId}/active`)
      .pipe(map((res) => res?.data ?? null));
  }

  create(request: CreateStrategicPlanRequest): Observable<StrategicPlan> {
    return this.http
      .post<ApiResponse<StrategicPlan>>(this.base, request)
      .pipe(map((res) => res.data));
  }

  update(id: string, request: UpdateStrategicPlanRequest): Observable<StrategicPlan> {
    return this.http
      .put<ApiResponse<StrategicPlan>>(`${this.base}/${id}`, request)
      .pipe(map((res) => res.data));
  }

  softDelete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`);
  }

  restore(id: string): Observable<StrategicPlan> {
    return this.http
      .patch<ApiResponse<StrategicPlan>>(`${this.base}/${id}/restore`, null)
      .pipe(map((res) => res.data));
  }
}
