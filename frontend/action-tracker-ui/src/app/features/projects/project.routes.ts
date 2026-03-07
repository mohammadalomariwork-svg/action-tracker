import { Routes } from '@angular/router';
import { authGuard } from '../../core/guards/auth.guard';
import { roleGuard } from '../../core/guards/role.guard';

import { ProjectFormComponent } from './components/project-form/project-form.component';
import { ProjectDetailComponent } from './components/project-detail/project-detail.component';

const projectRoutes: Routes = [
  {
    path: '',
    redirectTo: 'workspaces',
    pathMatch: 'full',
  },
  {
    path: ':id',
    component: ProjectDetailComponent,
    canActivate: [authGuard],
  },
  {
    path: 'new',
    component: ProjectFormComponent,
    canActivate: [authGuard, roleGuard],
    data: { roles: ['Admin', 'Manager'] },
  },
  {
    path: 'edit/:id',
    component: ProjectFormComponent,
    canActivate: [authGuard, roleGuard],
    data: { roles: ['Admin', 'Manager'] },
  },
];

export default projectRoutes;
