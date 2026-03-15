import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, Observable, forkJoin } from 'rxjs';
import { tap } from 'rxjs/operators';
import { EffectivePermissionDto } from '../models/user-permission.model';
import { UserPermissionService } from './user-permission.service';
import { environment } from '../../../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class PermissionStateService {
  private readonly userPermissionService = inject(UserPermissionService);
  private readonly http = inject(HttpClient);

  private readonly _permissions$ = new BehaviorSubject<EffectivePermissionDto[]>([]);
  private readonly _visibleOrgUnitIds$ = new BehaviorSubject<string[]>([]);

  /** Emits the current authenticated user's effective permissions. */
  readonly permissions$: Observable<EffectivePermissionDto[]> =
    this._permissions$.asObservable();

  /** Emits the org unit IDs visible to the current user. */
  readonly visibleOrgUnitIds$: Observable<string[]> =
    this._visibleOrgUnitIds$.asObservable();

  /**
   * Loads the current user's effective permissions and visible org units
   * from the API and stores them. Call this after login or in APP_INITIALIZER.
   */
  loadPermissions(): Observable<[EffectivePermissionDto[], string[]]> {
    return forkJoin([
      this.userPermissionService.getMyEffectivePermissions(),
      this.http.get<string[]>(`${environment.apiUrl}/users/me/org-units`),
    ]).pipe(
      tap(([permissions, orgUnitIds]) => {
        this._permissions$.next(permissions);
        this._visibleOrgUnitIds$.next(orgUnitIds);
      })
    );
  }

  /**
   * Returns true if the current user has an allowed effective permission
   * for the given area and action matched by name (case-insensitive).
   */
  hasPermission(area: string, action: string): boolean {
    return this._permissions$.value.some(
      (p) =>
        p.areaName.toLowerCase()   === area.toLowerCase()   &&
        p.actionName.toLowerCase() === action.toLowerCase() &&
        p.isAllowed
    );
  }

  /**
   * Returns true if the current user has an allowed effective permission
   * for the given area and action matched by GUID ID.
   */
  hasPermissionById(areaId: string, actionId: string): boolean {
    return this._permissions$.value.some(
      (p) => p.areaId === areaId && p.actionId === actionId && p.isAllowed
    );
  }

  /**
   * Returns the list of org unit IDs the current user can see.
   */
  getVisibleOrgUnitIds(): string[] {
    return this._visibleOrgUnitIds$.value;
  }

  /**
   * Returns true if the given org unit ID is in the user's visible set.
   */
  canSeeOrgUnit(orgUnitId: string): boolean {
    return this._visibleOrgUnitIds$.value.includes(orgUnitId);
  }

  /**
   * Clears all stored permissions and org unit visibility.
   * Call this on logout to avoid stale data for the next user.
   */
  clearPermissions(): void {
    this._permissions$.next([]);
    this._visibleOrgUnitIds$.next([]);
  }
}
