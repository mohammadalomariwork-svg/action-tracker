import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';
import {
  ActionItemList,
  ActionItemDetail,
  CreateActionItem,
} from '../models/project.models';

/**
 * Service for managing project and standalone action items.
 * Provided at root level — auth headers are added automatically by the HTTP interceptor.
 */
@Injectable({ providedIn: 'root' })
export class ProjectActionItemService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiUrl}/action-items`;

  /**
   * Fetches standalone action items for a workspace (not linked to a project).
   * @param workspaceId The workspace to fetch action items for.
   * @returns Observable of action item list items.
   */
  getStandaloneByWorkspace(workspaceId: string): Observable<ActionItemList[]> {
    return this.http.get<ActionItemList[]>(
      `${this.apiUrl}/workspace/${workspaceId}/standalone`
    );
  }

  /**
   * Fetches all action items for a given project.
   * @param projectId The project to fetch action items for.
   * @returns Observable of action item list items.
   */
  getByProject(projectId: number): Observable<ActionItemList[]> {
    return this.http.get<ActionItemList[]>(`${this.apiUrl}/project/${projectId}`);
  }

  /**
   * Fetches all action items for a given milestone.
   * @param milestoneId The milestone to fetch action items for.
   * @returns Observable of action item list items.
   */
  getByMilestone(milestoneId: number): Observable<ActionItemList[]> {
    return this.http.get<ActionItemList[]>(`${this.apiUrl}/milestone/${milestoneId}`);
  }

  /**
   * Fetches a single action item by its ID.
   * @param id Primary key of the action item.
   * @returns Observable of the action item detail.
   */
  getById(id: number): Observable<ActionItemDetail> {
    return this.http.get<ActionItemDetail>(`${this.apiUrl}/${id}`);
  }

  /**
   * Creates a new action item.
   * @param data Creation payload.
   * @returns Observable of the created action item detail.
   */
  create(data: CreateActionItem): Observable<ActionItemDetail> {
    return this.http.post<ActionItemDetail>(this.apiUrl, data);
  }

  /**
   * Updates an existing action item.
   * @param id Primary key of the action item to update.
   * @param data Partial update payload.
   * @returns Observable of the updated action item detail.
   */
  update(id: number, data: Partial<CreateActionItem>): Observable<ActionItemDetail> {
    return this.http.put<ActionItemDetail>(`${this.apiUrl}/${id}`, data);
  }

  /**
   * Deletes an action item.
   * @param id Primary key of the action item to delete.
   */
  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }
}
