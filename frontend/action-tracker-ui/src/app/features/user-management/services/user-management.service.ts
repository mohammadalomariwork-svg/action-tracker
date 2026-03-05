import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../../../environments/environment';
import {
  ApiResponse,
  EmployeeSearchResult,
  RegisterADUserRequest,
  RegisterExternalUserRequest,
  RegisterUserResponse,
  UpdateUserRoleRequest,
  UserListItem,
  UserListResponse,
} from '../models/user-management.models';

@Injectable({ providedIn: 'root' })
export class UserManagementService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/users`;

  /** Returns a paginated list of all registered users. */
  getUsers(page: number, pageSize: number): Observable<UserListResponse> {
    return this.http
      .get<ApiResponse<UserListResponse>>(
        `${this.baseUrl}?page=${page}&pageSize=${pageSize}`
      )
      .pipe(map((res) => res.data));
  }

  /** Returns a single user by their identity ID. */
  getUserById(id: string): Observable<UserListItem> {
    return this.http.get<UserListItem>(`${this.baseUrl}/${id}`);
  }

  /** Registers a new external (local-JWT) user with username/password credentials. */
  registerExternalUser(
    request: RegisterExternalUserRequest
  ): Observable<RegisterUserResponse> {
    return this.http.post<RegisterUserResponse>(
      `${this.baseUrl}/register-external`,
      request
    );
  }

  /** Registers a new Azure AD user by linking their AD identity to the system. */
  registerADUser(
    request: RegisterADUserRequest
  ): Observable<RegisterUserResponse> {
    return this.http.post<RegisterUserResponse>(
      `${this.baseUrl}/register-ad`,
      request
    );
  }

  /**
   * Searches the AD employee directory for users not yet registered.
   * Results include an `alreadyRegistered` flag for each match.
   */
  searchEmployees(
    searchTerm: string,
    page: number,
    pageSize: number
  ): Observable<EmployeeSearchResult[]> {
    return this.http.get<EmployeeSearchResult[]>(
      `${this.baseUrl}/search-employees?searchTerm=${searchTerm}&page=${page}&pageSize=${pageSize}`
    );
  }

  /** Updates the assigned role for an existing user. */
  updateUserRole(request: UpdateUserRoleRequest): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/update-role`, request);
  }

  /** Marks a user as inactive; they can no longer authenticate. */
  deactivateUser(userId: string): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/${userId}/deactivate`, null);
  }
}
