import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../../../environments/environment';
import { ApiResponse } from '../../../core/models/api-response.model';
import {
  AppRoleDto,
  RoleUserDto,
  AssignRolePermissionsDto,
  AssignUsersToRoleDto,
} from '../models/role.model';
import { PermissionMatrixDto } from '../../permissions/models/role-permission.model';

@Injectable({ providedIn: 'root' })
export class RoleManagementService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl + '/roles';

  getAllRoles(): Observable<AppRoleDto[]> {
    return this.http
      .get<ApiResponse<AppRoleDto[]>>(this.baseUrl)
      .pipe(map((res) => res.data));
  }

  getRole(roleName: string): Observable<AppRoleDto> {
    return this.http
      .get<ApiResponse<AppRoleDto>>(`${this.baseUrl}/${roleName}`)
      .pipe(map((res) => res.data));
  }

  getRoleUsers(roleName: string): Observable<RoleUserDto[]> {
    return this.http
      .get<ApiResponse<RoleUserDto[]>>(`${this.baseUrl}/${roleName}/users`)
      .pipe(map((res) => res.data));
  }

  getRolePermissions(roleName: string): Observable<PermissionMatrixDto> {
    return this.http
      .get<ApiResponse<PermissionMatrixDto>>(`${this.baseUrl}/${roleName}/permissions`)
      .pipe(map((res) => res.data));
  }

  createRole(name: string): Observable<AppRoleDto> {
    return this.http
      .post<ApiResponse<AppRoleDto>>(this.baseUrl, { name })
      .pipe(map((res) => res.data));
  }

  deleteRole(roleName: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${roleName}`);
  }

  assignPermissionsToRole(dto: AssignRolePermissionsDto): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${dto.roleName}/permissions`, dto);
  }

  assignUsersToRole(dto: AssignUsersToRoleDto): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${dto.roleName}/users`, dto);
  }

  removeUsersFromRole(dto: { roleName: string; userIds: string[] }): Observable<void> {
    return this.http.request<void>('delete', `${this.baseUrl}/${dto.roleName}/users`, {
      body: dto,
    });
  }
}
