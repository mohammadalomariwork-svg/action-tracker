import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { ApiResponse } from '../core/models/api-response.model';
import {
  DataSyncJob,
  DataSyncJobRun,
  DataSyncProbeResult,
  UpdateDataSyncJob,
} from '../models/data-sync.model';

/** Admin Panel → Data Sync API (`/api/data-sync`). */
@Injectable({ providedIn: 'root' })
export class DataSyncService {
  private readonly http   = inject(HttpClient);
  private readonly apiUrl = `${environment.apiUrl}/data-sync`;

  getJobs(): Observable<ApiResponse<DataSyncJob[]>> {
    return this.http.get<ApiResponse<DataSyncJob[]>>(`${this.apiUrl}/jobs`);
  }

  getJob(id: string): Observable<ApiResponse<DataSyncJob>> {
    return this.http.get<ApiResponse<DataSyncJob>>(`${this.apiUrl}/jobs/${id}`);
  }

  updateJob(id: string, dto: UpdateDataSyncJob): Observable<ApiResponse<DataSyncJob>> {
    return this.http.put<ApiResponse<DataSyncJob>>(`${this.apiUrl}/jobs/${id}`, dto);
  }

  runNow(id: string): Observable<ApiResponse<DataSyncJob>> {
    return this.http.post<ApiResponse<DataSyncJob>>(`${this.apiUrl}/jobs/${id}/run`, {});
  }

  probe(id: string): Observable<ApiResponse<DataSyncProbeResult>> {
    return this.http.post<ApiResponse<DataSyncProbeResult>>(`${this.apiUrl}/jobs/${id}/probe`, {});
  }

  getRuns(id: string, take = 25): Observable<ApiResponse<DataSyncJobRun[]>> {
    const params = new HttpParams().set('take', take);
    return this.http.get<ApiResponse<DataSyncJobRun[]>>(`${this.apiUrl}/jobs/${id}/runs`, { params });
  }
}
