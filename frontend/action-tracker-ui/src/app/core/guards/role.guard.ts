import { inject } from '@angular/core';
import { ActivatedRouteSnapshot, CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

// Usage: { path: 'admin', component: AdminComponent, canActivate: [authGuard, roleGuard], data: { roles: ['Admin'] } }
export const roleGuard: CanActivateFn = (route: ActivatedRouteSnapshot) => {
  const authService = inject(AuthService);
  const router      = inject(Router);

  if (!authService.isAuthenticated()) {
    router.navigate(['/login']);
    return false;
  }

  const requiredRoles = (route.data['roles'] as string[] | undefined) ?? [];

  if (requiredRoles.some(role => authService.hasRole(role))) {
    return true;
  }

  router.navigate(['/unauthorized']);
  return false;
};
