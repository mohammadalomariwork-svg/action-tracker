import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';
import {
  ProjectBaseline,
  BaselineChangeRequest,
  ChangeRequestStatus,
} from '../models/project.models';

/**
 * Service for managing project baselines and baseline change requests.
 * Provided at root level — auth headers are added automatically by the HTTP interceptor.
 */
@Injectable({ providedIn: 'root' })
export class BaselineService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiUrl}/baseline`;

  /**
   * Fetches the baseline for a given project.
   * @param projectId The project to fetch the baseline for.
   * @returns Observable of the project baseline, or null if none exists.
   */
  getByProject(projectId: string): Observable<ProjectBaseline | null> {
    return this.http.get<ProjectBaseline | null>(`${this.apiUrl}/project/${projectId}`);
  }

  /**
   * Fetches all change requests for a given project.
   * @param projectId The project to fetch change requests for.
   * @returns Observable of baseline change requests.
   */
  getChangeRequests(projectId: string): Observable<BaselineChangeRequest[]> {
    return this.http.get<BaselineChangeRequest[]>(
      `${this.apiUrl}/project/${projectId}/change-requests`
    );
  }

  /**
   * Submits a new baseline change request.
   * @param data Change request payload.
   * @returns Observable of the created change request.
   */
  submitChangeRequest(data: Partial<BaselineChangeRequest>): Observable<BaselineChangeRequest> {
    return this.http.post<BaselineChangeRequest>(`${this.apiUrl}/change-requests`, data);
  }

  /**
   * Reviews (approves or rejects) a baseline change request.
   * @param id Primary key of the change request to review.
   * @param data Review decision and optional notes.
   * @returns Observable of the updated change request.
   */
  reviewChangeRequest(
    id: string,
    data: { status: ChangeRequestStatus; reviewNotes?: string }
  ): Observable<BaselineChangeRequest> {
    return this.http.put<BaselineChangeRequest>(
      `${this.apiUrl}/change-requests/${id}/review`,
      data
    );
  }

  /**
   * Implements an approved baseline change request.
   * @param id Primary key of the change request to implement.
   */
  implementChange(id: string): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/change-requests/${id}/implement`, {});
  }
}
