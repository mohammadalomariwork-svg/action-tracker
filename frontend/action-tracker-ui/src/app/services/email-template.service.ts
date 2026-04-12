import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { EmailTemplate, EmailTemplateListItem, UpdateEmailTemplate, EmailLog } from '../models/email-template.model';
import { ApiResponse, PagedResult } from '../core/models/api-response.model';

@Injectable({ providedIn: 'root' })
export class EmailTemplateService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiUrl}/email-templates`;

  getAll(): Observable<ApiResponse<EmailTemplateListItem[]>> {
    return this.http.get<ApiResponse<EmailTemplateListItem[]>>(this.apiUrl);
  }

  getById(id: string): Observable<ApiResponse<EmailTemplate>> {
    return this.http.get<ApiResponse<EmailTemplate>>(`${this.apiUrl}/${id}`);
  }

  update(id: string, dto: UpdateEmailTemplate): Observable<ApiResponse<EmailTemplate>> {
    return this.http.put<ApiResponse<EmailTemplate>>(`${this.apiUrl}/${id}`, dto);
  }

  getLogs(page: number, pageSize: number, templateKey?: string, status?: string): Observable<ApiResponse<PagedResult<EmailLog>>> {
    let params = new HttpParams()
      .set('page', page)
      .set('pageSize', pageSize);

    if (templateKey) params = params.set('templateKey', templateKey);
    if (status) params = params.set('status', status);

    return this.http.get<ApiResponse<PagedResult<EmailLog>>>(`${this.apiUrl}/logs`, { params });
  }
}
