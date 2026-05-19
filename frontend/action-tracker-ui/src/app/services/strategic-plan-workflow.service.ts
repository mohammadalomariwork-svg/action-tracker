import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  ReviewStrategicPlanApprovalRequest,
  RequestStrategicPlanRevision,
  StrategicPlanApprovalRequest,
  StrategicPlanApprovalSummary,
  StrategicPlanApprover,
  StrategicPlanLockState,
  SubmitStrategicPlanApprovalRequest,
  UpsertStrategicPlanApproversRequest,
} from '../models/strategic-plan-workflow.model';
import { ApiResponse } from '../core/models/api-response.model';

@Injectable({ providedIn: 'root' })
export class StrategicPlanWorkflowService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/strategic-plans/workflow`;
  private readonly plansBaseUrl = `${environment.apiUrl}/strategic-plans`;

  submit(dto: SubmitStrategicPlanApprovalRequest): Observable<ApiResponse<StrategicPlanApprovalRequest>> {
    return this.http.post<ApiResponse<StrategicPlanApprovalRequest>>(`${this.baseUrl}/submit`, dto);
  }

  requestRevision(dto: RequestStrategicPlanRevision): Observable<ApiResponse<StrategicPlanApprovalRequest>> {
    return this.http.post<ApiResponse<StrategicPlanApprovalRequest>>(`${this.baseUrl}/request-revision`, dto);
  }

  review(requestId: string, dto: ReviewStrategicPlanApprovalRequest): Observable<ApiResponse<StrategicPlanApprovalRequest>> {
    return this.http.put<ApiResponse<StrategicPlanApprovalRequest>>(`${this.baseUrl}/requests/${requestId}/review`, dto);
  }

  getForPlan(planId: string): Observable<ApiResponse<StrategicPlanApprovalRequest[]>> {
    return this.http.get<ApiResponse<StrategicPlanApprovalRequest[]>>(`${this.baseUrl}/plan/${planId}`);
  }

  getPendingReviews(): Observable<ApiResponse<StrategicPlanApprovalRequest[]>> {
    return this.http.get<ApiResponse<StrategicPlanApprovalRequest[]>>(`${this.baseUrl}/pending-reviews`);
  }

  getMyRequests(): Observable<ApiResponse<StrategicPlanApprovalRequest[]>> {
    return this.http.get<ApiResponse<StrategicPlanApprovalRequest[]>>(`${this.baseUrl}/my-requests`);
  }

  getPendingSummary(): Observable<ApiResponse<StrategicPlanApprovalSummary>> {
    return this.http.get<ApiResponse<StrategicPlanApprovalSummary>>(`${this.baseUrl}/pending-summary`);
  }

  canReview(planId: string): Observable<ApiResponse<{ canReview: boolean }>> {
    return this.http.get<ApiResponse<{ canReview: boolean }>>(`${this.baseUrl}/can-review/${planId}`);
  }

  getLockState(planId: string): Observable<ApiResponse<StrategicPlanLockState>> {
    return this.http.get<ApiResponse<StrategicPlanLockState>>(`${this.baseUrl}/lock-state/${planId}`);
  }

  // ── Sequential approval chain ───────────────────────────────────────────

  getApprovers(planId: string): Observable<ApiResponse<StrategicPlanApprover[]>> {
    return this.http.get<ApiResponse<StrategicPlanApprover[]>>(`${this.plansBaseUrl}/${planId}/approvers`);
  }

  upsertApprovers(
    planId: string,
    request: UpsertStrategicPlanApproversRequest,
  ): Observable<ApiResponse<StrategicPlanApprover[]>> {
    return this.http.put<ApiResponse<StrategicPlanApprover[]>>(`${this.plansBaseUrl}/${planId}/approvers`, request);
  }

  // ── Draft / revision (CR-001-G) ─────────────────────────────────────────
  // Clone the published plan's graph into a working draft. Returns the draft
  // plan id, which the UI then uses as the addressable resource for edits.
  openDraft(publishedPlanId: string): Observable<ApiResponse<string>> {
    return this.http.post<ApiResponse<string>>(`${this.plansBaseUrl}/${publishedPlanId}/drafts`, {});
  }

  // Returns 200 with the open draft id, or 204 (empty) when no draft exists.
  // Callers should treat undefined as "no draft".
  getCurrentDraft(publishedPlanId: string): Observable<ApiResponse<string> | null> {
    return this.http.get<ApiResponse<string> | null>(
      `${this.plansBaseUrl}/${publishedPlanId}/drafts/current`,
      { observe: 'body' });
  }

  // Hard-delete the draft (and its graph). Pending revision requests are
  // auto-rejected on the server.
  cancelDraft(draftId: string): Observable<void> {
    return this.http.delete<void>(`${this.plansBaseUrl}/drafts/${draftId}`);
  }
}
