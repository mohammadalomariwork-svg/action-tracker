import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../../../environments/environment';
import { ApiResponse } from '../../user-management/models/user-management.models';
import {
  BulkUpsertKpiTargetsRequest,
  CreateKpiRequest,
  Kpi,
  KpiListResponse,
  KpiTarget,
  KpiWithTargets,
  UpdateKpiRequest,
} from '../models/kpi.models';

@Injectable({ providedIn: 'root' })
export class KpiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/kpis`;

  /** Returns a paged list of KPIs, optionally filtered by strategic objective. */
  getAll(
    page: number,
    pageSize: number,
    objectiveId?: string,
    includeDeleted = false,
  ): Observable<KpiListResponse> {
    let params = new HttpParams()
      .set('page', String(page))
      .set('pageSize', String(pageSize))
      .set('includeDeleted', String(includeDeleted));
    if (objectiveId) params = params.set('objectiveId', objectiveId);
    return this.http
      .get<ApiResponse<KpiListResponse>>(this.base, { params })
      .pipe(map((res) => res.data));
  }

  /** Returns a single KPI with its targets, optionally filtered by year. */
  getById(id: string, year?: number): Observable<KpiWithTargets> {
    let params = new HttpParams();
    if (year !== undefined) params = params.set('year', String(year));
    return this.http
      .get<ApiResponse<KpiWithTargets>>(`${this.base}/${id}`, { params })
      .pipe(map((res) => res.data));
  }

  /** Returns all active KPIs for the specified strategic objective, ordered by KpiNumber. */
  getByObjective(objectiveId: string): Observable<Kpi[]> {
    return this.http
      .get<ApiResponse<Kpi[]>>(`${this.base}/by-objective/${objectiveId}`)
      .pipe(map((res) => res.data));
  }

  /** Creates a new KPI. KpiNumber is auto-assigned per strategic objective. */
  create(request: CreateKpiRequest): Observable<Kpi> {
    return this.http
      .post<ApiResponse<Kpi>>(this.base, request)
      .pipe(map((res) => res.data));
  }

  /** Updates name, description, calculation method, period, and unit of an existing KPI. */
  update(id: string, request: UpdateKpiRequest): Observable<Kpi> {
    return this.http
      .put<ApiResponse<Kpi>>(`${this.base}/${id}`, request)
      .pipe(map((res) => res.data));
  }

  /** Soft-deletes a KPI. */
  softDelete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`);
  }

  /** Restores a previously soft-deleted KPI. */
  restore(id: string): Observable<Kpi> {
    return this.http
      .post<ApiResponse<Kpi>>(`${this.base}/${id}/restore`, null)
      .pipe(map((res) => res.data));
  }

  /** Returns all targets for a KPI in the specified year, ordered by month. */
  getTargets(kpiId: string, year: number): Observable<KpiTarget[]> {
    const params = new HttpParams().set('year', String(year));
    return this.http
      .get<ApiResponse<KpiTarget[]>>(`${this.base}/${kpiId}/targets`, { params })
      .pipe(map((res) => res.data));
  }

  /** Saves all monthly targets for a KPI/year combination in one request. */
  bulkUpsertTargets(request: BulkUpsertKpiTargetsRequest): Observable<KpiTarget[]> {
    return this.http
      .post<ApiResponse<KpiTarget[]>>(`${this.base}/targets/bulk-upsert`, request)
      .pipe(map((res) => res.data));
  }
}
