import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ActionItem, ActionItemCreate, ActionItemFilter, ActionStatus, AssignableUser, CommentInfo } from '../models/action-item.model';
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
    if (filter.milestoneId) params = params.set('milestoneId', filter.milestoneId);
    if (filter.searchTerm) params = params.set('searchTerm', filter.searchTerm);
    if (filter.includeDeleted) params = params.set('includeDeleted', true);

    return this.http.get<ApiResponse<PagedResult<ActionItem>>>(this.apiUrl, { params });
  }

  getById(id: string): Observable<ApiResponse<ActionItem>> {
    return this.http.get<ApiResponse<ActionItem>>(`${this.apiUrl}/${id}`);
  }

  create(item: ActionItemCreate): Observable<ApiResponse<ActionItem>> {
    return this.http.post<ApiResponse<ActionItem>>(this.apiUrl, item);
  }

  update(id: string, item: Partial<ActionItemCreate>): Observable<ApiResponse<ActionItem>> {
    return this.http.put<ApiResponse<ActionItem>>(`${this.apiUrl}/${id}`, item);
  }

  delete(id: string): Observable<ApiResponse<void>> {
    return this.http.delete<ApiResponse<void>>(`${this.apiUrl}/${id}`);
  }

  updateStatus(id: string, status: ActionStatus): Observable<ApiResponse<ActionItem>> {
    return this.http.patch<ApiResponse<ActionItem>>(`${this.apiUrl}/${id}/status`, { status });
  }

  processOverdue(): Observable<ApiResponse<number>> {
    return this.http.post<ApiResponse<number>>(`${this.apiUrl}/process-overdue`, {});
  }

  getAssignableUsers(): Observable<ApiResponse<AssignableUser[]>> {
    return this.http.get<ApiResponse<AssignableUser[]>>(`${this.apiUrl}/assignable-users`);
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
