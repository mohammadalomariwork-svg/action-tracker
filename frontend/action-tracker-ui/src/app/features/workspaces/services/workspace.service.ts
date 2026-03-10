import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { ApiResponse } from '../../../core/models/api-response.model';
import {
  Workspace,
  WorkspaceList,
  WorkspaceSummary,
  CreateWorkspace,
  UpdateWorkspace,
  OrgUnitDropdownItem,
  UserDropdownItem,
} from '../models/workspace.model';

/**
 * Service for all workspace API operations.
 * Provided at root level — auth headers are added automatically by the
 * existing HTTP interceptor.
 */
@Injectable({ providedIn: 'root' })
export class WorkspaceService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiUrl}/workspaces`;

  /**
   * Fetches all active workspaces ordered by title.
   * Requires Admin or Manager role.
   */
  getWorkspaces(): Observable<ApiResponse<WorkspaceList[]>> {
    return this.http.get<ApiResponse<WorkspaceList[]>>(this.apiUrl);
  }

  getSummary(): Observable<ApiResponse<WorkspaceSummary>> {
    return this.http.get<ApiResponse<WorkspaceSummary>>(`${this.apiUrl}/summary`);
  }

  /**
   * Fetches the full details of a single workspace.
   * Requires Admin or Manager role.
   * @param id Primary key of the workspace.
   */
  getWorkspaceById(id: string): Observable<ApiResponse<Workspace>> {
    return this.http.get<ApiResponse<Workspace>>(`${this.apiUrl}/${id}`);
  }

  /**
   * Fetches all active workspaces where the given user is the admin.
   * Requires any authenticated user.
   * @param adminUserId The AspNetUsers.Id of the admin user.
   */
  getWorkspacesByAdmin(adminUserId: string): Observable<ApiResponse<WorkspaceList[]>> {
    return this.http.get<ApiResponse<WorkspaceList[]>>(
      `${this.apiUrl}/by-admin/${encodeURIComponent(adminUserId)}`
    );
  }

  /**
   * Creates a new workspace.
   * Requires Admin role.
   * @param data Creation payload.
   */
  createWorkspace(data: CreateWorkspace): Observable<ApiResponse<Workspace>> {
    return this.http.post<ApiResponse<Workspace>>(this.apiUrl, data);
  }

  /**
   * Updates an existing workspace.
   * Requires Admin role. The `data.id` must match the route `id`.
   * @param id Primary key of the workspace to update.
   * @param data Update payload.
   */
  updateWorkspace(id: string, data: UpdateWorkspace): Observable<ApiResponse<Workspace>> {
    return this.http.put<ApiResponse<Workspace>>(`${this.apiUrl}/${id}`, data);
  }

  /**
   * Soft-deletes a workspace (sets it inactive).
   * Requires Admin role.
   * @param id Primary key of the workspace to delete.
   */
  deleteWorkspace(id: string): Observable<ApiResponse<void>> {
    return this.http.delete<ApiResponse<void>>(`${this.apiUrl}/${id}`);
  }

  /**
   * Restores a soft-deleted workspace (sets it active again).
   * Requires Admin role.
   * @param id Primary key of the workspace to restore.
   */
  restoreWorkspace(id: string): Observable<ApiResponse<void>> {
    return this.http.patch<ApiResponse<void>>(`${this.apiUrl}/${id}/restore`, {});
  }

  /**
   * Returns non-deleted org units for the Organisation Unit dropdown.
   * Requires Admin role.
   */
  getOrgUnitsForDropdown(): Observable<ApiResponse<OrgUnitDropdownItem[]>> {
    return this.http.get<ApiResponse<OrgUnitDropdownItem[]>>(`${this.apiUrl}/org-units`);
  }

  /**
   * Returns active users for the Workspace Admin dropdown.
   * Requires Admin role.
   */
  getActiveUsersForDropdown(): Observable<ApiResponse<UserDropdownItem[]>> {
    return this.http.get<ApiResponse<UserDropdownItem[]>>(`${this.apiUrl}/active-users`);
  }
}
