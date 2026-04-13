import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  ProjectApprovalRequest,
  ProjectApprovalSummary,
  SubmitProjectApprovalRequest,
  ReviewProjectApprovalRequest,
} from '../features/projects/models/project-approval.models';
import { ApiResponse } from '../core/models/api-response.model';

@Injectable({ providedIn: 'root' })
export class ProjectWorkflowService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/projects/workflow`;

  submitForApproval(dto: SubmitProjectApprovalRequest): Observable<ApiResponse<ProjectApprovalRequest>> {
    return this.http.post<ApiResponse<ProjectApprovalRequest>>(`${this.baseUrl}/submit`, dto);
  }

  reviewApprovalRequest(requestId: string, dto: ReviewProjectApprovalRequest): Observable<ApiResponse<ProjectApprovalRequest>> {
    return this.http.put<ApiResponse<ProjectApprovalRequest>>(`${this.baseUrl}/requests/${requestId}/review`, dto);
  }

  getApprovalRequestsForProject(projectId: string): Observable<ApiResponse<ProjectApprovalRequest[]>> {
    return this.http.get<ApiResponse<ProjectApprovalRequest[]>>(`${this.baseUrl}/project/${projectId}`);
  }

  getPendingReviews(): Observable<ApiResponse<ProjectApprovalRequest[]>> {
    return this.http.get<ApiResponse<ProjectApprovalRequest[]>>(`${this.baseUrl}/pending-reviews`);
  }

  getMyRequests(): Observable<ApiResponse<ProjectApprovalRequest[]>> {
    return this.http.get<ApiResponse<ProjectApprovalRequest[]>>(`${this.baseUrl}/my-requests`);
  }

  getPendingSummary(): Observable<ApiResponse<ProjectApprovalSummary>> {
    return this.http.get<ApiResponse<ProjectApprovalSummary>>(`${this.baseUrl}/pending-summary`);
  }

  canReviewProject(projectId: string): Observable<ApiResponse<{ canReview: boolean }>> {
    return this.http.get<ApiResponse<{ canReview: boolean }>>(`${this.baseUrl}/can-review/${projectId}`);
  }

  validateSubmit(projectId: string): Observable<ApiResponse<{ isValid: boolean; errors: string[] }>> {
    return this.http.get<ApiResponse<{ isValid: boolean; errors: string[] }>>(`${this.baseUrl}/validate-submit/${projectId}`);
  }
}
