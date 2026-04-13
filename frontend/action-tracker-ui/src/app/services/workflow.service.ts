import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  WorkflowRequest,
  WorkflowRequestSummary,
  CreateDateChangeRequest,
  CreateStatusChangeRequest,
  ReviewWorkflowRequest,
  WorkflowDirection,
  CanReviewResponse
} from '../models/workflow.model';
import { ApiResponse, PagedResult } from '../core/models/api-response.model';

@Injectable({ providedIn: 'root' })
export class WorkflowService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/action-items/workflow`;

  createDateChangeRequest(dto: CreateDateChangeRequest): Observable<ApiResponse<WorkflowRequest>> {
    return this.http.post<ApiResponse<WorkflowRequest>>(`${this.baseUrl}/date-change-request`, dto);
  }

  createStatusChangeRequest(dto: CreateStatusChangeRequest): Observable<ApiResponse<WorkflowRequest>> {
    return this.http.post<ApiResponse<WorkflowRequest>>(`${this.baseUrl}/status-change-request`, dto);
  }

  reviewRequest(requestId: string, dto: ReviewWorkflowRequest): Observable<ApiResponse<WorkflowRequest>> {
    return this.http.put<ApiResponse<WorkflowRequest>>(`${this.baseUrl}/requests/${requestId}/review`, dto);
  }

  getPendingReviews(page = 1, pageSize = 20): Observable<ApiResponse<PagedResult<WorkflowRequest>>> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<ApiResponse<PagedResult<WorkflowRequest>>>(`${this.baseUrl}/pending-reviews`, { params });
  }

  getMyRequests(page = 1, pageSize = 20): Observable<ApiResponse<PagedResult<WorkflowRequest>>> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<ApiResponse<PagedResult<WorkflowRequest>>>(`${this.baseUrl}/my-requests`, { params });
  }

  getRequestsForActionItem(actionItemId: string): Observable<ApiResponse<WorkflowRequest[]>> {
    return this.http.get<ApiResponse<WorkflowRequest[]>>(`${this.baseUrl}/action-item/${actionItemId}`);
  }

  getPendingSummary(): Observable<ApiResponse<WorkflowRequestSummary>> {
    return this.http.get<ApiResponse<WorkflowRequestSummary>>(`${this.baseUrl}/pending-summary`);
  }

  escalate(actionItemId: string, reason: string): Observable<ApiResponse<void>> {
    return this.http.post<ApiResponse<void>>(`${this.baseUrl}/escalate`, { actionItemId, reason });
  }

  giveDirection(dto: WorkflowDirection): Observable<ApiResponse<void>> {
    return this.http.post<ApiResponse<void>>(`${this.baseUrl}/give-direction`, dto);
  }

  canReview(actionItemId: string): Observable<ApiResponse<CanReviewResponse>> {
    return this.http.get<ApiResponse<CanReviewResponse>>(`${this.baseUrl}/can-review/${actionItemId}`);
  }
}
