import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';
import {
  ProjectList,
  ProjectDetail,
  CreateProject,
  UpdateProject,
  ProjectBaseline,
} from '../models/project.models';

/**
 * Service for managing projects.
 * Provided at root level — auth headers are added automatically by the HTTP interceptor.
 */
@Injectable({ providedIn: 'root' })
export class ProjectService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiUrl}/projects`;

  /**
   * Fetches all projects belonging to a workspace.
   * @param workspaceId The workspace to fetch projects for.
   * @returns Observable of project list items.
   */
  getByWorkspace(workspaceId: number): Observable<ProjectList[]> {
    return this.http.get<ProjectList[]>(`${this.apiUrl}/workspace/${workspaceId}`);
  }

  /**
   * Fetches a single project by its ID.
   * @param id Primary key of the project.
   * @returns Observable of the project detail.
   */
  getById(id: number): Observable<ProjectDetail> {
    return this.http.get<ProjectDetail>(`${this.apiUrl}/${id}`);
  }

  /**
   * Fetches the full detail of a project including all nested entities.
   * @param id Primary key of the project.
   * @returns Observable of the full project detail.
   */
  getFullDetail(id: number): Observable<any> {
    return this.http.get<any>(`${this.apiUrl}/${id}/full`);
  }

  /**
   * Creates a new project.
   * @param data Creation payload.
   * @returns Observable of the created project detail.
   */
  create(data: CreateProject): Observable<ProjectDetail> {
    return this.http.post<ProjectDetail>(this.apiUrl, data);
  }

  /**
   * Updates an existing project.
   * @param id Primary key of the project to update.
   * @param data Update payload.
   * @returns Observable of the updated project detail.
   */
  update(id: number, data: UpdateProject): Observable<ProjectDetail> {
    return this.http.put<ProjectDetail>(`${this.apiUrl}/${id}`, data);
  }

  /**
   * Deletes a project.
   * @param id Primary key of the project to delete.
   */
  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }

  /**
   * Creates a baseline snapshot for a project.
   * @param id Primary key of the project to baseline.
   * @returns Observable of the created baseline.
   */
  baselineProject(id: number): Observable<ProjectBaseline> {
    return this.http.post<ProjectBaseline>(`${this.apiUrl}/${id}/baseline`, {});
  }

  /**
   * Fetches the baseline for a project.
   * @param id Primary key of the project.
   * @returns Observable of the project baseline.
   */
  getBaseline(id: number): Observable<ProjectBaseline> {
    return this.http.get<ProjectBaseline>(`${this.apiUrl}/${id}/baseline`);
  }
}
