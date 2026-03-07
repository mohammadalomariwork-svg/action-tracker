import { Routes } from '@angular/router';
import { authGuard } from '../../core/guards/auth.guard';

import { ActionItemDetailComponent } from '../projects/components/action-item-detail/action-item-detail.component';
import { ActionItemListComponent } from '../projects/components/action-items/action-item-list/action-item-list.component';

const actionItemRoutes: Routes = [
  {
    path: ':id',
    component: ActionItemDetailComponent,
    canActivate: [authGuard],
  },
  {
    path: 'new',
    component: ActionItemListComponent,
    canActivate: [authGuard],
  },
];

export default actionItemRoutes;
