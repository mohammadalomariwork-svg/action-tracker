import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { of } from 'rxjs';
import { catchError, map } from 'rxjs/operators';
import { PermissionStateService } from '../../features/permissions/services/permission-state.service';

/**
 * Route guard that loads the current user's effective permissions (if not already
 * loaded) then checks for the PermissionsManagement.View permission.
 * Redirects to /unauthorized if the permission is not present.
 */
export const permissionsManagementGuard: CanActivateFn = () => {
  const permissionState = inject(PermissionStateService);
  const router          = inject(Router);

  return permissionState.loadPermissions().pipe(
    map(() => {
      if (permissionState.hasPermission('Permissions Management', 'View')) {
        return true;
      }
      router.navigate(['/unauthorized']);
      return false;
    }),
    catchError(() => {
      // API call failed (e.g. not authenticated); fall back to current cached state
      if (permissionState.hasPermission('Permissions Management', 'View')) {
        return of(true);
      }
      router.navigate(['/unauthorized']);
      return of(false);
    })
  );
};
