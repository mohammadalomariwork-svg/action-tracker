import { Routes } from '@angular/router';
import { permissionGuard } from '../../core/guards/permission-data.guard';

import { WorkspaceListComponent } from './components/workspace-list/workspace-list.component';
import { WorkspaceFormComponent } from './components/workspace-form/workspace-form.component';
import { WorkspaceDetailComponent } from './components/workspace-detail/workspace-detail.component';
import { WorkspaceSettingsComponent } from './components/workspace-settings/workspace-settings.component';

const workspaceRoutes: Routes = [
  {
    path: '',
    component: WorkspaceListComponent,
    canActivate: [permissionGuard],
    data: { requiredArea: 'Workspaces', requiredAction: 'View' },
  },
  {
    path: 'new',
    component: WorkspaceFormComponent,
    canActivate: [permissionGuard],
    data: { requiredArea: 'Workspaces', requiredAction: 'Create' },
  },
  {
    path: 'edit/:id',
    component: WorkspaceFormComponent,
    canActivate: [permissionGuard],
    data: { requiredArea: 'Workspaces', requiredAction: 'Edit' },
  },
  {
    path: ':id/settings',
    component: WorkspaceSettingsComponent,
    canActivate: [permissionGuard],
    data: { requiredArea: 'Workspaces', requiredAction: 'View' },
  },
  {
    path: ':id',
    component: WorkspaceDetailComponent,
    canActivate: [permissionGuard],
    data: { requiredArea: 'Workspaces', requiredAction: 'View' },
  },
];

export default workspaceRoutes;
