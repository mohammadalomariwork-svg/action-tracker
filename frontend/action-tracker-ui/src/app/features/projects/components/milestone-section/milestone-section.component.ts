import {
  Component, OnInit, ChangeDetectionStrategy,
  inject, signal, input,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { HasPermissionDirective } from '../../../../shared';

import { MilestoneService } from '../../services/milestone.service';
import { ToastService } from '../../../../core/services/toast.service';
import { ProjectService } from '../../services/project.service';
import {
  MilestoneResponse,
  MilestoneCreate,
  MilestoneUpdate,
  MilestoneStatus,
  MilestoneStatusLabels,
} from '../../models/milestone.models';
import { AssignableUser } from '../../../../core/models/action-item.model';

@Component({
  selector: 'app-milestone-section',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, FormsModule, RouterLink, HasPermissionDirective],
  templateUrl: './milestone-section.component.html',
  styleUrl: './milestone-section.component.scss',
})
export class MilestoneSectionComponent implements OnInit {
  readonly projectId = input.required<string>();
  readonly isBaselined = input<boolean>(false);

  private readonly milestoneSvc       = inject(MilestoneService);
  private readonly projectSvc         = inject(ProjectService);
  private readonly toastSvc           = inject(ToastService);

  readonly milestones = signal<MilestoneResponse[]>([]);
  readonly loading = signal(false);
  readonly users = signal<AssignableUser[]>([]);

  // Form state
  readonly showForm = signal(false);
  readonly editingId = signal<string | null>(null);
  readonly submitting = signal(false);

  // Expanded milestone (for comments/documents)
  readonly expandedId = signal<string | null>(null);

  // Form model
  readonly formName = signal('');
  readonly formDescription = signal('');
  readonly formSequenceOrder = signal(1);
  readonly formPlannedStartDate = signal('');
  readonly formPlannedDueDate = signal('');
  readonly formActualCompletionDate = signal('');
  readonly formIsDeadlineFixed = signal(false);
  readonly formStatus = signal<MilestoneStatus>(MilestoneStatus.NotStarted);
  readonly formCompletionPercentage = signal(0);
  readonly formApproverUserId = signal('');

  readonly MilestoneStatus = MilestoneStatus;
  readonly MilestoneStatusLabels = MilestoneStatusLabels;
  readonly statusOptions = [
    MilestoneStatus.NotStarted,
    MilestoneStatus.InProgress,
    MilestoneStatus.Completed,
    MilestoneStatus.Delayed,
    MilestoneStatus.Cancelled,
  ];

  // Search & sort
  searchTerm = '';
  sortField: 'sequenceOrder' | 'name' | 'plannedStartDate' | 'plannedDueDate' = 'sequenceOrder';
  sortDirection: 'asc' | 'desc' = 'asc';

  // Pagination
  currentPage = 1;
  pageSize = 10;
  totalPages = 1;
  filteredMilestones: MilestoneResponse[] = [];
  pagedMilestones: MilestoneResponse[] = [];

  ngOnInit(): void {
    this.loadMilestones();
    this.loadUsers();
  }

  private loadMilestones(): void {
    this.loading.set(true);
    this.milestoneSvc.getByProject(this.projectId()).subscribe({
      next: r => {
        const all = r.data ?? [];
        this.milestones.set(all);
        this.applyFilters();
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.toastSvc.error('Failed to load milestones.');
      },
    });
  }

  private loadUsers(): void {
    this.projectSvc.getAssignableUsers().subscribe({
      next: r => this.users.set(r.data ?? []),
      error: () => {},
    });
  }

  // ── Search, Sort, Pagination ────────────────────────────────────────────────

