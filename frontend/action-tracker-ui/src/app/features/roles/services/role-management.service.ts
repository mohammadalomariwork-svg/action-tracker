import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';
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
    return this.http.get<AppRoleDto[]>(this.baseUrl);
  }

  getRole(roleName: string): Observable<AppRoleDto> {
    return this.http.get<AppRoleDto>(`${this.baseUrl}/${roleName}`);
  }

  getRoleUsers(roleName: string): Observable<RoleUserDto[]> {
    return this.http.get<RoleUserDto[]>(`${this.baseUrl}/${roleName}/users`);
  }

  getRolePermissions(roleName: string): Observable<PermissionMatrixDto> {
    return this.http.get<PermissionMatrixDto>(`${this.baseUrl}/${roleName}/permissions`);
  }

  createRole(name: string): Observable<AppRoleDto> {
    return this.http.post<AppRoleDto>(this.baseUrl, { name });
  }

  deleteRole(roleName: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${roleName}`);
  }

  assignPermissionsToRole(dto: AssignRolePermissionsDto): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/${dto.roleName}/permissions`, dto);
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
