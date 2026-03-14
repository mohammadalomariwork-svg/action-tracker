import { inject } from '@angular/core';
import { ActivatedRouteSnapshot, CanActivateFn, Router } from '@angular/router';
import { of } from 'rxjs';
import { catchError, map } from 'rxjs/operators';
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
 * Reads `requiredArea` and `requiredAction` from `ActivatedRouteSnapshot.data`,
 * loads the current user's effective permissions (idempotent), then checks
 * via `PermissionStateService.hasPermission()`. Redirects to `/access-denied`
 * if the permission is not met.
 */
export const permissionGuard: CanActivateFn = (route: ActivatedRouteSnapshot) => {
  const permissionState = inject(PermissionStateService);
  const router          = inject(Router);

  const area   = route.data['requiredArea']   as string | undefined;
  const action = route.data['requiredAction'] as string | undefined;

  if (!area || !action) {
    // Guard misconfigured — allow through and warn
    console.warn('permissionGuard: missing requiredArea or requiredAction in route data.');
    return true;
  }

  const deny = () => {
    router.navigate(['/access-denied']);
    return false;
  };

  return permissionState.loadPermissions().pipe(
    map(() =>
      permissionState.hasPermission(area, action) ? true : deny()
    ),
    catchError(() =>
      // API call failed; fall back to cached state
      of(permissionState.hasPermission(area, action) ? true : deny())
    )
  );
};
