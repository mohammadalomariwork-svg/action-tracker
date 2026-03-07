import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';
import {
  MilestoneList,
  MilestoneDetail,
  CreateMilestone,
  UpdateMilestone,
} from '../models/project.models';

/**
 * Service for managing project milestones.
 * Provided at root level — auth headers are added automatically by the HTTP interceptor.
 */
@Injectable({ providedIn: 'root' })
export class MilestoneService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiUrl}/milestones`;

  /**
   * Fetches all milestones for a given project.
   * @param projectId The project to fetch milestones for.
   * @returns Observable of milestone list items.
   */
  getByProject(projectId: string): Observable<MilestoneList[]> {
    return this.http.get<MilestoneList[]>(`${this.apiUrl}/project/${projectId}`);
  }

  /**
   * Fetches a single milestone by its ID.
   * @param id Primary key of the milestone.
   * @returns Observable of the milestone detail.
   */
  getById(id: string): Observable<MilestoneDetail> {
    return this.http.get<MilestoneDetail>(`${this.apiUrl}/${id}`);
  }

  /**
   * Creates a new milestone.
   * @param data Creation payload.
   * @returns Observable of the created milestone detail.
   */
  create(data: CreateMilestone): Observable<MilestoneDetail> {
    return this.http.post<MilestoneDetail>(this.apiUrl, data);
  }

  /**
   * Updates an existing milestone.
   * @param id Primary key of the milestone to update.
   * @param data Update payload.
   * @returns Observable of the updated milestone detail.
   */
  update(id: string, data: UpdateMilestone): Observable<MilestoneDetail> {
    return this.http.put<MilestoneDetail>(`${this.apiUrl}/${id}`, data);
  }

  /**
   * Deletes a milestone.
   * @param id Primary key of the milestone to delete.
   */
  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }

  /**
   * Reorders milestones within a project.
   * @param projectId The project whose milestones are being reordered.
   * @param orderedIds Array of milestone IDs in the desired order.
   */
  reorder(projectId: string, orderedIds: string[]): Observable<void> {
    return this.http.put<void>(
      `${this.apiUrl}/project/${projectId}/reorder`,
      orderedIds
    );
  }
}
