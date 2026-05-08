import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../environments/environment';
import { ApiResponse } from '../core/models/api-response.model';
import {
  CreateKpiActualRequest,
  KpiActual,
  KpiActualGridResponse,
  KpiActualSummary,
  ReviewKpiActualRequest,
  SubmitKpiActualRequest,
  UpdateKpiActualRequest,
} from '../models/kpi-actual.model';

/**
 * CR-001-F — frontend client for KPI Actuals (CRUD + workflow).
 */
@Injectable({ providedIn: 'root' })
export class KpiActualsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/kpis/actuals`;
  private readonly workflowUrl = `${environment.apiUrl}/kpis/actuals/workflow`;

  // ── CRUD ────────────────────────────────────────────────────────────────

  getGrid(kpiTargetId: string, year: number): Observable<ApiResponse<KpiActualGridResponse>> {
    const params = new HttpParams()
      .set('kpiTargetId', kpiTargetId)
      .set('year', year);
    return this.http.get<ApiResponse<KpiActualGridResponse>>(`${this.baseUrl}/grid`, { params });
  }

  listByKpi(kpiId: string, year: number): Observable<ApiResponse<KpiActual[]>> {
    const params = new HttpParams().set('year', year);
    return this.http.get<ApiResponse<KpiActual[]>>(`${this.baseUrl}/by-kpi/${kpiId}`, { params });
  }

  getHistory(
    kpiTargetId: string,
    periodType: number,
    periodIndex: number,
    year: number,
  ): Observable<ApiResponse<KpiActual[]>> {
    const params = new HttpParams()
      .set('kpiTargetId', kpiTargetId)
      .set('periodType', periodType)
      .set('periodIndex', periodIndex)
      .set('year', year);
    return this.http.get<ApiResponse<KpiActual[]>>(`${this.baseUrl}/history`, { params });
  }

  getById(id: string): Observable<ApiResponse<KpiActual>> {
    return this.http.get<ApiResponse<KpiActual>>(`${this.baseUrl}/${id}`);
  }

  create(dto: CreateKpiActualRequest): Observable<ApiResponse<KpiActual>> {
    return this.http.post<ApiResponse<KpiActual>>(this.baseUrl, dto);
  }

  update(id: string, dto: UpdateKpiActualRequest): Observable<ApiResponse<KpiActual>> {
    return this.http.put<ApiResponse<KpiActual>>(`${this.baseUrl}/${id}`, dto);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  // ── Workflow ────────────────────────────────────────────────────────────

  submit(actualId: string): Observable<ApiResponse<KpiActual>> {
    const dto: SubmitKpiActualRequest = { actualId };
    return this.http.post<ApiResponse<KpiActual>>(`${this.workflowUrl}/submit`, dto);
  }

  review(actualId: string, dto: ReviewKpiActualRequest): Observable<ApiResponse<KpiActual>> {
    return this.http.put<ApiResponse<KpiActual>>(`${this.workflowUrl}/requests/${actualId}/review`, dto);
  }

  getPendingReviews(): Observable<ApiResponse<KpiActual[]>> {
    return this.http.get<ApiResponse<KpiActual[]>>(`${this.workflowUrl}/pending-reviews`);
  }

  getMyRequests(): Observable<ApiResponse<KpiActual[]>> {
    return this.http.get<ApiResponse<KpiActual[]>>(`${this.workflowUrl}/my-requests`);
  }

  getPendingSummary(): Observable<ApiResponse<KpiActualSummary>> {
    return this.http.get<ApiResponse<KpiActualSummary>>(`${this.workflowUrl}/pending-summary`);
  }

  canReview(actualId: string): Observable<ApiResponse<{ canReview: boolean }>> {
    return this.http.get<ApiResponse<{ canReview: boolean }>>(`${this.workflowUrl}/can-review/${actualId}`);
  }
}
