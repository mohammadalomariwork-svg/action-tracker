import { Component, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { AdminBreadcrumbComponent } from '../shared/admin-breadcrumb/admin-breadcrumb.component';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { HasPermissionDirective } from '../../../../shared';

export interface AdminFeatureCard {
  id: string;
  title: string;
  description: string;
  icon: string;
  route: string;
  color: string;
  badge?: string;
  permissionArea: string;
}

@Component({
  selector: 'app-admin-panel',
  standalone: true,
  imports: [CommonModule, RouterLink, AdminBreadcrumbComponent, PageHeaderComponent, HasPermissionDirective],
  templateUrl: './admin-panel.component.html',
  styleUrl: './admin-panel.component.scss',
})
export class AdminPanelComponent {
  private readonly router = inject(Router);

  readonly features: AdminFeatureCard[] = [
    {
      id:              'org-chart',
      title:           'Organization Chart',
      description:     'Manage KU org units up to 10 hierarchy levels. Build your institutional structure.',
      icon:            'bi-diagram-3-fill',
      route:           '/admin/org-chart',
      color:           'primary',
      permissionArea:  'OrgChart',
    },
    {
      id:              'objectives',
      title:           'Strategic Objectives',
      description:     'Define and assign strategic objectives (SO-1, SO-2…) to organizational units.',
      icon:            'bi-bullseye',
      route:           '/admin/objectives',
      color:           'success',
      permissionArea:  'StrategicObjectives',
    },
    {
      id:              'kpis',
      title:           'KPIs & Targets',
      description:     'Create KPIs per objective with monthly, quarterly, or annual measurement targets.',
      icon:            'bi-graph-up-arrow',
      route:           '/admin/kpis',
      color:           'warning',
      permissionArea:  'KPIs',
    },
    {
      id:              'users',
      title:           'User Management',
      description:     'Manage system users, roles, and access control for both local and AD accounts.',
      icon:            'bi-people-fill',
      route:           '/admin/users',
      color:           'info',
      permissionArea:  'UserManagement',
    },
    {
      id:              'email-templates',
      title:           'Email Templates',
      description:     'Manage notification email templates and review sent email logs.',
      icon:            'bi-envelope-fill',
      route:           '/admin/email-templates',
      color:           'dark',
      permissionArea:  'Admin',
    },
    // Permissions Management card hidden per business request
    // {
    //   id:              'permissions',
    //   title:           'Permissions Management',
    //   description:     'Configure role permission matrices and manage user-level permission overrides.',
    //   icon:            'bi-shield-check-fill',
    //   route:           '/admin/permissions/roles',
    //   color:           'danger',
    //   permissionArea:  'PermissionsManagement',
    // },
    {
      id:              'roles',
      title:           'Roles Management',
      description:     'Create and manage system roles, assign permissions and users to each role.',
      icon:            'bi-person-badge-fill',
      route:           '/admin/roles',
      color:           'secondary',
      permissionArea:  'Roles',
    },
  ];

  navigate(route: string): void {
    this.router.navigate([route]);
  }
}
