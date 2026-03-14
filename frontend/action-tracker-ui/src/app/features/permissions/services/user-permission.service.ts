import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../../../../environments/environment';
import { ApiResponse } from '../../../core/models/api-response.model';
import {
  CreateUserPermissionOverrideDto,
  EffectivePermissionDto,
  UpdateUserPermissionOverrideDto,
  UserPermissionOverrideDto,
} from '../models/user-permission.model';

@Injectable({ providedIn: 'root' })
export class UserPermissionService {
  private readonly http    = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/user-permissions`;

  /** Returns all active permission overrides for the given user. */
  getOverridesByUser(userId: string): Observable<UserPermissionOverrideDto[]> {
    return this.http
      .get<ApiResponse<UserPermissionOverrideDto[]>>(
        `${this.baseUrl}/${encodeURIComponent(userId)}/overrides`
      )
      .pipe(map((res) => res.data));
  }

  /** Returns a single permission override by ID. */
  getOverrideById(id: string): Observable<UserPermissionOverrideDto> {
    return this.http
      .get<ApiResponse<UserPermissionOverrideDto>>(
        `${this.baseUrl}/overrides/${id}`
      )
      .pipe(map((res) => res.data));
  }

  /** Creates a new user-level permission override. */
  createOverride(
    dto: CreateUserPermissionOverrideDto
  ): Observable<UserPermissionOverrideDto> {
    return this.http
      .post<ApiResponse<UserPermissionOverrideDto>>(
        `${this.baseUrl}/overrides`,
        dto
      )
      .pipe(map((res) => res.data));
  }

  /** Updates a user permission override. */
  updateOverride(
    id: string,
    dto: UpdateUserPermissionOverrideDto
  ): Observable<UserPermissionOverrideDto> {
    return this.http
      .put<ApiResponse<UserPermissionOverrideDto>>(
        `${this.baseUrl}/overrides/${id}`,
        dto
      )
      .pipe(map((res) => res.data));
  }

  /** Soft-deletes a user permission override. */
  deleteOverride(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/overrides/${id}`);
  }

  /** Returns the merged effective permissions for a given user (admin view). */
  getEffectivePermissions(userId: string): Observable<EffectivePermissionDto[]> {
    return this.http
      .get<ApiResponse<EffectivePermissionDto[]>>(
        `${this.baseUrl}/${encodeURIComponent(userId)}/effective`
      )
      .pipe(map((res) => res.data));
  }

  /** Returns the effective permissions for the currently authenticated user. */
  getMyEffectivePermissions(): Observable<EffectivePermissionDto[]> {
    return this.http
      .get<ApiResponse<EffectivePermissionDto[]>>(`${this.baseUrl}/me/effective`)
      .pipe(map((res) => res.data));
  }
}
