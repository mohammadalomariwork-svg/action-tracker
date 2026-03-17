import {
  Component, OnInit, ChangeDetectionStrategy,
  inject, signal,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';

import { AuthService } from '../../../../core/services/auth.service';
import { ProjectService } from '../../services/project.service';
import { ToastService } from '../../../../core/services/toast.service';
import {
  ProjectResponse, ProjectStatus, ProjectPriority,
} from '../../models/project.models';
import { PagedResult } from '../../../../core/models/api-response.model';
import { HasPermissionDirective } from '../../../../shared';

export type MyProjectRole = 'manager' | 'sponsor';

export interface MyProjectEntry {
  project: ProjectResponse;
  role: MyProjectRole;
}

@Component({
  selector: 'app-my-projects',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, FormsModule, RouterLink, HasPermissionDirective],
  templateUrl: './my-projects.component.html',
  styleUrl: './my-projects.component.scss',
})
export class MyProjectsComponent implements OnInit {
  private readonly authSvc    = inject(AuthService);
  private readonly projectSvc = inject(ProjectService);
  private readonly toastSvc   = inject(ToastService);

  readonly currentUser = toSignal(this.authSvc.currentUser$, { initialValue: null });

  readonly loading    = signal(false);
  readonly allEntries = signal<MyProjectEntry[]>([]);

  // Filters
  searchTerm   = '';
  statusFilter: ProjectStatus | '' = '';
  roleFilter:   MyProjectRole  | '' = '';

  // Sort
  sortField: 'name' | 'plannedStartDate' | 'plannedEndDate' | 'status' = 'name';
  sortDirection: 'asc' | 'desc' = 'asc';

  // Pagination
  currentPage  = 1;
  pageSize     = 15;
  totalPages   = 1;
  filteredEntries: MyProjectEntry[] = [];
  pagedEntries:    MyProjectEntry[] = [];

  readonly ProjectStatus  = ProjectStatus;
  readonly ProjectPriority = ProjectPriority;

  readonly statusOptions: Array<{ value: ProjectStatus; label: string }> = [
    { value: ProjectStatus.Draft,     label: 'Draft' },
    { value: ProjectStatus.Active,    label: 'Active' },
    { value: ProjectStatus.OnHold,    label: 'On Hold' },
    { value: ProjectStatus.Completed, label: 'Completed' },
    { value: ProjectStatus.Cancelled, label: 'Cancelled' },
  ];

  ngOnInit(): void {
    this.loadProjects();
  }

  private loadProjects(): void {
    const userId = this.currentUser()?.userId;
    if (!userId) return;

    this.loading.set(true);
    this.projectSvc.getAll({
      pageNumber: 1, pageSize: 500,
      sortBy: 'name', sortDescending: false,
    }).subscribe({
      next: res => {
        const projects = (res.data as PagedResult<ProjectResponse>).items ?? [];
        const entries: MyProjectEntry[] = [];

        for (const p of projects) {
          if (p.projectManagerUserId === userId) {
            entries.push({ project: p, role: 'manager' });
          } else if (p.sponsors.some(s => s.userId === userId)) {
            entries.push({ project: p, role: 'sponsor' });
          }
        }

        this.allEntries.set(entries);
        this.applyFilters();
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.toastSvc.error('Failed to load projects.');
      },
    });
  }

  // ── Search / Filter / Sort / Pagination ─────────────────────────────────────

  applyFilters(): void {
    let result = [...this.allEntries()];

    if (this.searchTerm.trim()) {
      const term = this.searchTerm.toLowerCase();
      result = result.filter(e =>
        e.project.name.toLowerCase().includes(term) ||
        e.project.projectCode.toLowerCase().includes(term) ||
        (e.project.description ?? '').toLowerCase().includes(term)
      );
    }

    if (this.statusFilter) {
      result = result.filter(e => e.project.status === this.statusFilter);
    }

    if (this.roleFilter) {
      result = result.filter(e => e.role === this.roleFilter);
    }

    result.sort((a, b) => {
      let cmp = 0;
      switch (this.sortField) {
        case 'name':
          cmp = a.project.name.localeCompare(b.project.name); break;
        case 'plannedStartDate':
          cmp = new Date(a.project.plannedStartDate).getTime() - new Date(b.project.plannedStartDate).getTime(); break;
        case 'plannedEndDate':
          cmp = new Date(a.project.plannedEndDate).getTime() - new Date(b.project.plannedEndDate).getTime(); break;
        case 'status':
          cmp = a.project.status.localeCompare(b.project.status); break;
      }
      return this.sortDirection === 'asc' ? cmp : -cmp;
    });

    this.filteredEntries = result;
    this.totalPages = Math.max(1, Math.ceil(result.length / this.pageSize));
    if (this.currentPage > this.totalPages) this.currentPage = 1;
    this.updatePage();
  }

  updatePage(): void {
    const start = (this.currentPage - 1) * this.pageSize;
    this.pagedEntries = this.filteredEntries.slice(start, start + this.pageSize);
  }

  onSearchChange(): void {
    this.currentPage = 1;
    this.applyFilters();
  }

  toggleSort(field: typeof this.sortField): void {
    if (this.sortField === field) {
      this.sortDirection = this.sortDirection === 'asc' ? 'desc' : 'asc';
    } else {
      this.sortField = field;
      this.sortDirection = 'asc';
    }
    this.applyFilters();
  }

  goToPage(page: number): void {
    if (page < 1 || page > this.totalPages) return;
    this.currentPage = page;
    this.updatePage();
  }

  get pageStart(): number { return (this.currentPage - 1) * this.pageSize + 1; }
  get pageEnd():   number { return Math.min(this.currentPage * this.pageSize, this.filteredEntries.length); }

  get pages(): number[] {
    const p: number[] = [];
    for (let i = 1; i <= this.totalPages; i++) p.push(i);
    return p;
  }

  // ── Badge helpers ────────────────────────────────────────────────────────────

  statusBadgeClass(status: ProjectStatus): string {
    switch (status) {
      case ProjectStatus.Draft:      return 'mp-badge mp-badge--draft';
      case ProjectStatus.Active:     return 'mp-badge mp-badge--active';
      case ProjectStatus.OnHold:     return 'mp-badge mp-badge--onhold';
      case ProjectStatus.Completed:  return 'mp-badge mp-badge--completed';
      case ProjectStatus.Cancelled:  return 'mp-badge mp-badge--cancelled';
      default:                       return 'mp-badge mp-badge--draft';
    }
  }

  priorityBadgeClass(priority: ProjectPriority): string {
    switch (priority) {
      case ProjectPriority.Low:      return 'mp-pri mp-pri--low';
      case ProjectPriority.Medium:   return 'mp-pri mp-pri--medium';
      case ProjectPriority.High:     return 'mp-pri mp-pri--high';
      case ProjectPriority.Critical: return 'mp-pri mp-pri--critical';
      default:                       return 'mp-pri mp-pri--low';
    }
  }

  get managerCount():  number { return this.allEntries().filter(e => e.role === 'manager').length; }
  get sponsorCount():  number { return this.allEntries().filter(e => e.role === 'sponsor').length; }
}
