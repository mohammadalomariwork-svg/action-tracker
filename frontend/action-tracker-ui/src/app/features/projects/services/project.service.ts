import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';
import {
  ProjectResponse,
  ProjectCreate,
  ProjectUpdate,
  ProjectFilter,
  StrategicObjectiveOption,
} from '../models/project.models';
import { ApiResponse, PagedResult } from '../../../core/models/api-response.model';
import { AssignableUser } from '../../../core/models/action-item.model';

@Injectable({ providedIn: 'root' })
export class ProjectService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiUrl}/projects`;

  getAll(filter: ProjectFilter): Observable<ApiResponse<PagedResult<ProjectResponse>>> {
    let params = new HttpParams()
      .set('pageNumber', filter.pageNumber)
      .set('pageSize', filter.pageSize)
      .set('sortBy', filter.sortBy)
      .set('sortDescending', filter.sortDescending);

    if (filter.workspaceId) params = params.set('workspaceId', filter.workspaceId);
    if (filter.status) params = params.set('status', filter.status);
    if (filter.projectType) params = params.set('projectType', filter.projectType);
    if (filter.priority) params = params.set('priority', filter.priority);
    if (filter.searchTerm) params = params.set('searchTerm', filter.searchTerm);

    return this.http.get<ApiResponse<PagedResult<ProjectResponse>>>(this.apiUrl, { params });
  }

  getById(id: string): Observable<ApiResponse<ProjectResponse>> {
    return this.http.get<ApiResponse<ProjectResponse>>(`${this.apiUrl}/${id}`);
  }

  create(data: ProjectCreate): Observable<ApiResponse<ProjectResponse>> {
    return this.http.post<ApiResponse<ProjectResponse>>(this.apiUrl, data);
  }

  update(id: string, data: ProjectUpdate): Observable<ApiResponse<ProjectResponse>> {
    return this.http.put<ApiResponse<ProjectResponse>>(`${this.apiUrl}/${id}`, data);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }

  getStrategicObjectivesForWorkspace(workspaceId: string): Observable<ApiResponse<StrategicObjectiveOption[]>> {
    return this.http.get<ApiResponse<StrategicObjectiveOption[]>>(
      `${this.apiUrl}/strategic-objectives-for-workspace/${workspaceId}`
    );
  }

  getAssignableUsers(): Observable<ApiResponse<AssignableUser[]>> {
    return this.http.get<ApiResponse<AssignableUser[]>>(
      `${environment.apiUrl}/action-items/assignable-users`
    );
  }
}