  applyFilters(): void {
    let result = [...this.milestones()];

    // Search
    if (this.searchTerm.trim()) {
      const term = this.searchTerm.toLowerCase();
      result = result.filter(m =>
        m.name.toLowerCase().includes(term) ||
        m.milestoneCode.toLowerCase().includes(term) ||
        (m.description ?? '').toLowerCase().includes(term) ||
        (m.statusLabel ?? '').toLowerCase().includes(term)
      );
    }

    // Sort
    result.sort((a, b) => {
      let cmp = 0;
      if (this.sortField === 'sequenceOrder') {
        cmp = a.sequenceOrder - b.sequenceOrder;
      } else if (this.sortField === 'name') {
        cmp = a.name.localeCompare(b.name);
      } else if (this.sortField === 'plannedStartDate') {
        cmp = new Date(a.plannedStartDate).getTime() - new Date(b.plannedStartDate).getTime();
      } else if (this.sortField === 'plannedDueDate') {
        cmp = new Date(a.plannedDueDate).getTime() - new Date(b.plannedDueDate).getTime();
      }
      return this.sortDirection === 'asc' ? cmp : -cmp;
    });

    this.filteredMilestones = result;
    this.totalPages = Math.max(1, Math.ceil(result.length / this.pageSize));
    if (this.currentPage > this.totalPages) this.currentPage = 1;
    this.updatePage();
  }

  updatePage(): void {
    const start = (this.currentPage - 1) * this.pageSize;
    this.pagedMilestones = this.filteredMilestones.slice(start, start + this.pageSize);
  }

  onSearchChange(): void {
    this.currentPage = 1;
    this.applyFilters();
  }

  toggleSort(field: 'sequenceOrder' | 'name' | 'plannedStartDate' | 'plannedDueDate'): void {
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
    return Math.min(this.currentPage * this.pageSize, this.filteredMilestones.length);
  }

  get pages(): number[] {
    const pages: number[] = [];
    for (let i = 1; i <= this.totalPages; i++) pages.push(i);
    return pages;
  }

  // ── Status badge class ──────────────────────────────────────────────────────

  statusBadgeClass(s: MilestoneStatus): string {
    switch (+s) {
      case MilestoneStatus.NotStarted: return 'ms-status-badge--not-started';
      case MilestoneStatus.InProgress: return 'ms-status-badge--in-progress';
      case MilestoneStatus.Completed:  return 'ms-status-badge--completed';
      case MilestoneStatus.Delayed:    return 'ms-status-badge--delayed';
      case MilestoneStatus.Cancelled:  return 'ms-status-badge--cancelled';
      default:                         return 'ms-status-badge--not-started';
    }
  }

  // ── Form operations ─────────────────────────────────────────────────────────

  openCreateForm(): void {
    this.editingId.set(null);
    this.resetForm();
    const nextSeq = this.milestones().length > 0
      ? Math.max(...this.milestones().map(m => m.sequenceOrder)) + 1
      : 1;
    this.formSequenceOrder.set(nextSeq);
    this.showForm.set(true);
  }

  openEditForm(m: MilestoneResponse): void {
    this.editingId.set(m.id);
    this.formName.set(m.name);
    this.formDescription.set(m.description ?? '');
    this.formSequenceOrder.set(m.sequenceOrder);
    this.formPlannedStartDate.set(m.plannedStartDate?.substring(0, 10) ?? '');
    this.formPlannedDueDate.set(m.plannedDueDate?.substring(0, 10) ?? '');
    this.formActualCompletionDate.set(m.actualCompletionDate?.substring(0, 10) ?? '');
    this.formIsDeadlineFixed.set(m.isDeadlineFixed);
    this.formStatus.set(m.status);
    this.formCompletionPercentage.set(m.completionPercentage);
    this.formApproverUserId.set(m.approverUserId ?? '');
    this.showForm.set(true);
  }

  cancelForm(): void {
    this.showForm.set(false);
    this.editingId.set(null);
    this.resetForm();
  }

  submitForm(): void {
    if (!this.formName().trim()) {
      this.toastSvc.warning('Name is required.');
      return;
    }
    this.submitting.set(true);
    const editing = this.editingId();
    if (editing) {
      this.doUpdate(editing);
    } else {
      this.doCreate();
    }
  }

