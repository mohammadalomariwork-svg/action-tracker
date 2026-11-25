import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { ApiResponse } from '../core/models/api-response.model';
import {
  ActionUpdate, ActionUpdateCounts, ActionUpdateCreate, ActionUpdateInput, ActionUpdateList,
} from '../models/action-update.model';

/**
 * Action Updates (WO-2026-11 A-3) at action-item level (`subItemId` null) or
 * sub-item level. Reads need `ActionItems.View`, writes `ActionItems.Edit`.
 * 400 = empty or too long text, 404 = the action item / sub-item cannot be
 * opened or the update is not visible (someone else's draft), 409 = the update
 * has been submitted and can no longer be changed or deleted.
 */
@Injectable({ providedIn: 'root' })
export class ActionUpdateService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiBaseUrl}/action-items`;

  private url(actionItemId: string, subItemId: string | null, suffix = ''): string {
    const level = subItemId ? `/sub-items/${subItemId}` : '';
    return `${this.apiUrl}/${actionItemId}${level}/updates${suffix}`;
  }

  list(actionItemId: string, subItemId: string | null): Observable<ApiResponse<ActionUpdateList>> {
    return this.http.get<ApiResponse<ActionUpdateList>>(this.url(actionItemId, subItemId));
  }

  /** Counts for the action item and each of its sub-items. */
  counts(actionItemId: string): Observable<ApiResponse<ActionUpdateCounts>> {
    return this.http.get<ApiResponse<ActionUpdateCounts>>(this.url(actionItemId, null, '/counts'));
  }

  create(actionItemId: string, subItemId: string | null, body: ActionUpdateCreate): Observable<ApiResponse<ActionUpdate>> {
    return this.http.post<ApiResponse<ActionUpdate>>(this.url(actionItemId, subItemId), body);
  }

  /** Edits the caller's own draft. */
  update(actionItemId: string, subItemId: string | null, updateId: string, body: ActionUpdateInput): Observable<ApiResponse<ActionUpdate>> {
    return this.http.put<ApiResponse<ActionUpdate>>(this.url(actionItemId, subItemId, `/${updateId}`), body);
  }

  /** Submits the caller's own draft. One-way. */
  submit(actionItemId: string, subItemId: string | null, updateId: string): Observable<ApiResponse<ActionUpdate>> {
    return this.http.post<ApiResponse<ActionUpdate>>(this.url(actionItemId, subItemId, `/${updateId}/submit`), {});
  }

  /** Deletes the caller's own draft. */
  delete(actionItemId: string, subItemId: string | null, updateId: string): Observable<void> {
    return this.http.delete<void>(this.url(actionItemId, subItemId, `/${updateId}`));
  }
}
