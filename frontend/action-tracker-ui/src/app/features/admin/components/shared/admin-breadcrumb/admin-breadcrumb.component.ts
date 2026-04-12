import { Component, Input } from '@angular/core';
import { RouterLink }       from '@angular/router';
import { CommonModule }     from '@angular/common';

export interface BreadcrumbItem {
  label: string;
  route?: string;
}

@Component({
  selector: 'app-admin-breadcrumb',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './admin-breadcrumb.component.html',
  styleUrl: './admin-breadcrumb.component.scss',
})
export class AdminBreadcrumbComponent {
  @Input() items: BreadcrumbItem[] = [];
}
