import { Component, OnInit, DestroyRef, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { forkJoin } from 'rxjs';

import { WorkspaceService } from '../../services/workspace.service';
import { WorkspaceList, WorkspaceSummary } from '../../models/workspace.model';
import { BreadcrumbComponent } from '../../../../shared/components/breadcrumb/breadcrumb.component';

@Component({
  selector: 'app-workspace-list',
  standalone: true,
  imports: [CommonModule, FormsModule, BreadcrumbComponent],
  templateUrl: './workspace-list.component.html',
  styleUrl: './workspace-list.component.scss',
})
export class WorkspaceListComponent implements OnInit {
  private readonly workspaceService = inject(WorkspaceService);
  private readonly router           = inject(Router);
  private readonly destroyRef       = inject(DestroyRef);

  // Data
  allWorkspaces: WorkspaceList[] = [];
  filteredWorkspaces: WorkspaceList[] = [];
  pagedWorkspaces: WorkspaceList[] = [];
  summary: WorkspaceSummary | null = null;

  // State
  isLoading   = false;
  errorMessage: string | null = null;

  // Search & sort
  searchTerm = '';
  sortField: 'title' | 'organizationUnit' | 'createdAt' = 'createdAt';
  sortDirection: 'asc' | 'desc' = 'desc';

  // Pagination
  currentPage = 1;
  pageSize = 10;
  totalPages = 1;

  ngOnInit(): void {
    this.loadData();
  }

  loadData(): void {
    this.isLoading    = true;
    this.errorMessage = null;

    forkJoin({
      workspaces: this.workspaceService.getWorkspaces(),
      summary: this.workspaceService.getSummary()
    })
    .pipe(takeUntilDestroyed(this.destroyRef))
    .subscribe({
      next: ({ workspaces, summary }) => {
        this.allWorkspaces = workspaces.data ?? [];
        this.summary = summary.data ?? null;
        this.applyFilters();
        this.isLoading = false;
      },
      error: (err) => {
        this.errorMessage = err?.error?.message ?? 'Failed to load workspaces.';
        this.isLoading    = false;
      },
    });
  }

  applyFilters(): void {
    let result = [...this.allWorkspaces];

    // Search
    if (this.searchTerm.trim()) {
      const term = this.searchTerm.toLowerCase();
      result = result.filter(w =>
        w.title.toLowerCase().includes(term) ||
        w.organizationUnit.toLowerCase().includes(term) ||
        w.adminUserNames.toLowerCase().includes(term)
      );
    }

    // Sort
    result.sort((a, b) => {
      let cmp = 0;
      if (this.sortField === 'title') {
        cmp = a.title.localeCompare(b.title);
      } else if (this.sortField === 'organizationUnit') {
        cmp = a.organizationUnit.localeCompare(b.organizationUnit);
      } else {
        cmp = new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime();
      }
      return this.sortDirection === 'asc' ? cmp : -cmp;
    });

    this.filteredWorkspaces = result;
    this.totalPages = Math.max(1, Math.ceil(result.length / this.pageSize));
    if (this.currentPage > this.totalPages) this.currentPage = 1;
    this.updatePage();
  }

  updatePage(): void {
    const start = (this.currentPage - 1) * this.pageSize;
    this.pagedWorkspaces = this.filteredWorkspaces.slice(start, start + this.pageSize);
  }

  onSearchChange(): void {
    this.currentPage = 1;
    this.applyFilters();
  }

  toggleSort(field: 'title' | 'organizationUnit'): void {
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

  get pageStart(): number {
    return (this.currentPage - 1) * this.pageSize + 1;
  }

  get pageEnd(): number {
    return Math.min(this.currentPage * this.pageSize, this.filteredWorkspaces.length);
  }

  get pages(): number[] {
    const pages: number[] = [];
    for (let i = 1; i <= this.totalPages; i++) pages.push(i);
    return pages;
  }

  getInitials(title: string): string {
    return title
      .split(/\s+/)
      .filter(w => w.length > 0)
      .map(w => w[0].toUpperCase())
      .slice(0, 2)
      .join('');
  }

  getAvatarColor(name: string): string {
    const colors = [
      '#3b82f6', '#ef4444', '#f59e0b', '#10b981', '#8b5cf6',
      '#ec4899', '#06b6d4', '#f97316', '#6366f1', '#14b8a6'
    ];
    let hash = 0;
    for (let i = 0; i < name.length; i++) {
      hash = name.charCodeAt(i) + ((hash << 5) - hash);
    }
    return colors[Math.abs(hash) % colors.length];
  }

  formatDate(dateStr: string): string {
    const d = new Date(dateStr);
    return d.toLocaleDateString('en-US', { month: 'short', year: 'numeric' });
  }

  onView(id: string): void {
    this.router.navigate(['/workspaces', id]);
  }

  onEdit(id: string): void {
    this.router.navigate(['/workspaces/edit', id]);
  }

  onDelete(id: string): void {
    if (!confirm('Are you sure you want to delete this workspace?')) return;

    this.workspaceService
      .deleteWorkspace(id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => this.loadData(),
        error: (err) => {
          this.errorMessage = err?.error?.message ?? 'Failed to delete workspace.';
        },
      });
  }

  onRestore(id: string): void {
    if (!confirm('Restore this workspace?')) return;

    this.workspaceService
      .restoreWorkspace(id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => this.loadData(),
        error: (err) => {
          this.errorMessage = err?.error?.message ?? 'Failed to restore workspace.';
        },
      });
  }

  onCreateNew(): void {
    this.router.navigate(['/workspaces/new']);
  }
}
