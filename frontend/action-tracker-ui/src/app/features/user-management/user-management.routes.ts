import { Routes } from '@angular/router';
import { adminGuard } from '../../core/guards/admin.guard';

export const USER_MANAGEMENT_ROUTES: Routes = [
  {
    path: '',
    canActivate: [adminGuard],
    loadComponent: () =>
      import('./components/user-list/user-list.component').then(
        m => m.UserListComponent
      ),
  },
  {
    path: 'register-external',
    canActivate: [adminGuard],
    loadComponent: () =>
      import('./components/register-external-user/register-external-user.component').then(
        m => m.RegisterExternalUserComponent
      ),
  },
  {
    path: 'register-ad',
    canActivate: [adminGuard],
    loadComponent: () =>
      import('./components/register-ad-user/register-ad-user.component').then(
        m => m.RegisterADUserComponent
      ),
  },
];