  private doCreate(): void {
    const dto: MilestoneCreate = {
      name: this.formName().trim(),
      description: this.formDescription().trim() || undefined,
      sequenceOrder: this.formSequenceOrder(),
      plannedStartDate: this.formPlannedStartDate(),
      plannedDueDate: this.formPlannedDueDate(),
      isDeadlineFixed: this.formIsDeadlineFixed(),
      completionPercentage: this.formCompletionPercentage(),
      approverUserId: this.formApproverUserId() || undefined,
    };

    this.milestoneSvc.create(this.projectId(), dto).subscribe({
      next: r => {
        this.milestones.update(list => [...list, r.data].sort((a, b) => a.sequenceOrder - b.sequenceOrder));
        this.applyFilters();
        this.showForm.set(false);
        this.resetForm();
        this.submitting.set(false);
        this.toastSvc.success('Milestone created.');
      },
      error: (err) => {
        this.submitting.set(false);
        this.toastSvc.error(err?.error?.message ?? 'Failed to create milestone.');
      },
    });
  }

  private doUpdate(milestoneId: string): void {
    const dto: MilestoneUpdate = {
      name: this.formName().trim(),
      description: this.formDescription().trim() || undefined,
      sequenceOrder: this.formSequenceOrder(),
      plannedStartDate: this.formPlannedStartDate(),
      plannedDueDate: this.formPlannedDueDate(),
      actualCompletionDate: this.formActualCompletionDate() || undefined,
      isDeadlineFixed: this.formIsDeadlineFixed(),
      status: this.formStatus(),
      completionPercentage: this.formCompletionPercentage(),
      approverUserId: this.formApproverUserId() || undefined,
    };

    this.milestoneSvc.update(this.projectId(), milestoneId, dto).subscribe({
      next: r => {
        this.milestones.update(list =>
          list.map(m => m.id === milestoneId ? r.data : m)
            .sort((a, b) => a.sequenceOrder - b.sequenceOrder)
        );
        this.applyFilters();
        this.showForm.set(false);
        this.editingId.set(null);
        this.resetForm();
        this.submitting.set(false);
        this.toastSvc.success('Milestone updated.');
      },
      error: (err) => {
        this.submitting.set(false);
        this.toastSvc.error(err?.error?.message ?? 'Failed to update milestone.');
      },
    });
  }

  deleteMilestone(m: MilestoneResponse): void {
    if (!confirm(`Delete milestone "${m.name}"?`)) return;

    this.milestoneSvc.delete(this.projectId(), m.id).subscribe({
      next: () => {
        this.milestones.update(list => list.filter(x => x.id !== m.id));
        this.applyFilters();
        this.toastSvc.success('Milestone deleted.');
      },
      error: () => this.toastSvc.error('Failed to delete milestone.'),
    });
  }

  toggleExpand(id: string): void {
    this.expandedId.set(this.expandedId() === id ? null : id);
  }

  milestoneStatusClass(s: MilestoneStatus): string {
    switch (+s) {
      case MilestoneStatus.NotStarted: return 'badge bg-secondary';
      case MilestoneStatus.InProgress: return 'badge bg-primary';
      case MilestoneStatus.Completed:  return 'badge bg-success';
      case MilestoneStatus.Delayed:    return 'badge bg-warning text-dark';
      case MilestoneStatus.Cancelled:  return 'badge bg-danger';
      default:                         return 'badge bg-light text-dark';
    }
  }

  varianceClass(days: number | null | undefined): string {
    if (days == null) return '';
    if (days > 0) return 'text-danger';
    if (days < 0) return 'text-success';
    return '';
  }

  varianceLabel(days: number | null | undefined): string {
    if (days == null) return 'N/A';
    if (days === 0) return 'On track';
    if (days > 0) return `${days}d behind`;
    return `${Math.abs(days)}d ahead`;
  }

  private resetForm(): void {
    this.formName.set('');
    this.formDescription.set('');
    this.formSequenceOrder.set(1);
    this.formPlannedStartDate.set('');
    this.formPlannedDueDate.set('');
    this.formActualCompletionDate.set('');
    this.formIsDeadlineFixed.set(false);
    this.formStatus.set(MilestoneStatus.NotStarted);
    this.formCompletionPercentage.set(0);
    this.formApproverUserId.set('');
  }
}
