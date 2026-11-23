import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { tap } from 'rxjs/operators';
import { environment } from '../../environments/environment';
import { ApiResponse } from '../core/models/api-response.model';
import {
  WorkCalendar,
  WorkCalendarInput,
  WorkCalendarUsage,
} from '../models/work-calendar.model';
import { WorkspaceCalendarService } from './workspace-calendar.service';

/**
 * Admin Panel → Calendars API (`/api/calendars`).
 * Rule violations (duplicate name, the default calendar, a calendar in use)
 * come back as 409 with the reason in `error.message`.
 * Every write drops the date forms' cached workspace calendars
 * ({@link WorkspaceCalendarService}), so they pick the change up at once.
 */
@Injectable({ providedIn: 'root' })
export class WorkCalendarService {
  private readonly http   = inject(HttpClient);
  private readonly apiUrl = `${environment.apiUrl}/calendars`;
  private readonly workspaceCalendars = inject(WorkspaceCalendarService);
  private readonly dropCachedCalendars = () => this.workspaceCalendars.invalidate();

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
    return this.http.post<ApiResponse<WorkCalendar>>(this.apiUrl, dto).pipe(tap(this.dropCachedCalendars));
  }

  /** Replaces the calendar's details, its seven day rows and its full exception list. */
  update(id: string, dto: WorkCalendarInput): Observable<ApiResponse<WorkCalendar>> {
    return this.http.put<ApiResponse<WorkCalendar>>(`${this.apiUrl}/${id}`, dto).pipe(tap(this.dropCachedCalendars));
  }

  /** Makes this calendar the default; the previous default loses the flag. */
  setDefault(id: string): Observable<ApiResponse<WorkCalendar>> {
    return this.http.put<ApiResponse<WorkCalendar>>(`${this.apiUrl}/${id}/default`, {}).pipe(tap(this.dropCachedCalendars));
  }

  /** Soft delete. 204 on success. */
  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`).pipe(tap(this.dropCachedCalendars));
  }

  /** Restore a soft-deleted calendar. 204 on success. */
  restore(id: string): Observable<void> {
    return this.http.patch<void>(`${this.apiUrl}/${id}/restore`, {}).pipe(tap(this.dropCachedCalendars));
  }
}
