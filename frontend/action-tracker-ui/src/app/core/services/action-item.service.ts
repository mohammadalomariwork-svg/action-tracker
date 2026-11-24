import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ActionItem, ActionItemCreate, ActionItemFilter, ActionItemMyStats, ActionItemUpdate, ActionStatus, CommentInfo } from '../models/action-item.model';
import { ApiResponse, PagedResult } from '../models/api-response.model';

@Injectable({ providedIn: 'root' })
export class ActionItemService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiBaseUrl}/action-items`;

  getAll(filter: ActionItemFilter): Observable<ApiResponse<PagedResult<ActionItem>>> {
    let params = new HttpParams()
      .set('pageNumber', filter.pageNumber)
      .set('pageSize', filter.pageSize)
      .set('sortBy', filter.sortBy)
      .set('sortDescending', filter.sortDescending);

    if (filter.status != null) params = params.set('status', filter.status);
    if (filter.priority != null) params = params.set('priority', filter.priority);
    if (filter.assigneeId) params = params.set('assigneeId', filter.assigneeId);
    if (filter.workspaceId) params = params.set('workspaceId', filter.workspaceId);
    if (filter.projectId) params = params.set('projectId', filter.projectId);
    if (filter.milestoneId) params = params.set('milestoneId', filter.milestoneId);
    if (filter.isStandalone != null) params = params.set('isStandalone', filter.isStandalone);
    if (filter.searchTerm) params = params.set('searchTerm', filter.searchTerm);
    if (filter.includeDeleted) params = params.set('includeDeleted', true);
    if (filter.openOnly)            params = params.set('openOnly', true);
    if (filter.overdueOnly)         params = params.set('overdueOnly', true);
    if (filter.isEscalated != null) params = params.set('isEscalated', filter.isEscalated);
    if (filter.dueBucket)           params = params.set('dueBucket', filter.dueBucket);
    if (filter.ageBucket)           params = params.set('ageBucket', filter.ageBucket);

    return this.http.get<ApiResponse<PagedResult<ActionItem>>>(this.apiUrl, { params });
  }

  getById(id: string): Observable<ApiResponse<ActionItem>> {
    return this.http.get<ApiResponse<ActionItem>>(`${this.apiUrl}/${id}`);
  }

  create(item: ActionItemCreate): Observable<ApiResponse<ActionItem>> {
    return this.http.post<ApiResponse<ActionItem>>(this.apiUrl, item);
  }

  update(id: string, item: ActionItemUpdate): Observable<ApiResponse<ActionItem>> {
    return this.http.put<ApiResponse<ActionItem>>(`${this.apiUrl}/${id}`, item);
  }

  delete(id: string): Observable<ApiResponse<void>> {
    return this.http.delete<ApiResponse<void>>(`${this.apiUrl}/${id}`);
  }

  restore(id: string): Observable<void> {
    return this.http.patch<void>(`${this.apiUrl}/${id}/restore`, {});
  }

  updateStatus(id: string, status: ActionStatus): Observable<ApiResponse<ActionItem>> {
    return this.http.patch<ApiResponse<ActionItem>>(`${this.apiUrl}/${id}/status`, { status });
  }

  processOverdue(): Observable<ApiResponse<number>> {
    return this.http.post<ApiResponse<number>>(`${this.apiUrl}/process-overdue`, {});
  }

  getMyStats(): Observable<ApiResponse<ActionItemMyStats>> {
    return this.http.get<ApiResponse<ActionItemMyStats>>(`${this.apiUrl}/my-stats`);
  }

  getMyActions(filter: Omit<ActionItemFilter, 'assigneeId'>): Observable<ApiResponse<PagedResult<ActionItem>>> {
    let params = new HttpParams()
      .set('pageNumber', filter.pageNumber)
      .set('pageSize', filter.pageSize)
      .set('sortBy', filter.sortBy)
      .set('sortDescending', filter.sortDescending);

    if (filter.status != null)   params = params.set('status', filter.status);
    if (filter.priority != null) params = params.set('priority', filter.priority);
    if (filter.searchTerm)       params = params.set('searchTerm', filter.searchTerm);
    if (filter.includeDeleted)   params = params.set('includeDeleted', true);
    if (filter.openOnly)            params = params.set('openOnly', true);
    if (filter.overdueOnly)         params = params.set('overdueOnly', true);
    if (filter.isEscalated != null) params = params.set('isEscalated', filter.isEscalated);
    if (filter.dueBucket)           params = params.set('dueBucket', filter.dueBucket);
    if (filter.ageBucket)           params = params.set('ageBucket', filter.ageBucket);

    return this.http.get<ApiResponse<PagedResult<ActionItem>>>(`${this.apiUrl}/my-actions`, { params });
  }

  getCreatedByMe(filter: Omit<ActionItemFilter, 'assigneeId'>): Observable<ApiResponse<PagedResult<ActionItem>>> {
    let params = new HttpParams()
      .set('pageNumber', filter.pageNumber)
      .set('pageSize', filter.pageSize)
      .set('sortBy', filter.sortBy)
      .set('sortDescending', filter.sortDescending);

    if (filter.status != null)   params = params.set('status', filter.status);
    if (filter.priority != null) params = params.set('priority', filter.priority);
    if (filter.searchTerm)       params = params.set('searchTerm', filter.searchTerm);
    if (filter.includeDeleted)   params = params.set('includeDeleted', true);

    return this.http.get<ApiResponse<PagedResult<ActionItem>>>(`${this.apiUrl}/created-by-me`, { params });
  }

  /** Fetches every action item assigned to the current user (no pagination). */
  getAllMyActions(): Observable<ApiResponse<PagedResult<ActionItem>>> {
    const params = new HttpParams()
      .set('pageNumber', 1)
      .set('pageSize', 100)   // backend clamps at 100
      .set('sortBy', 'dueDate')
      .set('sortDescending', false);
    return this.http.get<ApiResponse<PagedResult<ActionItem>>>(`${this.apiUrl}/my-actions`, { params });
  }

  // Comments
  getComments(actionItemId: string): Observable<ApiResponse<CommentInfo[]>> {
    return this.http.get<ApiResponse<CommentInfo[]>>(`${this.apiUrl}/${actionItemId}/comments`);
  }

  addComment(actionItemId: string, body: { content: string; isHighImportance: boolean }): Observable<ApiResponse<CommentInfo>> {
    return this.http.post<ApiResponse<CommentInfo>>(`${this.apiUrl}/${actionItemId}/comments`, body);
  }

  updateComment(actionItemId: string, commentId: string, body: { content: string; isHighImportance: boolean }): Observable<ApiResponse<CommentInfo>> {
    return this.http.put<ApiResponse<CommentInfo>>(`${this.apiUrl}/${actionItemId}/comments/${commentId}`, body);
  }

  deleteComment(actionItemId: string, commentId: string): Observable<ApiResponse<void>> {
    return this.http.delete<ApiResponse<void>>(`${this.apiUrl}/${actionItemId}/comments/${commentId}`);
  }
}
