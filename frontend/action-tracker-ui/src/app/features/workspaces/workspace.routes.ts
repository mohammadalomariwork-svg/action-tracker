import { Routes } from '@angular/router';
import { roleGuard } from '../../core/guards/role.guard';

import { WorkspaceListComponent } from './components/workspace-list/workspace-list.component';
import { WorkspaceFormComponent } from './components/workspace-form/workspace-form.component';
import { WorkspaceDetailComponent } from './components/workspace-detail/workspace-detail.component';

const workspaceRoutes: Routes = [
  {
    path: '',
    component: WorkspaceListComponent,
    canActivate: [roleGuard],
    data: { roles: ['Admin', 'Manager'] },
  },
  {
    path: 'new',
    component: WorkspaceFormComponent,
    canActivate: [roleGuard],
    data: { roles: ['Admin'] },
  },
  {
    path: 'edit/:id',
    component: WorkspaceFormComponent,
    canActivate: [roleGuard],
    data: { roles: ['Admin'] },
  },
  {
    path: ':id',
    component: WorkspaceDetailComponent,
    canActivate: [roleGuard],
    data: { roles: ['Admin', 'Manager'] },
  },
];

export default workspaceRoutes;
