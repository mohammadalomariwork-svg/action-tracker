import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { DocumentInfo } from '../models/document.model';
import { ApiResponse } from '../models/api-response.model';

@Injectable({ providedIn: 'root' })
export class DocumentService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiBaseUrl}/documents`;

  getByEntity(entityType: string, entityId: string): Observable<ApiResponse<DocumentInfo[]>> {
    const params = new HttpParams()
      .set('entityType', entityType)
      .set('entityId', entityId);
    return this.http.get<ApiResponse<DocumentInfo[]>>(this.apiUrl, { params });
  }

  upload(entityType: string, entityId: string, name: string, file: File): Observable<ApiResponse<DocumentInfo>> {
    const formData = new FormData();
    formData.append('entityType', entityType);
    formData.append('entityId', entityId);
    formData.append('name', name);
    formData.append('file', file);
    return this.http.post<ApiResponse<DocumentInfo>>(this.apiUrl, formData);
  }

  download(documentId: string): Observable<Blob> {
    return this.http.get(`${this.apiUrl}/${documentId}/download`, {
      responseType: 'blob',
    });
  }

  delete(documentId: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${documentId}`);
  }
}
