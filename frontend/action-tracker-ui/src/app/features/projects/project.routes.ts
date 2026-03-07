import { Routes } from '@angular/router';
import { roleGuard } from '../../core/guards/role.guard';

import { ProjectFormComponent } from './components/project-form/project-form.component';

const projectRoutes: Routes = [
  {
    path: 'new',
    component: ProjectFormComponent,
    canActivate: [roleGuard],
    data: { roles: ['Admin', 'Manager'] },
  },
  {
    path: ':id/edit',
    component: ProjectFormComponent,
    canActivate: [roleGuard],
    data: { roles: ['Admin', 'Manager'] },
  },
];

export default projectRoutes;
