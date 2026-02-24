import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { noAuthGuard } from './core/guards/no-auth.guard';
import { roleGuard } from './core/guards/role.guard';

export const routes: Routes = [
  { path: '', redirectTo: 'dashboard', pathMatch: 'full' },

  {
    path: 'login',
    canActivate: [noAuthGuard],
    loadComponent: () =>
      import('./features/auth/login/login.component').then(m => m.LoginComponent),
  },

  {
    path: 'unauthorized',
    loadComponent: () =>
      import('./features/unauthorized/unauthorized.component').then(
        m => m.UnauthorizedComponent
      ),
  },

  {
    path: '',
    loadComponent: () =>
      import('./layout/layout.component').then(m => m.LayoutComponent),
    canActivate: [authGuard],
    children: [
      {
        path: 'dashboard',
        loadComponent: () =>
          import('./features/dashboard/team-dashboard/team-dashboard.component').then(
            m => m.TeamDashboardComponent
          ),
      },
      {
        path: 'management',
        canActivate: [roleGuard(['Admin', 'Manager'])],
        loadComponent: () =>
          import('./features/management/management-dashboard/management-dashboard.component').then(
            m => m.ManagementDashboardComponent
          ),
      },
      {
        path: 'actions',
        loadComponent: () =>
          import('./features/actions/action-list/action-list.component').then(
            m => m.ActionListComponent
          ),
      },
      {
        path: 'actions/new',
        loadComponent: () =>
          import('./features/actions/action-form/action-form.component').then(
            m => m.ActionFormComponent
          ),
      },
      {
        path: 'actions/:id/edit',
        loadComponent: () =>
          import('./features/actions/action-form/action-form.component').then(
            m => m.ActionFormComponent
          ),
      },
      {
        path: 'reports',
        canActivate: [roleGuard(['Admin', 'Manager'])],
        loadComponent: () =>
          import('./features/reports/reports.component').then(
            m => m.ReportsComponent
          ),
      },
    ],
  },

  { path: '**', redirectTo: 'dashboard' },
];
