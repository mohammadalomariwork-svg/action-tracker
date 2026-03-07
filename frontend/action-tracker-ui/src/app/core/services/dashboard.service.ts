import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { DashboardKpi, ManagementDashboard, StatusBreakdown, TeamWorkload } from '../models/dashboard.model';
import { ApiResponse } from '../models/api-response.model';

@Injectable({ providedIn: 'root' })
export class DashboardService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiBaseUrl}/dashboard`;

  getKpis(): Observable<ApiResponse<DashboardKpi>> {
    return this.http.get<ApiResponse<DashboardKpi>>(`${this.apiUrl}/kpis`);
  }

  getManagementDashboard(): Observable<ApiResponse<ManagementDashboard>> {
    return this.http.get<ApiResponse<ManagementDashboard>>(`${this.apiUrl}/management`);
  }

  getTeamWorkload(): Observable<ApiResponse<TeamWorkload[]>> {
    return this.http.get<ApiResponse<TeamWorkload[]>>(`${this.apiUrl}/team-workload`);
  }

  getStatusBreakdown(): Observable<ApiResponse<StatusBreakdown[]>> {
    return this.http.get<ApiResponse<StatusBreakdown[]>>(`${this.apiUrl}/status-breakdown`);
  }
}
