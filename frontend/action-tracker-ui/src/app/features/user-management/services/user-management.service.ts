import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../../../environments/environment';
import {
  ApiResponse,
  AssignUserOrgUnitRequest,
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

  /** Returns a paginated, searchable, sortable list of all registered users. */
  getUsers(
    page: number,
    pageSize: number,
    search: string = '',
    sortBy: string = 'fullName',
    sortDir: string = 'asc'
  ): Observable<UserListResponse> {
    const params = new HttpParams()
      .set('page', String(page))
      .set('pageSize', String(pageSize))
      .set('search', search)
      .set('sortBy', sortBy)
      .set('sortDir', sortDir);

    return this.http
      .get<ApiResponse<UserListResponse>>(`${this.baseUrl}`, { params })
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
   * At least one of name / arabicName / empNo must be provided.
   * Results include an `alreadyRegistered` flag for each match.
   */
  searchEmployees(
    searchTerm: string,
    page: number,
    pageSize: number
  ): Observable<EmployeeSearchResult[]> {
    const params = new HttpParams()
      .set('searchTerm', searchTerm)
      .set('page', String(page))
      .set('pageSize', String(pageSize));
    return this.http
      .get<ApiResponse<EmployeeSearchResult[]>>(
        `${this.baseUrl}/search-employees`, { params }
      )
      .pipe(map((res) => res.data));
  }

  /** Updates the assigned role for an existing user. */
  updateUserRole(request: UpdateUserRoleRequest): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/update-role`, request);
  }

  /** Marks a user as inactive; they can no longer authenticate. */
  deactivateUser(userId: string): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/${userId}/deactivate`, null);
  }

  /** Restores a deactivated user, allowing them to authenticate again. */
  reactivateUser(userId: string): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/${userId}/reactivate`, null);
  }

  /** Assigns (or clears) the org unit for a user. Pass null orgUnitId to unassign. */
  assignOrgUnit(userId: string, request: AssignUserOrgUnitRequest): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/${userId}/assign-org-unit`, request);
  }

  /** Returns all role names from the database, sorted A → Z. */
  getRoles(): Observable<string[]> {
    return this.http
      .get<ApiResponse<string[]>>(`${this.baseUrl}/roles`)
      .pipe(map((res) => res.data));
  }
}
