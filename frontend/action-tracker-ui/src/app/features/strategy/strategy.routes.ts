import { Routes } from '@angular/router';
import { permissionGuard } from '../../core/guards/permission-data.guard';

export const STRATEGY_ROUTES: Routes = [
  {
    path: '',
    canActivate: [permissionGuard],
    data: { requiredArea: 'StrategicPlans', requiredAction: 'View' },
    loadComponent: () =>
      import('./strategy-shell/strategy-shell.component').then(m => m.StrategyShellComponent),
  },
  {
    path: 'org-units/:orgUnitId',
    canActivate: [permissionGuard],
    data: { requiredArea: 'StrategicPlans', requiredAction: 'View' },
    loadComponent: () =>
      import('./strategy-page/strategy-page.component').then(m => m.StrategyPageComponent),
  },
  {
    path: 'objectives/:objectiveId',
    canActivate: [permissionGuard],
    data: { requiredArea: 'StrategicObjectives', requiredAction: 'View' },
    loadComponent: () =>
      import('./objective-detail/objective-detail.component').then(m => m.ObjectiveDetailComponent),
  },
];
