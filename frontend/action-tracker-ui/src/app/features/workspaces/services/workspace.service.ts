import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { ApiResponse } from '../../../core/models/api-response.model';
import { AssignableUser } from '../../../core/models/action-item.model';
import {
  Workspace,
  WorkspaceList,
  WorkspaceSummary,
  WorkspaceStats,
  CreateWorkspace,
  UpdateWorkspace,
  OrgUnitDropdownItem,
  UserDropdownItem,
  WorkspaceMember,
  WorkspaceProjectManager,
  WorkspaceStrategicProjectApprover,
  AddWorkspaceMember,
  AddWorkspaceProjectManager,
  AddWorkspaceStrategicProjectApprover,
  AddMembersFromOrgUnitResult,
  ReorderWorkspaceStrategicProjectApprovers,
  SetWorkspaceStrategy,
  StrategicPlanDropdownItem,
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

  getSummary(orgUnitId?: string): Observable<ApiResponse<WorkspaceSummary>> {
    const params: Record<string, string> = {};
    if (orgUnitId) params['orgUnitId'] = orgUnitId;
    return this.http.get<ApiResponse<WorkspaceSummary>>(`${this.apiUrl}/summary`, { params });
  }

  getWorkspaceStats(id: string): Observable<ApiResponse<WorkspaceStats>> {
    return this.http.get<ApiResponse<WorkspaceStats>>(`${this.apiUrl}/${id}/stats`);
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

  // ── Members ────────────────────────────────────────────

  getMembers(workspaceId: string): Observable<ApiResponse<WorkspaceMember[]>> {
    return this.http.get<ApiResponse<WorkspaceMember[]>>(`${this.apiUrl}/${workspaceId}/members`);
  }

  /**
   * Returns users eligible to be picked as action-item assignees in this
   * workspace — i.e. workspace admins, regular members, and project managers.
   * Used by the action-item create/edit form to scope the Assignee(s) picker.
   */
  getAssignableUsers(workspaceId: string): Observable<ApiResponse<AssignableUser[]>> {
    return this.http.get<ApiResponse<AssignableUser[]>>(
      `${this.apiUrl}/${workspaceId}/assignable-users`);
  }

  addMember(workspaceId: string, payload: AddWorkspaceMember): Observable<ApiResponse<WorkspaceMember>> {
    return this.http.post<ApiResponse<WorkspaceMember>>(`${this.apiUrl}/${workspaceId}/members`, payload);
  }

  removeMember(workspaceId: string, userId: string): Observable<ApiResponse<void>> {
    return this.http.delete<ApiResponse<void>>(
      `${this.apiUrl}/${workspaceId}/members/${encodeURIComponent(userId)}`
    );
  }

  /**
   * Bulk-adds every active user in the given org unit as a workspace member.
   * Server-side: existing members are skipped silently and reported back in
   * the result counters.
   */
  addMembersFromOrgUnit(
    workspaceId: string,
    orgUnitId: string,
  ): Observable<ApiResponse<AddMembersFromOrgUnitResult>> {
    return this.http.post<ApiResponse<AddMembersFromOrgUnitResult>>(
      `${this.apiUrl}/${workspaceId}/members/from-org-unit`,
      { orgUnitId }
    );
  }

  // ── Strategy linking ───────────────────────────────────

  getStrategicPlansForDropdown(): Observable<ApiResponse<StrategicPlanDropdownItem[]>> {
    return this.http.get<ApiResponse<StrategicPlanDropdownItem[]>>(`${this.apiUrl}/strategic-plans`);
  }

  setStrategy(workspaceId: string, payload: SetWorkspaceStrategy): Observable<ApiResponse<Workspace>> {
    return this.http.put<ApiResponse<Workspace>>(`${this.apiUrl}/${workspaceId}/strategy`, payload);
  }

  // ── Project Managers ───────────────────────────────────

  getProjectManagers(workspaceId: string): Observable<ApiResponse<WorkspaceProjectManager[]>> {
    return this.http.get<ApiResponse<WorkspaceProjectManager[]>>(
      `${this.apiUrl}/${workspaceId}/project-managers`);
  }

  addProjectManager(workspaceId: string, payload: AddWorkspaceProjectManager): Observable<ApiResponse<WorkspaceProjectManager>> {
    return this.http.post<ApiResponse<WorkspaceProjectManager>>(
      `${this.apiUrl}/${workspaceId}/project-managers`, payload);
  }

  removeProjectManager(workspaceId: string, userId: string): Observable<ApiResponse<void>> {
    return this.http.delete<ApiResponse<void>>(
      `${this.apiUrl}/${workspaceId}/project-managers/${encodeURIComponent(userId)}`);
  }

  // ── Strategic Project Approvers ────────────────────────

  getStrategicProjectApprovers(workspaceId: string): Observable<ApiResponse<WorkspaceStrategicProjectApprover[]>> {
    return this.http.get<ApiResponse<WorkspaceStrategicProjectApprover[]>>(
      `${this.apiUrl}/${workspaceId}/strategic-project-approvers`);
  }

  addStrategicProjectApprover(workspaceId: string, payload: AddWorkspaceStrategicProjectApprover): Observable<ApiResponse<WorkspaceStrategicProjectApprover>> {
    return this.http.post<ApiResponse<WorkspaceStrategicProjectApprover>>(
      `${this.apiUrl}/${workspaceId}/strategic-project-approvers`, payload);
  }

  removeStrategicProjectApprover(workspaceId: string, userId: string): Observable<ApiResponse<void>> {
    return this.http.delete<ApiResponse<void>>(
      `${this.apiUrl}/${workspaceId}/strategic-project-approvers/${encodeURIComponent(userId)}`);
  }

  reorderStrategicProjectApprovers(workspaceId: string, payload: ReorderWorkspaceStrategicProjectApprovers): Observable<ApiResponse<void>> {
    return this.http.put<ApiResponse<void>>(
      `${this.apiUrl}/${workspaceId}/strategic-project-approvers/reorder`, payload);
  }
}
