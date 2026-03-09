import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { CommentInfo } from '../models/comment.model';
import { ApiResponse } from '../models/api-response.model';

@Injectable({ providedIn: 'root' })
export class CommentService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiBaseUrl}/comments`;

  getByEntity(entityType: string, entityId: string): Observable<ApiResponse<CommentInfo[]>> {
    const params = new HttpParams()
      .set('entityType', entityType)
      .set('entityId', entityId);
    return this.http.get<ApiResponse<CommentInfo[]>>(this.apiUrl, { params });
  }

  add(entityType: string, entityId: string, body: { content: string; isHighImportance: boolean }): Observable<ApiResponse<CommentInfo>> {
    const params = new HttpParams()
      .set('entityType', entityType)
      .set('entityId', entityId);
    return this.http.post<ApiResponse<CommentInfo>>(this.apiUrl, body, { params });
  }

  update(commentId: string, body: { content: string; isHighImportance: boolean }): Observable<ApiResponse<CommentInfo>> {
    return this.http.put<ApiResponse<CommentInfo>>(`${this.apiUrl}/${commentId}`, body);
  }

  delete(commentId: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${commentId}`);
  }
}
