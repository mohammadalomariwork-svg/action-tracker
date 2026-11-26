import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { environment } from '../../environments/environment';
import { ApiResponse } from '../core/models/api-response.model';
import { AssignableUser } from '../core/models/action-item.model';
import {
  PeopleLookupUser, PersonOption, ProjectPeople, ProjectPersonRole,
} from '../models/project-people.model';

/**
 * Project people (WO-2026-11 P-2) on `/projects/:id/people` and the look-ups
 * project forms need without seeing the workspace:
 *
 * - `GET /projects/:id/people` (`Projects.View`; 404 for a hidden project)
 * - `POST /projects/:id/people` `{ userId, role }` and
 *   `DELETE /projects/:id/people/:role/:userId` (`Projects.Edit` and a project
 *   the caller manages — 403 otherwise; 400 for the last sponsor or a PM not on
 *   the workspace list; 409 for a duplicate)
 * - `GET /projects/people-lookup?search=` — any active user (`Projects.Edit`, D13)
 * - `GET /projects/:id/assignable-users` — the assignee pool of the project's
 *   items: workspace pool + team, or only the project's people for a caller who
 *   reaches the project through a role (D4)
 */
@Injectable({ providedIn: 'root' })
export class ProjectPeopleService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/projects`;

  get(projectId: string): Observable<ApiResponse<ProjectPeople>> {
    return this.http.get<ApiResponse<ProjectPeople>>(`${this.base}/${projectId}/people`);
  }

  add(projectId: string, userId: string, role: ProjectPersonRole): Observable<ApiResponse<ProjectPeople>> {
    return this.http.post<ApiResponse<ProjectPeople>>(`${this.base}/${projectId}/people`, { userId, role });
  }

  remove(projectId: string, role: ProjectPersonRole, userId: string): Observable<ApiResponse<ProjectPeople>> {
    return this.http.delete<ApiResponse<ProjectPeople>>(
      `${this.base}/${projectId}/people/${role}/${encodeURIComponent(userId)}`);
  }

  /** Active users by name or e-mail, as picker options. */
  lookup(search: string, take = 20): Observable<PersonOption[]> {
    const params = new HttpParams().set('search', search).set('take', take);
    return this.http
      .get<ApiResponse<PeopleLookupUser[]>>(`${this.base}/people-lookup`, { params })
      .pipe(map(res => (res.data ?? []).map(u => ({
        userId: u.id, fullName: u.displayName, email: u.email ?? null, orgUnitName: u.orgUnitName ?? null,
      }))));
  }

  assignableUsers(projectId: string): Observable<ApiResponse<AssignableUser[]>> {
    return this.http.get<ApiResponse<AssignableUser[]>>(`${this.base}/${projectId}/assignable-users`);
  }
}
