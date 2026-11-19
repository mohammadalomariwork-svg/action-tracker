import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../models/api-response.model';
import {
  ActionItemsDashboard, MyActionsDashboard, ProjectsDashboard,
  StrategyDashboard, TeamMemberActivity,
} from '../models/dashboard-analytics.model';

/**
 * Data for the Overview, Action Items, Projects and Strategy dashboards.
 * Every endpoint is already scoped server-side to what the user may see.
 */
@Injectable({ providedIn: 'root' })
export class DashboardAnalyticsService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiBaseUrl}/dashboard`;

  getMyActions(): Observable<MyActionsDashboard> {
    return this.http.get<ApiResponse<MyActionsDashboard>>(`${this.apiUrl}/my-actions`).pipe(map(r => r.data));
  }

  getTeamActivity(): Observable<TeamMemberActivity[]> {
    return this.http.get<ApiResponse<TeamMemberActivity[]>>(`${this.apiUrl}/team-activity`).pipe(map(r => r.data ?? []));
  }

  getActionItems(): Observable<ActionItemsDashboard> {
    return this.http.get<ApiResponse<ActionItemsDashboard>>(`${this.apiUrl}/action-items`).pipe(map(r => r.data));
  }

  getProjects(): Observable<ProjectsDashboard> {
    return this.http.get<ApiResponse<ProjectsDashboard>>(`${this.apiUrl}/projects`).pipe(map(r => r.data));
  }

  getStrategy(year?: number): Observable<StrategyDashboard> {
    let params = new HttpParams();
    if (year != null) params = params.set('year', year);
    return this.http.get<ApiResponse<StrategyDashboard>>(`${this.apiUrl}/strategy`, { params }).pipe(map(r => r.data));
  }
}
