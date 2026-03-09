import { Routes } from '@angular/router';
import { authGuard } from '../../core/guards/auth.guard';
import { roleGuard } from '../../core/guards/role.guard';

const projectRoutes: Routes = [
  {
    path: 'new',
    loadComponent: () =>
      import('./components/project-form/project-form.component').then(m => m.ProjectFormComponent),
    canActivate: [authGuard, roleGuard],
    data: { roles: ['Admin', 'Manager'] },
  },
  {
    path: 'edit/:id',
    loadComponent: () =>
      import('./components/project-form/project-form.component').then(m => m.ProjectFormComponent),
    canActivate: [authGuard, roleGuard],
    data: { roles: ['Admin', 'Manager'] },
  },
  {
    path: ':id',
    loadComponent: () =>
      import('./components/project-detail/project-detail.component').then(m => m.ProjectDetailComponent),
    canActivate: [authGuard],
  },
];

export default projectRoutes;
