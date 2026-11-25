import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { ApiResponse } from '../core/models/api-response.model';
import { MyActionTagOption, WorkspaceTag } from '../models/action-item-tag.model';

/**
 * Tag look-ups (WO-2026-11 A-5). Both need `ActionItems.View`. Tags are saved
 * with the action item (`tags` on POST / PUT `/action-items`).
 */
@Injectable({ providedIn: 'root' })
export class ActionItemTagService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiBaseUrl;

  /** The tags of one workspace (404 when the caller cannot see it; empty while the server has no tag tables). */
  workspaceTags(workspaceId: string, search?: string, limit = 500): Observable<ApiResponse<WorkspaceTag[]>> {
    let params = new HttpParams().set('limit', limit);
    if (search?.trim()) params = params.set('search', search.trim());
    return this.http.get<ApiResponse<WorkspaceTag[]>>(`${this.base}/workspaces/${workspaceId}/tags`, { params });
  }

  /** My Actions filter options: the tags on my items, each with its workspace. */
  myActionTags(): Observable<ApiResponse<MyActionTagOption[]>> {
    return this.http.get<ApiResponse<MyActionTagOption[]>>(`${this.base}/action-items/my-actions/tags`);
  }
}
