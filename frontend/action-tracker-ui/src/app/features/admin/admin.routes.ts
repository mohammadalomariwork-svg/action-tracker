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
    data: { requiredArea: 'Org Chart', requiredAction: 'View' },
  },
  {
    path: 'objectives',
    component: ObjectivesListComponent,
    canActivate: [permissionGuard],
    data: { requiredArea: 'Strategic Objectives', requiredAction: 'View' },
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
    data: { requiredArea: 'Permissions Management', requiredAction: 'View' },
    loadComponent: () =>
      import('../permissions/pages/role-permissions/role-permissions-page.component')
        .then(m => m.RolePermissionsPageComponent),
  },
  {
    path: 'permissions/users',
    canActivate: [permissionGuard],
    data: { requiredArea: 'Permissions Management', requiredAction: 'View' },
    loadComponent: () =>
      import('../permissions/pages/user-overrides/user-overrides-page.component')
        .then(m => m.UserOverridesPageComponent),
  },
];
