import { Routes } from '@angular/router';
import { MsalRedirectComponent } from '@azure/msal-angular';
import { authGuard } from './core/guards/auth.guard';
import { loginGuard } from './core/guards/login.guard';
import { roleGuard } from './core/guards/role.guard';
import { adminGuard } from './core/guards/admin.guard';

export const routes: Routes = [
  { path: '', redirectTo: 'dashboard', pathMatch: 'full' },

  {
    path: 'login',
    canActivate: [loginGuard],
    loadComponent: () =>
      import('./features/auth/login/login.component').then(m => m.LoginComponent),
  },

  {
    path: 'unauthorized',
    loadComponent: () =>
      import('./features/auth/unauthorized/unauthorized.component').then(
        m => m.UnauthorizedComponent
      ),
  },

  // MSAL popup redirect landing page.
  // After a successful Azure AD interactive login, the popup window is redirected
  // here (matching redirectUri in environment.msalConfig). MsalRedirectComponent
  // calls handleRedirectObservable(), which resolves the auth code, posts the
  // result back to the opener window, and closes the popup. Must live outside
  // authGuard/loginGuard so that the popup can load it without being redirected.
  {
    path: 'auth_fallback',
    component: MsalRedirectComponent,
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
        canActivate: [roleGuard],
        data: { roles: ['Admin', 'Manager'] },
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
        canActivate: [roleGuard],
        data: { roles: ['Admin', 'Manager'] },
        loadComponent: () =>
          import('./features/reports/reports.component').then(
            m => m.ReportsComponent
          ),
      },
      {
        path: 'workspaces',
        loadChildren: () =>
          import('./features/workspaces/workspace.routes').then(m => m.default),
      },
      {
        path: 'projects',
        loadChildren: () =>
          import('./features/projects/project.routes').then(m => m.default),
      },
      {
        path: 'workspaces/:id',
        loadComponent: () =>
          import('./features/workspaces/components/workspace-detail/workspace-detail.component').then(
            m => m.WorkspaceDetailComponent
          ),
        canActivate: [authGuard],
      },
      {
        path: 'action-items',
        loadChildren: () =>
          import('./features/action-items/action-item.routes').then(m => m.default),
      },
      {
        path: 'admin',
        loadChildren: () =>
          import('./features/admin/admin.routes').then(m => m.ADMIN_ROUTES),
        canActivate: [adminGuard],
      },
      {
        path: 'admin/users',
        canActivate: [adminGuard],
        loadChildren: () =>
          import('./features/user-management/user-management.routes').then(
            m => m.USER_MANAGEMENT_ROUTES
          ),
      },
    ],
  },

  { path: '**', redirectTo: 'dashboard' },
];
