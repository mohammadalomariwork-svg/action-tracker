import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { EmployeeProfile } from '../models/employee-profile.model';
import { ApiResponse } from '../models/api-response.model';

@Injectable({ providedIn: 'root' })
export class ProfileService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiBaseUrl}/profile`;

  getMyProfile(): Observable<ApiResponse<EmployeeProfile>> {
    return this.http.get<ApiResponse<EmployeeProfile>>(`${this.apiUrl}/me`);
  }
}
