import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { ProjectRisk, ProjectRiskSummary, CreateProjectRisk, UpdateProjectRisk, ProjectRiskStats } from '../models/project-risk.model';
import { ApiResponse, PagedResult } from '../core/models/api-response.model';

@Injectable({ providedIn: 'root' })
export class ProjectRiskService {
  private readonly http = inject(HttpClient);

  private baseUrl(projectId: string): string {
    return `${environment.apiUrl}/projects/${projectId}/risks`;
  }

  getRisksByProject(
    projectId: string,
    page: number,
    pageSize: number,
    status?: string,
    rating?: string,
    category?: string,
  ): Observable<ApiResponse<PagedResult<ProjectRiskSummary>>> {
    let params = new HttpParams()
      .set('page', page)
      .set('pageSize', pageSize);

    if (status) params = params.set('status', status);
    if (rating) params = params.set('rating', rating);
    if (category) params = params.set('category', category);

    return this.http.get<ApiResponse<PagedResult<ProjectRiskSummary>>>(this.baseUrl(projectId), { params });
  }

  getRiskById(projectId: string, riskId: string): Observable<ApiResponse<ProjectRisk>> {
    return this.http.get<ApiResponse<ProjectRisk>>(`${this.baseUrl(projectId)}/${riskId}`);
  }

  getRiskStats(projectId: string): Observable<ApiResponse<ProjectRiskStats>> {
    return this.http.get<ApiResponse<ProjectRiskStats>>(`${this.baseUrl(projectId)}/stats`);
  }

  createRisk(projectId: string, dto: CreateProjectRisk): Observable<ApiResponse<ProjectRisk>> {
    return this.http.post<ApiResponse<ProjectRisk>>(this.baseUrl(projectId), dto);
  }

  updateRisk(projectId: string, riskId: string, dto: UpdateProjectRisk): Observable<ApiResponse<ProjectRisk>> {
    return this.http.put<ApiResponse<ProjectRisk>>(`${this.baseUrl(projectId)}/${riskId}`, dto);
  }

  deleteRisk(projectId: string, riskId: string): Observable<ApiResponse<void>> {
    return this.http.delete<ApiResponse<void>>(`${this.baseUrl(projectId)}/${riskId}`);
  }

  restoreRisk(projectId: string, riskId: string): Observable<ApiResponse<void>> {
    return this.http.patch<ApiResponse<void>>(`${this.baseUrl(projectId)}/${riskId}/restore`, {});
  }
}
