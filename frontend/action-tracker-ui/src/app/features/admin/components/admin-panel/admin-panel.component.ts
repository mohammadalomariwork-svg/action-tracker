import { Component, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { AdminBreadcrumbComponent } from '../shared/admin-breadcrumb/admin-breadcrumb.component';

export interface AdminFeatureCard {
  id: string;
  title: string;
  description: string;
  icon: string;
  route: string;
  color: string;
  badge?: string;
}

@Component({
  selector: 'app-admin-panel',
  standalone: true,
  imports: [CommonModule, RouterLink, AdminBreadcrumbComponent],
  templateUrl: './admin-panel.component.html',
  styleUrl: './admin-panel.component.scss',
})
export class AdminPanelComponent {
  private readonly router = inject(Router);

  readonly features: AdminFeatureCard[] = [
    {
      id:          'org-chart',
      title:       'Organization Chart',
      description: 'Manage KU org units up to 10 hierarchy levels. Build your institutional structure.',
      icon:        'bi-diagram-3-fill',
      route:       '/admin/org-chart',
      color:       'primary',
    },
    {
      id:          'objectives',
      title:       'Strategic Objectives',
      description: 'Define and assign strategic objectives (SO-1, SO-2…) to organizational units.',
      icon:        'bi-bullseye',
      route:       '/admin/objectives',
      color:       'success',
    },
    {
      id:          'kpis',
      title:       'KPIs & Targets',
      description: 'Create KPIs per objective with monthly, quarterly, or annual measurement targets.',
      icon:        'bi-graph-up-arrow',
      route:       '/admin/kpis',
      color:       'warning',
    },
    {
      id:          'users',
      title:       'User Management',
      description: 'Manage system users, roles, and access control for both local and AD accounts.',
      icon:        'bi-people-fill',
      route:       '/admin/users',
      color:       'info',
    },
  ];

  navigate(route: string): void {
    this.router.navigate([route]);
  }
}
