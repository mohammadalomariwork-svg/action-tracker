import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { ApiResponse } from '../core/models/api-response.model';
import {
  ActionItemSubItem, ActionItemSubItemInput, ActionItemSubItemList,
} from '../models/action-item-sub-item.model';

/**
 * Sub-items on action items (WO-2026-11 A-2). Reads need `ActionItems.View`,
 * writes `ActionItems.Edit`; `setDone` is also allowed for a user assigned to
 * that sub-item. 400 = a rule refused the save (reason in `error.message`),
 * 404 = the parent cannot be opened, 409 = the parent is Done or Cancelled.
 */
@Injectable({ providedIn: 'root' })
export class ActionItemSubItemService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiBaseUrl}/action-items`;

  private url(actionItemId: string, suffix = ''): string {
    return `${this.apiUrl}/${actionItemId}/sub-items${suffix}`;
  }

  list(actionItemId: string): Observable<ApiResponse<ActionItemSubItemList>> {
    return this.http.get<ApiResponse<ActionItemSubItemList>>(this.url(actionItemId));
  }

  create(actionItemId: string, body: ActionItemSubItemInput): Observable<ApiResponse<ActionItemSubItem>> {
    return this.http.post<ApiResponse<ActionItemSubItem>>(this.url(actionItemId), body);
  }

  update(actionItemId: string, subItemId: string, body: ActionItemSubItemInput): Observable<ApiResponse<ActionItemSubItem>> {
    return this.http.put<ApiResponse<ActionItemSubItem>>(this.url(actionItemId, `/${subItemId}`), body);
  }

  delete(actionItemId: string, subItemId: string): Observable<void> {
    return this.http.delete<void>(this.url(actionItemId, `/${subItemId}`));
  }

  setDone(actionItemId: string, subItemId: string, isDone: boolean): Observable<ApiResponse<ActionItemSubItem>> {
    return this.http.patch<ApiResponse<ActionItemSubItem>>(this.url(actionItemId, `/${subItemId}/done`), { isDone });
  }

  /** Every live sub-item of the parent, in the new order. */
  reorder(actionItemId: string, subItemIds: string[]): Observable<ApiResponse<ActionItemSubItemList>> {
    return this.http.put<ApiResponse<ActionItemSubItemList>>(this.url(actionItemId, '/reorder'), { subItemIds });
  }
}
