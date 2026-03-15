import { inject } from '@angular/core';
import { ActivatedRouteSnapshot, CanActivateFn, Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { filter } from 'rxjs/operators';
import { PermissionStateService } from '../../features/permissions/services/permission-state.service';

/**
 * Generic permission route guard.
 *
 * Add to a route like this:
 * ```ts
 * {
 *   path: 'projects',
 *   canActivate: [permissionGuard],
 *   data: { requiredArea: 'Projects', requiredAction: 'View' },
 *   ...
 * }
 * ```
 *
 * Reads `requiredArea` and `requiredAction` from `ActivatedRouteSnapshot.data`.
 * - If both are absent: allows navigation unconditionally.
 * - If permissions are not yet loaded: waits for the first non-empty emission
 *   from `permissions$` (guards against race conditions on hard refresh where
 *   the APP_INITIALIZER may not have resolved yet).
 * - Redirects to `/access-denied?reason=insufficient-permissions` when the
 *   user lacks the required permission.
 */
export const permissionGuard: CanActivateFn = async (route: ActivatedRouteSnapshot) => {
  const permissionState = inject(PermissionStateService);
  const router          = inject(Router);

  const area   = route.data['requiredArea']   as string | undefined;
  const action = route.data['requiredAction'] as string | undefined;

  // Routes without permission requirements are always allowed.
  if (!area || !action) {
    return true;
  }

  const deny = () => {
    router.navigate(['/access-denied'], {
      queryParams: { reason: 'insufficient-permissions' },
    });
    return false;
  };

  // BehaviorSubject emits its current value immediately on subscribe.
  // If the snapshot is empty it means APP_INITIALIZER hasn't finished yet
  // (or failed) — trigger a load and wait for the first non-empty emission.
  const current = await firstValueFrom(permissionState.permissions$);

  if (current.length === 0) {
    try {
      // Kick off loading (idempotent) and wait for permissions to arrive.
      permissionState.loadPermissions().subscribe();
      await firstValueFrom(
        permissionState.permissions$.pipe(filter(perms => perms.length > 0))
      );
    } catch {
      return deny();
    }
  }

  return permissionState.hasPermission(area, action) ? true : deny();
};
