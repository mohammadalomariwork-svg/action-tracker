import { DestroyRef, Injectable, Signal, effect, inject, signal, untracked } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, Subscription, of } from 'rxjs';
import { catchError, map, shareReplay, tap } from 'rxjs/operators';
import { environment } from '../../environments/environment';
import { ApiResponse } from '../core/models/api-response.model';
import { WorkspaceCalendar, WorkspaceCalendarOption } from '../models/work-calendar.model';
import { CalendarRules, createCalendarRules, isIsoDate } from '../shared/utils/work-calendar.util';

/**
 * A workspace's calendar ready for date fields: the rules from
 * `work-calendar.util.ts` plus the window they are complete for.
 */
export interface WorkspaceCalendarRules {
  calendar: WorkspaceCalendar;
  rules: CalendarRules;
  /**
   * True when `date` lies inside the loaded window, so every exception that can
   * affect it is known. Outside it the client says nothing and the server decides.
   */
  covers(date: string): boolean;
}

/** Same lifetime as the server's `WorkCalendarCache`. */
const CACHE_TTL_MS = 10 * 60 * 1000;

interface CacheEntry {
  loadedAt: number;
  rules$: Observable<WorkspaceCalendarRules | null>;
}

/**
 * Workspace calendar setting (WO-2026-11 C-3):
 * `GET/PUT /api/workspaces/{id}/calendar` and `GET /api/workspaces/calendars`.
 *
 * Date forms read the calendar through {@link get} (or {@link injectWorkspaceCalendar});
 * results are cached per workspace for ten minutes and dropped when the
 * workspace's calendar is changed here or any calendar is edited on the
 * Calendars admin page. The server checks every saved date regardless — the
 * client copy only warns early.
 */
@Injectable({ providedIn: 'root' })
export class WorkspaceCalendarService {
  private readonly http   = inject(HttpClient);
  private readonly apiUrl = `${environment.apiUrl}/workspaces`;
  private readonly cache  = new Map<string, CacheEntry>();

  /** The calendar that applies to the workspace, raw (Workspace Settings). */
  getCalendar(workspaceId: string): Observable<ApiResponse<WorkspaceCalendar>> {
    return this.http.get<ApiResponse<WorkspaceCalendar>>(`${this.apiUrl}/${workspaceId}/calendar`);
  }

  /** Active calendars a workspace can use, default first (needs `Workspaces.View`). */
  getOptions(): Observable<ApiResponse<WorkspaceCalendarOption[]>> {
    return this.http.get<ApiResponse<WorkspaceCalendarOption[]>>(`${this.apiUrl}/calendars`);
  }

  /** Points the workspace at a calendar; null = back to the default (needs `Workspaces.Edit`). */
  setCalendar(workspaceId: string, workCalendarId: string | null): Observable<ApiResponse<WorkspaceCalendar>> {
    return this.http
      .put<ApiResponse<WorkspaceCalendar>>(`${this.apiUrl}/${workspaceId}/calendar`, { workCalendarId })
      .pipe(tap(() => this.invalidate(workspaceId)));
  }

  /**
   * The workspace's calendar rules, cached. Emits null when the calendar cannot
   * be loaded (unknown workspace, network error): date fields then show no
   * warning and the server's answer stands.
   */
  get(workspaceId: string): Observable<WorkspaceCalendarRules | null> {
    const cached = this.cache.get(workspaceId);
    if (cached && Date.now() - cached.loadedAt < CACHE_TTL_MS) return cached.rules$;

    const rules$ = this.getCalendar(workspaceId).pipe(
      map(res => (res?.data ? toWorkspaceCalendarRules(res.data) : null)),
      catchError(() => {
        this.cache.delete(workspaceId);
        return of(null);
      }),
      shareReplay({ bufferSize: 1, refCount: false }),
    );
    this.cache.set(workspaceId, { loadedAt: Date.now(), rules$ });
    return rules$;
  }

  /** Drops one workspace's cached calendar, or all of them. */
  invalidate(workspaceId?: string): void {
    if (workspaceId) this.cache.delete(workspaceId);
    else this.cache.clear();
  }
}

/** Wraps a loaded workspace calendar in the client rules (also used by specs). */
export function toWorkspaceCalendarRules(calendar: WorkspaceCalendar): WorkspaceCalendarRules {
  const rules = createCalendarRules(calendar.days ?? [], calendar.exceptions ?? []);
  return {
    calendar,
    rules,
    covers: date => isIsoDate(date) && date >= calendar.from && date <= calendar.to,
  };
}

/**
 * Signal of the calendar for whichever workspace `workspaceId` returns, kept up
 * to date as that changes. Call in an injection context (a field initialiser
 * or the constructor). Null while loading, when there is no workspace yet, or
 * when the calendar cannot be loaded.
 */
export function injectWorkspaceCalendar(
  workspaceId: () => string | null | undefined,
): Signal<WorkspaceCalendarRules | null> {
  const service = inject(WorkspaceCalendarService);
  const result  = signal<WorkspaceCalendarRules | null>(null);
  let subscription: Subscription | undefined;

  effect(() => {
    const id = workspaceId() || null;
    untracked(() => {
      subscription?.unsubscribe();
      if (result()?.calendar.workspaceId !== id) result.set(null);
      if (id) subscription = service.get(id).subscribe(rules => result.set(rules));
    });
  });
  inject(DestroyRef).onDestroy(() => subscription?.unsubscribe());

  return result.asReadonly();
}
