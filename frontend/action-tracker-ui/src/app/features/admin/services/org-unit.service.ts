import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../../../environments/environment';
import { ApiResponse } from '../../user-management/models/user-management.models';
import {
  CreateOrgUnitRequest,
  OrgUnit,
  OrgUnitListResponse,
  OrgUnitTree,
  UpdateOrgUnitRequest,
} from '../models/org-chart.models';

@Injectable({ providedIn: 'root' })
export class OrgUnitService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/orgunits`;

  /** Returns the full recursive org chart tree starting from the root, or null if no units exist. */
  getTree(includeDeleted = false): Observable<OrgUnitTree | null> {
    const params = new HttpParams().set('includeDeleted', String(includeDeleted));
    return this.http
      .get<ApiResponse<OrgUnitTree | null>>(`${this.base}/tree`, { params })
      .pipe(map((res) => res.data));
  }

  /** Returns a paged flat list of all org units. */
  getAll(page: number, pageSize: number, includeDeleted = false): Observable<OrgUnitListResponse> {
    const params = new HttpParams()
      .set('page', String(page))
      .set('pageSize', String(pageSize))
      .set('includeDeleted', String(includeDeleted));
    return this.http
      .get<ApiResponse<OrgUnitListResponse>>(this.base, { params })
      .pipe(map((res) => res.data));
  }

  /** Returns a single org unit by its ID. */
  getById(id: string): Observable<OrgUnit> {
    return this.http
      .get<ApiResponse<OrgUnit>>(`${this.base}/${id}`)
      .pipe(map((res) => res.data));
  }

  /** Returns all direct children of the specified org unit. */
  getChildren(parentId: string): Observable<OrgUnit[]> {
    return this.http
      .get<ApiResponse<OrgUnit[]>>(`${this.base}/${parentId}/children`)
      .pipe(map((res) => res.data));
  }

  /** Creates a new org unit. Throws on duplicate root or max depth exceeded. */
  create(request: CreateOrgUnitRequest): Observable<OrgUnit> {
    return this.http
      .post<ApiResponse<OrgUnit>>(this.base, request)
      .pipe(map((res) => res.data));
  }

  /** Updates an existing org unit's name, description, code, or parent. */
  update(id: string, request: UpdateOrgUnitRequest): Observable<OrgUnit> {
    return this.http
      .put<ApiResponse<OrgUnit>>(`${this.base}/${id}`, request)
      .pipe(map((res) => res.data));
  }

  /** Soft-deletes the org unit and all its descendants. */
  softDelete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`);
  }

  /** Restores a previously soft-deleted org unit (descendants are not restored automatically). */
  restore(id: string): Observable<OrgUnit> {
    return this.http
      .post<ApiResponse<OrgUnit>>(`${this.base}/${id}/restore`, null)
      .pipe(map((res) => res.data));
  }

  // ── CR-001-H: Org-unit head + plan-approver preview ─────────────────────

  /** Returns the user designated as this org unit's head, or null. */
  getHead(orgUnitId: string): Observable<ApiResponse<OrgUnitHead | null>> {
    return this.http.get<ApiResponse<OrgUnitHead | null>>(`${this.base}/${orgUnitId}/head`);
  }

  /** Assigns or replaces the org unit head. */
  assignHead(orgUnitId: string, userId: string): Observable<ApiResponse<OrgUnitHead>> {
    return this.http.put<ApiResponse<OrgUnitHead>>(`${this.base}/${orgUnitId}/head`, { userId });
  }

  /** Clears the org unit head. */
  removeHead(orgUnitId: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${orgUnitId}/head`);
  }

  /** Returns the resolved plan approver (level-3 ancestor head), or null. */
  getPlanApprover(orgUnitId: string): Observable<ApiResponse<OrgUnitPlanApprover | null>> {
    return this.http.get<ApiResponse<OrgUnitPlanApprover | null>>(`${this.base}/${orgUnitId}/plan-approver`);
  }
}

export interface OrgUnitHead {
  orgUnitId: string;
  orgUnitName: string;
  orgUnitLevel: number;
  userId: string;
  displayName: string;
  email: string;
}

export interface OrgUnitPlanApprover {
  userId: string;
  displayName: string;
  email: string;
  sourceOrgUnitId: string;
  sourceOrgUnitLevel: number;
  sourceOrgUnitName: string;
}
