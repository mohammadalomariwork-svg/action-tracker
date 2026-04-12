import { Routes } from '@angular/router';
import { authGuard } from '../../core/guards/auth.guard';
import { permissionGuard } from '../../core/guards/permission-data.guard';

const projectRoutes: Routes = [
  {
    path: 'my',
    loadComponent: () =>
      import('./components/my-projects/my-projects.component').then(m => m.MyProjectsComponent),
    canActivate: [authGuard],
  },
  {
    path: 'new',
    loadComponent: () =>
      import('./components/project-form/project-form.component').then(m => m.ProjectFormComponent),
    canActivate: [authGuard, permissionGuard],
    data: { requiredArea: 'Projects', requiredAction: 'Create' },
  },
  {
    path: 'edit/:id',
    loadComponent: () =>
      import('./components/project-form/project-form.component').then(m => m.ProjectFormComponent),
    canActivate: [authGuard, permissionGuard],
    data: { requiredArea: 'Projects', requiredAction: 'Edit' },
  },
  {
    path: ':projectId/milestones/:milestoneId',
    loadComponent: () =>
      import('./components/milestone-detail/milestone-detail.component').then(m => m.MilestoneDetailComponent),
    canActivate: [authGuard],
  },
  {
    path: ':projectId/risks/:riskId',
    loadComponent: () =>
      import('./components/risk-detail/risk-detail.component').then(m => m.RiskDetailComponent),
    canActivate: [authGuard],
  },
  {
    path: ':id',
    loadComponent: () =>
      import('./components/project-detail/project-detail.component').then(m => m.ProjectDetailComponent),
    canActivate: [authGuard],
  },
];

export default projectRoutes;
