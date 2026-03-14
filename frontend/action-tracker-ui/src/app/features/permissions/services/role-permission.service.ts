import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../../../environments/environment';
import { ApiResponse } from '../../../core/models/api-response.model';
import {
  CreateRolePermissionDto,
  PermissionMatrixDto,
  RolePermissionDto,
  UpdateRolePermissionDto,
} from '../models/role-permission.model';

@Injectable({ providedIn: 'root' })
export class RolePermissionService {
  private readonly http    = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/role-permissions`;

  /** Returns all role names defined in the system. */
  getAllRoles(): Observable<string[]> {
    return this.http
      .get<ApiResponse<string[]>>(`${this.baseUrl}/roles`)
      .pipe(map((res) => res.data));
  }

  /** Returns all active permissions for the given role. */
  getByRole(roleName: string): Observable<RolePermissionDto[]> {
    return this.http
      .get<ApiResponse<RolePermissionDto[]>>(
        `${this.baseUrl}/${encodeURIComponent(roleName)}`
      )
      .pipe(map((res) => res.data));
  }

  /** Returns a single role permission by ID. */
  getById(id: string): Observable<RolePermissionDto> {
    return this.http
      .get<ApiResponse<RolePermissionDto>>(`${this.baseUrl}/${id}/detail`)
      .pipe(map((res) => res.data));
  }

  /** Returns the full permission matrix for the given role. */
  getMatrix(roleName: string): Observable<PermissionMatrixDto> {
    return this.http
      .get<ApiResponse<PermissionMatrixDto>>(
        `${this.baseUrl}/matrix/${encodeURIComponent(roleName)}`
      )
      .pipe(map((res) => res.data));
  }

  /** Creates a new role permission. */
  create(dto: CreateRolePermissionDto): Observable<RolePermissionDto> {
    return this.http
      .post<ApiResponse<RolePermissionDto>>(this.baseUrl, dto)
      .pipe(map((res) => res.data));
  }

  /** Updates a role permission's scope and active flag. */
  update(id: string, dto: UpdateRolePermissionDto): Observable<RolePermissionDto> {
    return this.http
      .put<ApiResponse<RolePermissionDto>>(`${this.baseUrl}/${id}`, dto)
      .pipe(map((res) => res.data));
  }

  /** Soft-deletes a role permission. */
  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}
