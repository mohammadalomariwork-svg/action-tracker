import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of, shareReplay, tap, catchError, map } from 'rxjs';
import { environment } from '../../environments/environment';
import { ApiResponse } from '../core/models/api-response.model';
import { ProjectClassification, ProjectClassificationInput } from '../models/project-classification.model';

/**
 * Internal project classifications of a workspace (WO-2026-11 P-1) on
 * `/workspaces/:id/project-classifications`. Reads need `Projects.View` or
 * `Workspaces.View` and a workspace the caller can see (or whose Project
 * Managers list they are on) — 404 otherwise; writes need `Workspaces.Edit`.
 * A project's classification is saved with the project
 * (`internalClassificationId` on POST / PUT `/projects`).
 */
@Injectable({ providedIn: 'root' })
export class ProjectClassificationService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiBaseUrl;

  /** Per-workspace cache of the dropdown options (dropped on every write and after 5 minutes). */
  private readonly cache = new Map<string, { at: number; list$: Observable<ProjectClassification[]> }>();
  private static readonly CacheMs = 5 * 60 * 1000;

  private url(workspaceId: string, rest = ''): string {
    return `${this.base}/workspaces/${workspaceId}/project-classifications${rest}`;
  }

  /** Every live classification of the workspace (active and inactive), by sort order, with usage counts. */
  list(workspaceId: string): Observable<ApiResponse<ProjectClassification[]>> {
    return this.http.get<ApiResponse<ProjectClassification[]>>(this.url(workspaceId));
  }

  /**
   * The list for dropdowns and filters, cached per workspace. Never errors:
   * a failed load (404 / 403) gives an empty list, so a form still works —
   * the project's current classification comes with the project itself.
   */
  options(workspaceId: string): Observable<ProjectClassification[]> {
    const hit = this.cache.get(workspaceId);
    if (hit && Date.now() - hit.at < ProjectClassificationService.CacheMs) return hit.list$;

    const list$ = this.list(workspaceId).pipe(
      map(res => res.data ?? []),
      catchError(() => {
        this.cache.delete(workspaceId);
        return of([] as ProjectClassification[]);
      }),
      shareReplay({ bufferSize: 1, refCount: false }),
    );
    this.cache.set(workspaceId, { at: Date.now(), list$ });
    return list$;
  }

  create(workspaceId: string, input: ProjectClassificationInput): Observable<ApiResponse<ProjectClassification>> {
    return this.http
      .post<ApiResponse<ProjectClassification>>(this.url(workspaceId), { name: input.name })
      .pipe(tap(() => this.invalidate(workspaceId)));
  }

  /** Rename and / or (de)activate. */
  update(workspaceId: string, id: string, input: Required<ProjectClassificationInput>): Observable<ApiResponse<ProjectClassification>> {
    return this.http
      .put<ApiResponse<ProjectClassification>>(this.url(workspaceId, `/${id}`), input)
      .pipe(tap(() => this.invalidate(workspaceId)));
  }

  /** New order: every live classification once. */
  reorder(workspaceId: string, classificationIds: string[]): Observable<ApiResponse<ProjectClassification[]>> {
    return this.http
      .put<ApiResponse<ProjectClassification[]>>(this.url(workspaceId, '/reorder'), { classificationIds })
      .pipe(tap(() => this.invalidate(workspaceId)));
  }

  /** Soft delete; 409 while any project (also a deleted one) uses it. */
  delete(workspaceId: string, id: string): Observable<void> {
    return this.http
      .delete<void>(this.url(workspaceId, `/${id}`))
      .pipe(tap(() => this.invalidate(workspaceId)));
  }

  /** Drops the cached options of a workspace (or all). */
  invalidate(workspaceId?: string): void {
    if (workspaceId) this.cache.delete(workspaceId);
    else this.cache.clear();
  }
}
