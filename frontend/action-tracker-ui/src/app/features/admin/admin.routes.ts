import { Routes } from '@angular/router';
import { adminGuard } from '../../core/guards/admin.guard';
import { permissionGuard } from '../../core/guards/permission-data.guard';

import { AdminPanelComponent }      from './components/admin-panel/admin-panel.component';
import { OrgChartListComponent }    from './components/org-chart/org-chart-list.component';
import { ObjectivesListComponent }  from './components/strategic-objectives/objectives-list.component';
import { KpiListComponent }         from './components/kpis/kpi-list.component';
import { KpiTargetsComponent }      from './components/kpis/kpi-targets.component';

export const ADMIN_ROUTES: Routes = [
  {
    path: '',
    component: AdminPanelComponent,
    canActivate: [adminGuard],
  },
  {
    path: 'org-chart',
    component: OrgChartListComponent,
    canActivate: [permissionGuard],
    data: { requiredArea: 'OrgChart', requiredAction: 'View' },
  },
  {
    path: 'objectives',
    component: ObjectivesListComponent,
    canActivate: [permissionGuard],
    data: { requiredArea: 'StrategicObjectives', requiredAction: 'View' },
  },
  {
    path: 'kpis',
    component: KpiListComponent,
    canActivate: [permissionGuard],
    data: { requiredArea: 'KPIs', requiredAction: 'View' },
  },
  {
    path: 'kpis/:kpiId/targets',
    component: KpiTargetsComponent,
    canActivate: [permissionGuard],
    data: { requiredArea: 'KPIs', requiredAction: 'View' },
  },
  {
    path: 'permissions/roles',
    canActivate: [permissionGuard],
    data: { requiredArea: 'PermissionsManagement', requiredAction: 'View' },
    loadComponent: () =>
      import('../permissions/pages/role-permissions/role-permissions-page.component')
        .then(m => m.RolePermissionsPageComponent),
  },
  {
    path: 'permissions/users',
    canActivate: [permissionGuard],
    data: { requiredArea: 'PermissionsManagement', requiredAction: 'View' },
    loadComponent: () =>
      import('../permissions/pages/user-overrides/user-overrides-page.component')
        .then(m => m.UserOverridesPageComponent),
  },
  {
    path: 'roles',
    canActivate: [permissionGuard],
    data: { requiredArea: 'Roles', requiredAction: 'View' },
    loadComponent: () =>
      import('../roles/pages/roles-list/roles-list-page.component')
        .then(m => m.RolesListPageComponent),
  },
  {
    path: 'roles/:roleName/permissions',
    canActivate: [permissionGuard],
    data: { requiredArea: 'Roles', requiredAction: 'Edit' },
    loadComponent: () =>
      import('../roles/pages/role-permissions/role-permissions-page.component')
        .then(m => m.RolePermissionsPageComponent),
  },
  {
    path: 'roles/:roleName/users',
    canActivate: [permissionGuard],
    data: { requiredArea: 'Roles', requiredAction: 'Assign' },
    loadComponent: () =>
      import('../roles/pages/role-users/role-users-page.component')
        .then(m => m.RoleUsersPageComponent),
  },
];
