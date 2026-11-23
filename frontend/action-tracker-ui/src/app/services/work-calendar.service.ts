import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { ApiResponse } from '../core/models/api-response.model';
import {
  WorkCalendar,
  WorkCalendarInput,
  WorkCalendarUsage,
} from '../models/work-calendar.model';

/**
 * Admin Panel → Calendars API (`/api/calendars`).
 * Rule violations (duplicate name, the default calendar, a calendar in use)
 * come back as 409 with the reason in `error.message`.
 */
@Injectable({ providedIn: 'root' })
export class WorkCalendarService {
  private readonly http   = inject(HttpClient);
  private readonly apiUrl = `${environment.apiUrl}/calendars`;

  /** All calendars, default first. `includeDeleted` also lists soft-deleted ones. */
  getAll(includeDeleted = false): Observable<ApiResponse<WorkCalendar[]>> {
    const params = new HttpParams().set('includeDeleted', includeDeleted);
    return this.http.get<ApiResponse<WorkCalendar[]>>(this.apiUrl, { params });
  }

  getById(id: string): Observable<ApiResponse<WorkCalendar>> {
    return this.http.get<ApiResponse<WorkCalendar>>(`${this.apiUrl}/${id}`);
  }

  /** Workspaces using the calendar; ones the caller cannot see are only counted. */
  getUsage(id: string): Observable<ApiResponse<WorkCalendarUsage>> {
    return this.http.get<ApiResponse<WorkCalendarUsage>>(`${this.apiUrl}/${id}/usage`);
  }

  create(dto: WorkCalendarInput): Observable<ApiResponse<WorkCalendar>> {
    return this.http.post<ApiResponse<WorkCalendar>>(this.apiUrl, dto);
  }

  /** Replaces the calendar's details, its seven day rows and its full exception list. */
  update(id: string, dto: WorkCalendarInput): Observable<ApiResponse<WorkCalendar>> {
    return this.http.put<ApiResponse<WorkCalendar>>(`${this.apiUrl}/${id}`, dto);
  }

  /** Makes this calendar the default; the previous default loses the flag. */
  setDefault(id: string): Observable<ApiResponse<WorkCalendar>> {
    return this.http.put<ApiResponse<WorkCalendar>>(`${this.apiUrl}/${id}/default`, {});
  }

  /** Soft delete. 204 on success. */
  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }

  /** Restore a soft-deleted calendar. 204 on success. */
  restore(id: string): Observable<void> {
    return this.http.patch<void>(`${this.apiUrl}/${id}/restore`, {});
  }
}
