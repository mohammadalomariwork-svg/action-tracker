import { Injectable, inject } from '@angular/core';
import { BehaviorSubject, Observable } from 'rxjs';
import { tap } from 'rxjs/operators';
import { EffectivePermissionDto } from '../models/user-permission.model';
import { UserPermissionService } from './user-permission.service';

@Injectable({ providedIn: 'root' })
export class PermissionStateService {
  private readonly userPermissionService = inject(UserPermissionService);

  private readonly _permissions$ = new BehaviorSubject<EffectivePermissionDto[]>([]);

  /** Emits the current authenticated user's effective permissions. */
  readonly permissions$: Observable<EffectivePermissionDto[]> =
    this._permissions$.asObservable();

  /**
   * Loads the current user's effective permissions from the API and stores them.
   * Call this after a successful login or during APP_INITIALIZER.
   */
  loadPermissions(): Observable<EffectivePermissionDto[]> {
    return this.userPermissionService.getMyEffectivePermissions().pipe(
      tap((permissions) => this._permissions$.next(permissions))
    );
  }

  /**
   * Returns true if the current user has an allowed effective permission
   * for the given area and action (case-insensitive).
   */
  hasPermission(area: string, action: string): boolean {
    return this._permissions$.value.some(
      (p) =>
        p.area.toLowerCase()   === area.toLowerCase()   &&
        p.action.toLowerCase() === action.toLowerCase() &&
        p.isAllowed
    );
  }

  /**
   * Clears all stored permissions.
   * Call this on logout to avoid stale permissions for the next user.
   */
  clearPermissions(): void {
    this._permissions$.next([]);
  }
}
