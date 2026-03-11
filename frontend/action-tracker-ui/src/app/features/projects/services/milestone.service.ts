import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';
import {
  MilestoneResponse,
  MilestoneCreate,
  MilestoneUpdate,
  MilestoneStats,
} from '../models/milestone.models';
import { ApiResponse } from '../../../core/models/api-response.model';

@Injectable({ providedIn: 'root' })
export class MilestoneService {
  private readonly http = inject(HttpClient);

  private url(projectId: string): string {
    return `${environment.apiUrl}/projects/${projectId}/milestones`;
  }

  getByProject(projectId: string): Observable<ApiResponse<MilestoneResponse[]>> {
    return this.http.get<ApiResponse<MilestoneResponse[]>>(this.url(projectId));
  }

  getById(projectId: string, milestoneId: string): Observable<ApiResponse<MilestoneResponse>> {
    return this.http.get<ApiResponse<MilestoneResponse>>(`${this.url(projectId)}/${milestoneId}`);
  }

  create(projectId: string, data: MilestoneCreate): Observable<ApiResponse<MilestoneResponse>> {
    return this.http.post<ApiResponse<MilestoneResponse>>(this.url(projectId), data);
  }

  update(projectId: string, milestoneId: string, data: MilestoneUpdate): Observable<ApiResponse<MilestoneResponse>> {
    return this.http.put<ApiResponse<MilestoneResponse>>(`${this.url(projectId)}/${milestoneId}`, data);
  }

  delete(projectId: string, milestoneId: string): Observable<void> {
    return this.http.delete<void>(`${this.url(projectId)}/${milestoneId}`);
  }

  baseline(projectId: string): Observable<void> {
    return this.http.post<void>(`${this.url(projectId)}/baseline`, {});
  }

  getProjectStats(projectId: string): Observable<ApiResponse<MilestoneStats>> {
    return this.http.get<ApiResponse<MilestoneStats>>(`${this.url(projectId)}/stats`);
  }
}
