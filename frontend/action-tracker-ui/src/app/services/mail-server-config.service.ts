import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { MailServerConfig, UpdateMailServerConfig } from '../models/mail-server-config.model';
import { ApiResponse } from '../core/models/api-response.model';

@Injectable({ providedIn: 'root' })
export class MailServerConfigService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiUrl}/mail-server-config`;

  get(): Observable<ApiResponse<MailServerConfig | null>> {
    return this.http.get<ApiResponse<MailServerConfig | null>>(this.apiUrl);
  }

  save(dto: UpdateMailServerConfig): Observable<ApiResponse<MailServerConfig>> {
    return this.http.put<ApiResponse<MailServerConfig>>(this.apiUrl, dto);
  }
}
